# Approval Flow Service Plan

## Purpose
Task T13 requires a standalone approval flow service that replaces the ad-hoc logic embedded in `MyToolWindowControl`. This plan describes the target architecture, operational rules, UI contract, telemetry, and testing strategy necessary to deliver reliable approval handling for both exec commands and patch applications.

## Current State Snapshot
- Approval queues, remembered decisions, and UI banners are all managed by the control (`ToolWindows/MyToolWindowControl.Approvals.cs:16-206`, `ToolWindows/MyToolWindowControl.xaml.cs:229-3437`).
- Exec approvals originate in `HandleExecApproval` (`ToolWindows/MyToolWindowControl.Exec.cs:18-115`), while patch approvals arrive via `HandleApplyPatchApproval` (`ToolWindows/MyToolWindowControl.xaml.cs:1241-1393`).
- Auto-approval depends on `CodexOptions.Mode` and remembered dictionaries (`ToolWindows/MyToolWindowControl.Approvals.cs:185-229`).
- UI banners (`ShowApprovalBanner`, `FullAccessBanner`) are manipulated directly through `FindName` (`ToolWindows/MyToolWindowControl.Approvals.cs:60-109`, `ToolWindows/MyToolWindowControl.xaml.cs:7295-7375`).
- Patch approvals trigger diff application inline (`ToolWindows/MyToolWindowControl.Approvals.cs:150-157`), and manual discard loops through the queue (`ToolWindows/MyToolWindowControl.xaml.cs:3285-3409`).

## 1. Approval Manager Design
- Introduce `ApprovalCoordinator` service with explicit exec and patch channels. Each request is represented by a `PendingApproval` record containing `ApprovalType`, `CallId`, `Signature`, `Prompt`, `Metadata`.
- Service exposes async APIs:
  - `Task QueueAsync(PendingApproval request);`
  - `Task AutoResolveAsync(PendingApproval request, ApprovalDecision decision);`
  - `event EventHandler<ApprovalPrompt>` signalling UI prompts.
- Exec and patch logic share queue infrastructure but allow pluggable callbacks (`IApprovalHandler`) so features (exec console module, diff module) register completion delegates.
- CLI integration: service depends on `ICodexCliMessenger` abstraction to send `ApprovalSubmissionFactory` payloads, decoupling UI from CLI pipe.

## 2. Session Memory & Reset Handling
- Extract remembered approvals into `IApprovalMemoryStore` with session-level dictionaries plus optional persisted scopes (future work). Store tracks exec and patch decisions keyed by normalized signatures, aligning with existing logic (`ToolWindows/MyToolWindowControl.Approvals.cs:222-229`).
- Provide explicit `Reset(SessionResetKind kind)` to clear memory when CLI restarts or workspace changes, replacing manual dictionary clears (`ToolWindows/MyToolWindowControl.xaml.cs:1953-1977`).
- Support opt-in persistence by emitting `ApprovalMemorySnapshot` to options service when mode changes.

## 3. UI Contract for Prompts & Banners
- Define `ApprovalPromptViewModel` with properties `{ Message, CanRemember, IsVisible, Options }` and commands `Approve`, `Deny`, `Cancel`. View binds to this VM instead of calling `ShowApprovalBanner` directly.
- `ApprovalCoordinator` raises `ApprovalPromptChanged` on dispatcher-safe context; UI layer simply updates the view-model.
- Full access banner becomes a reactive view-model (`FullAccessWarningViewModel`) subscribed to `IOptionsObserver`, replacing `UpdateFullAccessBannerAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:7295-7367`).
- Provide contract for `ApprovalSummaryBar` to display queue length and last decision, improving discoverability of pending actions.

## 4. Concurrency Rules for Queued Requests
- Service processes approvals sequentially; queue implemented as `Channel<PendingApproval>` (or `AsyncQueue`) to ensure FIFO matching current behavior (`ToolWindows/MyToolWindowControl.Approvals.cs:73-106`).
- Allow preemption for same call ID (e.g., patch cancellation) by supporting `TryRemove(callId)`—mirrors discard loop (`ToolWindows/MyToolWindowControl.xaml.cs:3333-3393`).
- Ensure only one active prompt at a time; subsequent requests wait until `CompleteAsync`. Provide `IAuthorityToken` pattern so modules can guarantee they own the active prompt when they call `ResolveAsync`.

## 5. Telemetry Hooks for Approval Decisions
- Emit structured telemetry via `TelemetryAggregator` when:
  - Request enqueued (captures type, signature hash, auto-approval flags).
  - Decision delivered (manual/auto, approval result, latency).
  - Memory reset events (count of entries cleared).
- Replace textual logging `LogAutoApprovalAsync` & `LogManualApprovalAsync` with telemetry events plus diagnostics sink fallback (`ToolWindows/MyToolWindowControl.Approvals.cs:231-259`).
- Provide correlation IDs to diff/exec modules for cross-feature analytics.

## 6. Coordination with Chat & Diff Modules
- Chat module: coordinator publishes `CanSend` state when auth gating or approval backlog should disable send; replaces `_authGatedSend` toggles and ensures chat input view-model observes approval backlog.
- Diff module: register a `PatchApprovalHandler` so when `ApprovalCoordinator` resolves a patch, it triggers diff apply through module API (`DiffModule.PatchApplyCoordinator.BeginAsync`). This removes direct call to `ApplySelectedDiffsAsync` (`ToolWindows/MyToolWindowControl.Approvals.cs:150-153`).
- Exec module: subscribe to exec approvals to hydrate exec view-model with pending/approved statuses (replacing status bar strings in `HandleExecApproval`).

## 7. Unit Tests for Remembered Decisions
- Cover `ApprovalMemoryStore` with tests verifying:
  - Signatures normalize and persist decisions across queue resets.
  - Reset clears memory according to reset kind (workspace vs CLI).
  - Memory respects approval mode transitions (Agent ↔ FullAccess) by automatically approving when appropriate.
- Mock coordinator to ensure remembered signatures short-circuit queue (mirrors `TryResolveExecApproval` & `TryResolvePatchApproval`).

## 8. Integration Tests with CLI Approval Events
- Simulate CLI event sequence for exec and patch approvals ensuring:
  - Auto-approval sends CLI response immediately, no prompt displayed.
  - Manual approval triggers prompt, user interaction resolves and notifies CLI.
  - Patch rejection removes pending diff application and diff module receives cancellation.
- Verify concurrency by enqueuing multiple approvals and asserting ordered CLI emissions, matching current pipeline in `HandleExecApproval`/`HandleApplyPatchApproval`.

## 9. Full Access Warning Workflow
- Separate full access notification into workflow state: `FullAccessState` enumerates {Hidden, PendingConfirmation, Active}. When options enter `AgentFullAccess`, coordinator asks user for confirmation once per session before enabling auto approvals; updates UI banner accordingly.
- Banner content becomes localized resource; clicking “Review Settings” opens options panel. Coordinator logs telemetry event whenever full access is toggled.
- Provide API for other modules to query `IsFullAccessEnabled` to adjust messaging (exec console, diff module).

## 10. Deliverable & Next Steps
- Artifact: `docs/approval-service-plan.md` (this document).
- Next actions:
  1. Scaffold `ApprovalCoordinator`, `ApprovalMemoryStore`, and view-models.
  2. Integrate with diff/exec modules as outlined.
  3. Migrate UI bindings to new view-models and retire `_approvalQueue`, `_activeApproval`, and banner helpers.
