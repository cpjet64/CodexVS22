# MyToolWindowControl Refactor Execution Roadmap

## Implementation Sequencing (Task Order & Ownership)
1. **Foundation Services (Weeks 1–2)**
   - T6 CLI Host Extraction – Agent A
   - T5 Threading Risk remediations (high-priority fixes) – Agent E
   - T12 Telemetry & Diagnostics – Agent B
2. **Core View-Models (Weeks 2–4)**
   - T7 Chat Transcript ViewModel – Agent B
   - T9 Exec Console Module – Agent D
   - T10 MCP & Prompts Module – Agent E
3. **State & Persistence (Weeks 3–5)**
   - T11 Options Integration – Agent A
   - T14 Session Persistence – Agent D
   - T13 Approval Flow Service – Agent C
4. **Diff & Approvals (Weeks 4–6)**
   - T8 Diff Module – Agent C
   - T15 UI Shell Orchestration – Agent E
5. **Integration & Commands (Weeks 5–7)**
   - T16 Command Routing – Agent A
   - T17 Testing Harness – Agent B
   - T18 Build/Packaging/CI – Agent C
6. **Docs & Finalization (Weeks 6–8)**
   - T19 Documentation/Onboarding – Agent D
   - T20 Execution Roadmap upkeep – Agent E (PM)

Dependencies: CLI host (T6) unblocks chat/diff/exec modules; approvals (T13) required before diff apply automation; session persistence (T14) integrates after chat VM baseline; UI shell (T15) consumes all module view-models.

## Integration Checkpoints & Handoffs
- **Checkpoint A (End Week 2):** CLI Host façade published; telemetry service stubs; chat/exec VMs scaffolded. Deliverable: combined integration branch, smoke tested using mock CLI.
- **Checkpoint B (End Week 4):** Chat, exec, MCP modules consume shared services; approval service interface agreed. Run module-level tests (unit + golden).
- **Checkpoint C (End Week 6):** Diff module integrated with approval manager; UI shell hosting all modules; command routing updated. Execute end-to-end regression in VS experimental instance.
- **Handoff Protocol:** Each checkpoint requires README updates plus recorded Loom walkthrough (optional) and backlog ticket status change. Incoming module owner reviews consumer contracts before merging.

## Daily Sync Artifacts & Communication
- **Standup Doc:** Shared markdown (OneNote or Teams Wiki) with sections Yesterday/Today/Blockers per agent; update before 10:00.
- **Async Check-ins:** #codex-vs-refactor Slack channel for blockers with `[BLOCKER]` tag. Use thread replies for resolution.
- **Weekly Planning:** 30-minute call Mondays to review roadmap adjustments, risk burndown, and checkpoint readiness. Minutes stored in `/docs/meeting-notes/YYYY-WW.md`.

## Definition of Done per Module
- Functional parity with existing control (interactive UAT checklist).
- Unit/integration tests at ≥80% coverage for new code (per module harness in T17).
- Telemetry events routed via new telemetry service with sample payload validated.
- Accessibility checks: keyboard navigation, screen reader pass (NVDA/Narrator), high contrast.
- Documentation stub updated (module README + main docs as needed).
- QA smoke checklist executed in experimental VS instance; results logged in `/docs/post-test/`.

## Code Review Ownership & Workflow
- Assign primary reviewer per module (e.g., Agent C reviews T8/T13, Agent B reviews telemetry-related PRs).
- Secondary reviewer rotates weekly to ensure cross-training. Use GitHub CODEOWNERS updates to enforce.
- Require design doc link + test evidence before requesting review; reviewers respond within 24 hours.
- Major architectural changes require 30-minute design review call with relevant owners before merge.

## Risk Mitigation & Rollback Plans
- Maintain `staging/refactor-shell` branch; fast-forward `main` only after automated suite + manual acceptance.
- Feature flags guard new modules (`CodexOptions.ExperimentalShell`). Ability to disable via options if regressions appear.
- Rollback checklist: revert to prior VSIX build, disable experimental flag, restore legacy control partial classes.
- Risks & mitigations:
  - **Threading regressions:** integrate JoinableTask diagnostics (T5) and guard with stress tests.
  - **CLI instability:** provide shim to revert to old host until T6 verified in production.
  - **Schedule slip:** checkpoint gating ensures modules merge sequentially; buffer week between C and release.

## Resource & Tooling Alignment
- Tooling: ensure all devs have VS 2022 Preview, Codex CLI latest, NVDA, Inspect.exe.
- Licenses: update JetBrains dotTrace (for perf) and Axe Windows (accessibility) for Agents C & E.
- Test rigs: allocate two clean VS experimental instances (Agent B & D) plus Azure VM for CI load testing.
- Coordinate with IT for additional 50GB on build agents to cache new test artifacts.

## CI Capacity & Environment Scheduling
- Update Azure DevOps pipeline with new test stages: unit (parallel), integration (serialized), accessibility (nightly).
- Reserve nightly CI slot 00:00–02:00 UTC; ensure no conflicting pipelines via DevOps environment locks.
- Add pre-merge validation pipeline `Codex-Refactor-Validation` triggered on PRs touching `Modules/`.
- Schedule weekly full regression (Saturday) on dedicated build agent to avoid weekday contention.

## Post-Refactor Validation Checklist
- Run end-to-end scenario suite (chat, diff apply, exec, MCP tools, approvals) using scripted VS automation.
- Perform performance benchmark comparing key metrics vs baseline (load time, message latency, diff render) stored in `perf.csv`.
- Re-run accessibility audits (Axe, Narrator, contrast) and log defects.
- Validate telemetry dashboards ingest new events; update monitoring alerts.
- Conduct support readiness review: documentation, troubleshooting playbooks, release notes.
- Execute roll-forward/rollback simulation to ensure feature flag behavior.

## Timeline & Tracking
- Gantt-style tracker maintained in Notion page `Codex VS Refactor Timeline`; export snapshot weekly.
- Roadmap owner (Agent E) updates this document at each checkpoint; changes tracked via PRs.

## Deliverable
This roadmap (`docs/execution-roadmap.md`) fulfills Task T20, covering sequencing, checkpoints, communications, DoD, reviews, risk/rollback, resource planning, CI scheduling, and validation.
