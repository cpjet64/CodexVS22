using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodexVS22.Shared.Cli
{
    public interface ICodexCliHost : IDisposable
    {
        CliHostState State { get; }

        event EventHandler<CliHostStateChangedEventArgs> StateChanged;

        event EventHandler<CliEnvelope> EnvelopeReceived;

        event EventHandler<CliDiagnostic> DiagnosticReceived;

        Task<CliConnectionResult> ConnectAsync(CliConnectionRequest request, CancellationToken cancellationToken);

        Task DisconnectAsync(CancellationToken cancellationToken);

        Task<bool> SendAsync(string payload, CancellationToken cancellationToken = default);

        Task<CodexAuthenticationResult> CheckAuthenticationAsync(CancellationToken cancellationToken);

        Task<bool> LoginAsync(CancellationToken cancellationToken);

        Task<bool> LogoutAsync(CancellationToken cancellationToken);

        Task<CliHeartbeatInfo> EnsureHeartbeatAsync(CancellationToken cancellationToken);

        Task<CliHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken);
    }
}
