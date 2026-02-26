using System;
using System.Threading;
using System.Threading.Tasks;
using CodexVS22.Core.Cli;
using CodexVS22.Shared.Cli;
using CodexVS22.Shared.Utilities;
using global::CodexVS22;

namespace CodexVS22.Core
{
    /// <summary>
    /// Legacy adapter that preserves the historic CodexCliHost surface while delegating to the new
    /// CLI session service. This allows existing tool window logic to compile while the refactor
    /// migrates modules onto the shared abstractions.
    /// </summary>
    public sealed class CodexCliHost : IDisposable
    {
        private readonly CliSessionService _session;
        private readonly CancellationTokenSource _cts = new();

        public CodexCliHost()
            : this(ServiceLocator.GetRequiredService<CliSessionService>())
        {
        }

        internal CodexCliHost(CliSessionService session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.StdoutLineReceived += OnStdout;
            _session.DiagnosticReceived += OnDiagnostic;
        }

        public event Action<string> OnStdoutLine;
        public event Action<string> OnStderrLine;
        public event Action<string> OnInfo;

        public static string LastVersion { get; private set; }
        public static string LastRolloutPath { get; set; }

        public async Task<bool> StartAsync(CodexOptions options, string workingDir)
        {
            var result = await _session.ConnectAsync(options, workingDir, _cts.Token).ConfigureAwait(false);
            return result.IsSuccess;
        }

        public async Task<bool> SendAsync(string jsonLine)
        {
            if (string.IsNullOrWhiteSpace(jsonLine))
                return false;

            return await _session.SendRawAsync(jsonLine, _cts.Token).ConfigureAwait(false);
        }

        public async Task<CodexAuthenticationResult> CheckAuthenticationAsync(CodexOptions options, string workingDir)
        {
            await _session.ConnectAsync(options, workingDir, _cts.Token).ConfigureAwait(false);
            return await _session.CheckAuthenticationAsync(_cts.Token).ConfigureAwait(false);
        }

        public Task<bool> LoginAsync(CodexOptions options, string workingDir)
        {
            return LoginCoreAsync(options, workingDir);
        }

        public Task<bool> LogoutAsync(CodexOptions options, string workingDir)
        {
            return LogoutCoreAsync(options, workingDir);
        }

        public void Dispose()
        {
            _session.StdoutLineReceived -= OnStdout;
            _session.DiagnosticReceived -= OnDiagnostic;
            _cts.Cancel();
            try
            {
                _session.DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // ignore disposal failures
            }
            finally
            {
                _cts.Dispose();
            }
        }

        private void OnStdout(object sender, string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            OnStdoutLine?.Invoke(line);
        }

        private void OnDiagnostic(object sender, CliDiagnostic diagnostic)
        {
            if (diagnostic == null)
                return;

            if (diagnostic.Severity == CliDiagnosticSeverity.Error)
            {
                OnStderrLine?.Invoke(diagnostic.Message);
            }
            else
            {
                OnInfo?.Invoke(diagnostic.Message);
                if (diagnostic.Message.StartsWith("CLI version:", StringComparison.OrdinalIgnoreCase))
                {
                    LastVersion = diagnostic.Message.Substring("CLI version:".Length).Trim();
                }
            }
        }

        private async Task<bool> LoginCoreAsync(CodexOptions options, string workingDir)
        {
            var connect = await _session.ConnectAsync(options, workingDir, _cts.Token).ConfigureAwait(false);
            if (!connect.IsSuccess)
                return false;
            return await _session.LoginAsync(_cts.Token).ConfigureAwait(false);
        }

        private async Task<bool> LogoutCoreAsync(CodexOptions options, string workingDir)
        {
            var connect = await _session.ConnectAsync(options, workingDir, _cts.Token).ConfigureAwait(false);
            if (!connect.IsSuccess)
                return false;
            return await _session.LogoutAsync(_cts.Token).ConfigureAwait(false);
        }
    }
}
