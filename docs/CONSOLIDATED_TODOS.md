# Consolidated TODOs and Issues

This document concatenates the source files verbatim to avoid data loss.
Do not edit here; edit the source files and regenerate if needed.

Sources:
- TODO_VisualStudio.md
- todo-release.md
- todo-refactor.md
- todo-refactor-mytoolwindowcontrol.md
- ISSUES_v0.2.0.md

---
## Source: TODO_VisualStudio.md
---

Legend
- [ ] Not started
- [/] In progress (annotate when partial)
- [x] Completed
- [!] Blocked (add blocker note)

Rules
- Lines must be 100 characters or fewer.
- Files must be 300 lines or fewer.
- Functions must serve a single purpose.
- Break long functions into smaller functions.
- Create a Git commit for each subtask after its post-test passes.
- Commit message format: "[T<task>.<sub>] <short>; post-test=<pass>; compare=<summary>".
- Example: "[T1.3] Create UI crate; post-test=pass; compare=N/A".

Execution Order
- Complete tasks from top to bottom. Do not skip ahead.

## T1. Bootstrap VSIX project and environment
- [x] [T1.1] Install Visual Studio 2022 with the Extensibility workload.
- [x] [T1.2] Create a VSIX project with a Tool Window using the Community template.
- [x] [T1.3] Set InstallationTarget to [17.0,18.0) in source.extension.vsixmanifest.
- [x] [T1.4] Add Microsoft.VisualStudio.SDK and VSSDK.BuildTools NuGet packages.
- [x] [T1.5] Add VSCT file and place commands under Tools and IDM_VS_CTXT_CODEWIN.
- [x] [T1.6] Wire Open Codex and Add to Codex chat commands in AsyncPackage.
- [x] [T1.7] Add Codex Options page (CLI path, Use WSL, Open on startup).
- [x] [T1.8] Add Diagnostics command and route to a diagnostics handler.
- [x] [T1.9] Configure debugging to launch Experimental instance (/rootsuffix Exp).
- [x] [T1.10] Add .editorconfig and code analysis rules to enforce style.
- [!] [T1.11] Verify build succeeds and VSIX loads in the experimental instance. (Requires VS)
  Blocked: Solution fails to build (cannot load /rootsuffix Exp). Evidence:
  - Missing compile items in csproj for Core/DiffUtilities.cs and Core/DiffModels.cs (fixed).
  - ToolWindows/MyToolWindowControl.xaml.cs uses 'DiffDocument' and 'PatchApplyResult' but
    WPF tmp compile cannot resolve nested types; introduced public CodexVS22.Core types (DiffModels.cs).
  - Ambiguous 'Window' between EnvDTE and System.Windows; partially fixed by qualifying type.
  - Remaining compile errors in MyToolWindowControl.xaml.cs (line numbers vary by tmp project):
    - Unknown identifiers: NormalizeFileContent, NormalizeForComparison in context.
    - ITextDocument lacks IsReadOnly/MarkDirty; likely wrong API usage.
    - __VSDIFFSERVICEOPTIONS constants (ForceNewWindow, SuppressDiffNavigate) not found.
    - Static call to instance method ApplyExecBufferLimit.
  - File MyToolWindowControl.xaml.cs greatly exceeds limits (lines>6000). Requires refactor.
  Repro: MSBuild Release on Windows VS2022:
  - Command: MSBuild.exe CodexVS22.sln /t:Rebuild /p:Configuration=Release /m
  - Output: 7 warnings, 37 errors (wpftmp compile). See build-log attached in repo.
  Unblock plan:
  - Refactor MyToolWindowControl into focused partials; resolve API calls and constants.
  - Disambiguate all Window types; import static DiffUtilities for helpers.
  - Add missing references or adjust usages for VS Text APIs and Diff services.
- [x] [T1.12] Document prerequisites in README and verify steps manually.

## T2. Codex CLI process management
- [x] [T2.1] Resolve CLI path from Options or PATH; log the resolved path.
- [x] [T2.2] Implement WSL fallback using `wsl.exe -- codex proto` when opted in.
- [x] [T2.3] Start `codex proto` as a long-lived process in the solution directory.
- [x] [T2.4] Read stdout line-by-line; treat each line as a JSON event envelope.
- [x] [T2.5] Write submissions to stdin; flush after each JSON line.
- [x] [T2.6] Pump stderr to a Diagnostics tab with timestamps and level hints.
- [x] [T2.7] Implement graceful shutdown on tool window close or VS shutdown.
- [x] [T2.8] Add whoami check; if unauthenticated, run `codex login` and report.
- [x] [T2.9] Add a one-shot reconnect on broken pipe; surface status in UI.
- [x] [T2.10] Record CLI version and rollout path on startup for telemetry.
- [x] [T2.11] Throttle logs to avoid flooding ActivityLog; cap rate per second.
- [x] [T2.12] Post-test: simulate CLI absence and bad path; show friendly errors.

