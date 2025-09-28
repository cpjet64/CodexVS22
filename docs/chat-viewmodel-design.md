# Chat Transcript ViewModel Design

## Turn Models
- `ChatTurnId` (struct) combines CLI `event_id` with local monotonic index to keep ordering deterministic even when deltas arrive out of order.
- Base `ChatTurnModel` holds `Id`, `Role` (`User`, `Assistant`, `Status`), `TimestampUtc`, `DisplayHeader`, `IsStreaming`, and `Metadata` dictionary for diff/exec references.
- `UserTurnModel` adds original input text, redacted preview, optional `SourceItems` (files/snippets shared via MCP), and `RetryOf` pointer for resend flows.
- `AssistantTurnModel` tracks `ObservableCollection<ChatSegmentModel>` (markdown/plain/code segments), `StreamingBuffer` for active deltas, `LinkedDiffSessionId`, and `SuggestedActions` (apply diff, run command, open link).
- `StatusTurnModel` represents banners such as stream errors, approvals pending, or CLI diagnostics with `StatusKind` enum (`Info`, `Warning`, `Error`, `Success`) plus `ActionCommand` for user choices.
- View-model exposes read-only `ReadOnlyObservableCollection<ChatTurnViewModel>`; converters map role → bubble chrome without direct XAML element inspection.

## Streaming Delta Handling
- `IChatSessionService` publishes `ChatMessageDelta` records through `IModuleMessenger`; each delta contains `TurnId`, `DeltaText`, `SegmentType`, `IsFinal`, and token metadata.
- `ChatTranscriptReducer` (store layer) aggregates deltas into immutable `ChatTranscriptState`, buffering text off the UI thread and emitting batched updates via `DispatcherQueue.EnqueueAsync` only when necessary (initial delta, periodic flush, final message).
- `AssistantTurnModel` exposes `ApplyDelta(ChatMessageDelta delta)` which appends to `StreamingBuffer`, normalizes text via `ChatTextUtilities`, and converts to segments using markdown pipeline; once `IsFinal` arrives the buffer flushes and view raises `PropertyChanged` without re-creating the collection.
- Heartbeats or disconnect events inject `StatusTurnModel` entries rather than manipulating WPF controls directly, allowing transcript replay UI to remain responsive.
- Streaming indicator binding moves to `ChatInputViewModel.IsStreaming` driven by reducer events so UI simply toggles `Visibility`.

## Safe Paste and Send Gating
- Introduce `IInputSafetyService` that inspects pasted text for max length, secrets heuristics, and multi-line code fences. Service can request confirmation when thresholds exceeded, leveraging `IApprovalService` if policy demands.
- `ChatInputViewModel` owns `PasteCommand` that calls `IClipboardService.GetTextAsync()`, passes through safety checks (size > 5k chars, contains `BEGIN PRIVATE KEY`, etc.), and either inserts sanitized text or opens confirmation dialog using mediator.
- `SendCommand` uses guard `CanSend => !IsSending && InputText.Trim().Length > 0 && InputSafetyState == Allowed`. Reasoning effort/model selectors update `SendPayload` before serialization so CLI formatting remains in service.
- Gating results (denied, warned, confirmed) surface as `StatusTurnModel` entries so history shows why a send was blocked.

## Approval Hook Points
- `ChatTranscriptViewModel` raises `ChatActionRequested` messages (`SubmitPrompt`, `Retry`, `CopySensitiveText`) that route through `IApprovalService` when the current `CodexOptions.ApprovalMode` is stricter than `Chat`.
- Assistant-suggested actions (Apply Diff, Run Exec, Open URL) create `ApprovalRequest` objects referencing the source turn. Resolutions feed back via mediator to the originating module (Diff/Exec) ensuring consistent UI messaging.
- When policy requires manual review, `ChatActionLock` objects keep send commands disabled until approval resolves; the approvals banner view-model subscribes to the same request IDs for UI alignment.
- Rejections add `StatusTurnModel` entries with guidance (e.g., “Exec denied by policy”) and log telemetry via `ITelemetryService.RecordApprovalOutcome`.

