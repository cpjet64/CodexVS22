using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodexVS22.Core.Approvals;

namespace CodexVS22.Shared.Approvals
{
    public interface IApprovalService
    {
        event EventHandler<ApprovalPrompt> PromptRaised;

        event EventHandler<PendingApproval> ApprovalResolved;

        int PendingCount { get; }

        Task QueueAsync(PendingApproval request, CancellationToken cancellationToken = default);

        bool TryResolveRemembered(PendingApproval request, out ApprovalDecision decision);

        Task ResolveAsync(string callId, ApprovalDecision decision, bool rememberDecision, CancellationToken cancellationToken = default);

        void Reset();

        IReadOnlyCollection<PendingApproval> SnapshotPending();
    }
}
