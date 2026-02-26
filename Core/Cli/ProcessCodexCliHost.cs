using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodexVS22.Shared.Cli;
using Newtonsoft.Json.Linq;
using global::CodexVS22;

namespace CodexVS22.Core.Cli
{
    public sealed class ProcessCodexCliHost : ICodexCliHost
    {
        private readonly ICliDiagnosticsSink _diagnosticsSink;
        private readonly object _gate = new();

        public event EventHandler<CliEnvelope> EnvelopeReceived;
        public event EventHandler<CliDiagnostic> DiagnosticReceived;

        private Process _process;
        private StreamWriter _stdin;
        private CancellationTokenSource _pumpCts;
        private CliConnectionRequest _currentRequest;
        private DateTimeOffset _startTime;
        private CliReconnectPolicy _reconnectPolicy;
        private CliHeartbeatInfo _heartbeatInfo = CliHeartbeatInfo.Empty;

        private CliHostState _state = CliHostState.Stopped;

        public ProcessCodexCliHost(ICliDiagnosticsSink diagnosticsSink)
        {
            _diagnosticsSink = diagnosticsSink ?? throw new ArgumentNullException(nameof(diagnosticsSink));
        }

        public event EventHandler<CliHostStateChangedEventArgs> StateChanged;

        public CliHostState State
        {
            get => _state;
            private set
            {
                if (_state == value)
                    return;
                _state = value;
                StateChanged?.Invoke(this, new CliHostStateChangedEventArgs(value));
            }
        }