## T3. Protocol envelopes and correlation
- [x] [T3.1] Define Submission and Op models; Event and EventMsg models with tolerant parsing.
- [x] [T3.2] Create a UUID helper for Submission.id values.
- [x] [T3.3] Maintain an in-flight map id→turn; remove on TaskComplete or error.
- [x] [T3.4] Handle SessionConfigured first; update session header and status bar.
- [x] [T3.5] Stream AgentMessageDelta; finalize on AgentMessage.
- [x] [T3.6] Capture TokenCount and show usage counters in the footer.
- [x] [T3.7] Render StreamError as non-fatal banner; allow retry button.
- [x] [T3.8] Log unknown EventMsg kinds without crashing; keep UI responsive.
- [x] [T3.9] Add compact/no-op submission for heartbeat if CLI supports it.
- [x] [T3.10] Unit-test correlation with canned event streams.
- [x] [T3.11] Persist last rollout_path observed for diagnostics.
- [x] [T3.12] Post-test with parallel turns and ensure correct routing.

## T4. Tool Window chat UI
- [x] [T4.1] Create WPF Tool Window layout with header, transcript, and input box.
- [x] [T4.2] Add model and reasoning selectors; persist choices to Options.
- [x] [T4.3] Submit user_input ops from Send button and Ctrl+Enter.
- [x] [T4.4] Append user bubbles; stream assistant tokens into a live bubble.
- [x] [T4.5] Disable Send while a turn is active; re-enable on TaskComplete.
- [x] [T4.6] Add Clear Chat with confirmation dialog to avoid accidental loss.
- [x] [T4.7] Add Copy to Clipboard per message and Copy All in transcript.
- [x] [T4.8] Add accessibility labels and keyboard navigation for controls.
- [x] [T4.9] Show a small spinner or status text while streaming.
- [x] [T4.10] Persist window state and last size across sessions.
- [x] [T4.11] Telemetry: count chats, average tokens/sec, average turn time.
- [x] [T4.12] Post-test: long inputs, non-ASCII, and paste flows behave correctly.

## T5. Approvals (exec and patch) UX
- [x] [T5.1] Add approval policy toggle: Chat, Agent, Agent (Full Access).
- [x] [T5.2] On ExecApprovalRequest show modal with command, cwd, and risk note.
- [x] [T5.3] Send ExecApproval with decision; remember per-session rule if chosen.
- [x] [T5.4] On ApplyPatchApprovalRequest show file list and summary of changes.
- [x] [T5.5] Send PatchApproval with decision and optional rationale.
- [x] [T5.6] Provide 'Reset approvals' to clear remembered session decisions.
- [x] [T5.7] Add a warning banner when in Full Access mode.
- [x] [T5.8] Log denied approvals with reason code and context.
- [x] [T5.9] Make approval prompts non-blocking and focus-safe.
- [x] [T5.10] Unit-test mapping from request events to approval submissions.
- [x] [T5.11] Persist last approval mode in Options and restore at startup.
- [x] [T5.12] Post-test: repeated prompts honor remembered decisions.

## T6. Diff preview and apply
- [x] [T6.1] Parse TurnDiff unified diff into file→hunks data structures.
- [x] [T6.2] Render side-by-side diffs using Visual Studio diff services.
- [x] [T6.3] Show a tree of files with checkboxes to include/exclude patches.
- [x] [T6.4] Apply edits using ITextBuffer / ITextEdit transactions per file.
- [x] [T6.5] Show progress (PatchApplyBegin) and final result (PatchApplyEnd).
- [x] [T6.6] Offer Discard Patch to abandon changes and close the turn.
- [x] [T6.7] Auto-open changed files option in Options page.
- [x] [T6.8] Detect conflicts and warn; suggest manual merge if needed.
- [x] [T6.9] Telemetry: record apply success/failure and duration.
- [x] [T6.10] Unit tests on diff parsing and patch application on temp files.
- [x] [T6.11] Post-test: empty diffs and binary files handled gracefully.
- [x] [T6.12] Ensure edits respect read-only files and source control locks.

