# Diff Experience Module Plan

## Scope & Goals
Task T8 decomposes the diff experience currently baked into `MyToolWindowControl` into dedicated services and view-models. The module must own diff parsing, tree presentation, Visual Studio diff viewer integration, patch application, telemetry, and approval coordination. This plan documents the target design and satisfies every subtask from Task T8.

## Current Implementation Highlights
- Source pointers: `ToolWindows/MyToolWindowControl.Mcp.cs:152`, `ToolWindows/MyToolWindowControl.xaml.cs:2033`, `ToolWindows/MyToolWindowControl.xaml.cs:4065`, `ToolWindows/MyToolWindowControl.xaml.cs:4681`, `ToolWindows/MyToolWindowControl.xaml.cs:7091`, `ToolWindows/MyToolWindowControl.xaml.cs:7227`, `ToolWindows/MyToolWindowControl.Approvals.cs:114`
- Diff events arrive through `HandleTurnDiff` where raw payloads are filtered (`ProcessDiffDocumentsAsync`) and pushed into a mutable tree (`UpdateDiffTreeAsync`). All logic lives on the control and assumes UI-thread affinity (`ToolWindows/MyToolWindowControl.Mcp.cs:152`).
- Tree items combine selection, hierarchy, and document payloads in one `DiffTreeItem` type, with checkbox propagation implemented directly against WPF elements (`ToolWindows/MyToolWindowControl.xaml.cs:2033`).
- Patch apply reuses command handlers within the control to walk selections, resolve full paths, and write files (`ToolWindows/MyToolWindowControl.xaml.cs:4065`). Conflict detection relies on snapshot comparisons inside `ApplyDocumentTextAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:4681`).
- Visual Studio diff viewers are opened inline using `IVsDifferenceService` without abstraction, and temporary files are dropped into `%TEMP%\CodexVS22\Diffs` without cleanup (`ToolWindows/MyToolWindowControl.xaml.cs:7091`, `ToolWindows/MyToolWindowControl.xaml.cs:7227`).
- Approval gating invokes `ApplySelectedDiffsAsync` directly after a patch approval resolves (`ToolWindows/MyToolWindowControl.Approvals.cs:114`).

These observations inform the proposed module boundaries.

## 1. Diff Parsing Service Layer
**Objective:** Split diff parsing into a service so CLI payload handling is headless and testable.

- Introduce `IDiffDocumentParser` with `Task<IReadOnlyList<DiffDocument>> ParseAsync(JToken payload, DiffParseOptions options)`. Implementation wraps current `DiffUtilities.ExtractDocuments` plus filtering and normalization (`ToolWindows/MyToolWindowControl.Mcp.cs:232`).
- Move filtering (binary skip, empty diff) and normalization (path splitting, dedupe) from the control into the parser. Service exposes hooks for future ignore rules.
- Parser runs off the UI thread; consumers receive immutable `DiffSessionSnapshot` containing `ImmutableDictionary<string, DiffDocument>` and tree metadata. UI adapts snapshot on dispatcher.

## 2. Diff Tree View-Model with Checkbox Logic
**Objective:** Plan a dedicated view-model to own selectable diff hierarchy.

- Define `DiffTreeViewModel` exposing `ReadOnlyObservableCollection<DiffNodeView>` for binding. Each node holds `Name`, `RelativePath`, `IsDirectory`, `IsChecked`, `HasConflicts`, and `Children`.
- Checkbox state propagation moves into view-model methods (`SetChecked(bool?)`) instead of WPF event handlers. Use a recursive state machine that mirrors logic currently inside `DiffTreeItem` (`ToolWindows/MyToolWindowControl.xaml.cs:2033`).
- Introduce `IDiffSelectionService` to compute counts and selections; replace `CountSelectedDiffFiles` and `GetSelectedDiffDocuments` (currently inline at `ToolWindows/MyToolWindowControl.xaml.cs:3149` and `ToolWindows/MyToolWindowControl.xaml.cs:3197`).
- Support tri-state checkboxes by tracking aggregate selection state per node. View-model raises change notifications to UI while maintaining dispatcher affinity via a synchronization context wrapper.

## 3. VS Diff Service Interface
**Objective:** Define an interface isolating Visual Studio diff viewer usage.

- Create `IVsDiffViewerService` with methods:
  - `Task ShowDiffAsync(DiffDocument doc, DiffViewOptions options);`
  - `Task<bool> IsViewerOpenAsync(string path);`
- Implementation encapsulates current `IVsDifferenceService` integration and temporary file wiring (`ToolWindows/MyToolWindowControl.xaml.cs:7091`). Options capture caption, labels, and reuse policy.
- Provide extension to inject difference service via MEF/DI for unit testing by mocking the interface.

## 4. Temp File Storage & Cleanup Policy
**Objective:** Determine storage strategy for diff temp files.

- Replace ad-hoc `%TEMP%\CodexVS22\Diffs` with `ITempFileManager` that scopes files to a diff session ID. The manager deletes files on session disposal or VS shutdown to avoid orphaned files (currently `CreateTempDiffFile` never cleans up, `ToolWindows/MyToolWindowControl.xaml.cs:7227`).
- Persist temp files under `%LocalAppData%\Codex\Diffs` to allow multi-file sessions, ensuring unique subdirectories per session. Provide explicit `CleanupAsync(sessionId)` invoked when diff tree resets.
- For long-lived diff viewers, expose `ITempFileHandle` that disposes lazily when the diff window closes, using `IVsWindowFrame` events to trigger cleanup.

