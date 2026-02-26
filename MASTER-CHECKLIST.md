# MASTER-CHECKLIST

Last updated: 2026-02-26
Source of truth inputs: `todo-refactor.md`, `todo-release.md`, `docs/CONSOLIDATED_TODOS.md`, `README.md`

## M0. Orchestrator Preflight
- [x] Confirm `MASTER-CHECKLIST.md` and `EXECUTION-PLAN.md` exist.
- [x] Capture dirty-worktree baseline and active change sets.
- [ ] Classify generated/transient artifacts and align `.gitignore` if needed.
- [x] Verify `ensure-vcvars.ps1` bootstrap succeeds in this session.

## M1. Build Health Recovery
- [x] Close `todo-release.md` blocker `[B1.g]` with successful Release build evidence.
- [x] Regenerate and store build log in `build-log.txt` (or approved replacement path).
- [x] Close `[B3]` by documenting successful build outcome.

## M2. Refactor Critical Path
- [x] Complete `R2` (CLI host service migration) in `todo-refactor.md`.
- [ ] Complete dependent module migrations: `R4`, `R5`, `R6`, `R7`, `R8`, `R10`, `R11`.
- [ ] Verify migrated modules replace legacy `MyToolWindowControl` responsibilities.

## M3. Integration
- [ ] Complete `R12` UI shell composition.
- [ ] Complete `R13` command routing and VS package integration.
- [ ] Validate end-to-end flows: chat, diff, exec, approvals, MCP, options.

## M4. Quality Gates
- [ ] Complete `R14` testing harness and coverage gates.
- [ ] Complete `R15` build/CI pipeline updates.
- [ ] Run full local build + tests cleanly with no gate regressions.

## M5. Release Verification
- [ ] Complete verification tasks `V1`-`V4` in `todo-release.md`.
- [ ] Complete release tasks `R1`-`R9` in `todo-release.md`.
- [ ] Complete final validation `F1`-`F3` with evidence links.

## M6. Documentation and Handoff
- [ ] Complete docs/status tasks `D1`-`D4` in `todo-release.md`.
- [ ] Ensure checklist/task docs match code reality.
- [ ] Produce final sign-off summary and residual risk list.
