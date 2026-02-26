using System;
using CodexVS22.Shared.Cli;

namespace CodexVS22.Core.State
{
    /// <summary>
    /// Tracks aggregated Codex CLI session metadata in a threadsafe, observable snapshot.
    /// </summary>
    public sealed class CodexSessionStore : ICodexSessionStore
    {
        private readonly object _gate = new();
        private CodexSessionSnapshot _snapshot = CodexSessionSnapshot.Create();

        public event EventHandler<CodexSessionChangedEventArgs> SessionChanged;

        public CodexSessionSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    return _snapshot;
                }
            }
        }

        public void UpdateHostState(CliHostState state)
        {
            Apply(
                snapshot => snapshot.HostState == state
                    ? snapshot
                    : snapshot with
                    {
                        HostState = state,
                        UpdatedAt = DateTimeOffset.UtcNow
                    },
                CodexSessionChangeKind.HostState,
                state.ToString());
        }

        public void RecordEnvelope(CliEnvelope envelope)
        {
            if (envelope == null)
                return;

            var now = DateTimeOffset.UtcNow;
            Apply(
                snapshot => snapshot with
                {
                    LastEnvelope = new CodexEnvelopeSnapshot(
                        envelope.EventType,
                        now,
                        snapshot.LastEnvelope.Count + 1),
                    UpdatedAt = now
                },
                CodexSessionChangeKind.Envelope,
                envelope.EventType);
        }

        public void RecordDiagnostic(CliDiagnostic diagnostic)
        {
            if (diagnostic == null)
                return;

            var summary = CliDiagnosticSummary.FromDiagnostic(diagnostic);
            Apply(
                snapshot => snapshot with
                {
                    LastDiagnostic = summary,
                    UpdatedAt = summary.Timestamp
                },
                CodexSessionChangeKind.Diagnostic,
                diagnostic.Category);
        }

        public void RecordAuthentication(CodexAuthenticationResult result)
        {
            var now = DateTimeOffset.UtcNow;
            Apply(
                snapshot => snapshot with
                {
                    Authentication = new CodexAuthenticationSnapshot(
                        true,
                        result.IsAuthenticated,
                        result.Message,
                        now),
                    UpdatedAt = now
                },
                CodexSessionChangeKind.Authentication,
                result.IsAuthenticated ? "Authenticated" : "Unauthenticated");
        }

        public void RecordHeartbeat(CliHeartbeatInfo heartbeat)
        {
            if (heartbeat == null)
                heartbeat = CliHeartbeatInfo.Empty;

            var now = DateTimeOffset.UtcNow;
            Apply(
                snapshot => snapshot with
                {
                    Heartbeat = new CodexHeartbeatSnapshot(heartbeat, now),
                    UpdatedAt = now
                },
                CodexSessionChangeKind.Heartbeat,
                heartbeat.OperationType);
        }

        public void Reset(SessionResetReason reason = SessionResetReason.Unknown)
        {
            Apply(_ => CodexSessionSnapshot.Create(), CodexSessionChangeKind.Reset, reason.ToString());
        }

        private void Apply(Func<CodexSessionSnapshot, CodexSessionSnapshot> mutator, CodexSessionChangeKind kind, string detail)
        {
            if (mutator == null)
                throw new ArgumentNullException(nameof(mutator));

            CodexSessionSnapshot updated;
            bool changed;
            lock (_gate)
            {
                var candidate = mutator(_snapshot);
                changed = !Equals(candidate, _snapshot);
                if (!changed)
                {
                    return;
                }

                _snapshot = candidate;
                updated = candidate;
            }

            SessionChanged?.Invoke(this, new CodexSessionChangedEventArgs(updated, kind, detail));
        }
    }

    public interface ICodexSessionStore
    {
        event EventHandler<CodexSessionChangedEventArgs> SessionChanged;

        CodexSessionSnapshot Current { get; }

        void UpdateHostState(CliHostState state);

        void RecordEnvelope(CliEnvelope envelope);

        void RecordDiagnostic(CliDiagnostic diagnostic);

        void RecordAuthentication(CodexAuthenticationResult result);

        void RecordHeartbeat(CliHeartbeatInfo heartbeat);

        void Reset(SessionResetReason reason = SessionResetReason.Unknown);
    }

    public sealed class CodexSessionChangedEventArgs : EventArgs
    {
        public CodexSessionChangedEventArgs(CodexSessionSnapshot snapshot, CodexSessionChangeKind kind, string detail)
        {
            Snapshot = snapshot;
            Kind = kind;
            Detail = detail ?? string.Empty;
        }

        public CodexSessionSnapshot Snapshot { get; }

        public CodexSessionChangeKind Kind { get; }

        public string Detail { get; }
    }

    public enum CodexSessionChangeKind
    {
        Unknown,
        HostState,
        Envelope,
        Diagnostic,
        Authentication,
        Heartbeat,
        Reset
    }

    public enum SessionResetReason
    {
        Unknown,
        Manual,
        WorkspaceChanged,
        OptionsChanged,
        HostRestarted
    }

    public sealed record CodexSessionSnapshot
    {
        public Guid SessionId { get; init; } = Guid.NewGuid();

        public CliHostState HostState { get; init; } = CliHostState.Stopped;

        public CodexAuthenticationSnapshot Authentication { get; init; } = CodexAuthenticationSnapshot.Empty;

        public CodexHeartbeatSnapshot Heartbeat { get; init; } = CodexHeartbeatSnapshot.Empty;

        public CodexEnvelopeSnapshot LastEnvelope { get; init; } = CodexEnvelopeSnapshot.Empty;

        public CliDiagnosticSummary? LastDiagnostic { get; init; }

        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

        public static CodexSessionSnapshot Create()
        {
            var now = DateTimeOffset.UtcNow;
            return new CodexSessionSnapshot
            {
                SessionId = Guid.NewGuid(),
                HostState = CliHostState.Stopped,
                Authentication = CodexAuthenticationSnapshot.Empty,
                Heartbeat = CodexHeartbeatSnapshot.Empty,
                LastEnvelope = CodexEnvelopeSnapshot.Empty,
                UpdatedAt = now
            };
        }
    }

    public readonly record struct CodexAuthenticationSnapshot(bool HasValue, bool IsAuthenticated, string Message, DateTimeOffset? Timestamp)
    {
        public static CodexAuthenticationSnapshot Empty { get; } = new(false, false, string.Empty, null);
    }

    public readonly record struct CodexHeartbeatSnapshot(CliHeartbeatInfo Info, DateTimeOffset? Timestamp)
    {
        public static CodexHeartbeatSnapshot Empty { get; } = new(CliHeartbeatInfo.Empty, null);
    }

    public readonly record struct CodexEnvelopeSnapshot(string EventType, DateTimeOffset? Timestamp, int Count)
    {
        public static CodexEnvelopeSnapshot Empty { get; } = new(string.Empty, null, 0);
    }

    public readonly record struct CliDiagnosticSummary(CliDiagnosticSeverity Severity, string Category, string Message, DateTimeOffset Timestamp)
    {
        public static CliDiagnosticSummary FromDiagnostic(CliDiagnostic diagnostic)
        {
            if (diagnostic == null)
                throw new ArgumentNullException(nameof(diagnostic));

            return new CliDiagnosticSummary(
                diagnostic.Severity,
                diagnostic.Category,
                diagnostic.Message,
                diagnostic.Timestamp);
        }
    }
}
