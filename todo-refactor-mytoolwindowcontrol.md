# MyToolWindowControl Refactor Master Plan

## [x] Task T1 - Baseline Anatomy (Either, Agent A)
- Prompt: Task T1 - Execute every subtask in Task T1 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Capture current file and partial counts with line metrics
- [x] Inventory public, internal, and private members for exposure map
- [x] Trace CLI, diff, exec, and MCP entry points into the control
- [x] Map UI element names to handlers and data sources
- [x] Record threading contexts for every async method
- [x] Note cross-partial dependencies and shared static state
- [x] Identify duplicate helpers or shadowed utilities
- [x] List external services (VS SDK, toolkit, Codex host)
- [x] Flag regions likely to break during extraction
- [x] Summarize findings in baseline.md for downstream teams

## [x] Task T2 - Decomposition Strategy (Either, Agent B)
- Prompt: Task T2 - Execute every subtask in Task T2 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Define target architecture (views, view-models, services)
- [x] Propose namespace and folder layout for new modules
- [x] Decide on MVVM vs MVVM-lite binding approach
- [x] Specify required interfaces for CLI host integration
- [x] Outline diff pipeline separation (parsing, view, apply)
- [x] Plan exec console responsibilities post-split
- [x] Determine shared state store or mediator pattern usage
- [x] Document telemetry and approval flow touchpoints
- [x] Produce sequencing diagram for event routing
- [x] Publish strategy summary to docs/refactor-strategy.md

## [x] Task T3 - State Ownership Map (Either, Agent C)
- Prompt: Task T3 - Execute every subtask in Task T3 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Catalogue all fields and classify by feature domain
- [x] Decide future home for each state bucket
- [x] Define immutable vs mutable state boundaries
- [x] Identify initialization order requirements
- [x] Mark state that must persist across sessions
- [x] Flag state requiring thread affinity safeguards
- [x] Propose backing models for diff and exec collections
- [x] Document state transitions during lifecycle events
- [x] Align state map with options and persistence rules
- [x] Deliver state-ownership.xlsx for planners

## [x] Task T4 - Binding Inventory (Either, Agent D)
- Prompt: Task T4 - Execute every subtask in Task T4 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [ ] Audit XAML bindings and code-behind lookups
- [ ] List commands, click handlers, and routed events
- [ ] Determine bindings needing conversion to ICommand
- [ ] Identify controls requiring view-model properties
- [ ] Mark UI elements referencing static helpers
- [ ] Note data templates and item sources in use
- [ ] Capture validation rules or converters referenced
- [ ] Flag accessibility automation peers to preserve
- [ ] Plan binding changes for diff tree and exec console
- [ ] Commit inventory to docs/xaml-binding-map.md

## [x] Task T5 - Threading Risk Assessment (Either, Agent E)
- Prompt: Task T5 - Execute every subtask in Task T5 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] List all async void methods and justify retention
- [x] Enumerate ThreadHelper switches and contexts
- [x] Identify background operations needing services layer
- [x] Flag shared collections lacking synchronization
- [x] Review timer and heartbeat usage for race risks
- [x] Document UI access requirements post-refactor
- [x] Recommend task factories or schedulers per feature
- [x] Assess CLI reconnect path for multi-thread safety
- [x] Define thread-safe logging and telemetry patterns
- [x] Summarize risks in docs/threading-assessment.md

## [x] Task T6 - CLI Host Extraction (Either, Agent A)
- Prompt: Task T6 - Execute every subtask in Task T6 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Design CodexCliHost facade interface for DI
- [x] Specify lifetime and ownership semantics
- [x] Plan message pump decoupling from UI thread
- [x] Identify serialization helpers to relocate
- [x] Outline reconnect and heartbeat responsibilities
- [x] Define diagnostics channel contract post-extraction
- [x] Enumerate tests required for new CLI service
- [x] Detail error propagation strategy to UI layer
- [x] Map options integration to host factory
- [x] Produce cli-host-spec.md for implementation phase

