using System;
using System.Threading;
using System.Threading.Tasks;
using CodexVS22.Shared.Cli;
using Newtonsoft.Json.Linq;
using global::CodexVS22;

namespace CodexVS22.Core.Cli
{
    public sealed class CliSessionService : IDisposable
    {
        private readonly ICodexCliHost _host;
        private readonly ICliMessageRouter _router;
        private readonly ICliMessageSerializer _serializer;
        private readonly ICodexOptionsProvider _optionsProvider;

        private bool _subscribed;
        private CliHeartbeatTemplate _heartbeatTemplate;

        public CliSessionService(
            ICodexCliHost host,
            ICliMessageRouter router,
            ICliMessageSerializer serializer,
            ICodexOptionsProvider optionsProvider)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
        }

        public event EventHandler<string> StdoutLineReceived;

        public event EventHandler<CliDiagnostic> DiagnosticReceived;

        public CliHostState State => _host.State;

        public async Task<CliConnectionResult> ConnectAsync(CodexOptions optionsOverride, string workingDirectory, CancellationToken cancellationToken)
        {
            if (!_subscribed)
            {
                _host.EnvelopeReceived += OnEnvelopeReceived;
                _host.DiagnosticReceived += OnDiagnosticReceived;
                _subscribed = true;
            }

            var options = optionsOverride ?? _optionsProvider.GetCurrentOptions() ?? new CodexOptions();
            var workingDir = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory;
            var request = new CliConnectionRequest(options, workingDir);
            return await _host.ConnectAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            return _host.DisconnectAsync(cancellationToken);
        }

        public Task<bool> SendRawAsync(string payload, CancellationToken cancellationToken)
        {
            return _host.SendAsync(payload, cancellationToken);
        }

        public Task<bool> SendUserInputAsync(string text, CancellationToken cancellationToken)
        {
            var payload = _serializer.CreateUserInputSubmission(text);
            return SendRawAsync(payload, cancellationToken);
        }

        public Task<bool> SendExecCancelAsync(string execId, CancellationToken cancellationToken)
        {
            var payload = _serializer.CreateExecCancel(execId);
            return SendRawAsync(payload, cancellationToken);
        }

        public async Task<CliHeartbeatInfo> EnsureHeartbeatAsync(CancellationToken cancellationToken)
        {
            if (_heartbeatTemplate != null)
            {
                if (_host is ProcessCodexCliHost processHost)
                {
                    processHost.SetHeartbeatInfo(_heartbeatTemplate.ToInfo());
                }

                var payload = _serializer.CreateHeartbeatSubmission(_heartbeatTemplate.OpTemplate);
                if (!string.IsNullOrEmpty(payload))
                    await _host.SendAsync(payload, cancellationToken).ConfigureAwait(false);

                return _heartbeatTemplate.ToInfo();
            }

            return await _host.EnsureHeartbeatAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task<CodexAuthenticationResult> CheckAuthenticationAsync(CancellationToken cancellationToken)
        {
            return _host.CheckAuthenticationAsync(cancellationToken);
        }

        public Task<bool> LoginAsync(CancellationToken cancellationToken)
        {
            return _host.LoginAsync(cancellationToken);
        }

        public Task<bool> LogoutAsync(CancellationToken cancellationToken)
        {
            return _host.LogoutAsync(cancellationToken);
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                _host.EnvelopeReceived -= OnEnvelopeReceived;
                _host.DiagnosticReceived -= OnDiagnosticReceived;
                _subscribed = false;
            }
        }

        private void OnEnvelopeReceived(object sender, CliEnvelope envelope)
        {
            StdoutLineReceived?.Invoke(this, envelope.Raw);

            if (envelope.Payload is JObject payload && CliHeartbeatHelper.TryCreateHeartbeatTemplate(payload, out var template))
            {
                _heartbeatTemplate = template;
                if (_host is ProcessCodexCliHost processHost)
                {
                    processHost.SetHeartbeatInfo(template.ToInfo());
                }
            }

            _ = _router.RouteAsync(envelope);
        }

        private void OnDiagnosticReceived(object sender, CliDiagnostic diagnostic)
        {
            DiagnosticReceived?.Invoke(this, diagnostic);
        }
    }
}
