using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;

namespace CodexVS22.Core
{
    public sealed class CodexCliHost : IDisposable
    {
        private readonly object _gate = new();
        private Process _proc;
        private StreamWriter _stdin;
        private CancellationTokenSource _cts;
        private string _lastResolved;
        private CodexOptions _lastOptions;
        private string _lastWorkingDir;
        private bool _reconnected;

        public event Action<string> OnStdoutLine;
        public event Action<string> OnStderrLine;
        public event Action<string> OnInfo;

        public async Task<bool> StartAsync(CodexOptions options, string workingDir)
        {
            await EnsureDiagnosticsPaneAsync();

            _lastOptions = options;
            _lastWorkingDir = workingDir;
            _reconnected = false;
            var (fileName, args) = ResolveCli(options);
            _lastResolved = $"{fileName} {args}";
            await LogInfoAsync($"Resolved CLI: {_lastResolved}");

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrEmpty(workingDir) ? Environment.CurrentDirectory : workingDir
            };

            _cts = new CancellationTokenSource();
            try
            {
                lock (_gate)
                {
                    _proc = Process.Start(startInfo);
                    _stdin = _proc?.StandardInput;
                }
                if (_proc == null)
                {
                    await LogErrorAsync("Failed to start codex process");
                    return false;
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Start error: {ex.Message}");
                return false;
            }

            _ = Task.Run(() => PumpAsync(_proc.StandardOutput, _cts.Token, isErr: false));
            _ = Task.Run(() => PumpAsync(_proc.StandardError, _cts.Token, isErr: true));
            OnInfo?.Invoke("codex started");
            return true;
        }

        public async Task CheckAuthenticationAsync(CodexOptions options, string workingDir)
        {
            try
            {
                var (file, args) = ResolveCli(options);
                // Convert proto command to whoami for check when not using WSL
                if (file.Equals("wsl.exe", StringComparison.OrdinalIgnoreCase))
                    args = "-- codex whoami";
                else
                    (file, args) = (file, "whoami");

                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = string.IsNullOrEmpty(workingDir) ? Environment.CurrentDirectory : workingDir
                };
                using var p = Process.Start(psi);
                var outText = await p.StandardOutput.ReadToEndAsync();
                var errText = await p.StandardError.ReadToEndAsync();
                p.WaitForExit(5000);
                if (p.ExitCode != 0 || outText.Contains("not logged", StringComparison.OrdinalIgnoreCase))
                {
                    await LogErrorAsync("Codex CLI not authenticated. Run 'codex login' in a terminal.");
                }
                else
                {
                    await LogInfoAsync($"whoami: {outText.Trim()}");
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"whoami check failed: {ex.Message}");
            }
        }

        public async Task SendAsync(string jsonLine)
        {
            try
            {
                StreamWriter writer;
                lock (_gate) writer = _stdin;
                if (writer == null)
                    return;
                await writer.WriteLineAsync(jsonLine);
                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Write error: {ex.Message}");
                await TryReconnectAsync();
            }
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
                if (_proc != null && !_proc.HasExited)
                {
                    _proc.Kill();
                }
                _proc?.Dispose();
                _stdin?.Dispose();
            }
            catch { /* ignored */ }
        }

        private async Task PumpAsync(StreamReader reader, CancellationToken token, bool isErr)
        {
            try
            {
                string line;
                while (!token.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
                {
                    if (isErr)
                    {
                        OnStderrLine?.Invoke(line);
                        await LogErrorAsync(line);
                    }
                    else
                    {
                        OnStdoutLine?.Invoke(line);
                    }
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Pump error: {ex.Message}");
            }
        }

        private async Task TryReconnectAsync()
        {
            if (_reconnected) return;
            _reconnected = true;
            await LogInfoAsync("Attempting one-shot reconnect...");
            Dispose();
            await StartAsync(_lastOptions, _lastWorkingDir);
        }

        private static (string fileName, string args) ResolveCli(CodexOptions options)
        {
            var exe = (options?.CliExecutable ?? string.Empty).Trim();
            var useWsl = options?.UseWsl == true;

            if (useWsl)
            {
                // wsl.exe -- codex proto
                return ("wsl.exe", "-- codex proto");
            }
            if (!string.IsNullOrEmpty(exe))
            {
                return (exe, "proto");
            }
            return ("codex", "proto");
        }

        private static async Task EnsureDiagnosticsPaneAsync()
        {
            await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        }

        private static async Task LogInfoAsync(string message)
        {
            var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
            await pane.WriteLineAsync($"[info] {DateTime.Now:HH:mm:ss} {message}");
        }

        private static async Task LogErrorAsync(string message)
        {
            var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
            await pane.WriteLineAsync($"[err ] {DateTime.Now:HH:mm:ss} {message}");
        }
    }
}