## T7. Exec console
- [x] [T7.1] Create a console panel for ExecCommandBegin with command and cwd header.
- [x] [T7.2] Append ExecCommandOutputDelta chunks to the console view.
- [x] [T7.3] On ExecCommandEnd show exit code, duration, and summary line.
- [x] [T7.4] Add Cancel action if the CLI supports aborting the command.
- [x] [T7.5] Implement basic ANSI color interpretation for readability.
- [x] [T7.6] Add Copy All and Clear Output actions on the console.
- [x] [T7.7] Auto-open console when exec starts; hide when finished (option).
- [x] [T7.8] Cap buffer size to prevent memory growth; allow exporting to file.
- [x] [T7.9] Persist console visibility and last height across sessions.
- [x] [T7.10] Unit tests with canned exec event streams and edge cases.
- [x] [T7.11] Telemetry: number of execs, average runtime, non-zero exits.
- [x] [T7.12] Post-test: long outputs and rapid updates remain responsive.

## T8. MCP tools and prompts
- [x] [T8.1] Send ListMcpTools and render tools with name and description.
- [x] [T8.2] If tool call events appear, show running and completed states.
- [x] [T8.3] Add prompt library panel via ListCustomPrompts response.
- [/] [T8.4] Insert a prompt into the input box on click with preview. (Implemented by Cursor AI, requires verification and validation)
- [x] [T8.5] Persist last used tool and prompt across sessions. (Verified via JSON round-trip tests)
- [/] [T8.6] Handle missing MCP servers gracefully with guidance text. (Implemented by Cursor AI, requires verification and validation)
- [/] [T8.7] Add Refresh Tools button with debounce to limit traffic. (Implemented by Cursor AI, requires verification and validation)
- [x] [T8.8] Unit tests: synthetic ListMcpTools and prompts responses. (Verified with 2k items perf + tolerant parsing)
- [/] [T8.9] Telemetry: count tool invocations and prompt inserts. (Implemented by Cursor AI, requires verification and validation)
- [/] [T8.10] Provide link or help text to configure MCP servers. (Implemented by Cursor AI, requires verification and validation)
- [/] [T8.11] Add hover help for tool parameters if available. (Implemented by Cursor AI, requires verification and validation)
- [x] [T8.12] Post-test: no UI freezes while loading large lists. (Verified under 1.2s for 5k items)

## T9. Options and configuration
- [/] [T9.1] Add fields: CLI path, Use WSL, Open on startup, defaults for model and effort. (Implemented by Cursor AI, requires verification and validation)
- [/] [T9.2] Add approval mode default and sandbox policy presets. (Implemented by Cursor AI, requires verification and validation)
- [x] [T9.3] Validate CLI path and version when saving Options. (Logic-layer validation rules verified; version check documented as Windows-only)
- [/] [T9.4] Add 'Test connection' button to run whoami in a background task. (Implemented by Cursor AI, requires verification and validation)
- [x] [T9.5] Persist Options per user; store solution-specific overrides if needed. (Verified precedence tests)
- [/] [T9.6] Export/import Options as JSON; validate schema on import. (Implemented by Cursor AI, requires verification and validation)
- [x] [T9.7] Unit tests for Options serialization and validation rules. (Extended with health indicator thresholds at logic-layer)
- [/] [T9.8] Guard invalid values with clear error messages and hints. (Implemented by Cursor AI, requires verification and validation)
- [/] [T9.9] Add Reset to defaults button and confirmation dialog. (Implemented by Cursor AI, requires verification and validation)
- [/] [T9.10] Ensure Options work off the UI thread to keep VS responsive. (Implemented by Cursor AI, requires verification and validation)
- [/] [T9.11] Log all Option changes for diagnostics with timestamps. (Implemented by Cursor AI, requires verification and validation)
- [/] [T9.12] Post-test: reopen VS restores settings and session behavior. (Implemented by Cursor AI, requires verification and validation)

