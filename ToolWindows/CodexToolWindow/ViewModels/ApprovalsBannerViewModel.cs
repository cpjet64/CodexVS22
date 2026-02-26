using System.Collections.ObjectModel;
using System.Linq;
using CodexVS22.Core.Approvals;
using CodexVS22.Shared.Approvals;

namespace CodexVS22.ToolWindows.CodexToolWindow.ViewModels
{
    public class ApprovalsBannerViewModel
    {
        private readonly IApprovalService _approvalService;

        public ApprovalsBannerViewModel(IApprovalService approvalService = null)
        {
            _approvalService = approvalService ?? new ApprovalService();
            _approvalService.PromptRaised += OnPromptRaised;
            _approvalService.ApprovalResolved += OnApprovalResolved;
        }

        public ObservableCollection<string> PendingApprovals { get; } = new();

        public string ActiveCallId { get; private set; } = string.Empty;

        public string ActivePrompt { get; private set; } = string.Empty;

        public int QueueLength { get; private set; }

        public bool IsVisible => !string.IsNullOrWhiteSpace(ActiveCallId);

        public void Queue(PendingApproval request)
        {
            _approvalService.QueueAsync(request).GetAwaiter().GetResult();
            RefreshQueue();
        }

        public void ResolveActive(bool approved, bool remember)
        {
            if (string.IsNullOrWhiteSpace(ActiveCallId))
            {
                return;
            }

            _approvalService.ResolveAsync(ActiveCallId, approved ? ApprovalDecision.Approved : ApprovalDecision.Denied, remember)
                .GetAwaiter()
                .GetResult();
            RefreshQueue();
        }

        public void Reset()
        {
            _approvalService.Reset();
            PendingApprovals.Clear();
            ActiveCallId = string.Empty;
            ActivePrompt = string.Empty;
            QueueLength = 0;
        }

        private void OnPromptRaised(object sender, ApprovalPrompt prompt)
        {
            ActiveCallId = prompt.Request.CallId;
            ActivePrompt = prompt.Request.Prompt;
            QueueLength = prompt.QueueLength;
            RefreshQueue();
        }

        private void OnApprovalResolved(object sender, PendingApproval approval)
        {
            if (approval is null)
            {
                return;
            }

            if (approval.CallId == ActiveCallId)
            {
                ActiveCallId = string.Empty;
                ActivePrompt = string.Empty;
            }
        }

        private void RefreshQueue()
        {
            var snapshot = _approvalService.SnapshotPending();
            PendingApprovals.Clear();
            foreach (var item in snapshot.Select(static pending => $"{pending.ApprovalType}: {pending.Prompt}"))
            {
                PendingApprovals.Add(item);
            }

            QueueLength = snapshot.Count;
        }
    }
}
