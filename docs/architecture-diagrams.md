# Architecture Diagrams

These diagrams visualize the chat, diff, and exec pipelines after the refactor. They accompany the high-level description in `README.md` and the module plans in `/docs`.

## Chat Turn Lifecycle
```mermaid
sequenceDiagram
    participant User
    participant ChatVM as ChatTranscriptViewModel
    participant SessionStore as CodexSessionStore
    participant Coordinator as Session Coordinator
    participant CLI as Codex CLI

    User->>ChatVM: SendCommand(text)
    ChatVM->>Coordinator: Dispatch(ChatRequest)
    Coordinator->>CLI: Send chat_envelope
    CLI-->>Coordinator: chat.delta (stream)
    Coordinator-->>SessionStore: Reduce(ChatTurnDelta)
    SessionStore-->>ChatVM: Notify delta
    CLI-->>Coordinator: chat.final
    Coordinator-->>SessionStore: Reduce(ChatTurnFinal)
    SessionStore-->>ChatVM: Update turn + telemetry
```

## Diff Apply Flow
```mermaid
sequenceDiagram
    participant CLI as Codex CLI
    participant Coordinator as Session Coordinator
    participant DiffSvc as DiffSessionService
    participant Approvals as ApprovalService
    participant DiffVM as DiffReviewViewModel
    participant Workspace as IDiffWorkspaceApplier

    CLI-->>Coordinator: turn_diff preview
    Coordinator-->>DiffSvc: DiffDocuments
    DiffSvc-->>DiffVM: Build tree + selection summary
    DiffVM->>Approvals: Request approval(signature)
    Approvals->>DiffVM: Show banner
    User->>Approvals: Approve
    Approvals->>Coordinator: Send approval_envelope
    Coordinator->>CLI: approval_envelope
    CLI-->>Coordinator: patch_apply
    Coordinator-->>DiffSvc: ApplyResult
    DiffSvc->>Workspace: Apply patch
    Workspace-->>DiffSvc: Success/failure
    DiffSvc-->>DiffVM: Update state + telemetry
```

## Exec Command Lifecycle
```mermaid
sequenceDiagram
    participant CLI as Codex CLI
    participant Coordinator as Session Coordinator
    participant ExecSvc as ExecSessionService
    participant ExecVM as ExecConsoleViewModel
    participant Telemetry as TelemetryService

    CLI-->>Coordinator: exec.approval request
    Coordinator->>Approvals: Queue request
    Approvals->>Coordinator: Approval result
    Coordinator->>CLI: approval response
    CLI-->>Coordinator: exec.begin
    Coordinator-->>ExecSvc: Start(execId, metadata)
    ExecSvc-->>ExecVM: Create turn (running)
    CLI-->>Coordinator: exec.delta
    Coordinator-->>ExecSvc: Append output
    ExecSvc-->>ExecVM: Update buffer
    ExecSvc-->>Telemetry: Record delta
    CLI-->>Coordinator: exec.end(exitCode)
    Coordinator-->>ExecSvc: Complete turn
    ExecSvc-->>ExecVM: Mark completed
    ExecSvc-->>Telemetry: Record completion
```

## Session Persistence Overview
```mermaid
flowchart LR
    SessionStore -->|Snapshot| Repo[Session Repository]
    Repo -->|jsonl| Disk[(%LOCALAPPDATA%/CodexVS/Sessions)]
    Disk --> Repo
    Repo -->|Hydrate| SessionStore
    Options[Options Toggle] --> Repo
    Telemetry[TelemetryService] --> Repo
```

Refer to `docs/session-persistence-plan.md` for serialization details and `docs/telemetry-diagnostics-plan.md` for persistence telemetry.