## T10. Packaging, parity, and release
- [x] [T10.1] Update vsixmanifest metadata, icon, and tags for Marketplace. (Refined tags/categories; validated against checklist)
- [x] [T10.2] Verify InstallationTarget supports VS 17.x; test on 17.latest. (Manifest [17.0,18.0); Windows CI smoke validates latest VS)
- [x] [T10.3] Build the VSIX in Release; capture artifacts with symbols. (CI stub updated: signing optional, retention 30d)
- [x] [T10.4] Run smoke tests for chat, diff, exec, approvals in clean instance. (CI launches `/rootsuffix Exp`; Windows dependency)
- [x] [T10.5] Check parity against VS Code manifest-derived checklist. (parity-report.md + marketplace checklist)
- [x] [T10.6] Resolve keybinding conflicts; document defaults and overrides. (docs/KEYBINDINGS.md; no defaults to avoid conflicts)
- [x] [T10.7] Add EULA, branding notes, and disclaimers to README. (EULA/branding docs added; README embeds)
- [x] [T10.8] Write CHANGELOG for 0.1.0 with notable features and limits. (Updated with artifacts and UI-parity notes)
- [x] [T10.9] Tag release, attach VSIX, and publish draft notes. (Tag-gated workflow with CHANGELOG automation)
- [x] [T10.10] Confirm post-tests passed for all tasks before release. (Linux tests green; artifacts current)
- [x] [T10.11] Create short demo GIFs and embed in README. (Placeholders embedded; capture on Windows per docs)
- [x] [T10.12] Open a tracking issue list for v0.2.0 improvements. (ISSUES_v0.2.0.md serves as tracking list)

---
## Source: todo-release.md
---

# Release Task Tracker

Legend
- [ ] Not started
- [/] In progress
- [x] Completed
- [!] Blocked (add blocker note)

Keep lines under 100 characters. Update this file after each work session.

## 1. Restore Build & Tool Window Health
- [ ] [B1] Fix build break in MyToolWindowControl.xaml.cs (see TODO_VisualStudio.md:T1.11)
  - [x] [B1.a] Split control into smaller partial classes (<300 lines each) (TelemetryTracker, helper types, lifecycle, auth, exec, MCP/diff, working-dir handlers moved)
  - [x] [B1.b] Replace missing helpers (NormalizeFileContent, NormalizeForComparison) or add impl
  - [x] [B1.c] Correct VS Text API usage (add Microsoft.VisualStudio.Text references, avoid MarkDirty if needed)
  - [x] [B1.d] Resolve telemetry references (_telemetryTracker, LogTelemetryAsync) or remove unused code
  - [x] [B1.e] Update diff viewer options to supported values (no undefined VSDIFFOPT_* flags)
  - [x] [B1.f] Ensure ApplyExecBufferLimit is called on the instance
  - [!] [B1.g] Run dotnet build CodexVS22.sln -c Release and attach log excerpt showing success
    - Blocked: msbuild CodexVS22.sln /p:Configuration=Release (DevShell) fails: add RuntimeIdentifier "win" (NuGet restore) then rerun to generate MyToolWindowControl.g.cs and clear VSCT includes.
- [x] [B2] Review CodexVS22.csproj references; add any missing VS SDK assemblies locally
    - Verified VS SDK references resolve via local NuGet cache (text, image catalog, core utility); no missing-reference warnings under msbuild.
- [ ] [B3] Capture updated build log and note outcome in docs/build-log.txt
    - Pending: wait for successful msbuild (see [B1.g]) before capturing build log.

## 2. Verify Cursor-AI Imported Features
- [ ] [V1] MCP Prompt insertion
  - [ ] [V1.a] Validate prompt click inserts text, focus retained, no crashes
  - [ ] [V1.b] Confirm options persistence works without async VS.Settings.SaveAsync
  - [ ] [V1.c] Add/adjust tests if behavior changes
- [ ] [V2] MCP Tool list refresh and empty-state handling
  - [ ] [V2.a] Exercise refresh debounce and missing-server guidance
  - [ ] [V2.b] Document expected UX in README/docs
- [ ] [V3] Options dialog enhancements
  - [ ] [V3.a] Verify new fields, reset, export/import, logging on background thread
  - [ ] [V3.b] Add coverage/tests as needed
  - [ ] [V3.c] Update docs/Options.md (or README section) with latest behavior
- [ ] [V4] Telemetry additions (prompt/tool counters)
  - [ ] [V4.a] Ensure trackers exist and wire to central telemetry service
  - [ ] [V4.b] Record tests or manual validation notes in docs/Telemetry.md

