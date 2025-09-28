# Documentation and Onboarding Plan

This plan fulfills Task T19 by coordinating documentation updates, onboarding guidance, and release communications for the MyToolWindowControl refactor.

## Updated Artifacts
| Artifact | Purpose | Status |
| --- | --- | --- |
| `README.md` | Architecture overview, module table, parity & troubleshooting pointers | ✅ Updated with MVVM map, module owners, parity notes |
| `docs/architecture-diagrams.md` | Mermaid diagrams for chat/diff/exec pipelines | ✅ Added (referenced by README) |
| `docs/troubleshooting.md` | Quick fixes for chat, diff, exec, session resume | ✅ Added |
| `CHANGELOG.md` | Unreleased section tracking refactor deliverables | ✅ Placeholder entries added |
| `telemetry-diagnostics-plan.md` | Added persistence telemetry + privacy guidance | ✅ Updated |
| `parity-report.md` | Highlighted VS Code differences for onboarding | ✅ Updated summary |

## Module Owner Directory
- **Chat Transcript** – Maintained via `docs/chat-viewmodel-design.md`; ping Chat maintainers for transcript/store issues.
- **Diff Review** – `docs/diff-module-plan.md`; coordinates with approvals team.
- **Exec Console** – `docs/exec-module-plan.md`; owns ANSI rendering and buffer policy.
- **MCP Tools & Prompts** – `docs/mcp-module-plan.md`; handles refresh debouncing and hover help.
- **Session Persistence** – `docs/session-persistence-plan.md`; responsible for autosave, resume, encryption.
- **Telemetry & Diagnostics** – `docs/telemetry-diagnostics-plan.md`; governs event names, throttling, redaction.
- **Options & CLI Host** – `docs/options-integration-plan.md` and `docs/cli-host-spec.md`; ensures toggles map to services.

Keep this directory in sync when maintainers rotate or new modules spin up.

## Troubleshooting & Parity Notes
- Troubleshooting: link users and contributors to `docs/troubleshooting.md` before diving into code.
- Parity: `parity-report.md` now fronts differences vs the VS Code extension; new contributors should review it during onboarding to understand Windows-specific gaps.

## Telemetry & Privacy Alignment
- Telemetry events for session persistence (`session.persist.*`, `session.resume.*`) are documented in `docs/session-persistence-plan.md` and cross-referenced in `docs/telemetry-diagnostics-plan.md`.
- Options toggle guidance is consolidated in README configuration notes and the telemetry plan.
- Ensure PRs touching telemetry or privacy update both docs in lockstep.

## Blog / Release Narrative Outline
1. **Problem Statement** – explain why MyToolWindowControl required refactoring (complex partials, manual bindings).
2. **New Architecture** – summarize MVVM modules, include diagram snapshots from `docs/architecture-diagrams.md`.
3. **Key Features** – highlight session resume, improved exec console, approval clarity.
4. **Compatibility & Parity** – reference `parity-report.md`; note VS Code feature differences.
5. **Telemetry & Privacy** – describe opt-in telemetry, persistence encryption.
6. **Getting Started** – link README quick start and onboarding checklist.
7. **Call to Action** – invite feedback and contributions via module owners table.

A trimmed version can feed release notes (`RELEASE_NOTES_vNext.md`) once implementation ships.

## Onboarding Checklist
1. Read `README.md` architecture overview and skim `docs/architecture-diagrams.md`.
2. Identify the module you'll work on and review the matching plan under `/docs`.
3. Run the extension in VS Experimental instance; validate CLI connectivity using troubleshooting guide.
4. Run `dotnet test` and review telemetry/diff plans for relevant assertions.
5. Configure telemetry and session persistence toggles to understand privacy footprint.
6. Before opening PRs, update CHANGELOG `[Unreleased]` and cross-reference doc owners table.
7. For documentation updates, follow guidelines in this plan and ensure cross-links remain valid.

## Next Actions
- Keep this plan updated as implementation lands (e.g., rename modules, add new diagrams).
- Add release-note snippets to `RELEASE_NOTES_v0.2.0.md` (future) using the narrative outline above.
- Schedule quarterly documentation reviews to ensure onboarding steps match the codebase.
