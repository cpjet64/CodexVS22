using System;
using System.Collections.Generic;
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
            var resolvedWorkingDir = ResolveWorkingDirectory(workingDir);
            var (fileName, args) = ResolveCli(options);
            if (options?.UseWsl == true)
            {
                args = BuildWslArguments(args, resolvedWorkingDir);
            }
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
                WorkingDirectory = resolvedWorkingDir
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
                var msg = ex.Message;
                await LogErrorAsync($"Start error: {msg}");
                await VS.StatusBar.ShowMessageAsync(
                    "Codex: Failed to start CLI. Configure path in Tools → Options → Codex.");
                return false;
            }

            _ = Task.Run(() => PumpAsync(_proc.StandardOutput, _cts.Token, isErr: false));
            _ = Task.Run(() => PumpAsync(_proc.StandardError, _cts.Token, isErr: true));
            OnInfo?.Invoke("codex started");
            _ = Task.Run(() => CaptureVersionAsync(options, workingDir));
            return true;
        }

        public async Task<CodexAuthenticationResult> CheckAuthenticationAsync(
            CodexOptions options,
            string workingDir)
        {
            try
            {
                var resolvedWorkingDir = ResolveWorkingDirectory(workingDir);
                var (file, args) = ResolveCodexCommand(options, "login status", resolvedWorkingDir);
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

                var result = await RunProcessOnceAsync(psi);
                var (success, message) = InterpretLoginStatus(result);

                if (success)
                {
                    if (!string.IsNullOrEmpty(message))
                        await LogInfoAsync(message);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(result.StandardError))
                    {
                        foreach (var line in SplitLines(result.StandardError))
                            await LogErrorAsync(line);
                    }

                    if (!string.IsNullOrEmpty(message))
                    {
                        foreach (var line in SplitLines(message))
                            await LogErrorAsync(line);
                    }

                    await LogErrorAsync(
                        "Codex CLI not authenticated. Run 'codex login' in a terminal.");
                }

                return new CodexAuthenticationResult(success, message);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"login status check failed: {ex.Message}");
                return new CodexAuthenticationResult(false, string.Empty);
            }
        }

        public async Task<bool> LoginAsync(CodexOptions options, string workingDir)
        {
            return await ExecuteCodexUtilityAsync(options, workingDir, "login", "login");
        }

        public async Task<bool> LogoutAsync(CodexOptions options, string workingDir)
        {
            return await ExecuteCodexUtilityAsync(options, workingDir, "logout", "logout");
        }

        public async Task<bool> SendAsync(string jsonLine)
        {
            try
            {
                StreamWriter writer;
                lock (_gate) writer = _stdin;
                if (writer == null)
                    return false;
                await writer.WriteLineAsync(jsonLine);
                await writer.FlushAsync();
                return true;
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Write error: {ex.Message}");
                await TryReconnectAsync();
                return false;
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

        private static (string fileName, string args) ResolveCodexCommand(
            CodexOptions options,
            string subcommand,
            string workingDir)
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

        private static async Task<ProcessResult> RunProcessOnceAsync(ProcessStartInfo psi)
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.Run(() => process.WaitForExit());

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new ProcessResult(process.ExitCode, stdout ?? string.Empty, stderr ?? string.Empty);
        }

        private async Task<bool> ExecuteCodexUtilityAsync(
            CodexOptions options,
            string workingDir,
            string subcommand,
            string friendlyName)
        {
            try
            {
                var resolvedWorkingDir = ResolveWorkingDirectory(workingDir);
                var (file, args) = ResolveCodexCommand(options, subcommand, resolvedWorkingDir);
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

                await LogInfoAsync($"codex {friendlyName} starting...");
                var result = await RunProcessOnceAsync(psi);

                if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    foreach (var line in SplitLines(result.StandardOutput))
                        await LogInfoAsync(line);
                }

                if (!string.IsNullOrWhiteSpace(result.StandardError))
                {
                    foreach (var line in SplitLines(result.StandardError))
                        await LogErrorAsync(line);
                }

                if (result.ExitCode != 0)
                {
                    await LogErrorAsync(
                        $"codex {friendlyName} failed with exit code {result.ExitCode}");
                }

                return result.ExitCode == 0;
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"codex {friendlyName} failed: {ex.Message}");
                return false;
            }
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
            using var reader = new StringReader(value);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    yield return line.TrimEnd();
            }
        }

        private static (bool success, string message) InterpretLoginStatus(ProcessResult result)
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

            // Command succeeded but output is unfamiliar; surface it while assuming success.
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

        private readonly struct ProcessResult
        {
            public ProcessResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            public int ExitCode { get; }
            public string StandardOutput { get; }
            public string StandardError { get; }
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

        private static async Task EnsureDiagnosticsPaneAsync()
        {
            await DiagnosticsPane.GetAsync();
        }

        private static int _rateCount;
        private static int _rateSecond;
        private const int MaxPerSecond = 20;

        private static async Task WriteThrottledAsync(string prefix, string message)
        {
            var now = DateTime.Now;
            if (_rateSecond != now.Second)
            {
                _rateSecond = now.Second;
                _rateCount = 0;
            }
            if (_rateCount++ > MaxPerSecond)
            {
                // Drop excessive logs to keep VS responsive
                return;
            }
            var pane = await DiagnosticsPane.GetAsync();
            await pane.WriteLineAsync($"[{prefix}] {now:HH:mm:ss} {message}");
        }

        private static Task LogInfoAsync(string message) => WriteThrottledAsync("info", message);
        private static Task LogErrorAsync(string message) => WriteThrottledAsync("err ", message);

        public static string LastVersion { get; private set; }
        public static string LastRolloutPath { get; set; }

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
                using var p = Process.Start(psi);
                var outText = await p.StandardOutput.ReadToEndAsync();
                p.WaitForExit(5000);
                LastVersion = outText.Trim();
                await LogInfoAsync($"CLI version: {LastVersion}");
                // If event stream includes rollout_path later, capture it there (T3.11)
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"version check failed: {ex.Message}");
            }
        }
    }
}