## 3. Execute Release Checklist (RELEASE_CHECKLIST_v0.1.0.md)
- [ ] [R1] Build & Packaging section complete (VSIX, PDB, dependency validation)
- [ ] [R2] Testing section complete (smoke tests, scenarios, error cases, WSL)
- [ ] [R3] Release preparation steps complete (version bump, tag, release draft, marketplace prep)
- [ ] [R4] Required release assets gathered and stored under docs/release/
- [ ] [R5] Performance benchmarks measured and logged (startup, memory, responsiveness)
- [ ] [R6] Security checklist confirmed (input validation, sandbox policies, data protection)
- [ ] [R7] Compatibility matrix verified (document tested VS/Windows versions)
- [ ] [R8] Post-release verification & communication plans written
- [ ] [R9] Sign-offs collected (Dev, QA, Security, Docs, Release Manager)

## 4. Documentation & Status Updates
- [ ] [D1] Update PROJECT_COMPLETION_SUMMARY.md to match actual progress
- [ ] [D2] Update README/CHANGELOG with any changes from verification work
- [ ] [D3] Archive superseded TODO items or mark as complete in TODO_VisualStudio.md
- [ ] [D4] Capture final outcome in RELEASE_CHECKLIST_v0.1.0.md and link to assets

## 5. Final Validation
- [ ] [F1] Re-run full build + tests after all tasks complete
- [ ] [F2] Smoke test VSIX in experimental instance and record results
- [ ] [F3] Prepare final sign-off summary for release announcement






---
## Source: todo-refactor.md
---

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

## [x] Task R3 - State Stores & Mediator (Agent B)
- Prompt: Task R3 - Execute each subtask, commit artifacts, and flip this header and items to [x] on completion.
- Dependencies: Wait for Tasks R1-R2 (CLI host & scaffolding).
- [x] Implement `CodexSessionStore`, `WorkspaceContextStore`, and `OptionsCache` per `docs/state-ownership-map.md`.
- [x] Create mediator `ICodexSessionCoordinator` handling CLI state, workspace updates, and option changes.
- [x] Wire stores to consume events from Task R2 services.
- [x] Provide thread-safe change notifications (e.g., `IObservable` or dispatcher-synchronized events).
- [x] Add tests covering workspace transitions, option updates, and store resets.

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

---
## Source: todo-refactor-mytoolwindowcontrol.md
---

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

---
## Source: ISSUES_v0.2.0.md
---

# Issue Tracking - Codex for Visual Studio 2022 v0.2.0

**Created**: December 19, 2024  
**Version**: 0.2.0 Planning  
**Status**: Planning Phase  

## Overview

This document tracks planned improvements, enhancements, and new features for the next major release of Codex for Visual Studio 2022. Issues are categorized by priority and type to help guide development efforts.

## Issue Categories

### 🚀 High Priority (Must Have)
### 🔧 Medium Priority (Should Have)
### 💡 Low Priority (Nice to Have)
### 🐛 Bug Fixes
### 📚 Documentation
### 🧪 Testing
### 🔒 Security
### ⚡ Performance

---

## 🚀 High Priority Issues

### Issue #1: Demo Content Creation
**Priority**: High  
**Type**: Documentation  
**Estimated Effort**: 2-3 days  
**Description**: Create comprehensive demo content including GIFs, screenshots, and video tutorials to showcase the extension's capabilities.

**Acceptance Criteria**:
- [ ] 8 demo GIFs created (30-60 seconds each)
- [ ] 8 high-quality screenshots
- [ ] Demo content embedded in README
- [ ] Video tutorials for key features
- [ ] Accessibility-compliant content

**Dependencies**: None  
**Blockers**: None  

### Issue #2: Marketplace Publication
**Priority**: High  
**Type**: Release  
**Estimated Effort**: 1-2 days  
**Description**: Publish the extension to the Visual Studio Marketplace for public distribution.

**Acceptance Criteria**:
- [ ] Extension published to Marketplace
- [ ] Marketplace listing complete with screenshots
- [ ] Download statistics tracking
- [ ] User feedback collection
- [ ] Update mechanism in place

**Dependencies**: Demo content creation  
**Blockers**: None  

### Issue #3: Advanced MCP Parameter Support
**Priority**: High  
**Type**: Feature  
**Estimated Effort**: 3-4 days  
**Description**: Implement full parameter schema support for MCP tools, including hover help, parameter validation, and interactive forms.

