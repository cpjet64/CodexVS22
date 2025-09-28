# Options and Settings Integration Plan (Task T11)

## 1. Scope and Objectives
- Decouple the Visual Studio options page (`CodexOptions`) from UI code-behind so new services/view-models consume a typed options facade instead of reading dialog page state directly (`ToolWindows/MyToolWindowControl.xaml.cs:241`).
- Support both global and per-solution overrides (`Options/CodexOptions.cs:246-283`), exposing immutable snapshots to feature modules via DI.
- Guarantee thread-safe load/save flows when multiple background services (CLI host, workspace monitor, telemetry) react to option changes.
- Preserve existing export/import and validation behaviors while adding telemetry and “Test Connection” workflow hooks.

## 2. Mapping Options Fields to Services
| Option Field | Consumer(s) Today | Destination Service / Store |
| --- | --- | --- |
| `CliExecutable`, `UseWsl`, `SolutionCliExecutable`, `SolutionUseWsl` | `CodexCliHost` (`Core/CodexCliHost.cs:27`), tool window lifecycle (`ToolWindows/MyToolWindowControl.Lifecycle.cs:70`) | `ICliOptionsService` providing `CliConnectionSettings` snapshots for `ICodexCliHost` factory |
| `Mode` (`ApprovalMode`) | Approvals banner + exec/diff auto-approval logic (`ToolWindows/MyToolWindowControl.Approvals.cs:185`) | `IApprovalPolicyService` with observable `ApprovalPolicyState` consumed by approvals VM + CLI submission builder |
| `SandboxPolicy` | Future exec sandbox guard | `IExecPolicyService` for command gating |
| `DefaultModel`, `DefaultReasoning` | Chat send pipeline (`ToolWindows/MyToolWindowControl.Options.cs:31`) | `IChatPreferencesService` exposing defaults + change notifications |
| `AutoOpenPatchedFiles`, `AutoHideExecConsole`, `ExecConsoleVisible`, `ExecConsoleHeight`, `ExecOutputBufferLimit` | Diff/exec UI toggles (`ToolWindows/MyToolWindowControl.xaml.cs:545`, `ToolWindows/MyToolWindowControl.Exec.Helpers.cs:12`) | `ICodexLayoutStore` & `IExecConsolePreferences` to drive new view-model properties |
| Window geometry (`WindowWidth`, etc.) | Tool window layout management (`ToolWindows/MyToolWindowControl.Windowing.cs`) | `IToolWindowLayoutStore` managed by shell VM |
| `LastUsedTool`, `LastUsedPrompt` | MCP and prompt lists (`ToolWindows/MyToolWindowControl.Options.cs:74`) | `IMcpPreferencesService` and `IPromptLibraryService` |

All services source data from a shared `CodexOptionsCache` (read-only snapshots) to avoid each module touching `DialogPage`.

## 3. Thread-Safe Load/Save Strategy
- Introduce `CodexOptionsMonitor` implementing `IOptionsMonitor<CodexOptionsSnapshot>` (immutable record). It loads values on background thread using `JoinableTaskFactory.SwitchToMainThreadAsync` only when interacting with VS shell APIs.
- Employ a `ReaderWriterLockSlim` to guard cache updates so concurrent readers (CLI host, approval service) can access snapshots without blocking.
- Serialization of save operations:
  1. View-models raise intent -> services mutate domain stores.
  2. `CodexOptionsWriter` enqueues changes onto a dedicated background queue (`Channel<CodexOptionsMutation>`). Writer flushes to `DialogPage` on UI thread to comply with VS threading rules.
  3. After `SaveSettingsToStorage` completes, monitor publishes new snapshot via `OnOptionsChanged` event.
- Ensure host/services never block UI thread waiting for options; they await tasks with timeout + fallback to previous snapshot.

## 4. Defaults Synchronization
- Maintain a `CodexDefaultsCatalog` describing canonical defaults (mirrors `CodexOptions.ResetToDefaults`, `Options/CodexOptions.cs:291`).
- On snapshot creation, compute effective values (`GetEffectiveCliExecutable`, `GetEffectiveUseWsl`, etc.) and store both raw + effective fields for downstream consumers.
- Expose `ICodexDefaultsService` allowing UI to compare user-edited values vs defaults for visual indicators and “Reset” commands.
- When defaults change (e.g., new model), update catalog and add migration routine that adjusts stored values if left blank.

