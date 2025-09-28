# Testing Harness Plan for MyToolWindowControl Refactor

## Unit Test Targets per Module
- **Chat module**: reducers (`ChatTranscriptReducer`), view-model adapters, input safety heuristics, transcript persistence serialization, mediator message routing.
- **Diff module**: parsers (`DiffRequestParser`), tree builders, patch orchestrator state machine, error classification, telemetry adapters.
- **Exec module**: session service (command lifecycle, output normalization, cancel flow), output buffer trimming, telemetry fan-out, approval integration hooks.
- **Approvals service**: queue ordering, remember/forget persistence, policy evaluation, banner view-model coordination.
- **CLI transport**: transport abstraction handshake (start/stop, reconnect), heartbeat monitor timers, stderr diagnostics channel.
- **Shell orchestration**: session store reducers, mediator dispatch semantics, telemetry summary store, options sync surfaces.
- Tests live in `tests/` sub-folders with shared fakes in `tests/Common` and use deterministic clocks + `JoinableTaskContext` stubs.

## Integration Tests with Mock Codex CLI
- Provide `MockCliHost` implementing `ICliTransport`/`ICliSessionRouter` that replays scripted jsonl transcripts and captures envelopes sent by modules.
- Integration suites cover chat streaming, diff approval/apply, exec command lifecycle, and MCP refresh flows end-to-end through mediator/state store.
- Harness runs against isolated workspace temp directories; CLI responses loaded from `tests/fixtures/protocol/*.jsonl`.
- Use `xunit` collection fixtures to spin up mock CLI once per suite, ensuring deterministic timing with virtual scheduler.

## WPF UI Automation Smoke Coverage
- Adopt `WinAppDriver`-based smoke tests or `Microsoft.VisualStudio.IntegrationTestFramework` to open VS experimental instance.
- Key scenarios: load tool window, send chat prompt stub, approve diff, toggle exec console, exercise accessibility patterns.
- Bind automation IDs matching refactored view (transcript list, approvals banner, diff tree) for stable selectors.
- Smoke suite executed nightly due to runtime cost; per-PR runs limited to subset (tool window load + send prompt).

## Coverage Gates Aligned with TODO Rules
- Maintain `dotnet test` with `coverlet` instrumentation; enforce **70% line coverage** across new modules, matching TODO_VisualStudio guidance.
- Add module-level `ExcludeFromCoverage` only via documented justification tracked in TODO file.
- CI fails if coverage regression >2% relative to `main`; diff gating script runs in pre-commit hook and pipeline stage.

## Golden Diff Fixtures for Diff Module
- Store fixture pairs under `tests/fixtures/diff/{scenario}/` containing CLI payload (`input.json`) and expected rendered tree (`expected.yml`).
- Use `Verify` snapshots for tree structure + apply results; on changes require reviewer approval with `verify --approve` gated by GitHub workflow.
- Include conflict scenarios, large file truncation, binary patch skipping, and multi-root workspaces.

## vstest Category Mapping
- Apply `[Trait("Category", "Unit")]`, `"Integration"`, `"UI"`, `"Perf"` to control pipeline filters.
- CI matrix: PR -> `Unit` + `Integration`, nightly -> all categories, release -> all + `Perf`.
- Document mapping in `README` for developers; `dotnet test --filter Category=Unit` runs locally by default.

## Hermetic Setup for Approvals and Exec
- Approvals tests use in-memory policy store and deterministic clock; no real CLI required.
- Exec integration harness uses sandboxed temp directories with fake file system (e.g., `System.IO.Abstractions` test doubles) to avoid writing to workspace.
- Provide `ApprovalScenarioBuilder` DSL to sequence queue events, remember decisions, and assert state transitions without UI.

## Parallel Test Execution Safeguards
- Enable `xunit.parallelizeTestCollections = true` for unit tests; integration + UI suites marked `[Collection("Sequential")]` to avoid cross-talk.
- Use unique temp directories per test via `IAsyncLifetime` and `TestContext.Current.CancellationToken` for cleanup.
- Mock CLI processes share no static state; mediator/state store tests ensure isolation by creating new instances per test.

## Tooling for Protocol Transcript Replay
- Create `TranscriptReplayer` utility to stream recorded events with configurable pacing (immediate, real-time, step). Supports filtering by channel (chat/diff/exec).
- CLI recordings captured via `codex proto --record` and stored under `tests/fixtures/transcripts/` with metadata manifest (model, timestamp, scenario tags).
- Provide CLI command `dotnet run --project CodexVS22.Tests -- replay --scenario chat-happy-path` for manual debugging and regression reproduction.

## Next Steps
1. Scaffold `tests/` solution folder with `Unit`, `Integration`, `UI` projects adopting xUnit + Verify; configure shared Directory.Build.props.
2. Implement `MockCliHost` + `TranscriptReplayer` utilities and add first replay-based integration test for chat streaming.
3. Wire coverage + category filters into Azure Pipelines/YAML, gating merges on baseline coverage and module-specific suites.