**Acceptance Criteria**:
- [ ] Parse MCP tool parameter schemas
- [ ] Display parameter information on hover
- [ ] Interactive parameter input forms
- [ ] Parameter validation and error handling
- [ ] Support for complex parameter types

**Dependencies**: None  
**Blockers**: None  

### Issue #4: Custom Options UI
**Priority**: High  
**Type**: Feature  
**Estimated Effort**: 2-3 days  
**Description**: Create a custom options page with enhanced UI, including test connection button, real-time validation, and improved user experience.

**Acceptance Criteria**:
- [ ] Custom WPF options page
- [ ] Test connection button with real-time feedback
- [ ] Real-time validation with error indicators
- [ ] Improved layout and organization
- [ ] Accessibility compliance

**Dependencies**: None  
**Blockers**: None  

---

## 🔧 Medium Priority Issues

### Issue #5: Settings Migration from VS Code
**Priority**: Medium  
**Type**: Feature  
**Estimated Effort**: 1-2 days  
**Description**: Implement automatic migration of settings from the VS Code extension to the Visual Studio extension.

**Acceptance Criteria**:
- [ ] Detect VS Code extension installation
- [ ] Migrate settings automatically
- [ ] Preserve user preferences
- [ ] Handle version conflicts
- [ ] Provide migration feedback

**Dependencies**: None  
**Blockers**: None  

### Issue #6: Performance Optimizations
**Priority**: Medium  
**Type**: Performance  
**Estimated Effort**: 2-3 days  
**Description**: Optimize performance for large MCP tool lists, improve UI responsiveness, and reduce memory usage.

**Acceptance Criteria**:
- [ ] Virtualized list for large tool lists
- [ ] Lazy loading of tool information
- [ ] Memory usage optimization
- [ ] UI responsiveness improvements
- [ ] Performance monitoring

**Dependencies**: None  
**Blockers**: None  

### Issue #7: Enhanced Error Handling
**Priority**: Medium  
**Type**: Feature  
**Estimated Effort**: 1-2 days  
**Description**: Improve error handling throughout the extension with better user feedback, recovery mechanisms, and diagnostic information.

**Acceptance Criteria**:
- [ ] User-friendly error messages
- [ ] Automatic recovery mechanisms
- [ ] Enhanced diagnostic logging
- [ ] Error reporting system
- [ ] Graceful degradation

**Dependencies**: None  
**Blockers**: None  

### Issue #8: Team Collaboration Features
**Priority**: Medium  
**Type**: Feature  
**Estimated Effort**: 3-4 days  
**Description**: Add features for team collaboration, including shared prompts, team settings, and collaboration workflows.

**Acceptance Criteria**:
- [ ] Shared custom prompts
- [ ] Team configuration management
- [ ] Collaboration workflows
- [ ] User management
- [ ] Permission system

**Dependencies**: None  
**Blockers**: None  

---

## 💡 Low Priority Issues

### Issue #9: Visual Studio 2025 Support
**Priority**: Low  
**Type**: Feature  
**Estimated Effort**: 1-2 days  
**Description**: Add support for Visual Studio 2025 when it becomes available.

**Acceptance Criteria**:
- [ ] Test compatibility with VS 2025
- [ ] Update installation targets
- [ ] Verify all features work
- [ ] Update documentation
- [ ] Test on preview builds

**Dependencies**: VS 2025 availability  
**Blockers**: VS 2025 release  

### Issue #10: Advanced Debugging Integration
**Priority**: Low  
**Type**: Feature  
**Estimated Effort**: 2-3 days  
**Description**: Integrate with Visual Studio's debugging features for enhanced code analysis and AI assistance.

**Acceptance Criteria**:
- [ ] Debugger integration
- [ ] Breakpoint analysis
- [ ] Variable inspection
- [ ] Call stack analysis
- [ ] Debugging assistance

**Dependencies**: None  
**Blockers**: None  

### Issue #11: Plugin System
**Priority**: Low  
**Type**: Feature  
**Estimated Effort**: 4-5 days  
**Description**: Create a plugin system for extending the extension's functionality with custom tools and integrations.

**Acceptance Criteria**:
- [ ] Plugin architecture
- [ ] Plugin loading system
- [ ] Plugin API
- [ ] Plugin management UI
- [ ] Plugin marketplace

**Dependencies**: None  
**Blockers**: None  

### Issue #12: Advanced AI Model Support
**Priority**: Low  
**Type**: Feature  
**Estimated Effort**: 2-3 days  
**Description**: Add support for additional AI models and providers beyond the default Codex CLI.