## Transcript Persistence and Trimming
- `IChatTranscriptRepository` persists conversations per workspace in `%AppData%/Codex/transcripts/{solutionId}.json` with encryption-at-rest if telemetry privacy demands.
- On load, repository hydrates `ChatTranscriptState` into store which then rehydrates view-models; a lightweight migration layer upgrades stored schema versions.
- Maintain rolling window: keep last 50 turns or 40k characters (configurable), trimming oldest turns while preserving any pinned (bookmarked) messages.
- Auto-save triggers on `TurnFinalized`, `ActionTaken`, and every 30 seconds of inactivity via background scheduler; saves use `SafeFileWriter` to avoid corruption.
- Diff/exec linkage metadata persists so resuming sessions can rehydrate context or prompt user to refresh stale data.

## Clipboard Operations Mapping
- Replace direct `Clipboard.SetText` calls with `IClipboardService` abstraction supporting async APIs, unit testing, and fallback for VS automation restrictions.
- `CopyAllCommand` streams `ChatTranscriptFormatter` output (markdown/plain) and reports size before copying; large payloads route through `IApprovalService` for confirmation.
- Each assistant turn exposes `CopyMessageCommand` that depends on `IClipboardService` and surfaces operation status via `StatusTurnModel` rather than button animations embedded in code-behind.

## Telemetry Counters
- `ITelemetryService` records:
  - `chat.turn_started` / `chat.turn_completed` with tokens, latency, model, reasoning effort.
  - `chat.delta_received` counts and bytes per turn to monitor streaming health.
  - `chat.retry_initiated`, `chat.retry_success`, `chat.retry_abort` for resend workflow.
  - `chat.clipboard_copy`, `chat.clipboard_copy_all`, `chat.copy_blocked_sensitive` for auditing.
  - `chat.approval_requested` / `chat.approval_resolved` segmented by outcome and policy.
  - `chat.stream_error` with categorized failure reasons and retry decision.
- View-model emits telemetry through mediator to keep UI layer passive; telemetry batching happens in service layer to reduce noise.

## Testing Strategy
- Unit tests for `ChatTranscriptReducer` verifying delta buffering, finalization, trimming, and status injection (use sample event fixtures).
- Tests for `IInputSafetyService` heuristics covering large paste, secret detection, and confirmation flow (mock approval responses).
- View-model tests using `DispatcherQueue` fake to assert property changes and command availability transitions.
- Replay tests loading recorded CLI transcripts (jsonl) to ensure deterministic rendering and persistence round-tripping.
- Contract tests validating `IClipboardService` integration (no STA requirement) and telemetry emission counts via mock `ITelemetryService`.

## Public API for Tool Window Binding
- `ChatTranscriptViewModel`
  - `ReadOnlyObservableCollection<ChatTurnViewModel> Turns`
  - `ChatTurnViewModel? SelectedTurn`
  - `AsyncRelayCommand SendCommand`, `AsyncRelayCommand RetryLastCommand`, `ICommand CopyAllCommand`, `ICommand LoadOlderHistoryCommand`
  - `bool IsStreaming`, `bool CanRetry`, `string TokenUsageSummary`, `string StatusMessage`
- `ChatInputViewModel`
  - `string InputText`, `AsyncRelayCommand PasteCommand`, `AsyncRelayCommand AttachFileCommand`, `AsyncRelayCommand InsertPromptCommand`
  - `ObservableCollection<ModelChoiceViewModel> Models`, `ModelChoiceViewModel SelectedModel`
  - `ReasoningLevel SelectedReasoning`, `bool IsSendEnabled`
- `ChatTurnViewModel`
  - `ChatTurnId Id`, `ChatRole Role`, `DateTime TimestampLocal`, `ObservableCollection<ChatSegmentViewModel> Segments`
  - `ICommand CopyMessageCommand`, `ICommand RunSuggestedActionCommand`, `bool IsStreaming`, `bool IsPinned`
- Public surface intentionally XAML-friendly: uses `ObservableObject` from CommunityToolkit and no direct `Dispatcher` calls from view-model consumers.

## Summary
- Design pivots chat UX to a testable MVVM structure aligned with the Task T2 mediator/state-store strategy.
- Streaming, approvals, persistence, telemetry, and clipboard behaviors move into dedicated services so the tool window becomes a thin shell over `ChatTranscriptViewModel` and companions.
- Next steps: implement `ChatTranscriptReducer` prototype, wire into `CodexSessionStore`, and start migrating existing code-behind to the new commands incrementally.
