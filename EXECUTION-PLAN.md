# EXECUTION-PLAN

Last updated: 2026-02-26
Scope: `C:\Dev\repos\active\CodexVS22`
Primary objective: finish refactor and release verification with test-backed, reversible changes.

## 1. Governance and Constraints
- Operate with isolated worktrees for implementation tasks when feasible.
- Run Windows compile/test commands with vcvars bootstrap:
  - `& "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; <command>`
- Keep updates synchronized in:
  - `MASTER-CHECKLIST.md`
  - `EXECUTION-PLAN.md`
  - `.AGENTS/todo.md`
  - `docs/development-progress.md`
- Treat `todo-refactor.md` and `todo-release.md` task IDs as canonical execution IDs.

## 2. Current State Snapshot
- Branch: `main` ahead of `origin/main` by 2 commits.
- Working tree: dirty, includes in-progress refactor and test files.
- Refactor status highlights:
  - Completed: `R1`, `R3`
  - Open: `R2`, `R4`-`R17`
- Release status highlights:
  - Build health tasks `B1`/`B1.g` and `B3` completed (project-level rebuild evidence captured).
  - Open verification/release/docs/final validation: `V*`, `R*`, `D*`, `F*`

## 3. Phase Plan

### Phase A - Preflight and Baseline
1. Record and classify dirty-file change sets.
2. Confirm vcvars bootstrap availability.
3. Establish progress log and checklist baselines.
Acceptance criteria:
- Baseline recorded in `docs/development-progress.md`.
- Checklist + plan present and synchronized.

### Phase B - Build Health (Critical Path)
1. Execute Release build with vcvars bootstrap.
2. Resolve any blockers to close `B1.g`.
3. Capture build evidence and close `B3`.
Acceptance criteria:
- Successful Release build command output captured.
- `todo-release.md` updated for `B1.g` and `B3`.

### Phase C - Refactor Task Execution
1. Execute `R2` first (CLI host migration).
2. Run parallelizable modules after `R2`/`R3`: `R4`, `R5`, `R6`, `R7`, `R8`, `R10`, `R11`.
3. Integrate shell/command layers: `R12`, `R13`.
Acceptance criteria:
- Each task has implementation + tests + docs notes.
- Legacy dependencies reduced without regressions.

### Phase D - Validation and Pipeline Hardening
1. Complete `R14` testing harness and coverage checks.
2. Complete `R15` CI/build updates.
3. Execute full project test pass.
Acceptance criteria:
- Test commands pass locally.
- No coverage or quality gate regression.

### Phase E - Release Verification and Documentation
1. Complete `V1`-`V4`, `R1`-`R9`, `F1`-`F3`.
2. Complete docs tasks `D1`-`D4`.
3. Produce final sign-off summary.
Acceptance criteria:
- `todo-release.md` tasks closed with evidence references.
- Documentation reflects actual runtime behavior.

## 4. Immediate Batch (Active Now)
1. Phase A completion:
   - Write checklist/plan/progress/report artifacts.
   - Verify vcvars bootstrap.
2. Enter Phase B:
   - Run Release build to assess `B1.g`.
   - Capture blockers and define smallest next fix set.

## 5. Verification Commands
- Bootstrap check:
  - `& "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet`
- Restore/build:
  - `& "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; dotnet restore CodexVS22.sln`
  - `& "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; dotnet build CodexVS22.sln -c Release`
- Tests:
  - `& "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; dotnet test CodexVS22.Tests/CodexVS22.Tests.csproj -c Release`

## 6. Risk Notes
- Existing dirty worktree can mask regressions if not carefully baselined.
- VSIX-specific behavior requires Visual Studio experimental instance for full smoke validation.
- Some checklist items are historical and need reconciliation against current code.
