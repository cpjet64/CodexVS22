# MyToolWindowControl State Ownership Map

## Purpose
This document fulfills Task T3 by cataloguing every field scoped to `MyToolWindowControl`, grouping the state into logical domains, and defining the future ownership model, mutability rules, initialization ordering, persistence expectations, and thread-safety requirements. It also outlines the backing models we should introduce for diff/exec data, describes lifecycle transitions, and aligns the state map with existing option and persistence rules.

## Field Catalogue by Domain
Each domain lists the exact fields (type in parentheses), describes their current responsibilities, and records the proposed home after the refactor alongside key safeguards.

### 1. CLI Session & Heartbeat
- **Fields**: `_host (CodexCliHost)`, `_cliStarted (bool)`, `_environmentReadySource (TaskCompletionSource<EnvironmentSnapshot>)`, `_environmentReadyInitialized (int)`, `_heartbeatLock (object)`, `_heartbeatTimer (Timer)`, `_heartbeatState (HeartbeatState)`, `_heartbeatSending (int)`.
- **Current role**: Drive Codex CLI process lifetime, expose an async readiness signal, and throttle heartbeat pings.
- **Future home**: `CliSessionService` living in the services layer, injected into the tool-window view-models. Heartbeat logic becomes an internal helper of the service so UI never touches timers directly.
- **Mutability boundary**: `_host`, `_heartbeatTimer`, `_heartbeatState`, `_cliStarted` remain mutable but encapsulated; `_environmentReadySource` should behave as write-once. Expose read-only projections (e.g., `IObservable<HeartbeatState>`).
- **Initialization order**: Requires environment snapshot and workspace selection before starting; service should lazily spin up when the first consumer subscribes.
- **Persistence**: Session-only; the CLI host and heartbeat state never cross VS restarts.
- **Thread affinity**: Service performs background work on pooled threads; UI callers must marshal onto the main thread when binding to heartbeat results. Timer callbacks must re-enter the UI via dispatcher-safe methods.

### 2. Workspace & Environment Tracking
- **Fields**: `_workingDir (string)`, `_workingDirLock (SemaphoreSlim)`, `_solutionService (IVsSolution)`, `_solutionEvents (SolutionEventsSink)`, `_solutionEventsCookie (uint)`, `_solutionLoadedContext (UIContext)`, `_folderOpenContext (UIContext)`, `_waitingForSolutionLoad (bool)`, `_lastKnownSolutionRoot (string)`, `_lastKnownWorkspaceRoot (string)`, `ExtensionRoot (string)`.
- **Current role**: Resolve the active workspace, listen for solution/folder events, and synchronize CLI environment setup.
- **Future home**: `WorkspaceTracker` service plus a thin `SolutionEventsAdapter`. The tool window should only observe immutable workspace snapshots delivered through an event stream.
- **Mutability boundary**: Replace `_workingDirLock` with an async coordination primitive inside the service; expose the current workspace as an immutable record. Cached roots can be normalized, so they should become read-only properties of the snapshot.
- **Initialization order**: Workspace tracker initializes directly after options load and before CLI session start so restart requests have a cwd.
- **Persistence**: Workspace values are session-specific. Only `ExtensionRoot` remains static and can move to a shared `EnvironmentPaths` utility.
- **Thread affinity**: VS solution events arrive on the UI thread; the tracker should marshal updates to background threads before notifying consumers to avoid UI stalls.

### 3. Authentication & Send Gating
- **Fields**: `_authKnown (bool)`, `_isAuthenticated (bool)`, `_authOperationInProgress (bool)`, `_authMessage (string)`, `_authGatedSend (bool)`.
- **Current role**: Cache the latest authentication result and guard the Send button until Codex confirms login status.
- **Future home**: `AuthenticationViewModel` (or nested `CliSessionService.AuthenticationState`). UI binds to read-only properties; commands relay through a mediator.
- **Mutability boundary**: Mutations restricted to auth service; expose immutable snapshot updates. `_authMessage` should become a structured status record to prevent string parsing later.
- **Initialization order**: Requires CLI session to start; authentication handshake should fire immediately after workspace readiness.
- **Persistence**: Does not persist across sessions; real auth source of truth is the CLI.
- **Thread affinity**: All UI gating updates must execute on the dispatcher thread. Underlying service can run on background threads but not touch WPF elements directly.

