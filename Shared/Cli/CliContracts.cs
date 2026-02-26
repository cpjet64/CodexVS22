using System;
using Newtonsoft.Json.Linq;
using global::CodexVS22;

namespace CodexVS22.Shared.Cli
{
    public enum CliHostState
    {
        Stopped,
        Connecting,
        Connected,
        Reconnecting,
        Faulted,
        NeedsRestart
    }

    public enum CliDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum CliErrorKind
    {
        None,
        ProcessStart,
        WriteFailure,
        Authentication,
        Heartbeat,
        Unknown
    }

    public sealed class CliConnectionRequest
    {
        public CliConnectionRequest(CodexOptions options, string workingDirectory, CliReconnectPolicy reconnectPolicy = null)
        {
            Options = options ?? CodexVS22Package.OptionsInstance ?? new CodexOptions();
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory;
            ReconnectPolicy = reconnectPolicy ?? CliReconnectPolicy.Default;
        }

        public CodexOptions Options { get; }

        public string WorkingDirectory { get; }

        public CliReconnectPolicy ReconnectPolicy { get; }
    }

    public sealed class CliConnectionResult
    {
        public CliConnectionResult(bool isSuccess, CliError error = null)
        {
            IsSuccess = isSuccess;
            Error = error ?? CliError.None;
        }

        public bool IsSuccess { get; }

        public CliError Error { get; }

        public static CliConnectionResult Success { get; } = new(true);

        public static CliConnectionResult Failure(CliError error) => new(false, error);
    }

    public sealed class CliError
    {
        private CliError(CliErrorKind kind, string message, Exception exception = null)
        {
            Kind = kind;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public CliErrorKind Kind { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public static CliError None { get; } = new(CliErrorKind.None, string.Empty);

        public static CliError Create(CliErrorKind kind, string message, Exception exception = null) => new(kind, message, exception);
    }

    public sealed class CliReconnectPolicy
    {
        public CliReconnectPolicy(int maxAttempts, TimeSpan backoff)
        {
            MaxAttempts = Math.Max(0, maxAttempts);
            Backoff = backoff < TimeSpan.Zero ? TimeSpan.Zero : backoff;
        }

        public int MaxAttempts { get; }

        public TimeSpan Backoff { get; }

        public static CliReconnectPolicy Default { get; } = new(1, TimeSpan.FromSeconds(2));
    }

    public sealed class CliEnvelope
    {
        private CliEnvelope(string raw, JObject payload, string eventType)
        {
            Raw = raw ?? string.Empty;
            Payload = payload;
            EventType = eventType ?? string.Empty;
            ReceivedAt = DateTimeOffset.UtcNow;
        }

        public string Raw { get; }

        public JObject Payload { get; }

        public string EventType { get; }

        public DateTimeOffset ReceivedAt { get; }

        public static CliEnvelope FromRaw(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new CliEnvelope(string.Empty, null, string.Empty);

            try
            {
                var token = JObject.Parse(raw);
                var eventType = token.SelectToken("event.type")?.ToString() ?? token["type"]?.ToString() ?? string.Empty;
                return new CliEnvelope(raw, token, eventType);
            }
            catch
            {
                return new CliEnvelope(raw, null, string.Empty);
            }
        }
    }

    public sealed class CliDiagnostic
    {
        public CliDiagnostic(CliDiagnosticSeverity severity, string category, string message, Exception exception = null)
        {
            Severity = severity;
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
            Message = message ?? string.Empty;
            Exception = exception;
            Timestamp = DateTimeOffset.UtcNow;
        }

        public CliDiagnosticSeverity Severity { get; }

        public string Category { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public DateTimeOffset Timestamp { get; }

        public static CliDiagnostic Info(string category, string message) => new(CliDiagnosticSeverity.Info, category, message);

        public static CliDiagnostic Error(string category, string message, Exception exception = null) => new(CliDiagnosticSeverity.Error, category, message, exception);
    }

    public sealed class CliHeartbeatInfo
    {
        public CliHeartbeatInfo(TimeSpan interval, string operationType)
        {
            Interval = interval;
            OperationType = operationType ?? string.Empty;
        }

        public TimeSpan Interval { get; }

        public string OperationType { get; }

        public static CliHeartbeatInfo Empty { get; } = new(TimeSpan.Zero, string.Empty);
    }

    public sealed class CliHealthSnapshot
    {
        public CliHealthSnapshot(int processId, TimeSpan uptime, CliHostState state)
        {
            ProcessId = processId;
            Uptime = uptime;
            State = state;
        }

        public int ProcessId { get; }

        public TimeSpan Uptime { get; }

        public CliHostState State { get; }
    }

    public readonly struct CodexAuthenticationResult
    {
        public CodexAuthenticationResult(bool isAuthenticated, string message)
        {
            IsAuthenticated = isAuthenticated;
            Message = message ?? string.Empty;
        }

        public bool IsAuthenticated { get; }

        public string Message { get; }
    }

    public sealed class CliHostStateChangedEventArgs : EventArgs
    {
        public CliHostStateChangedEventArgs(CliHostState state)
        {
            State = state;
        }

        public CliHostState State { get; }
    }
}
