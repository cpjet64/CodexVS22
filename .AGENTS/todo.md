# TODO / Plan

## Planning Refresh - 2026-02-26
- [x] Check `Justfile` and current git state (`main...origin/main [ahead 10]`, clean tree).
- [x] Re-audit canonical trackers: `todo-refactor.md` and `todo-release.md`.
- [x] Refresh `MASTER-CHECKLIST.md` to match current completed/open milestones.
- [x] Refresh `EXECUTION-PLAN.md` with phase order and immediate execution batches.
- [x] Save this plan under `.AGENTS/plans/refresh-planning-docs-2026-02-26.md`.
- [x] Append the planning-refresh delta to `docs/standardization-report.md`.

## Orchestrator Run - 2026-02-26
- [x] Check `Justfile` and repo baseline status.
- [x] Audit for `MASTER-CHECKLIST.md` and `EXECUTION-PLAN.md`.
- [x] Create/update canonical checklist and execution plan files.
- [x] Record standardization report and development progress log.
- [x] Verify vcvars bootstrap in current session.
- [x] Run Release build to evaluate and close blocker `B1.g`.
- [x] Update `todo-release.md` based on build outcome.
- [x] Run test suite and capture pass/fail with next task batch proposal.
- [x] Start next implementation batch for `R2` follow-up and solution-level build/tooling reconciliation.

## Review
- Phase 0 completed: mandatory planning docs now exist and are aligned to current repo state.
- Build-health critical path progressed:
  - fixed missing compile includes in `CodexVS22.csproj` for new CLI/state/shared files.
  - `msbuild CodexVS22.csproj /t:Rebuild /p:Configuration=Release /p:DeployExtension=false` succeeded.
  - `build-log.txt` refreshed from successful rebuild.
  - `dotnet run --project CodexVS22.Tests/CodexVS22.Tests.csproj -c Release` reported `Correlation tests passed.`
- Ordered follow-up completed (`2 -> 3 -> 1`):
  - (2) Normalized build/test split in `Justfile` + GitHub workflows to use `msbuild` for VSIX project and `dotnet` for SDK-style tests.
  - (3) Performed warning pass without destabilizing build; retained clean rebuild and reduced noisy warning impact.
  - (1) Marked `R2` complete in `todo-refactor.md` after validating service wiring, helper extraction, and test harness execution.

## Review - Planning Refresh 2026-02-26
- Corrected stale assumptions in prior plan docs (old snapshot listed dirty tree and open `R2`).
- Canonical planning docs now match trackers:
  - Refactor complete: `R1`-`R3`; open: `R4`-`R17`.
  - Release complete: `B1`-`B3`; open: `V1`-`V4`, `R1`-`R9`, `D1`-`D4`, `F1`-`F3`.
- Immediate next batch is now explicit: `R4` + `R8`, then `R5`/`R6`, then `R7`/`R10`/`R11`.