### 4. Chat Transcript & Streaming
- **Fields**: `_assistantTurns (Dictionary<string, AssistantTurn>)`, `_lastUserInput (string)`, `_assistantChunkCounter (int)`.
- **Current role**: Maintain live chat turns keyed by call id, store the last prompt text, and throttle streaming bubble formatting.
- **Future home**: `ChatTranscriptViewModel` backed by immutable turn records and an observable collection for display ordering. `_assistantChunkCounter` should become a per-turn field managed inside the transcript model.
- **Mutability boundary**: Replace mutable dictionaries with an immutable turn store (e.g., `ImmutableDictionary`) plus dispatcher-owned `ReadOnlyObservableCollection` for binding. User input persists only through the input box view-model.
- **Initialization order**: Transcript resets during tool window activation and after CLI restarts, before auth prompts resume.
- **Persistence**: No persistence yet; future session-restore feature (Task T14/T15) could hydrate from transcript cache.
- **Thread affinity**: Updates originate on UI thread because WPF collections require dispatcher access. Streaming deltas should flow through a background channel then get marshalled to the view-model dispatcher scope.

### 5. Exec Console & CLI Output
- **Fields**: `_execTurns (Dictionary<string, ExecTurn>)`, `_execConsoleTurns (List<ExecTurn>)`, `_execCommandIndex (Dictionary<string,string>)`, `_execIdRemap (Dictionary<string,string>)`, `_lastExecFallbackId (string)`, `_execConsolePreferredHeight (double)`, `_suppressExecToggleEvent (bool)`, `AnsiCodeRegex (Regex)`, `AnsiBrushes (Brush[])`, `AnsiBrightBrushes (Brush[])`, `Base64Regex (Regex)`.
- **Current role**: Track exec tasks, map ids, store console pane layout, and render ANSI/base64 output.
- **Future home**: `ExecConsoleViewModel` plus `ExecOutputFormatter`. View-model holds observable turn collection; formatter translates CLI chunks into rich text off the UI thread.
- **Mutability boundary**: Collections should become observable read-only lists; id maps move into a dedicated `ExecTurnIndex` that owns mutations. Layout state (`_execConsolePreferredHeight`) moves to options store as an immutable setting snapshot.
- **Initialization order**: Requires CLI session and workspace readiness; layout height loads from options during window activation.
- **Persistence**: Layout preference persists via `CodexOptions`; exec turn data is session-only but should support transcript export later.
- **Thread affinity**: Formatter can decode ANSI/background, but additions to WPF collections must be dispatcher synchronized.

### 6. Diff Review & Patch Application
- **Fields**: `_diffTreeRoots (ObservableCollection<DiffTreeItem>)`, `_diffDocuments (Dictionary<string, DiffDocument>)`, `_suppressDiffSelectionUpdate (bool)`, `_diffTotalLeafCount (int)`, `_patchApplyInProgress (bool)`, `_patchApplyStartedAt (DateTime?)`, `_patchApplyExpectedFiles (int)`, `_lastPatchCallId (string)`, `_lastPatchSignature (string)`.
- **Current role**: Store diff tree state, aggregate selection counts, and coordinate patch apply telemetry.
- **Future home**: `DiffReviewViewModel` and `PatchApplyCoordinator`. The diff tree becomes a view-model with immutable nodes; patch coordinator tracks apply jobs and emits progress events to the UI.
- **Mutability boundary**: Replace mutable dictionaries with typed repositories; `DiffDocument` cache can move to a dedicated diff service storing immutable snapshots. `*_InProgress` flags become part of a `PatchJobState` record.
- **Initialization order**: Diff view-model resets when new diff stream begins and before approvals change state.
- **Persistence**: Diff selections are per session. Patch history should be captured for telemetry only.
- **Thread affinity**: Diff observable collections require dispatcher access; diff parsing can occur on background threads.

### 7. Approval Pipeline & Safety Switches
- **Fields**: `_approvalQueue (Queue<ApprovalRequest>)`, `_activeApproval (ApprovalRequest)`, `_rememberedExecApprovals (Dictionary<string,bool>)`, `_rememberedPatchApprovals (Dictionary<string,bool>)`, `_authGatedSend (bool)`.
- **Current role**: Queue CLI approval requests, remember decisions, coordinate exec/patch gating, and optionally disable Send.
- **Future home**: `ApprovalCoordinator` service with explicit policies (per-solution vs per-user). The tool window binds to a simple DTO representing the current prompt.
- **Mutability boundary**: Service manages all state; expose immutable snapshots for UI. Remembered approvals move into a persistent store keyed by workspace/project when policies allow.
- **Initialization order**: Requires CLI session and options (approval mode) before processing queue items.
- **Persistence**: Today session-only. Recommendation: persist remembered approvals per-solution (with user acknowledgment) in the future options store; mark as opt-in.
- **Thread affinity**: Queue operations happen on background thread but UI prompt display must re-enter dispatcher.

