# Session Persistence and Resume Plan

This document addresses Task T14 by defining how Codex for Visual Studio will persist and resume tool-window sessions. The scope covers transcript history, exec console output, diff approvals, and supporting metadata so users can reopen VS and continue where they left off without replaying CLI operations.

## 1. Data Required to Restore Sessions
| Domain | Persisted Data | Current Source | Notes |
| --- | --- | --- | --- |
| Chat transcript | Ordered turn list (user, assistant, status), turn IDs, timestamps, streaming completion flag, linked diff/exec references | `ChatTranscriptViewModel` design (`docs/chat-viewmodel-design.md`) and runtime data built from `_assistantTurns`, `_lastUserInput`, and `CreateChatBubble` (`ToolWindows/MyToolWindowControl.xaml.cs:12194`) | Persist final text plus minimal metadata; streaming deltas are not replayed on resume. |
| Exec console | Exec turn collection with `ExecId`, `DisplayHeader`, command metadata, run state, truncation counters, final buffer snapshot | `ExecTurn` creation and buffer logic (`ToolWindows/MyToolWindowControl.xaml.cs:10380`) and event handlers in `ToolWindows/MyToolWindowControl.Exec.cs` | Persist sanitized plain text (ANSI stripped) alongside optional diff of raw ANSI for re-rendering. |
| Diff workflow | Latest diff documents, tri-state selection state, discard/apply status, last patch call ID/signature | `_diffTreeRoots`, `_diffDocuments`, `_lastPatchCallId` (`ToolWindows/MyToolWindowControl.Mcp.cs:191-235`, `ToolWindows/MyToolWindowControl.xaml.cs:3557`) | Persist only when a diff is pending; mark documents as stale if workspace snapshot moved. |
| Approvals | Pending approval queue, active approval metadata, remembered approvals per signature | `_approvalQueue`, `_activeApproval`, `_rememberedExecApprovals`, `_rememberedPatchApprovals` (`ToolWindows/MyToolWindowControl.Approvals.cs:17-125`) | Remembered approvals remain session-only unless user opts in; pending queue can be replayed if CLI reconnects before expiry. |
| Chat input | Draft input text, cursor position, token estimate | `_lastUserInput` and `InputBox` binding (`ToolWindows/MyToolWindowControl.xaml.cs:12363`) | Persist only when user has unsent text; do not store clipboard history. |
| Workspace & model context | Working directory, selected solution/workspace ID, selected model/reasoning/approval mode | `_workingDir`, `_lastKnownSolutionRoot` (`ToolWindows/MyToolWindowControl.WorkingDirectory.cs:17-78`), `_selectedModel` (`ToolWindows/MyToolWindowControl.Options.cs:13-70`) | Working directory already tracked; persist session cache per normalized workspace so resume chooses correct file. |
| Telemetry snapshot | Turn/exec counters used for in-UI summaries | `_telemetry.GetSummary()` (`ToolWindows/MyToolWindowControl.xaml.cs:11883`) | Persist summary to avoid blank telemetry area on resume; refresh next time CLI emits events. |
| Stream error banner | Banner visibility, last error message, retry availability | `ShowStreamErrorBanner` (`ToolWindows/MyToolWindowControl.xaml.cs:11908`) | Allows UI to keep warning visible until user dismisses or retry succeeds. |

## 2. Serialization Format for Transcript Cache
- Use newline-delimited JSON (`*.jsonl`) grouped under a top-level session manifest. Each manifest stores:
  - `schemaVersion` (integer) and `createdUtc`/`updatedUtc` timestamps.
  - `workspaceId` (hashed solution path) and `optionsVersion` to detect mismatches.
  - Embedded arrays for `chatTurns`, `execTurns`, `diffSessions`, `approvals`, `uiState`.
- Turn entries follow the view-model definitions in `docs/chat-viewmodel-design.md` (`ChatTurnModel`, `ExecTurnModel`). Each entry contains:
  - `id`, `role`, `text`, `segments` (optional typed array), `linkedExecId`, `linkedDiffId`, `metadata` dictionary.
  - For exec turns: `normalizedCommand`, `header`, `status`, `exitCode`, `buffer` (plain text), `ansiBuffer` (optional), `truncatedBytes`.
- Diff sessions store `documents` array with `relativePath`, `content`, `selectionState`, and SHA-256 of the base file to detect drift.
- Approval queue uses FIFO array with `kind`, `callId`, `message`, `canRemember`, `queuedUtc`.
- `uiState` contains lightweight values: `draftInput`, `isStreamErrorVisible`, `streamErrorText`, `tokenSummary`.
- Manifest referencing approach allows streaming writes (append turns) and quick reads without loading entire session file into memory.

## 3. Storage Location and Privacy Controls
- Store sessions per workspace under `%LOCALAPPDATA%\CodexVS\Sessions\{workspaceHash}\session.jsonl`. `workspaceHash` = SHA-1 of normalized solution root to avoid leaking path names yet remain deterministic.
- Maintain manifest index `%LOCALAPPDATA%\CodexVS\Sessions\index.json` with list of known workspaces, last used timestamps, and schema versions for cleanup.
- Encrypt payload using DPAPI CurrentUser scope when `CodexOptions.Privacy.PersistenceEncryption` flag is enabled (default off to avoid perf regressions). Provide feature flag to disable persistence entirely.
- Ensure writes use `SafeFileWriter` (write to temp, flush, replace) and set permissions to user-only.
- Respect VS roaming profiles by keeping data in `LocalAppData` (non-roaming). Do not store in repo to avoid check-ins of transcripts.