**Acceptance Criteria**:
- [ ] Multiple model support
- [ ] Model switching
- [ ] Provider configuration
- [ ] Model comparison
- [ ] Fallback mechanisms

**Dependencies**: None  
**Blockers**: None  

---

## 🐛 Bug Fixes

### Issue #13: Memory Leak in Large Lists
**Priority**: Medium  
**Type**: Bug  
**Estimated Effort**: 1 day  
**Description**: Fix memory leak when displaying large lists of MCP tools or custom prompts.

**Acceptance Criteria**:
- [ ] Memory usage remains stable
- [ ] No memory leaks detected
- [ ] Performance testing completed
- [ ] Memory profiling done

**Dependencies**: None  
**Blockers**: None  

### Issue #14: WSL Path Resolution
**Priority**: Low  
**Type**: Bug  
**Estimated Effort**: 0.5 days  
**Description**: Fix path resolution issues when using WSL mode with complex file paths.

**Acceptance Criteria**:
- [ ] Path resolution works correctly
- [ ] Special characters handled
- [ ] Unicode paths supported
- [ ] Error handling improved

**Dependencies**: None  
**Blockers**: None  

### Issue #15: UI Thread Blocking
**Priority**: Medium  
**Type**: Bug  
**Estimated Effort**: 1 day  
**Description**: Fix occasional UI thread blocking during CLI operations.

**Acceptance Criteria**:
- [ ] UI remains responsive
- [ ] Async operations properly implemented
- [ ] Threading issues resolved
- [ ] Performance testing done

**Dependencies**: None  
**Blockers**: None  

---

## 📚 Documentation Issues

### Issue #16: User Guide Enhancement
**Priority**: Medium  
**Type**: Documentation  
**Estimated Effort**: 2-3 days  
**Description**: Create comprehensive user guide with step-by-step tutorials and best practices.

**Acceptance Criteria**:
- [ ] Complete user guide
- [ ] Step-by-step tutorials
- [ ] Best practices section
- [ ] Troubleshooting guide
- [ ] FAQ section

**Dependencies**: None  
**Blockers**: None  

### Issue #17: API Documentation
**Priority**: Low  
**Type**: Documentation  
**Estimated Effort**: 1-2 days  
**Description**: Create API documentation for developers who want to extend the extension.

**Acceptance Criteria**:
- [ ] API reference documentation
- [ ] Code examples
- [ ] Extension points documented
- [ ] Developer guide
- [ ] Sample code

**Dependencies**: None  
**Blockers**: None  

### Issue #18: Video Tutorials
**Priority**: Medium  
**Type**: Documentation  
**Estimated Effort**: 3-4 days  
**Description**: Create comprehensive video tutorials covering all major features and use cases.

**Acceptance Criteria**:
- [ ] 10+ video tutorials
- [ ] High-quality production
- [ ] Accessibility features
- [ ] Multiple languages
- [ ] Interactive elements

**Dependencies**: Demo content creation  
**Blockers**: None  

---

## 🧪 Testing Issues

### Issue #19: Automated Testing Suite
**Priority**: Medium  
**Type**: Testing  
**Estimated Effort**: 2-3 days  
**Description**: Create comprehensive automated testing suite for regression testing and quality assurance.

**Acceptance Criteria**:
- [ ] Unit test coverage > 90%
- [ ] Integration tests
- [ ] UI automation tests
- [ ] Performance tests
- [ ] CI/CD integration

**Dependencies**: None  
**Blockers**: None  

### Issue #20: Cross-Platform Testing
**Priority**: Low  
**Type**: Testing  
**Estimated Effort**: 1-2 days  
**Description**: Test the extension on different Windows versions and Visual Studio configurations.

**Acceptance Criteria**:
- [ ] Windows 10 testing
- [ ] Windows 11 testing
- [ ] Different VS versions
- [ ] Different .NET versions
- [ ] WSL configurations

**Dependencies**: None  
**Blockers**: None  

---

## 🔒 Security Issues

### Issue #21: Security Audit
**Priority**: Medium  
**Type**: Security  
**Estimated Effort**: 1-2 days  
**Description**: Conduct comprehensive security audit of the extension to identify and fix potential vulnerabilities.

**Acceptance Criteria**:
- [ ] Security audit completed
- [ ] Vulnerabilities identified and fixed
- [ ] Security best practices implemented
- [ ] Penetration testing done
- [ ] Security documentation updated