### 8. MCP Tools & Custom Prompts
- **Fields**: `_mcpTools (ObservableCollection<McpToolInfo>)`, `_mcpToolRuns (ObservableCollection<McpToolRun>)`, `_mcpToolRunIndex (Dictionary<string,McpToolRun>)`, `_lastMcpToolsRefresh (DateTime)`, `MaxMcpToolRuns (int)`, `_customPrompts (ObservableCollection<CustomPromptInfo>)`, `_customPromptIndex (Dictionary<string,CustomPromptInfo>)`, `_lastPromptsRefresh (DateTime)`, `RefreshDebounceSeconds (int)`.
- **Current role**: Maintain tool catalog, run history, prompt catalog, and refresh throttling.
- **Future home**: `McpCatalogViewModel` and `PromptLibraryViewModel` backed by a shared `CatalogRefreshService` that enforces debounce logic.
- **Mutability boundary**: Collections should be exposed as read-only observables; indexes live in the service with immutable snapshots delivered to consumers. `MaxMcpToolRuns` and `RefreshDebounceSeconds` become configuration constants.
- **Initialization order**: Requires CLI session and workspace so refresh commands know where to source data.
- **Persistence**: Catalogs are fetched from the CLI; only “last used” selections persist via options (already stored on `_options`).
- **Thread affinity**: Refreshes run in background; updates to observable collections must marshal to dispatcher.

### 9. Options & Model Selection
- **Fields**: `_options (CodexOptions)`, `_initializingSelectors (bool)`, `_selectedModel (string)`, `_selectedReasoning (string)`, `_selectedApprovalMode (CodexOptions.ApprovalMode)`, `ModelOptions (string[])`, `ReasoningOptions (string[])`, `ApprovalModeOptions (CodexOptions.ApprovalMode[])`, `DefaultModelName (string)`, `DefaultReasoningValue (string)`.
- **Current role**: Mirror persisted user preferences and populate combo boxes for model, reasoning, and approval mode.
- **Future home**: `OptionsViewModel` pulling from an injected `ICodexOptionsStore`. The arrays move to a shared configuration provider.
- **Mutability boundary**: `_options` exposed as immutable snapshot; UI updates trigger commands that request the store to mutate. `_initializingSelectors` becomes a local guard inside the view-model only.
- **Initialization order**: Options load before any UI binding, ideally during package initialization, so selectors can bind quickly.
- **Persistence**: Persists across sessions via `CodexOptions` store.
- **Thread affinity**: Options I/O may occur off thread; UI updates require dispatcher.

### 10. Window Chrome & Layout
- **Fields**: `_hostWindow (System.Windows.Window)`, `_windowEventsHooked (bool)`, `_execConsolePreferredHeight (double)` (shared with exec layout).
- **Current role**: Track ownership of the tool window host and apply layout persistence.
- **Future home**: `WindowLifetimeController` in the UI shell layer. Layout values move entirely into the options view-model.
- **Mutability boundary**: `_hostWindow` should be set once on attach and cleared on dispose; `_windowEventsHooked` becomes a derived property.
- **Initialization order**: Window hooking occurs after control loaded but before layout restoration.
- **Persistence**: Layout persisted via options; window reference is session-only.
- **Thread affinity**: WPF window interactions must remain on dispatcher thread.

### 11. Telemetry & Metrics
- **Fields**: `_telemetry (TelemetryTracker)`.
- **Current role**: Aggregate turn, exec, patch, and tool usage metrics.
- **Future home**: Standalone `TelemetryAggregator` service shared across view-models; UI simply invokes methods on events.
- **Mutability boundary**: Internal counters remain mutable but encapsulated; exposed summary strings should be immutable DTOs.
- **Initialization order**: Reset whenever transcript resets or CLI restarts.
- **Persistence**: Aggregated metrics publish to telemetry pipeline only; no persistence required.
- **Thread affinity**: Counters can update on background threads; summary projection for UI should marshal to dispatcher.

