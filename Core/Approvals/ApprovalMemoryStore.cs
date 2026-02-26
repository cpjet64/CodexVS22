using System;
using System.Collections.Generic;

namespace CodexVS22.Core.Approvals
{
    public sealed class ApprovalMemoryStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, ApprovalDecision> _execDecisions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ApprovalDecision> _patchDecisions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ApprovalDecision> _chatDecisions = new(StringComparer.OrdinalIgnoreCase);

        public bool TryResolve(ApprovalType approvalType, string signature, out ApprovalDecision decision)
        {
            var key = NormalizeKey(signature);
            lock (_gate)
            {
                return GetMap(approvalType).TryGetValue(key, out decision);
            }
        }

        public void Remember(ApprovalType approvalType, string signature, ApprovalDecision decision)
        {
            if (decision == ApprovalDecision.Pending)
            {
                return;
            }

            var key = NormalizeKey(signature);
            lock (_gate)
            {
                GetMap(approvalType)[key] = decision;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _execDecisions.Clear();
                _patchDecisions.Clear();
                _chatDecisions.Clear();
            }
        }

        private static string NormalizeKey(string signature)
        {
            return string.IsNullOrWhiteSpace(signature) ? string.Empty : signature.Trim();
        }

        private Dictionary<string, ApprovalDecision> GetMap(ApprovalType approvalType)
        {
            switch (approvalType)
            {
                case ApprovalType.Exec:
                    return _execDecisions;
                case ApprovalType.Patch:
                    return _patchDecisions;
                default:
                    return _chatDecisions;
            }
        }
    }
}