## 5. Per-Solution Override Coordination
- Create `ISolutionOptionsProvider` that queries `.vs/Codex/settings.json` (new storage) for overrides; merge with global snapshot using precedence: solution > workspace context > global.
- Workspace service raises `SolutionContextChanged` events; provider reloads overrides asynchronously and publishes merged snapshot to monitor.
- Provide `ICodexOptionsSerializer` responsible for persisting per-solution overrides with change tracking + file IO guarded by `JoinableTaskFactory` to respect VS threading (since DTE file operations require UI thread).

## 6. Validation Pipeline
- Retain `ValidateSettings()` checks but relocate to `CodexOptionsValidator` service that runs during save and import.
- On validation failure, accumulate `OptionsValidationIssue` list with severity + remediation text. Shell view-model surfaces these in UI (banner/toast) and telemetry (see §9).
- For CLI path validation, optionally prompt user with `File.Exists` check on background thread, then marshal results to UI for error message.
- Provide extensibility hooks so features can plug additional validators without modifying base options class.

## 7. Test Connection Workflow
- Implement `ITestConnectionService` using `ICodexCliHost` + `ICliOptionsService` to spin up ephemeral CLI process (or `codex login status`) verifying credentials.
- Options UI exposes `AsyncRelayCommand TestConnectionCommand` that disables button, calls service, and displays success/error result.
- Log attempts via telemetry (event `options_test_connection` with outcome, latency) and diagnostics sink for traceability.

## 8. JSON Import/Export Hooks
- Reuse existing `ExportToJson` / `ImportFromJson` (`Options/CodexOptions.cs:320-368`) but wrap in `CodexOptionsSerializer` so future modules can request export/import via command palette.
- Add schema versioning metadata when exporting to allow backward compatibility; store at root property `schema_version`.
- Validate imported JSON using `CodexOptionsValidator`; reject with descriptive message and ensure no partial state is applied.
- Provide CLI for headless import/export (reused in tests) through `ICodexOptionsSerializer` to support automation.

## 9. Approval Mode Persistence Alignments
- `IApprovalPolicyService` subscribes to options monitor; upon mode change, it updates approval state store and raises `ApprovalPolicyChanged` event to chat/diff/exec modules.
- For “remembered approvals” persistence, ensure service flushes caches when mode transitions from `AgentFullAccess` back to `Chat` to avoid stale data.
- Align UI combos (Task T6 spec) with view-model binding to service property rather than direct `_options` usage.

## 10. Telemetry for Option Changes
- Introduce `OptionsTelemetryAdapter` capturing events: `options_changed` (payload includes changed fields, sanitized), `options_imported`, `options_exported`, `test_connection_result`.
- Adapter listens to `CodexOptionsMonitor.OnOptionsChanged` and computes diff against previous snapshot using hashed values to avoid leaking raw paths.
- Emit events via existing `ITelemetryService` (referenced in `docs/refactor-strategy.md`).

## 11. Deliverables
1. Implement shared options infrastructure (`CodexOptionsMonitor`, `CodexOptionsWriter`, `CodexDefaultsCatalog`, `CodexOptionsValidator`).
2. Wire services (`ICliOptionsService`, `IApprovalPolicyService`, etc.) to DI container.
3. Replace direct `_options` reads in MyToolWindowControl partials with service injections.
4. Provide MVVM commands for options UI, including Test Connection + Reset defaults.
5. Write unit tests for:
   - Snapshot merge logic (global vs solution).
   - Thread-safe writer queue (no deadlocks under concurrent callers).
   - Validator behaviors (invalid CLI path, window size bounds, reasoning values).
   - JSON import/export round-trips with schema versioning.
   - Telemetry adapter diffing logic.
6. Add integration tests covering Test Connection flow using mocked CLI host.

## 12. Checklist Traceability
- Map options page fields to service interfaces → §2.
- Thread-safe load/save routines → §3.
- Default synchronization rules → §4.
- Per-solution override coordination → §5.
- Validation pipeline documentation → §6.
- Test Connection workflow plan → §7.
- JSON import/export hooks → §8.
- Approval mode persistence alignment → §9.
- Telemetry specification → §10.
- Deliverable artifact → this document (`docs/options-integration-plan.md`).