## 5. Patch Apply Transaction Boundaries
**Objective:** Outline apply flow managed as a transaction.

- Introduce `PatchApplyCoordinator` service owning states currently tracked by `_patchApplyInProgress`, `_patchApplyStartedAt`, `_patchApplyExpectedFiles` (`ToolWindows/MyToolWindowControl.xaml.cs:3661`).
- Transaction stages:
  1. `BeginAsync(selectionSnapshot)` – captures selected docs, logs telemetry, updates status banner.
  2. `ApplyAsync(doc)` – runs on background thread per file, returning `PatchOutcome` (Applied, Conflict, Failed) mirroring switch at `ToolWindows/MyToolWindowControl.xaml.cs:4221`.
  3. `CommitAsync(results)` – updates UI, telemetry, approvals, and resets diff tree (currently in `CompletePatchApplyProgressAsync`, `ToolWindows/MyToolWindowControl.xaml.cs:3797`).
- Provide rollback semantics: if apply fails mid-run, coordinator restores diff selection and surfaces aggregated errors without partial clearing.

## 6. Conflict Detection Surface & Messaging
**Objective:** Standardize conflict detection and user messaging.

- Retain normalized snapshot comparison in service form, abstracted behind `IPatchConflictDetector` originating from `ApplyDocumentTextAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:4681`).
- Expose conflict artifacts (e.g., path, detected divergence) to view-model so UI can mark nodes in tree with warning badges.
- Provide messaging pipeline using centralized `IDiffNotificationService`, replacing inline status text updates and diagnostics writes (`ToolWindows/MyToolWindowControl.xaml.cs:4333`).
- On conflict, automatically open VS diff viewer with current vs Codex content if user opts in, leveraging the diff viewer service.

## 7. Telemetry Hooks for Diff Workflows
**Objective:** Document telemetry integration.

- Move telemetry calls from `_telemetry` aggregator to a diff-focused tracker with events: `DiffSessionStarted`, `DiffDocumentViewed`, `PatchApplyStarted`, `PatchApplyCompleted`, `PatchConflict`, `PatchFailure` (current logging at `ToolWindows/MyToolWindowControl.xaml.cs:3661` and `ToolWindows/MyToolWindowControl.xaml.cs:4017`).
- Each event records counts, durations, selection sizes, and conflict/failure reasons. Data feeds into existing telemetry pipeline used by `TelemetryTracker`.
- Provide structured properties rather than string logs, enabling analytics on conflict frequency and apply success rate.

## 8. Automated Test Matrix
**Objective:** List required automated test coverage.

- **Unit tests**
  - Parser handles binary/empty diffs, path normalization, dedupe (covering logic at `ToolWindows/MyToolWindowControl.Mcp.cs:232`).
  - Diff tree checkbox propagation across nested directories (mirrors `DiffTreeItem` behaviors at `ToolWindows/MyToolWindowControl.xaml.cs:2033`).
  - Patch conflict detection when original text diverges (current check at `ToolWindows/MyToolWindowControl.xaml.cs:4697`).
- **Golden tests**
  - Snapshot-based diff tree rendering for complex hierarchies ensuring stable ordering (`BuildDiffTree`, `ToolWindows/MyToolWindowControl.xaml.cs:2549`).
  - Patch apply transcripts verifying telemetry payload shapes.
- **Integration tests**
  - Simulated CLI diff payload pipeline from event to tree to viewer (mock `IVsDiffViewerService`).
  - Patch approval flow where `ApprovalCoordinator` approves/denies requests and ensures coordinator reacts (current join point at `ToolWindows/MyToolWindowControl.Approvals.cs:114`).
  - Temp file cleanup validation verifying manager deletes session files when diff session resets.

## 9. Approval Coordination for Apply Gating
**Objective:** Ensure diff module cooperates with approval flow service.

- Replace direct call to `ApplySelectedDiffsAsync` inside `ResolveActiveApprovalAsync` with message-based handoff: approvals publish `PatchApprovalGranted`, diff module subscribes and invokes `PatchApplyCoordinator.BeginAsync` (`ToolWindows/MyToolWindowControl.Approvals.cs:150`).
- Provide `IPatchRequest` payload that includes signature, call ID, selected docs, and remembered decisions so module can resume mid-session if approvals queue clears.
- When user discards pending patch (`ToolWindows/MyToolWindowControl.xaml.cs:3608`), diff module notifies approval service to auto-deny outstanding patch requests to avoid stale prompts.

## 10. Deliverable: diff-module-plan.md
This document (`docs/diff-module-plan.md`) is the required artifact for Task T8. Upon implementation, it should guide engineers migrating diff logic out of the tool window.

## Next Steps & Ownership
1. Scaffold `DiffModule` folder containing parser, selection service, view-model, diff viewer adapter, patch coordinator, and telemetry hooks.
2. Wire services into dependency injection container used by the tool window.
3. Partner with Task T13 (Approval Flow Service) and Task T5 (Threading Risk) to ensure threading and approval contracts align.
4. Schedule test plan implementation per matrix above.