        public async Task<CliConnectionResult> ConnectAsync(CliConnectionRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (State == CliHostState.Connected)
                return CliConnectionResult.Success;

            State = CliHostState.Connecting;

            try
            {
                var resolvedWorkingDir = ResolveWorkingDirectory(request.WorkingDirectory);
                var (fileName, args) = ResolveCli(request.Options);
                if (request.Options?.UseWsl == true)
                {
                    args = BuildWslArguments(args, resolvedWorkingDir);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = resolvedWorkingDir
                };

                _pumpCts = new CancellationTokenSource();

                lock (_gate)
                {
                    _process = Process.Start(startInfo);
                    _stdin = _process?.StandardInput;
                }

                if (_process == null)
                {
                    await PublishDiagnosticAsync(CliDiagnostic.Error("Process", "Failed to start codex process"), cancellationToken).ConfigureAwait(false);
                    State = CliHostState.Faulted;
                    return CliConnectionResult.Failure(CliError.Create(CliErrorKind.ProcessStart, "Process did not start"));
                }

                _currentRequest = request;
                _reconnectPolicy = request.ReconnectPolicy;
                _startTime = DateTimeOffset.UtcNow;

                _ = Task.Run(() => PumpStdoutAsync(_process.StandardOutput, _pumpCts.Token), CancellationToken.None);
                _ = Task.Run(() => PumpStderrAsync(_process.StandardError, _pumpCts.Token), CancellationToken.None);

                await PublishDiagnosticAsync(CliDiagnostic.Info("Process", $"codex started (pid {_process.Id})"), cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => CaptureVersionAsync(request.Options, resolvedWorkingDir), CancellationToken.None);

                State = CliHostState.Connected;
                return CliConnectionResult.Success;
            }
            catch (Exception ex)
            {
                await PublishDiagnosticAsync(CliDiagnostic.Error("Process", $"Start error: {ex.Message}", ex), cancellationToken).ConfigureAwait(false);
                State = CliHostState.Faulted;
                return CliConnectionResult.Failure(CliError.Create(CliErrorKind.ProcessStart, ex.Message, ex));
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            State = CliHostState.Stopped;
            try
            {
                _pumpCts?.Cancel();
                Process process;
                lock (_gate)
                {
                    process = _process;
                    _process = null;
                    _stdin?.Dispose();
                    _stdin = null;
                }

                if (process != null)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }

                    process.Dispose();
                }

                await PublishDiagnosticAsync(CliDiagnostic.Info("Process", "codex stopped"), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await PublishDiagnosticAsync(CliDiagnostic.Error("Process", $"Stop failed: {ex.Message}", ex), cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<bool> SendAsync(string payload, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            try
            {
                StreamWriter writer;
                lock (_gate)
                {
                    writer = _stdin;
                }

                if (writer == null)
                    return false;

                await writer.WriteLineAsync(payload).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                await PublishDiagnosticAsync(CliDiagnostic.Error("Transport", $"Write error: {ex.Message}", ex), cancellationToken).ConfigureAwait(false);
                await AttemptReconnectAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        public async Task<CodexAuthenticationResult> CheckAuthenticationAsync(CancellationToken cancellationToken)
        {
            var request = _currentRequest ?? new CliConnectionRequest(CodexVS22Package.OptionsInstance ?? new CodexOptions(), Environment.CurrentDirectory);
            try
            {
                var resolvedWorkingDir = ResolveWorkingDirectory(request.WorkingDirectory);
                var (file, args) = ResolveCodexCommand(request.Options, "login status", resolvedWorkingDir);
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = resolvedWorkingDir
                };

                var result = await RunProcessOnceAsync(psi, cancellationToken).ConfigureAwait(false);
                var (success, message) = InterpretLoginStatus(result);

                if (success)
                {
                    if (!string.IsNullOrEmpty(message))
                        await PublishDiagnosticAsync(CliDiagnostic.Info("Auth", message), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(result.StandardError))
                    {
                        foreach (var line in SplitLines(result.StandardError))
                            await PublishDiagnosticAsync(CliDiagnostic.Error("Auth", line), cancellationToken).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(message))
                    {
                        foreach (var line in SplitLines(message))
                            await PublishDiagnosticAsync(CliDiagnostic.Error("Auth", line), cancellationToken).ConfigureAwait(false);
                    }

                    await PublishDiagnosticAsync(CliDiagnostic.Error("Auth", "Codex CLI not authenticated. Run 'codex login' in a terminal."), cancellationToken).ConfigureAwait(false);
                }

                return new CodexAuthenticationResult(success, message);
            }
            catch (Exception ex)
            {
                await PublishDiagnosticAsync(CliDiagnostic.Error("Auth", $"login status check failed: {ex.Message}", ex), cancellationToken).ConfigureAwait(false);
                return new CodexAuthenticationResult(false, string.Empty);
            }
        }

        public Task<bool> LoginAsync(CancellationToken cancellationToken)
        {
            var request = _currentRequest ?? new CliConnectionRequest(CodexVS22Package.OptionsInstance ?? new CodexOptions(), Environment.CurrentDirectory);
            return ExecuteCodexUtilityAsync(request, "login", "login", cancellationToken);
        }

        public Task<bool> LogoutAsync(CancellationToken cancellationToken)
        {
            var request = _currentRequest ?? new CliConnectionRequest(CodexVS22Package.OptionsInstance ?? new CodexOptions(), Environment.CurrentDirectory);
            return ExecuteCodexUtilityAsync(request, "logout", "logout", cancellationToken);
        }

        public Task<CliHeartbeatInfo> EnsureHeartbeatAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_heartbeatInfo);
        }

        public Task<CliHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken)
        {
            Process process;
            lock (_gate)
            {
                process = _process;
            }

            var uptime = process != null && !_startTime.Equals(default)
                ? DateTimeOffset.UtcNow - _startTime
                : TimeSpan.Zero;

            var snapshot = new CliHealthSnapshot(process?.Id ?? 0, uptime, State);
            return Task.FromResult(snapshot);
        }

        public void Dispose()
        {
            _pumpCts?.Cancel();
            lock (_gate)
            {
                _stdin?.Dispose();
                if (_process != null)
                {
                    try
                    {
                        if (!_process.HasExited)
                            _process.Kill();
                    }
                    catch
                    {
                        // ignore
                    }

                    _process.Dispose();
                    _process = null;
                }
            }
        }

        internal void SetHeartbeatInfo(CliHeartbeatInfo heartbeatInfo)
        {
            _heartbeatInfo = heartbeatInfo ?? CliHeartbeatInfo.Empty;
        }

        private async Task PumpStdoutAsync(StreamReader reader, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                        break;

                    EnvelopeReceived?.Invoke(this, CliEnvelope.FromRaw(line));
                }
            }
            catch (Exception ex)
            {
                await PublishDiagnosticAsync(CliDiagnostic.Error("Pump", $"Stdout pump error: {ex.Message}", ex), token).ConfigureAwait(false);
            }
        }

