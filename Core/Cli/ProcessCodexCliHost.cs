using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private string _activeThreadId = string.Empty;
        private string _connectWorkingDirectory = string.Empty;
        private long _rpcCounter;
        private bool _threadStartRequested;
        private readonly Queue<string> _pendingUserInputs = new();
        private readonly ConcurrentDictionary<string, string> _pendingRequestKinds = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> _approvalRequestByCallId = new(StringComparer.Ordinal);

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
                _connectWorkingDirectory = resolvedWorkingDir;
                _activeThreadId = string.Empty;
                _threadStartRequested = false;
                lock (_gate)
                {
                    _pendingUserInputs.Clear();
                }
                _pendingRequestKinds.Clear();
                _approvalRequestByCallId.Clear();

                _ = Task.Run(() => PumpStdoutAsync(_process.StandardOutput, _pumpCts.Token), CancellationToken.None);
                _ = Task.Run(() => PumpStderrAsync(_process.StandardError, _pumpCts.Token), CancellationToken.None);

                await PublishDiagnosticAsync(CliDiagnostic.Info("Process", $"codex started (pid {_process.Id})"), cancellationToken).ConfigureAwait(false);
                await InitializeAppServerSessionAsync(request, resolvedWorkingDir, cancellationToken).ConfigureAwait(false);
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
                var root = TryParseJson(payload);
                if (root != null)
                {
                    var method = root.Value<string>("method");
                    if (!string.IsNullOrWhiteSpace(method))
                    {
                        return await SendLineAsync(root.ToString(Newtonsoft.Json.Formatting.None), cancellationToken).ConfigureAwait(false);
                    }

                    var op = root["op"] as JObject;
                    var opType = op?.Value<string>("type");
                    if (!string.IsNullOrWhiteSpace(opType))
                    {
                        switch (opType)
                        {
                            case "user_input":
                                return await SendUserInputViaAppServerAsync(op, cancellationToken).ConfigureAwait(false);
                            case "exec_cancel":
                                return await SendTurnInterruptViaAppServerAsync(op, cancellationToken).ConfigureAwait(false);
                            case "exec_approval":
                            case "patch_approval":
                                return await SendApprovalViaAppServerAsync(op, cancellationToken).ConfigureAwait(false);
                            case "list_mcp_tools":
                                return await SendRpcRequestAsync("mcpServerStatus/list", new JObject { ["limit"] = 200 }, "mcp-list", cancellationToken).ConfigureAwait(false);
                            case "list_custom_prompts":
                                return await SendRpcRequestAsync("skills/list", BuildSkillsListParams(), "custom-prompts", cancellationToken).ConfigureAwait(false);
                            case "heartbeat":
                            case "noop":
                                return true;
                        }
                    }
                }

                return await SendLineAsync(payload, cancellationToken).ConfigureAwait(false);
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

                    foreach (var mapped in MapInboundLine(line))
                    {
                        EnvelopeReceived?.Invoke(this, CliEnvelope.FromRaw(mapped));
                    }
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

        private async Task InitializeAppServerSessionAsync(CliConnectionRequest request, string workingDir, CancellationToken cancellationToken)
        {
            var initializeParams = new JObject
            {
                ["clientInfo"] = new JObject
                {
                    ["name"] = "codexvs22",
                    ["title"] = "Codex VS22",
                    ["version"] = "0.2.0"
                }
            };

            await SendRpcRequestAsync("initialize", initializeParams, "initialize", cancellationToken).ConfigureAwait(false);
            await SendRpcNotificationAsync("initialized", new JObject(), cancellationToken).ConfigureAwait(false);
            await RequestThreadStartAsync(request.Options, workingDir, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> SendUserInputViaAppServerAsync(JObject op, CancellationToken cancellationToken)
        {
            var text = ExtractUserInputText(op);
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (string.IsNullOrWhiteSpace(_activeThreadId))
            {
                lock (_gate)
                {
                    _pendingUserInputs.Enqueue(text);
                }

                var request = _currentRequest ?? new CliConnectionRequest(CodexVS22Package.OptionsInstance ?? new CodexOptions(), _connectWorkingDirectory);
                await RequestThreadStartAsync(request.Options, ResolveWorkingDirectory(request.WorkingDirectory), cancellationToken).ConfigureAwait(false);
                return true;
            }

            return await SendTurnStartAsync(text, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> SendTurnInterruptViaAppServerAsync(JObject op, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_activeThreadId))
                return true;

            var turnId = op.Value<string>("id") ?? op.Value<string>("call_id") ?? string.Empty;
            var parameters = new JObject
            {
                ["threadId"] = _activeThreadId
            };

            if (!string.IsNullOrWhiteSpace(turnId))
                parameters["turnId"] = turnId;

            return await SendRpcRequestAsync("turn/interrupt", parameters, "turn-interrupt", cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> SendApprovalViaAppServerAsync(JObject op, CancellationToken cancellationToken)
        {
            var callId = op.Value<string>("call_id") ?? op.Value<string>("id") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(callId))
                return false;

            if (!_approvalRequestByCallId.TryRemove(callId, out var requestId))
            {
                return false;
            }

            var approved = op.Value<bool?>("approved");
            var decision = op.Value<string>("decision");
            if (!approved.HasValue)
            {
                approved = string.Equals(decision, "approved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(decision, "accept", StringComparison.OrdinalIgnoreCase);
            }

            var result = approved == true ? "accept" : "decline";
            var payload = new JObject
            {
                ["id"] = requestId,
                ["result"] = result
            };

            return await SendLineAsync(payload.ToString(Newtonsoft.Json.Formatting.None), cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> SendTurnStartAsync(string text, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_activeThreadId))
                return false;

            var parameters = new JObject
            {
                ["threadId"] = _activeThreadId,
                ["input"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = text ?? string.Empty
                    }
                }
            };

            return await SendRpcRequestAsync("turn/start", parameters, "turn-start", cancellationToken).ConfigureAwait(false);
        }

        private async Task RequestThreadStartAsync(CodexOptions options, string workingDir, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_activeThreadId) || _threadStartRequested)
                return;

            _threadStartRequested = true;
            var parameters = new JObject
            {
                ["cwd"] = ResolveWorkingDirectory(workingDir)
            };

            var model = (options?.DefaultModel ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(model))
                parameters["model"] = model;

            await SendRpcRequestAsync("thread/start", parameters, "thread-start", cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> SendRpcRequestAsync(string method, JObject parameters, string requestKind, CancellationToken cancellationToken)
        {
            var requestId = Interlocked.Increment(ref _rpcCounter).ToString();
            if (!string.IsNullOrWhiteSpace(requestKind))
                _pendingRequestKinds[requestId] = requestKind;

            var payload = new JObject
            {
                ["id"] = requestId,
                ["method"] = method ?? string.Empty,
                ["params"] = parameters ?? new JObject()
            };

            return await SendLineAsync(payload.ToString(Newtonsoft.Json.Formatting.None), cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> SendRpcNotificationAsync(string method, JObject parameters, CancellationToken cancellationToken)
        {
            var payload = new JObject
            {
                ["method"] = method ?? string.Empty,
                ["params"] = parameters ?? new JObject()
            };

            return await SendLineAsync(payload.ToString(Newtonsoft.Json.Formatting.None), cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> SendLineAsync(string payload, CancellationToken cancellationToken)
        {
            StreamWriter writer;
            lock (_gate)
            {
                writer = _stdin;
            }

            if (writer == null)
                return false;

            await writer.WriteLineAsync(payload ?? string.Empty).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
            return true;
        }

        private IEnumerable<string> MapInboundLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                yield break;

            var root = TryParseJson(line);
            if (root == null)
            {
                yield return line;
                yield break;
            }

            var method = root.Value<string>("method") ?? string.Empty;
            var requestId = root["id"]?.ToString() ?? string.Empty;
            var parameters = root["params"] as JObject;
            var result = root["result"] as JObject;
            var error = root["error"] as JObject;

            if (!string.IsNullOrWhiteSpace(requestId) && result != null)
            {
                foreach (var mappedResponse in MapRpcResponse(requestId, result))
                    yield return mappedResponse;

                yield break;
            }

            if (!string.IsNullOrWhiteSpace(requestId) && error != null)
            {
                var message = error.Value<string>("message") ?? "App Server request failed.";
                yield return CreateLegacyEvent("stream_error", new JObject
                {
                    ["id"] = requestId,
                    ["message"] = message
                });
                yield break;
            }

            if (string.IsNullOrWhiteSpace(method))
            {
                yield return line;
                yield break;
            }

            foreach (var mapped in MapRpcNotification(method, requestId, parameters))
                yield return mapped;
        }

        private IEnumerable<string> MapRpcResponse(string requestId, JObject result)
        {
            if (_pendingRequestKinds.TryRemove(requestId, out var kind))
            {
                switch (kind)
                {
                    case "thread-start":
                        var threadId = result.SelectToken("thread.id")?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(threadId))
                        {
                            _activeThreadId = threadId;
                            _threadStartRequested = false;
                            yield return CreateSessionConfiguredEvent(threadId);
                            _ = Task.Run(() => FlushPendingUserInputsAsync(CancellationToken.None), CancellationToken.None);
                        }
                        yield break;
                    case "mcp-list":
                        yield return CreateLegacyEvent("list_mcp_tools", new JObject
                        {
                            ["tools"] = FlattenMcpTools(result)
                        });
                        yield break;
                    case "custom-prompts":
                        yield return CreateLegacyEvent("list_custom_prompts", new JObject
                        {
                            ["prompts"] = FlattenSkills(result)
                        });
                        yield break;
                }
            }
        }

        private IEnumerable<string> MapRpcNotification(string method, string requestId, JObject parameters)
        {
            parameters ??= new JObject();

            switch (method)
            {
                case "thread/started":
                    var threadId = parameters.SelectToken("thread.id")?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(threadId))
                    {
                        _activeThreadId = threadId;
                        _threadStartRequested = false;
                        yield return CreateSessionConfiguredEvent(threadId);
                        _ = Task.Run(() => FlushPendingUserInputsAsync(CancellationToken.None), CancellationToken.None);
                    }
                    yield break;
                case "item/agentMessage/delta":
                    yield return CreateLegacyEvent("agent_message_delta", new JObject
                    {
                        ["id"] = parameters.Value<string>("turnId") ?? parameters.Value<string>("itemId") ?? string.Empty,
                        ["text_delta"] = parameters.Value<string>("delta") ?? parameters.Value<string>("text") ?? string.Empty
                    });
                    yield break;
                case "item/started":
                    var startedItem = parameters["item"] as JObject;
                    var startedType = startedItem?.Value<string>("type") ?? string.Empty;
                    if (string.Equals(startedType, "commandExecution", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateLegacyEvent("exec_command_begin", new JObject
                        {
                            ["id"] = parameters.Value<string>("turnId") ?? startedItem?.Value<string>("id") ?? string.Empty,
                            ["command"] = startedItem?.Value<string>("command") ?? string.Empty,
                            ["cwd"] = startedItem?.Value<string>("cwd") ?? string.Empty
                        });
                    }
                    else if (string.Equals(startedType, "mcpToolCall", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateLegacyEvent("tool_call_begin", startedItem ?? new JObject());
                    }
                    else if (string.Equals(startedType, "fileChange", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateLegacyEvent("patch_apply_begin", startedItem ?? new JObject());
                    }
                    yield break;
                case "item/commandExecution/outputDelta":
                    yield return CreateLegacyEvent("exec_command_output_delta", new JObject
                    {
                        ["id"] = parameters.Value<string>("turnId") ?? parameters.Value<string>("itemId") ?? string.Empty,
                        ["delta"] = parameters.Value<string>("delta") ?? string.Empty
                    });
                    yield break;
                case "item/completed":
                    var completedItem = parameters["item"] as JObject;
                    var completedType = completedItem?.Value<string>("type") ?? string.Empty;
                    if (string.Equals(completedType, "agentMessage", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateLegacyEvent("agent_message", new JObject
                        {
                            ["id"] = parameters.Value<string>("turnId") ?? completedItem?.Value<string>("id") ?? string.Empty,
                            ["text"] = completedItem?.Value<string>("text") ?? string.Empty
                        });
                    }
                    else if (string.Equals(completedType, "commandExecution", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateLegacyEvent("exec_command_end", completedItem ?? new JObject());
                    }
                    else if (string.Equals(completedType, "mcpToolCall", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateLegacyEvent("tool_call_end", completedItem ?? new JObject());
                    }
                    else if (string.Equals(completedType, "fileChange", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateLegacyEvent("patch_apply_end", completedItem ?? new JObject());
                    }
                    yield break;
                case "item/commandExecution/requestApproval":
                    var execCallId = parameters.Value<string>("itemId") ?? requestId ?? Guid.NewGuid().ToString();
                    if (!string.IsNullOrWhiteSpace(requestId))
                        _approvalRequestByCallId[execCallId] = requestId;
                    yield return CreateLegacyEvent("exec_approval_request", new JObject
                    {
                        ["id"] = execCallId,
                        ["call_id"] = execCallId,
                        ["command"] = parameters.Value<string>("command") ?? string.Empty,
                        ["cwd"] = parameters.Value<string>("cwd") ?? string.Empty,
                        ["reason"] = parameters.Value<string>("reason") ?? string.Empty
                    });
                    yield break;
                case "item/fileChange/requestApproval":
                    var patchCallId = parameters.Value<string>("itemId") ?? requestId ?? Guid.NewGuid().ToString();
                    if (!string.IsNullOrWhiteSpace(requestId))
                        _approvalRequestByCallId[patchCallId] = requestId;
                    yield return CreateLegacyEvent("apply_patch_approval_request", new JObject
                    {
                        ["id"] = patchCallId,
                        ["call_id"] = patchCallId,
                        ["reason"] = parameters.Value<string>("reason") ?? string.Empty
                    });
                    yield break;
                case "turn/diff/updated":
                    yield return CreateLegacyEvent("turn_diff", parameters["diff"] as JObject ?? new JObject
                    {
                        ["diff"] = parameters["diff"] ?? string.Empty
                    });
                    yield break;
                case "turn/completed":
                    var turn = parameters["turn"] as JObject;
                    yield return CreateLegacyEvent("task_complete", new JObject
                    {
                        ["id"] = turn?.Value<string>("id") ?? parameters.Value<string>("turnId") ?? string.Empty,
                        ["status"] = turn?.Value<string>("status") ?? string.Empty
                    });
                    yield break;
                case "app/list/updated":
                    yield return CreateLegacyEvent("list_mcp_tools", new JObject
                    {
                        ["tools"] = parameters["data"] as JArray ?? new JArray()
                    });
                    yield break;
            }
        }

        private async Task FlushPendingUserInputsAsync(CancellationToken cancellationToken)
        {
            var queued = new List<string>();
            lock (_gate)
            {
                while (_pendingUserInputs.Count > 0)
                {
                    var text = _pendingUserInputs.Dequeue();
                    if (!string.IsNullOrWhiteSpace(text))
                        queued.Add(text);
                }
            }

            foreach (var text in queued)
            {
                await SendTurnStartAsync(text, cancellationToken).ConfigureAwait(false);
            }
        }

        private static JObject TryParseJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                return JObject.Parse(raw);
            }
            catch
            {
                return null;
            }
        }

        private static string CreateLegacyEvent(string kind, JObject payload)
        {
            var msg = payload != null
                ? (JObject)payload.DeepClone()
                : new JObject();
            msg["kind"] = kind ?? string.Empty;
            return new JObject
            {
                ["msg"] = msg
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string ExtractUserInputText(JObject op)
        {
            if (op == null)
                return string.Empty;

            var text = op.Value<string>("text");
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            var items = op["items"] as JArray;
            if (items == null)
                return string.Empty;

            foreach (var item in items.OfType<JObject>())
            {
                var itemText = item.Value<string>("text");
                if (!string.IsNullOrWhiteSpace(itemText))
                    return itemText;
            }

            return string.Empty;
        }

        private JObject BuildSkillsListParams()
        {
            var cwd = ResolveWorkingDirectory(_connectWorkingDirectory);
            return new JObject
            {
                ["cwds"] = new JArray(cwd),
                ["forceReload"] = true
            };
        }

        private JArray FlattenMcpTools(JObject result)
        {
            var output = new JArray();
            var data = result["data"] as JArray;
            if (data == null)
                return output;

            foreach (var server in data.OfType<JObject>())
            {
                var serverName = server.Value<string>("name") ?? server.Value<string>("id") ?? string.Empty;
                var tools = server["tools"] as JArray;
                if (tools == null)
                    continue;

                foreach (var tool in tools.OfType<JObject>())
                {
                    output.Add(new JObject
                    {
                        ["id"] = tool.Value<string>("name") ?? tool.Value<string>("id") ?? string.Empty,
                        ["name"] = tool.Value<string>("name") ?? tool.Value<string>("id") ?? string.Empty,
                        ["description"] = tool.Value<string>("description") ?? string.Empty,
                        ["server"] = serverName
                    });
                }
            }

            return output;
        }

        private JArray FlattenSkills(JObject result)
        {
            var prompts = new JArray();
            var data = result["data"] as JArray;
            if (data == null)
                return prompts;

            foreach (var entry in data.OfType<JObject>())
            {
                var skills = entry["skills"] as JArray;
                if (skills == null)
                    continue;

                foreach (var skill in skills.OfType<JObject>())
                {
                    prompts.Add(new JObject
                    {
                        ["id"] = skill.Value<string>("name") ?? string.Empty,
                        ["name"] = skill.Value<string>("name") ?? string.Empty,
                        ["description"] = skill.Value<string>("description") ?? string.Empty,
                        ["source"] = "skill"
                    });
                }
            }

            return prompts;
        }

        private string CreateSessionConfiguredEvent(string threadId)
        {
            return CreateLegacyEvent("session_configured", new JObject
            {
                ["thread_id"] = threadId ?? string.Empty,
                ["rollout_path"] = threadId ?? string.Empty
            });
        }

        private static (string fileName, string args) ResolveCli(CodexOptions options)
        {
            var exe = (options?.CliExecutable ?? string.Empty).Trim();
            var useWsl = options?.UseWsl == true;

            if (useWsl)
            {
                return ("wsl.exe", "-- codex app-server");
            }
            if (!string.IsNullOrEmpty(exe))
            {
                return (exe, "app-server");
            }
            return ("codex", "app-server");
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
