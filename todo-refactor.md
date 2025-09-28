# MyToolWindowControl Refactor Execution Plan

## [x] Task R1 - Infrastructure & Solution Scaffold (Agent A)
- Prompt: Task R1 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: None (kickoff task).
- [x] Review `docs/refactor-strategy.md` and `baseline.md` for scope confirmation.
- [x] Create folder structure under `ToolWindows/CodexToolWindow` and `Modules/*` per strategy doc.
- [x] Add placeholder XAML views (shell, chat, diff, exec, approvals, MCP) with minimal layout.
- [x] Add new projects/items to `.csproj` with `Compile`/`Page` entries; keep legacy files intact for incremental migration.
- [x] Introduce `Shared/Cli`, `Shared/Messaging`, `Shared/Options`, `Shared/Telemetry` namespaces with empty stubs.
- [x] Update StyleCop/EditorConfig rules to cover new folders.
- [x] Ensure build still succeeds (`dotnet build` or msbuild) after scaffolding.

## [ ] Task R2 - Codex CLI Host Service (Agent A)
- Prompt: Task R2 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Task R1 scaffolding.
- [ ] Implement `ICodexCliHost`, `CliSessionService`, and supporting contracts from `docs/cli-host-spec.md`.
- [ ] Relocate serialization helpers from legacy partials into `Shared/Cli`.
- [ ] Replace direct `CodexCliHost` usage with service registrations in `CodexVS22Package`.
- [ ] Provide `ICliMessageRouter` emitting typed envelopes for modules.
- [ ] Add unit tests for connection, reconnection, heartbeat, and diagnostics flow.
- [ ] Document migration summary in `docs/cli-host-spec.md` (append "Implementation Notes" section).

## [ ] Task R3 - State Stores & Mediator (Agent B)
- Prompt: Task R3 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R1-R2 (CLI host & scaffolding).
- [ ] Implement `CodexSessionStore`, `WorkspaceContextStore`, and `OptionsCache` per `docs/state-ownership-map.md`.
- [ ] Create mediator `ICodexSessionCoordinator` handling CLI state, workspace updates, and option changes.
- [ ] Wire stores to consume events from Task R2 services.
- [ ] Provide thread-safe change notifications (e.g., `IObservable` or dispatcher-synchronized events).
- [ ] Add tests covering workspace transitions, option updates, and store resets.

## [ ] Task R4 - Chat Transcript Module (Agent B)
- Prompt: Task R4 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R2-R3 (CLI + stores).
- [ ] Implement `ChatTranscriptViewModel`, models, and services defined in `docs/chat-viewmodel-design.md`.
- [ ] Migrate chat handlers from legacy partials to new module using mediator events.
- [ ] Recreate safe-paste, send gating, transcript persistence hooks.
- [ ] Bind new chat view to view-model and ensure streaming updates via dispatcher.
- [ ] Cover with unit tests and protocol replay tests per design doc.

## [ ] Task R5 - Diff Review Module (Agent C)
- Prompt: Task R5 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R2-R3 (CLI + stores); coordinate with R8 when ready.
- [ ] Build services/view-models per `docs/diff-module-plan.md`.
- [ ] Implement diff parsing, tree management, and patch apply coordination detached from UI.
- [ ] Integrate approvals for apply gating via new approval service.
- [ ] Provide golden diff tests and integration coverage for patch transactions.
- [ ] Hook up new diff view into shell layout.

## [ ] Task R6 - Exec Console Module (Agent D)
- Prompt: Task R6 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R2-R3 (CLI + stores); coordinate with R8 approvals.
- [ ] Implement view-model and formatter per `docs/exec-module-plan.md`.
- [ ] Offload ANSI/base64 decoding to background service.
- [ ] Support cancellation, export, copy, and telemetry updates.
- [ ] Write tests for long output, ANSI formatting, and cancellation flows.
- [ ] Replace legacy exec handlers with module integration.

## [ ] Task R7 - MCP Tools & Prompt Module (Agent E)
- Prompt: Task R7 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R2-R3 (CLI + stores).
- [ ] Follow `docs/mcp-module-plan.md` to implement services and view-models.
- [ ] Handle refresh debounce, caching, hover help, and accessibility requirements.
- [ ] Add integration tests using mock CLI responses.
- [ ] Bind MCP and prompt panels in new UI.