        private async Task PumpStderrAsync(StreamReader reader, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                        break;

                    await PublishDiagnosticAsync(CliDiagnostic.Error("StdErr", line), token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await PublishDiagnosticAsync(CliDiagnostic.Error("Pump", $"Stderr pump error: {ex.Message}", ex), token).ConfigureAwait(false);
            }
        }

        private async Task PublishDiagnosticAsync(CliDiagnostic diagnostic, CancellationToken cancellationToken)
        {
            if (diagnostic == null)
                return;

            DiagnosticReceived?.Invoke(this, diagnostic);
            await _diagnosticsSink.LogAsync(diagnostic, cancellationToken).ConfigureAwait(false);
        }

        private async Task AttemptReconnectAsync(CancellationToken cancellationToken)
        {
            if (_reconnectPolicy == null || _reconnectPolicy.MaxAttempts <= 0 || _currentRequest == null)
                return;

            State = CliHostState.Reconnecting;

            for (var attempt = 0; attempt < _reconnectPolicy.MaxAttempts; attempt++)
            {
                await PublishDiagnosticAsync(CliDiagnostic.Info("Transport", $"Attempting reconnect {attempt + 1}/{_reconnectPolicy.MaxAttempts}"), cancellationToken).ConfigureAwait(false);
                var result = await ConnectAsync(_currentRequest, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                    return;

                await Task.Delay(_reconnectPolicy.Backoff, cancellationToken).ConfigureAwait(false);
            }

            State = CliHostState.Faulted;
        }

        private async Task<bool> ExecuteCodexUtilityAsync(CliConnectionRequest request, string subcommand, string friendlyName, CancellationToken cancellationToken)
        {
            try
            {
                var resolvedWorkingDir = ResolveWorkingDirectory(request.WorkingDirectory);
                var (file, args) = ResolveCodexCommand(request.Options, subcommand, resolvedWorkingDir);
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    CreateNoWindow = true,
                    WorkingDirectory = resolvedWorkingDir
                };

                await PublishDiagnosticAsync(CliDiagnostic.Info("Process", $"codex {friendlyName} starting..."), cancellationToken).ConfigureAwait(false);
                var result = await RunProcessOnceAsync(psi, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    foreach (var line in SplitLines(result.StandardOutput))
                        await PublishDiagnosticAsync(CliDiagnostic.Info("Process", line), cancellationToken).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(result.StandardError))
                {
                    foreach (var line in SplitLines(result.StandardError))
                        await PublishDiagnosticAsync(CliDiagnostic.Error("Process", line), cancellationToken).ConfigureAwait(false);
                }

                if (result.ExitCode != 0)
                {
                    await PublishDiagnosticAsync(CliDiagnostic.Error("Process", $"codex {friendlyName} failed with exit code {result.ExitCode}"), cancellationToken).ConfigureAwait(false);
                }

                return result.ExitCode == 0;
            }
            catch (Exception ex)
            {
                await PublishDiagnosticAsync(CliDiagnostic.Error("Process", $"codex {friendlyName} failed: {ex.Message}", ex), cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcessOnceAsync(ProcessStartInfo psi, CancellationToken cancellationToken)
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return (process.ExitCode, stdout ?? string.Empty, stderr ?? string.Empty);
        }

        private async Task CaptureVersionAsync(CodexOptions options, string workingDir)
        {
            try
            {
                var resolvedWorkingDir = ResolveWorkingDirectory(workingDir);
                var (file, args) = ResolveCli(options);
                if (file.Equals("wsl.exe", StringComparison.OrdinalIgnoreCase))
                    args = BuildWslArguments("-- codex --version", resolvedWorkingDir);
                else
                    args = "--version";
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = resolvedWorkingDir
                };
                using var process = Process.Start(psi);
                var outText = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await PublishDiagnosticAsync(CliDiagnostic.Info("Process", $"CLI version: {outText.Trim()}"), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await PublishDiagnosticAsync(CliDiagnostic.Error("Process", $"version check failed: {ex.Message}"), CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static (string fileName, string args) ResolveCli(CodexOptions options)
        {
            var exe = (options?.CliExecutable ?? string.Empty).Trim();
            var useWsl = options?.UseWsl == true;

            if (useWsl)
            {
                return ("wsl.exe", "-- codex proto");
            }
            if (!string.IsNullOrEmpty(exe))
            {
                return (exe, "proto");
            }
            return ("codex", "proto");
        }

        private static (string fileName, string args) ResolveCodexCommand(CodexOptions options, string subcommand, string workingDir)
        {
            var exe = (options?.CliExecutable ?? string.Empty).Trim();
            if (options?.UseWsl == true)
            {
                var args = $"-- codex {subcommand}";
                args = BuildWslArguments(args, workingDir);
                return ("wsl.exe", args);
            }

            if (!string.IsNullOrEmpty(exe))
            {
                return (exe, subcommand);
            }

            return ("codex", subcommand);
        }

        private static string ResolveWorkingDirectory(string workingDir)
        {
            return string.IsNullOrEmpty(workingDir) ? Environment.CurrentDirectory : workingDir;
        }

        private static string BuildWslArguments(string args, string workingDir)
        {
            if (string.IsNullOrWhiteSpace(args) || args.Contains("--cd"))
                return args;

            var wslPath = ConvertWindowsPathToWsl(workingDir);
            if (string.IsNullOrWhiteSpace(wslPath))
                return args;

            return $"--cd {EscapeWslArgument(wslPath)} {args}";
        }

        private static string ConvertWindowsPathToWsl(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            if (path.StartsWith("/", StringComparison.Ordinal))
                return path;

            if (path.StartsWith("\\\\wsl$\\", StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = path.Substring("\\\\wsl$\\".Length).Replace('\\', '/');
                return $"/{trimmed}";
            }

            path = path.Replace('\\', '/');
            if (path.Length >= 2 && path[1] == ':')
            {
                var drive = char.ToLowerInvariant(path[0]);
                var remainder = path.Substring(2).TrimStart('/');
                return string.IsNullOrEmpty(remainder)
                    ? $"/mnt/{drive}"
                    : $"/mnt/{drive}/{remainder}";
            }

            return path;
        }

        private static string EscapeWslArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\".\"";

            var escaped = value.Replace("\"", "\\\"");
            return $"\"{escaped}\"";
        }

        private static IEnumerable<string> SplitLines(string value)
        {
            using var reader = new StringReader(value ?? string.Empty);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    yield return line.TrimEnd();
            }
        }

        private static (bool success, string message) InterpretLoginStatus((int ExitCode, string StandardOutput, string StandardError) result)
        {
            var stdout = (result.StandardOutput ?? string.Empty).Trim();
            var stderr = (result.StandardError ?? string.Empty).Trim();
            var combined = Combine(stdout, stderr);

            if (result.ExitCode != 0)
            {
                return (false, combined);
            }

            if (IsLoggedIn(stdout) || IsLoggedIn(stderr))
            {
                var message = !string.IsNullOrWhiteSpace(stdout) ? stdout : stderr;
                return (true, message);
            }

            if (IndicatesLoggedOut(stdout) || IndicatesLoggedOut(stderr))
            {
                return (false, combined);
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                return (false, combined);
            }

            if (string.IsNullOrWhiteSpace(combined))
            {
                return (true, string.Empty);
            }

            return (true, combined);
        }

        private static string Combine(string stdout, string stderr)
        {
            if (string.IsNullOrWhiteSpace(stdout))
                return string.IsNullOrWhiteSpace(stderr) ? string.Empty : stderr;
            if (string.IsNullOrWhiteSpace(stderr))
                return stdout;
            return stdout + Environment.NewLine + stderr;
        }

        private static bool IsLoggedIn(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return text.IndexOf("logged in", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IndicatesLoggedOut(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            if (text.IndexOf("not logged", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (text.IndexOf("log in", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("logged in", StringComparison.OrdinalIgnoreCase) < 0)
                return true;

            return false;
        }
    }
}