## [x] Task T7 - Chat Transcript ViewModel (Either, Agent B)
- Prompt: Task T7 - Execute every subtask in Task T7 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Define turn models (user, assistant, status)
- [x] Plan streaming delta handling without UI coupling
- [x] Specify safe paste and send gating logic
- [x] Design approval hook points for chat actions
- [x] Determine transcript persistence and trimming rules
- [x] Map clipboard operations to service helpers
- [x] Outline telemetry counters for chat view-model
- [x] Identify required unit and replay tests
- [x] Document public API for tool window binding
- [x] Draft chat-viewmodel-design.md deliverable

## [x] Task T8 - Diff Experience Module (Either, Agent C)
- Prompt: Task T8 - Execute every subtask in Task T8 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Split diff parsing into dedicated service layer
- [x] Plan diff tree view-model with checkbox logic
- [x] Define interface for Visual Studio diff service usage
- [x] Determine storage for temp files and cleanup policy
- [x] Outline patch apply transaction boundaries
- [x] Specify conflict detection surface and messaging
- [x] Document telemetry hooks for diff workflows
- [x] List automated tests (unit, golden, integration)
- [x] Coordinate with approvals for apply gating
- [x] Publish diff-module-plan.md artifact

## [x] Task T9 - Exec Console Module (Either, Agent D)
- Prompt: Task T9 - Execute every subtask in Task T9 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Design exec turn view-model and buffer limits
- [x] Plan ANSI rendering pipeline outside UI code
- [x] Define exec cancellation and status flow
- [x] Map output export and copy actions to services
- [x] Align exec telemetry with new module boundaries
- [x] Identify shared utility reuse vs duplication
- [x] Specify diagnostics logging for exec events
- [x] Determine tests for long running output scenarios
- [x] Document diff between CLI exec and VS output window
- [x] Produce exec-module-plan.md for contributors

## [x] Task T10 - MCP and Prompts Module (Either, Agent E)
- Prompt: Task T10 - Execute every subtask in Task T10 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Model MCP tool metadata and run records
- [x] Plan prompt library view-model interactions
- [x] Define refresh debounce and caching strategy
- [x] Coordinate option persistence for selections
- [x] Outline error handling for missing servers
- [x] Specify telemetry for tool and prompt usage
- [x] Enumerate integration tests with mocked CLI
- [x] Document accessibility and keyboard needs
- [x] Align hover help with new data providers
- [x] Deliver mcp-module-plan.md summary

## [x] Task T11 - Options and Settings Integration (Either, Agent A)
- Prompt: Task T11 - Execute every subtask in Task T11 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Map options page fields to new service interfaces
- [x] Ensure thread-safe load and save routines
- [x] Define defaults sync between UI and stored values
- [x] Coordinate per-solution overrides with state store
- [x] Document validation pipeline within options layer
- [x] Plan Test Connection workflow with new abstractions
- [x] Outline JSON import/export hooks for settings
- [x] Align approval mode persistence with new model
- [x] Specify telemetry for option changes
- [x] Publish options-integration-plan.md

## [x] Task T12 - Telemetry and Diagnostics Wiring (Either, Agent B)
- Prompt: Task T12 - Execute every subtask in Task T12 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Inventory existing telemetry counters and events
- [x] Decide owner service for telemetry aggregation
- [x] Plan diagnostic pane logging entry points
- [x] Define rate limiting and redaction helpers
- [x] Map errors to user-facing banners post-refactor
- [x] Outline opt-in telemetry control flow updates
- [x] Identify test coverage for telemetry accuracy
- [x] Align CLI stderr handling with diagnostics module
- [x] Document reporting expectations for CI pipelines
- [x] Produce telemetry-diagnostics-plan.md

## [x] Task T13 - Approval Flow Service (Either, Agent C)
- Prompt: Task T13 - Execute every subtask in Task T13 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Design approval manager with exec and patch support
- [x] Define session memory and reset handling
- [x] Specify UI contract for prompts and banners
- [x] Determine concurrency rules for queued requests
- [x] Map telemetry hooks for approvals decisions
- [x] Coordinate with chat and diff modules for callbacks
- [x] Identify unit tests for remembered decisions
- [x] Plan integration tests with CLI approval events
- [x] Document full access warning workflow
- [x] Publish approval-service-plan.md

