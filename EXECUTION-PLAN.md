# EXECUTION-PLAN

Last updated: 2026-02-26
Scope: `C:\Dev\repos\active\CodexVS22`
Primary objective: complete remaining refactor milestones (`R4`-`R17`) and close release readiness tasks (`V*`, `R*`, `D*`, `F*`) with evidence-backed verification.

## 1. Governance and Operating Rules
- Use `todo-refactor.md` and `todo-release.md` task IDs as canonical status IDs.
- Keep progress synchronized in `MASTER-CHECKLIST.md`, `EXECUTION-PLAN.md`, `.AGENTS/todo.md`, and `docs/standardization-report.md`.
- Run build/test through vcvars bootstrap when commands depend on Visual Studio toolchain:
  - `& "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; <command>`
- Keep changes minimal and reversible; verify each batch before moving to the next.

## 2. Current State Snapshot (Audit)
- Git branch: `main...origin/main [ahead 10]`.
- Working tree: clean at planning refresh time.
- Refactor tracker:
  - Complete: `R1`, `R2`, `R3`
  - Open: `R4` through `R17`
- Release tracker:
  - Complete: `B1`, `B2`, `B3`
  - Open: `V1`-`V4`, `R1`-`R9`, `D1`-`D4`, `F1`-`F3`

## 3. Phase Plan

### Phase A - Module Completion (Refactor Core)
1. Complete `R4` Chat Transcript Module.
2. Complete `R8` Approval Flow Service in parallel with `R4` integration needs.
3. Complete `R5`, `R6`, `R7`, `R10`, `R11` to finish feature/service modules.
Acceptance criteria:
- Each task has implementation notes and tests in touched areas.
- No regressions against existing chat/diff/exec/MCP/options flows.

### Phase B - Integration and Quality Hardening
1. Complete `R12` UI Shell Composition.
2. Complete `R13` Command Routing & VS Package Integration.
3. Complete `R14` Testing Harness & Coverage.
4. Complete `R15` Build & CI Pipeline Updates.
Acceptance criteria:
- Full feature paths route through new module boundaries.
- `just ci-fast` passes after integration, then `just ci-deep` passes before release-finalization.

### Phase C - Documentation and Final Refactor Closure
1. Complete `R16` Documentation & Onboarding updates.
2. Complete `R17` Execution Coordination & Final Validation.
Acceptance criteria:
- Refactor tracker reflects closed tasks with evidence.
- User-facing docs and troubleshooting guidance match current behavior.

### Phase D - Release Verification and Sign-Off
1. Close verification items `V1`-`V4`.
2. Close release checklist items `R1`-`R9`.
3. Close documentation closure items `D1`-`D4`.
4. Close final validation items `F1`-`F3`.
Acceptance criteria:
- `todo-release.md` fully updated with evidence links/notes.
- Release artifacts and validation logs are complete and reproducible.

## 4. Immediate Next Batch
1. Execute Batch A.1: `R4` + `R8`.
2. Verify Batch A.1 with `just ci-fast`.
3. If green, execute Batch A.2: `R5` + `R6`.
4. Re-run verification, then execute Batch A.3: `R7` + `R10` + `R11`.

## 5. Verification Commands
- Fast local gate:
  - `just ci-fast`
- Deep local gate:
  - `just ci-deep`
- Direct commands if needed:
  - `& "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; msbuild CodexVS22.sln /t:Restore /m`
  - `& "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; msbuild CodexVS22.csproj /t:Rebuild /p:Configuration=Release /p:DeployExtension=false /m`
  - `& "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; dotnet run --project CodexVS22.Tests/CodexVS22.Tests.csproj -c Release`

## 6. Risks and Controls
- Visual Studio experimental-instance validation is still required for final release tasks.
- Mixed legacy/new module paths may hide routing regressions until `R12`/`R13` is complete.
- Release checklist completion depends on evidence collection discipline in docs and trackers.
