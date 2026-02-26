# MASTER-CHECKLIST

Last updated: 2026-02-26
Scope: `C:\Dev\repos\active\CodexVS22`
Audit sources: `git status --short --branch`, `todo-refactor.md`, `todo-release.md`, `README.md`, `Justfile`

## M0. Baseline and Governance
- [x] Confirm canonical planning docs exist: `MASTER-CHECKLIST.md`, `EXECUTION-PLAN.md`.
- [x] Confirm repo state snapshot: `main` ahead of `origin/main` by 10 commits; working tree clean.
- [x] Confirm stack and workflow inputs (`.NET/VSIX`, `Justfile`, refactor/release trackers).
- [ ] Re-run baseline snapshot before each implementation batch and update docs if scope shifts.

## M1. Refactor Program Status
- [x] Confirm completed refactor milestones: `R1`, `R2`, `R3`.
- [ ] Deliver feature module migrations: `R4`, `R5`, `R6`, `R7`, `R8`, `R9`, `R10`, `R11`.
- [ ] Deliver integration milestones: `R12`, `R13`.
- [ ] Deliver quality/documentation milestones: `R14`, `R15`, `R16`, `R17`.

## M2. Release Tracker Status
- [x] Build recovery items complete: `B1`, `B2`, `B3` (including `B1.g` evidence note).
- [ ] Verify imported feature behavior: `V1`, `V2`, `V3`, `V4`.
- [ ] Complete release checklist execution: `R1` through `R9` in `todo-release.md`.
- [ ] Complete documentation closure: `D1` through `D4`.
- [ ] Complete final validation: `F1` through `F3`.

## M3. Immediate Batch (Next Actionable Work)
- [ ] Batch A: execute `R4` + `R8` together (chat module plus approval flow service).
- [ ] Batch A verification: run `just ci-fast` and targeted module/unit tests after `R4`/`R8`.
- [ ] Batch B: execute `R5`, `R6`, `R7`, `R10`, `R11` after Batch A is stable.
- [ ] Batch C: execute `R12`, `R13`, then `R14`/`R15` quality gates.
- [ ] Batch D: close release verification items `V1`-`V4`, then `R1`-`R9`, `D1`-`D4`, `F1`-`F3`.

## M4. Quality Gates
- [ ] `just hygiene`
- [ ] `just restore`
- [ ] `just build-vsix`
- [ ] `just test`
- [ ] `just ci-fast` before closing each major batch.
- [ ] `just ci-deep` before release-signoff tasks (`F*`).

## M5. Documentation and Tracking Discipline
- [ ] Keep status synchronized across `todo-refactor.md`, `todo-release.md`, and `docs/development-progress.md`.
- [ ] Update `docs/standardization-report.md` after each planning refresh.
- [ ] Keep this checklist focused on current state; remove stale historical notes when superseded.
