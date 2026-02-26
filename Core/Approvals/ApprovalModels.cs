using System;
using System.Collections.Generic;

namespace CodexVS22.Core.Approvals
{
    public enum ApprovalType
    {
        Exec = 0,
        Patch = 1,
        ChatAction = 2
    }

    public enum ApprovalDecision
    {
        Pending = 0,
        Approved = 1,
        Denied = 2
    }

    public sealed class PendingApproval
    {
        public PendingApproval(string callId, ApprovalType approvalType, string signature, string prompt, IDictionary<string, string>? metadata = null)
        {
            CallId = string.IsNullOrWhiteSpace(callId) ? string.Empty : callId.Trim();
            ApprovalType = approvalType;
            Signature = string.IsNullOrWhiteSpace(signature) ? string.Empty : signature.Trim();
            Prompt = string.IsNullOrWhiteSpace(prompt) ? string.Empty : prompt.Trim();
            Metadata = metadata is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
            CreatedUtc = DateTime.UtcNow;
        }

        public string CallId { get; }

        public ApprovalType ApprovalType { get; }

        public string Signature { get; }

        public string Prompt { get; }

        public IDictionary<string, string> Metadata { get; }

        public DateTime CreatedUtc { get; }
    }

    public sealed class ApprovalPrompt
    {
        public ApprovalPrompt(PendingApproval request, int queueLength)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            QueueLength = queueLength < 0 ? 0 : queueLength;
        }

        public PendingApproval Request { get; }

        public int QueueLength { get; }
    }
}