### 12. Output Decoration Utilities
- **Fields**: `AnsiCodeRegex (Regex)`, `AnsiBrushes (Brush[])`, `AnsiBrightBrushes (Brush[])`, `Base64Regex (Regex)`.
- **Current role**: Support exec output decoding.
- **Future home**: Move to `OutputFormatting` utility in the shared UI helpers assembly.
- **Mutability boundary**: All immutable; they can be static readonly constants.
- **Initialization order**: Static initialization on module load; no runtime sequencing concerns.
- **Persistence**: Not persisted.
- **Thread affinity**: Thread-safe because data is immutable.

## Initialization Dependencies
1. **Options load** – hydrate selectors and layout defaults before UI renders.
2. **Workspace tracker** – compute working directory and environment snapshot; only after this can the CLI session start.
3. **CLI session service** – boot Codex CLI, attach heartbeat, and raise readiness.
4. **Authentication handshake** – update gating state and unlock Send when credentials are verified.
5. **Data catalogs** – fetch MCP tools and custom prompts once CLI is ready; apply debounce timers.
6. **Transcript/exec/diff view-models** – subscribe to CLI event streams after services emit readiness so UI collections are safe to mutate.
7. **Approval coordinator** – only processes queues after options (approval mode) and CLI session are both available.

## Persistence & Thread-Affinity Markers
- **Persisted via `CodexOptions`**: `_selectedModel`, `_selectedReasoning`, `_selectedApprovalMode`, `_execConsolePreferredHeight`, window dimensions (currently stored in `_options`). Recommendation: expose these through an immutable options snapshot and ensure updates route through a persistence service.
- **Session-only (do not persist)**: CLI session state, transcript/exec/diff collections, approval queues, remembered approvals (unless we upgrade to per-solution storage), MCP catalogs, heartbeat state, workspace cache strings.
- **Thread-sensitive state**: All `ObservableCollection<>` fields and UI references must stay on the dispatcher thread. Locks (`_workingDirLock`, `_heartbeatLock`) should disappear once services encapsulate concurrency; any remaining synchronization should rely on async pipelines rather than explicit locks.

## Diff & Exec Backing Models
- Introduce `DiffSession` (immutable) containing the tree, document cache, and selection aggregates. View-model exposes `ReadOnlyObservableCollection<DiffNodeView>` built from the session snapshot, while mutations occur by replacing the snapshot.
- Introduce `ExecSession` with `ImmutableDictionary<string, ExecTurnState>` and per-turn output buffers. Provide `ExecTurnView` objects for the UI, updated via dispatcher-safe diffing rather than mutating lists in place.
- Provide dedicated formatters (`AnsiChunkFormatter`, `Base64Decoder`) that run off the UI thread and raise structured events.

## Lifecycle State Transitions
- **Control load** → options snapshot applied → workspace tracker subscribes → CLI session service starts.
- **CLI ready** → authentication check → transcript/exec/diff view-models reset to clean state.
- **User sends prompt** → transcript view-model records pending turn → CLI responses update transcript and telemetry.
- **Exec request arrives** → approval coordinator enqueues → upon approval, exec view-model creates turn, telemetry logs begin/end, diff/prompt selectors remain untouched.
- **Patch request approved** → patch coordinator marks `_patchApplyInProgress` → diff view-model locks selection updates → after apply completes, state resets and telemetry records outcome.
- **Workspace change** → workspace tracker clears catalogs → CLI session restarts → cascading reset of authentication, transcript, exec, diff, approvals.

## Options & Persistence Alignment
- Map selectors (`_selectedModel`, `_selectedReasoning`, `_selectedApprovalMode`) and layout (`_execConsolePreferredHeight`) directly onto the options service with explicit commands so UI never mutates `_options` directly.
- Ensure approval mode changes propagate to the approval coordinator before processing queued requests.
- Store "last used" tool/prompt identifiers inside the options service rather than on the view-model to avoid redundant state across domains.
- Surface persistence policies for remembered approvals (session-only vs future persisted) so downstream teams can implement opt-in storage without touching the UI layer.

## Risks & Recommendations
- Tight coupling between CLI events and UI collections today risks cross-thread access. Breaking state into services with dispatcher-aware adapters eliminates this hazard.
- Shared mutable dictionaries (exec/diff/prompts) should be replaced with immutable snapshots to simplify testing and replay.
- Approval memory currently resets every session; decide policy before extracting to avoid regressions for power users expecting resets.
- Heartbeat timer ownership must move off the control to ensure disposal happens even when the UI unloads.

## Deliverables Generated
- `scratch/state_fields.csv` – raw field inventory.
- `scratch/state_fields.json` – structured field metadata for automation.
- `docs/state-ownership-map.md` – this summary for planners.
