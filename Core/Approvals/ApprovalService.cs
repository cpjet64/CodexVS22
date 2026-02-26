using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodexVS22.Shared.Approvals;

namespace CodexVS22.Core.Approvals
{
    public sealed class ApprovalService : IApprovalService
    {
        private readonly object _gate = new();
        private readonly Queue<PendingApproval> _pending = new();
        private readonly ApprovalMemoryStore _memoryStore;
        private PendingApproval? _active;

        public ApprovalService(ApprovalMemoryStore? memoryStore = null)
        {
            _memoryStore = memoryStore ?? new ApprovalMemoryStore();
        }

        public event EventHandler<ApprovalPrompt>? PromptRaised;

        public event EventHandler<PendingApproval>? ApprovalResolved;

        public int PendingCount
        {
            get
            {
                lock (_gate)
                {
                    return _pending.Count + (_active is null ? 0 : 1);
                }
            }
        }

        public Task QueueAsync(PendingApproval request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (TryResolveRemembered(request, out var rememberedDecision))
            {
                request.Metadata["remembered"] = "true";
                request.Metadata["decision"] = rememberedDecision == ApprovalDecision.Approved ? "approved" : "denied";
                ApprovalResolved?.Invoke(this, request);
                return Task.CompletedTask;
            }

            ApprovalPrompt? prompt = null;
            lock (_gate)
            {
                if (_active is null)
                {
                    _active = request;
                    prompt = new ApprovalPrompt(request, _pending.Count + 1);
                }
                else
                {
                    _pending.Enqueue(request);
                }
            }

            if (prompt is not null)
            {
                PromptRaised?.Invoke(this, prompt);
            }

            return Task.CompletedTask;
        }

        public bool TryResolveRemembered(PendingApproval request, out ApprovalDecision decision)
        {
            if (request is null)
            {
                decision = ApprovalDecision.Pending;
                return false;
            }

            return _memoryStore.TryResolve(request.ApprovalType, request.Signature, out decision);
        }

        public Task ResolveAsync(string callId, ApprovalDecision decision, bool rememberDecision, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(callId))
            {
                throw new ArgumentException("Call ID is required.", nameof(callId));
            }

            cancellationToken.ThrowIfCancellationRequested();

            PendingApproval resolved;
            ApprovalPrompt? nextPrompt = null;
            lock (_gate)
            {
                if (_active is null || !string.Equals(_active.CallId, callId.Trim(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("No active approval matches the provided call ID.");
                }

                resolved = _active;
                resolved.Metadata["decision"] = decision == ApprovalDecision.Approved ? "approved" : "denied";
                if (rememberDecision && !string.IsNullOrWhiteSpace(resolved.Signature))
                {
                    _memoryStore.Remember(resolved.ApprovalType, resolved.Signature, decision);
                }

                _active = _pending.Count > 0 ? _pending.Dequeue() : null;
                if (_active is not null)
                {
                    nextPrompt = new ApprovalPrompt(_active, _pending.Count + 1);
                }
            }

            ApprovalResolved?.Invoke(this, resolved);
            if (nextPrompt is not null)
            {
                PromptRaised?.Invoke(this, nextPrompt);
            }

            return Task.CompletedTask;
        }

        public void Reset()
        {
            lock (_gate)
            {
                _active = null;
                _pending.Clear();
                _memoryStore.Reset();
            }
        }

        public IReadOnlyCollection<PendingApproval> SnapshotPending()
        {
            lock (_gate)
            {
                var list = new List<PendingApproval>(_pending.Count + 1);
                if (_active is not null)
                {
                    list.Add(_active);
                }

                list.AddRange(_pending.ToList());
                return list;
            }
        }
    }
}