## [x] Task T14 - Session Persistence and Resume (Either, Agent D)
- Prompt: Task T14 - Execute every subtask in Task T14 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Catalog data required to restore sessions
- [x] Define serialization format for transcript cache
- [x] Plan storage location respecting privacy rules
- [x] Coordinate resume flow with CLI reconnect logic
- [x] Outline versioning strategy for stored sessions
- [x] Map telemetry to persistence load failures
- [x] Specify background save cadence and triggers
- [x] Identify tests for resume across VS restarts
- [x] Document fallback when resume fails gracefully
- [x] Produce session-persistence-plan.md

## [x] Task T15 - UI Shell and Window Orchestration (Either, Agent E)
- Prompt: Task T15 - Execute every subtask in Task T15 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Design tool window composition root
- [x] Map dispatcher usage for cross-thread updates
- [x] Plan host window event hooks in new architecture
- [x] Coordinate layout persistence with view-model state
- [x] Define spinner and status indicator bindings
- [x] Specify accessibility automation peers ownership
- [x] Align clear chat and copy all actions with VM
- [x] Document resizing and layout persistence logic
- [x] Outline tests for window open/close sequences
- [x] Publish ui-shell-plan.md deliverable

## [x] Task T16 - Command Routing and Integration (Either, Agent A)
- Prompt: Task T16 - Execute every subtask in Task T16 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Audit VSCT commands against new module APIs
- [x] Plan AsyncPackage command handlers refactor
- [x] Define Add to Chat command data flow post-split
- [x] Coordinate diagnostics command with services layer
- [x] Map toolbar and context menu updates to bindings
- [x] Ensure keyboard shortcuts remain functional
- [x] Document command ownership per module
- [x] Identify tests for command invocation paths
- [x] Align command telemetry with new architecture
- [x] Publish command-routing-plan.md

## [x] Task T17 - Testing Strategy and Harness (Either, Agent B)
- Prompt: Task T17 - Execute every subtask in Task T17 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Define unit test targets for each new module
- [x] Plan integration tests with mock Codex CLI
- [x] Outline WPF UI automation smoke coverage
- [x] Ensure coverage gates align with TODO rules
- [x] Specify golden diff fixtures for diff module
- [x] Map vstest categories for CI pipelines
- [x] Document hermetic setup for approvals and exec
- [x] Plan parallel test execution safeguards
- [x] Identify tooling for replaying protocol transcripts
- [x] Produce testing-harness-plan.md

## [x] Task T18 - Build, Packaging, and CI Updates (Either, Agent C)
- Prompt: Task T18 - Execute every subtask in Task T18 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Adjust csproj includes for new files and folders
- [x] Update StyleCop and analyzers for new layout
- [x] Plan msbuild targets for module resource files
- [x] Revise CI scripts for additional test suites
- [x] Coordinate VSIX asset list for refactored content
- [x] Ensure symbol and source indexing remain correct
- [x] Document signing implications post-refactor
- [x] Plan release pipeline validation against new modules
- [x] Align coverage reports with restructured code
- [x] Publish build-ci-update-plan.md

## [x] Task T19 - Documentation and Developer Onboarding (Either, Agent D)
- Prompt: Task T19 - Execute every subtask in Task T19 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Update README with new architecture overview
- [x] Refresh contributing guide with module owners
- [x] Document troubleshooting for refactored features
- [x] Provide parity notes vs VS Code implementation
- [x] Create diagrams for chat/diff/exec flows
- [x] Update CHANGELOG placeholders for refactor release
- [x] Plan blog or release notes narrative structure
- [x] Align docs with telemetry and privacy updates
- [x] Prepare onboarding checklist for new contributors
- [x] Produce docs-onboarding-plan.md

## [x] Task T20 - Execution Roadmap and Coordination (Either, Agent E)
- Prompt: Task T20 - Execute every subtask in Task T20 located in @todo-refactor-mytoolwindowcontrol.md, deliver required artifacts, and flip this task header and per item to [x] when complete.
- [x] Sequence module implementation order across agents
- [x] Define integration checkpoints and handoffs
- [x] Plan daily sync artifacts (standups, blockers)
- [x] Establish definition of done per module
- [x] Coordinate code review ownership per task group
- [x] Outline risk mitigation and rollback plans
- [x] Align resource needs (tooling, licenses, test rigs)
- [x] Schedule CI capacity and environment bookings
- [x] Prepare post-refactor validation checklist
- [x] Publish execution-roadmap.md