## 4. Resume Flow Coordination with CLI Reconnect
1. **Control load**: options snapshot loads, workspace tracker resolves `_workingDir` (`ToolWindows/MyToolWindowControl.WorkingDirectory.cs:17-78`).
2. **Session repository**: hydrate manifest for current workspace before CLI start; pre-populate view-models with cached turns in "stale" state.
3. **CLI start**: once `CodexCliHost.StartAsync` completes (`ToolWindows/MyToolWindowControl.xaml.cs:737-776`), transition cached exec/diff items to "awaiting confirmation".
4. **CLI handshake**: run authentication checks (`ToolWindows/MyToolWindowControl.Authentication.cs:15-100`); if not authenticated, display persisted transcript but block resume actions until login.
5. **Resume reconciliation**: for each cached exec turn, send `exec status` probe if CLI supports, otherwise mark output as read-only with "stale" badge. Pending approvals re-enqueued via `EnqueueApprovalRequest` if CLI connection is live; if CLI restart invalidates call IDs, drop with fallback card.
6. **Diff patches**: verify base file hash; if mismatched, show "patch stale" banner and require refresh. Otherwise rebuild diff tree from cached docs and await user actions.
7. **Finalization**: once CLI confirms readiness, switch `IsResuming` flag off so new events append normally. Persist new session snapshot immediately to ensure resumed state stored.

## 5. Versioning Strategy
- Increment `schemaVersion` whenever structure changes. Maintain migration pipeline that upgrades older manifest entries to latest model via immutable transformations.
- Store `appVersion` (extension semantic version) alongside schema to aid support.
- When incompatible changes detected, prompt user: "Codex session data is outdated; starting fresh" and archive old file to `session.v{n}.bak` for manual inspection.
- Include per-section version fields (`chatVersion`, `execVersion`) so modules can evolve independently without global migrations.

## 6. Telemetry Mapping for Persistence Events
| Event | Trigger | Properties |
| --- | --- | --- |
| `session.persist.save` | Successful background save | `workspaceId`, `turnCount`, `execCount`, `bytes`, `durationMs`, `trigger` (turn, timer, shutdown) |
| `session.persist.failure` | Save failure (exception) | `workspaceId`, `errorType`, `trigger`, `attempt` |
| `session.resume.start` | Control begins resume | `workspaceId`, `hasDraft`, `hasPendingDiff`, `hasPendingExec` |
| `session.resume.success` | Resume completed without critical failures | `workspaceId`, `turnCount`, `execCount`, `diffCount`, `elapsedMs` |
| `session.resume.partial` | Resume succeeded with stale elements | `workspaceId`, `staleExecCount`, `staleDiffCount`, `droppedApprovals` |
| `session.resume.failure` | Resume aborted | `workspaceId`, `errorType`, `hadBackup` |
- Telemetry routes through `TelemetryTracker` extension methods to keep module consistent (`ToolWindows/MyToolWindowControl.Exec.cs:111-200` shows existing usage).

## 7. Background Save Cadence and Triggers
- Immediate save on critical milestones: `TurnFinalized`, `ExecCompleted`, `DiffReceived`, `ApprovalQueued/Resolved`, `StreamErrorShown/Dismissed` to minimize data loss.
- Debounced periodic save every 30 seconds while transcript is active (align with chat plan). Use `DispatcherTimer` or background `TaskScheduler` to avoid blocking UI.
- Save on VS shutdown/unload by hooking `MyToolWindowControl.OnUnloaded` (`ToolWindows/MyToolWindowControl.Lifecycle.cs:118`) and package disposal.
- Implement backpressure: throttle to max 1 save/5 seconds to avoid disk churn during streaming.

## 8. Tests for Resume Across VS Restarts
- **Unit**: Serializer/deserializer round-trips for chat/exec/diff models; ensure schema migration coverage with sample fixtures.
- **Integration**: Harness that boots `MyToolWindowControl`, simulates CLI events, persists session, disposes control, and rehydrates verifying UI bindings populated (leveraging MVVM once refactor lands).
- **File-system**: Tests for hashed workspace path, SafeFileWriter behavior under concurrent save attempts, and permission enforcement.
- **Telemetry**: Verify events fire via mock telemetry service for success/failure states.
- **Stress**: Simulate large transcripts (40k+ chars) and long-running exec output to ensure loader does not block UI thread more than threshold (e.g., <200ms on dispatcher).
- **Privacy**: Static analysis/unit tests confirming secrets (like tokens flagged by `IInputSafetyService`) are redacted before serialization.

## 9. Fallback When Resume Fails
- If manifest missing or corrupt: log diagnostics entry, emit `session.resume.failure`, clear caches, and show non-intrusive banner: "Previous session could not be restored; start a new session." Ensure banner dismissible.
- If CLI rejects pending approvals or resume handshake: mark corresponding items as stale with action to refresh or discard.
- Always keep original file as `.bak` for support to inspect. After repeated failures (e.g., 3 consecutive), automatically disable persistence and notify user with instructions to re-enable via options.
- Provide command `Codex: Clear Saved Session` to delete cached data manually.

## 10. Deliverable
- Output of this plan is `docs/session-persistence-plan.md` (this document).
- Upon implementation, update `CodexOptions` UI with toggles for "Persist session between restarts" and "Encrypt saved session".
- Coordination handoffs: share data schema with Chat (Task T7), Exec (Task T9), Diff (Task T8), and CLI host (Task T6) teams to confirm model alignment before coding.