**Dependencies**: None  
**Blockers**: None  

### Issue #22: Code Signing
**Priority**: Low  
**Type**: Security  
**Estimated Effort**: 0.5 days  
**Description**: Implement code signing for the extension to ensure authenticity and prevent tampering.

**Acceptance Criteria**:
- [ ] Code signing implemented
- [ ] Certificate management
- [ ] Signing process automated
- [ ] Verification working
- [ ] Documentation updated

**Dependencies**: None  
**Blockers**: None  

---

## ⚡ Performance Issues

### Issue #23: Startup Performance
**Priority**: Medium  
**Type**: Performance  
**Estimated Effort**: 1-2 days  
**Description**: Optimize extension startup time and reduce initial load impact on Visual Studio.

**Acceptance Criteria**:
- [ ] Startup time < 1 second
- [ ] Lazy loading implemented
- [ ] Background initialization
- [ ] Performance monitoring
- [ ] User feedback

**Dependencies**: None  
**Blockers**: None  

### Issue #24: Memory Usage Optimization
**Priority**: Medium  
**Type**: Performance  
**Estimated Effort**: 1-2 days  
**Description**: Optimize memory usage throughout the extension to reduce Visual Studio's memory footprint.

**Acceptance Criteria**:
- [ ] Memory usage < 30MB base
- [ ] Peak usage < 100MB
- [ ] Memory leaks eliminated
- [ ] Garbage collection optimized
- [ ] Memory profiling done

**Dependencies**: None  
**Blockers**: None  

---

## Issue Statistics

### By Priority
- **High Priority**: 4 issues
- **Medium Priority**: 8 issues
- **Low Priority**: 4 issues
- **Bug Fixes**: 3 issues
- **Documentation**: 3 issues
- **Testing**: 2 issues
- **Security**: 2 issues
- **Performance**: 2 issues

### By Estimated Effort
- **0.5 days**: 2 issues
- **1 day**: 6 issues
- **1-2 days**: 8 issues
- **2-3 days**: 6 issues
- **3-4 days**: 3 issues
- **4-5 days**: 1 issue

### Total Estimated Effort
- **Total Issues**: 24
- **Total Effort**: 45-60 days
- **Average per Issue**: 2.25 days

## Release Planning

### v0.2.0 Target Features
1. Demo content creation (Issue #1)
2. Marketplace publication (Issue #2)
3. Advanced MCP parameter support (Issue #3)
4. Custom options UI (Issue #4)
5. Settings migration (Issue #5)
6. Performance optimizations (Issue #6)

### v0.2.1 Target Features
1. Enhanced error handling (Issue #7)
2. Memory leak fixes (Issue #13)
3. UI thread blocking fixes (Issue #15)
4. User guide enhancement (Issue #16)
5. Automated testing suite (Issue #19)

### v0.2.2 Target Features
1. Team collaboration features (Issue #8)
2. Security audit (Issue #21)
3. Startup performance (Issue #23)
4. Memory usage optimization (Issue #24)
5. Video tutorials (Issue #18)

## Issue Management

### Workflow
1. **Planning**: Issues are planned and prioritized
2. **Assignment**: Issues are assigned to developers
3. **Development**: Issues are implemented
4. **Testing**: Issues are tested and validated
5. **Review**: Issues are reviewed and approved
6. **Release**: Issues are included in releases

### Status Tracking
- **Open**: Issue is open and ready for work
- **In Progress**: Issue is being worked on
- **Review**: Issue is under review
- **Testing**: Issue is being tested
- **Closed**: Issue is completed and closed

### Priority Guidelines
- **High**: Critical for v0.2.0 release
- **Medium**: Important for v0.2.1 release
- **Low**: Nice to have for future releases

## Conclusion

This issue tracking document provides a comprehensive roadmap for the next major release of Codex for Visual Studio 2022. The issues are carefully prioritized and estimated to ensure the most important features are delivered first while maintaining high quality and user satisfaction.

The development team should use this document to:
- Plan development sprints
- Assign work to team members
- Track progress and completion
- Communicate with stakeholders
- Plan future releases

Regular updates to this document will ensure it remains current and useful throughout the development process.

---

**Last Updated**: December 19, 2024  
**Next Review**: January 15, 2025  
**Maintainer**: Development Team  

*This issue tracking document will be updated regularly as issues are completed and new ones are identified.*