## [ ] Task R8 - Approval Flow Service (Agent C)
- Prompt: Task R8 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Task R3 (stores/mediator).
- [ ] Implement approval manager per `docs/approval-service-plan.md`.
- [ ] Provide API for exec/diff/chat modules to request/resolve approvals.
- [ ] Migrate remembered decision logic into service, with persistence hooks via options store.
- [ ] Add unit/integration tests for approval lifecycle and telemetry.

## [ ] Task R9 - Session Persistence & Resume (Agent D)
- Prompt: Task R9 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R3-R8 (stores + feature modules).
- [ ] Implement persistence layer per `docs/session-persistence-plan.md`.
- [ ] Serialize transcript/diff/exec state to disk respecting privacy redactions.
- [ ] Integrate resume logic into mediator and view-models.
- [ ] Test resume across VS restarts (unit + integration).

## [ ] Task R10 - Options & Settings Integration (Agent E)
- Prompt: Task R10 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R2-R3 (CLI + stores).
- [ ] Rework options page bindings to new stores per `docs/options-integration-plan.md`.
- [ ] Ensure Test Connection, JSON import/export, and reset flows use new services.
- [ ] Provide diagnostics logging hooks via telemetry service.
- [ ] Update unit tests covering validation and persistence precedence.

## [ ] Task R11 - Telemetry & Diagnostics Pipeline (Agent A)
- Prompt: Task R11 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R2-R3 (CLI + stores).
- [ ] Implement telemetry aggregator per `docs/telemetry-diagnostics-plan.md`.
- [ ] Centralize Diagnostics pane logging via `ICliDiagnosticsSink` and module hooks.
- [ ] Enforce redaction, throttling, and opt-in control paths.
- [ ] Add tests verifying counters, rate limiting, and reporting contracts.

## [ ] Task R12 - UI Shell Composition (Agent B)
- Prompt: Task R12 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R3-R11 (stores, modules, telemetry).
- [ ] Assemble shell view + view-model per `docs/ui-shell-plan.md`.
- [ ] Wire child views, banners, status indicators, and window persistence logic.
- [ ] Ensure accessibility peers and keyboard navigation survive.
- [ ] Remove legacy code-behind reliance on direct UI manipulation.

## [ ] Task R13 - Command Routing & VS Package Integration (Agent C)
- Prompt: Task R13 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R4-R12 (modules + shell).
- [ ] Update VSCT handlers, AsyncPackage wiring per `docs/command-routing-plan.md`.
- [ ] Ensure Add to Chat, Diagnostics, and context commands target new modules.
- [ ] Validate command telemetry and shortcut functionality.
- [ ] Adjust tests for command invocation paths.

## [ ] Task R14 - Testing Harness & Coverage (Agent D)
- Prompt: Task R14 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R4-R13 (modules, approvals, shell, commands).
- [ ] Implement test suites per `docs/testing-harness-plan.md`.
- [ ] Add hermetic CLI mocks, replay fixtures, and UI automation smoke tests.
- [ ] Configure coverage gates and categorize tests for CI.

## [ ] Task R15 - Build & CI Pipeline Updates (Agent E)
- Prompt: Task R15 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R11 and R14 (telemetry + test harness).
- [ ] Apply `docs/build-ci-update-plan.md` adjustments to csproj and workflows.
- [ ] Ensure Release build produces symbols, signed VSIX, and new artifacts.
- [ ] Update CI to run new test matrices (Windows + WSL where applicable).

## [ ] Task R16 - Documentation & Onboarding (Agent A)
- Prompt: Task R16 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R12-R15 (shell, commands, CI).
- [ ] Update README, CONTRIBUTING, SECURITY, etc., following `docs/docs-onboarding-plan.md`.
- [ ] Add architecture diagrams/screenshots reflecting new UI.
- [ ] Record troubleshooting updates and parity notes.

## [ ] Task R17 - Execution Coordination & Final Validation (Agent B)
- Prompt: Task R17 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R1-R16 (full stack).
- [ ] Follow `docs/execution-roadmap.md` to schedule integration checkpoints.
- [ ] Run full regression (chat/diff/exec/approvals) in Experimental VS and WSL fallback.
- [ ] Complete CHANGELOG + SemVer bump and prepare release notes.
- [ ] Confirm TODO_VisualStudio.md and todo-refactor.md status reflects completion.
