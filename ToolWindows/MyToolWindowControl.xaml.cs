using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using CodexVS22.Core;
using CodexVS22.Core.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows.Threading;
using DteProject = EnvDTE.Project;
using DteProjects = EnvDTE.Projects;
using DteProjectItem = EnvDTE.ProjectItem;
using DteProjectItems = EnvDTE.ProjectItems;
using DteSolution = EnvDTE.Solution;

namespace CodexVS22
{
  public partial class MyToolWindowControl : UserControl
  {
    private CodexCliHost _host;
    private readonly Dictionary<string, AssistantTurn> _assistantTurns = new();
    private readonly Dictionary<string, ExecTurn> _execTurns = new();
    private readonly Dictionary<string, string> _execCommandIndex = new();
    private readonly Dictionary<string, string> _execIdRemap = new();
    private readonly Dictionary<string, bool> _rememberedExecApprovals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _rememberedPatchApprovals = new(StringComparer.Ordinal);
    private readonly Queue<ApprovalRequest> _approvalQueue = new();
    private ApprovalRequest _activeApproval;
    private string _lastUserInput;
    private CodexOptions _options;
    private string _workingDir;
    private bool _authKnown;
    private bool _isAuthenticated;
    private bool _authOperationInProgress;
    private string _authMessage = string.Empty;
    private bool _authGatedSend;
    private string _lastExecFallbackId;
    private IVsSolution _solutionService;
    private SolutionEventsSink _solutionEvents;
    private uint _solutionEventsCookie;
    private bool _cliStarted;
    private readonly SemaphoreSlim _workingDirLock = new(1, 1);
    private static readonly string ExtensionRoot = NormalizeDirectory(AppContext.BaseDirectory);
    private UIContext _solutionLoadedContext;
    private UIContext _folderOpenContext;
    private bool _waitingForSolutionLoad;
    private string _lastKnownSolutionRoot = string.Empty;
    private string _lastKnownWorkspaceRoot = string.Empty;
    private static readonly TaskCompletionSource<EnvironmentSnapshot> _environmentReadySource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static int _environmentReadyInitialized;

    private static long _approvalCounter;
    private readonly object _heartbeatLock = new();
    private Timer _heartbeatTimer;
    private HeartbeatState _heartbeatState;
    private int _heartbeatSending;
    private bool _initializingSelectors;
    private string _selectedModel = DefaultModelName;
    private string _selectedReasoning = DefaultReasoningValue;
    private Window _hostWindow;
    private bool _windowEventsHooked;
    private sealed class AssistantTurn
    {
      public AssistantTurn(TextBlock bubble)
      {
        Bubble = bubble;
      }

      public TextBlock Bubble { get; }
      public StringBuilder Buffer { get; } = new StringBuilder();
    }

    private enum ApprovalKind
    {
      Exec,
      Patch
    }

    private sealed class ApprovalRequest
    {
      public ApprovalRequest(ApprovalKind kind, string callId, string message, string signature, bool canRemember)
      {
        Kind = kind;
        CallId = callId;
        Message = message;
        Signature = signature;
        CanRemember = canRemember;
      }

      public ApprovalKind Kind { get; }
      public string CallId { get; }
      public string Message { get; }
      public string Signature { get; }
      public bool CanRemember { get; }
    }

    private sealed class ExecTurn
    {
      public ExecTurn(Border container, TextBlock body, TextBlock header, string normalizedCommand)
      {
        Container = container;
        Body = body;
        Header = header;
        NormalizedCommand = normalizedCommand;
      }

      public Border Container { get; }
      public TextBlock Body { get; }
      public TextBlock Header { get; }
      public string NormalizedCommand { get; set; }
      public StringBuilder Buffer { get; } = new StringBuilder();
    }

    private readonly struct CandidateSeed
    {
      public CandidateSeed(string source, string path, bool isWorkspaceRoot)
      {
        Source = source;
        Path = path;
        IsWorkspaceRoot = isWorkspaceRoot;
      }

      public string Source { get; }
      public string Path { get; }
      public bool IsWorkspaceRoot { get; }
    }

    private sealed class WorkingDirectoryCandidate
    {
      public WorkingDirectoryCandidate(string source, string path, bool exists, bool hasSolution, bool hasProject, int depth, bool isWorkspaceRoot)
      {
        Source = source;
        Path = path;
        Exists = exists;
        HasSolution = hasSolution;
        HasProject = hasProject;
        Depth = depth;
        IsWorkspaceRoot = isWorkspaceRoot;
        IsInsideExtension = IsInsideExtensionRoot(path);
      }

      public string Source { get; }
      public string Path { get; }
      public bool Exists { get; }
      public bool HasSolution { get; }
      public bool HasProject { get; }
      public int Depth { get; }
      public bool IsWorkspaceRoot { get; }
      public bool IsInsideExtension { get; }
    }

    private sealed class WorkingDirectoryResolution
    {
      public WorkingDirectoryResolution(WorkingDirectoryCandidate selected, List<WorkingDirectoryCandidate> candidates)
      {
        Selected = selected;
        Candidates = candidates ?? new List<WorkingDirectoryCandidate>();
      }

      public WorkingDirectoryCandidate Selected { get; }
      public List<WorkingDirectoryCandidate> Candidates { get; }
    }

    private sealed class HeartbeatState
    {
      public HeartbeatState(TimeSpan interval, JObject opTemplate, string opType)
      {
        Interval = interval;
        OpTemplate = opTemplate;
        OpType = opType;
      }

      public TimeSpan Interval { get; }
      public JObject OpTemplate { get; }
      public string OpType { get; }
    }

    private static readonly string[] ModelOptions = new[]
    {
      "gpt-4.1",
      "gpt-4.1-mini",
      "o1-mini"
    };

    private static readonly string[] ReasoningOptions = new[]
    {
      "none",
      "medium",
      "high"
    };

    private const string DefaultModelName = "gpt-4.1";
    private const string DefaultReasoningValue = "medium";

    private static bool IsInsideExtensionRoot(string path)
    {
      var normalized = NormalizeDirectory(path);
      if (string.IsNullOrEmpty(normalized) || string.IsNullOrEmpty(ExtensionRoot))
        return false;

      return normalized.StartsWith(ExtensionRoot, StringComparison.OrdinalIgnoreCase);
    }

    private async Task InitializeSelectorsAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      _initializingSelectors = true;
      try
      {
        var modelBox = this.FindName("ModelCombo") as ComboBox;
        if (modelBox != null)
        {
          modelBox.SelectionChanged -= OnModelSelectionChanged;
          modelBox.ItemsSource = ModelOptions;
          _selectedModel = NormalizeModel(_options?.DefaultModel);
          modelBox.SelectedItem = _selectedModel;
          modelBox.SelectionChanged += OnModelSelectionChanged;
        }

        var reasoningBox = this.FindName("ReasoningCombo") as ComboBox;
        if (reasoningBox != null)
        {
          reasoningBox.SelectionChanged -= OnReasoningSelectionChanged;
          reasoningBox.ItemsSource = ReasoningOptions;
          _selectedReasoning = NormalizeReasoning(_options?.DefaultReasoning);
          reasoningBox.SelectedItem = _selectedReasoning;
          reasoningBox.SelectionChanged += OnReasoningSelectionChanged;
        }
      }
      finally
      {
        _initializingSelectors = false;
      }
    }

    private static string NormalizeModel(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return DefaultModelName;

      foreach (var option in ModelOptions)
      {
        if (string.Equals(option, value, StringComparison.OrdinalIgnoreCase))
          return option;
      }

      return DefaultModelName;
    }

    private static string NormalizeReasoning(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return DefaultReasoningValue;

      foreach (var option in ReasoningOptions)
      {
        if (string.Equals(option, value, StringComparison.OrdinalIgnoreCase))
          return option;
      }

      return DefaultReasoningValue;
    }

    private void OnModelSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      if (_initializingSelectors)
        return;

      var selected = (sender as ComboBox)?.SelectedItem as string;
      var normalized = NormalizeModel(selected ?? string.Empty);
      if (string.Equals(normalized, _selectedModel, StringComparison.Ordinal))
        return;

      _selectedModel = normalized;
      if (_options != null)
        _options.DefaultModel = normalized;
      QueueOptionSave();
    }

    private void OnReasoningSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      if (_initializingSelectors)
        return;

      var selected = (sender as ComboBox)?.SelectedItem as string;
      var normalized = NormalizeReasoning(selected ?? string.Empty);
      if (string.Equals(normalized, _selectedReasoning, StringComparison.Ordinal))
        return;

      _selectedReasoning = normalized;
      if (_options != null)
        _options.DefaultReasoning = normalized;
      QueueOptionSave();
    }

    private static void QueueOptionSave()
    {
      var options = CodexVS22Package.OptionsInstance;
      if (options == null)
        return;

      ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
      {
        try
        {
          await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
          options.SaveSettingsToStorage();
        }
        catch
        {
          // ignore persistence failures; options page will handle fallback
        }
      });
    }
    public MyToolWindowControl()
    {
      InitializeComponent();
    }

    public static MyToolWindowControl Current { get; private set; }

    internal static void SignalEnvironmentReady(EnvironmentSnapshot snapshot)
    {
      if (Interlocked.Exchange(ref _environmentReadyInitialized, 1) == 0)
        _environmentReadySource.TrySetResult(snapshot);
      else if (!_environmentReadySource.Task.IsCompleted)
        _environmentReadySource.TrySetResult(snapshot);
    }

    internal static Task WaitForUiContextAsync(UIContext context, CancellationToken ct)
    {
      if (context == null || context.IsActive)
        return Task.CompletedTask;

      var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

      void OnChanged(object sender, UIContextChangedEventArgs args)
      {
        if (!args.Activated)
          return;

        context.UIContextChanged -= OnChanged;
        tcs.TrySetResult(null);
      }

      context.UIContextChanged += OnChanged;

      if (ct.CanBeCanceled)
      {
        ct.Register(() =>
        {
          context.UIContextChanged -= OnChanged;
          tcs.TrySetCanceled();
        });
      }

      return tcs.Task;
    }

    private static async Task<EnvironmentSnapshot> WaitForEnvironmentReadyAsync()
    {
      var readyTask = _environmentReadySource.Task;
      if (readyTask.IsCompleted)
        return await readyTask.ConfigureAwait(true);

      var completed = await Task.WhenAny(readyTask, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(true);
      return completed == readyTask ? await readyTask.ConfigureAwait(true) : EnvironmentSnapshot.Empty;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
      ThreadHelper.JoinableTaskFactory.Run(async () =>
      {
        await OnLoadedAsync(sender, e);
      });
    }

    private async Task OnLoadedAsync(object sender, RoutedEventArgs e)
    {
      if (_host != null) return;
      Current = this;
      _options = CodexVS22Package.OptionsInstance ?? new CodexOptions();
      _host = CreateHost();

      _selectedModel = NormalizeModel(_options?.DefaultModel);
      _selectedReasoning = NormalizeReasoning(_options?.DefaultReasoning);
      if (_options != null)
      {
        _options.DefaultModel = _selectedModel;
        _options.DefaultReasoning = _selectedReasoning;
      }

      await InitializeSelectorsAsync();
      await UpdateFullAccessBannerAsync();
      ApplyWindowPreferences();

      await AdviseSolutionEventsAsync();

      await UpdateAuthenticationStateAsync(false, false, "Checking Codex authentication...", true);

      var environmentSnapshot = await WaitForEnvironmentReadyAsync();
      ApplyEnvironmentSnapshot(environmentSnapshot);

      _workingDir = await DetermineInitialWorkingDirectoryAsync();

      var started = await _host.StartAsync(_options, _workingDir);
      _cliStarted = started;
      if (!started)
      {
        await UpdateAuthenticationStateAsync(true, false, "Failed to start Codex CLI. Check Diagnostics.", false);
        return;
      }

      var auth = await _host.CheckAuthenticationAsync(_options, _workingDir);
      await HandleAuthenticationResultAsync(auth);
      FocusInputBox();
      UpdateTelemetryUi();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
      DisposeHost();
      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await CleanupSolutionSubscriptionsAsync());
      UnhookWindowEvents();
      if (Current == this) Current = null;
    }

    private void ApplyEnvironmentSnapshot(EnvironmentSnapshot snapshot)
    {
      if (!string.IsNullOrEmpty(snapshot.WorkspaceRoot))
        _lastKnownWorkspaceRoot = NormalizeDirectory(snapshot.WorkspaceRoot);

      if (!string.IsNullOrEmpty(snapshot.SolutionRoot))
        _lastKnownSolutionRoot = NormalizeDirectory(snapshot.SolutionRoot);
    }

    private CodexCliHost CreateHost()
    {
      var host = new CodexCliHost();
      host.OnStdoutLine += HandleStdout;
      host.OnStderrLine += HandleStderr;
      return host;
    }

    private void DisposeHost()
    {
      StopHeartbeatTimer();
      if (_host == null) return;
      _host.OnStdoutLine -= HandleStdout;
      _host.OnStderrLine -= HandleStderr;
      _host.Dispose();
      _host = null;
      _cliStarted = false;
      ClearApprovalState();
    }

    private async Task<bool> RestartCliAsync()
    {
      DisposeHost();
      _rememberedExecApprovals.Clear();
      _rememberedPatchApprovals.Clear();
       ClearApprovalState();
      _host = CreateHost();
      var options = _options ?? new CodexOptions();
      var dir = _workingDir ?? string.Empty;
      var started = await _host.StartAsync(options, dir);
      _cliStarted = started;
      if (!started)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync("[error] Failed to restart Codex CLI");
      }
      return started;
    }

    private async Task UpdateAuthenticationStateAsync(
      bool known,
      bool isAuthenticated,
      string message,
      bool inProgress)
    {
      _authKnown = known;
      _isAuthenticated = isAuthenticated;
      _authMessage = message ?? string.Empty;
      _authOperationInProgress = inProgress;
      await RefreshAuthUiAsync();
    }

    private async Task RefreshAuthUiAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      var banner = this.FindName("AuthBanner") as Border;
      var text = this.FindName("AuthMessage") as TextBlock;
      var login = this.FindName("LoginButton") as Button;
      var logout = this.FindName("LogoutButton") as Button;
      var shouldShowBanner = _authOperationInProgress || (!_isAuthenticated && _authKnown);

      if (banner != null)
        banner.Visibility = shouldShowBanner ? Visibility.Visible : Visibility.Collapsed;

      if (text != null)
      {
        if (!string.IsNullOrWhiteSpace(_authMessage))
          text.Text = _authMessage;
        else if (_authOperationInProgress)
          text.Text = "Checking Codex authentication...";
        else if (_authKnown && !_isAuthenticated)
          text.Text = "Codex login required. Click Login to continue.";
        else
          text.Text = "Codex is authenticated.";
      }

      if (login != null)
      {
        var showLogin = !_isAuthenticated || _authOperationInProgress;
        login.Visibility = showLogin ? Visibility.Visible : Visibility.Collapsed;
        login.IsEnabled = !_authOperationInProgress && !_isAuthenticated;
      }

      if (logout != null)
      {
        logout.Visibility = _authKnown && _isAuthenticated ? Visibility.Visible : Visibility.Collapsed;
        logout.IsEnabled = !_authOperationInProgress;
      }

      if (this.FindName("SendButton") is Button send)
      {
        if (!_authKnown || !_isAuthenticated || _authOperationInProgress)
        {
          if (send.IsEnabled)
          {
            send.IsEnabled = false;
            _authGatedSend = true;
          }
        }
        else if (_authGatedSend)
        {
          send.IsEnabled = true;
          _authGatedSend = false;
        }
      }
    }

    private async Task HandleAuthenticationResultAsync(
      CodexCliHost.CodexAuthenticationResult result)
    {
      var whoami = ExtractFirstLine(result.Message);
      if (result.IsAuthenticated)
      {
        var msg = string.IsNullOrEmpty(whoami)
          ? "Codex is authenticated."
          : whoami;
        await UpdateAuthenticationStateAsync(true, true, msg, false);
      }
      else
      {
        var msg = string.IsNullOrEmpty(whoami)
          ? "Codex login required. Click Login to continue."
          : whoami;
        await UpdateAuthenticationStateAsync(true, false, msg, false);
      }
    }

    private static string ExtractFirstLine(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

      using var reader = new StringReader(value);
      string line;
      while ((line = reader.ReadLine()) != null)
      {
        if (!string.IsNullOrWhiteSpace(line))
          return line.Trim();
      }

      return string.Empty;
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
      try
      {
        await EnsureWorkingDirectoryUpToDateAsync("login-click");

        var host = _host;
        if (host == null)
          return;

        await UpdateAuthenticationStateAsync(_authKnown, _isAuthenticated, "Opening Codex login flow...", true);
        var options = _options ?? new CodexOptions();
        var dir = _workingDir ?? string.Empty;

        var ok = await host.LoginAsync(options, dir);
        if (!ok)
        {
          await UpdateAuthenticationStateAsync(true, _isAuthenticated, "codex login failed. Check Diagnostics.", false);
          return;
        }

        await UpdateAuthenticationStateAsync(true, _isAuthenticated, "Restarting Codex CLI...", true);
        var restarted = await RestartCliAsync();
        if (!restarted)
        {
          await UpdateAuthenticationStateAsync(true, false, "CLI restart failed after login. See Diagnostics.", false);
          return;
        }

        await UpdateAuthenticationStateAsync(true, _isAuthenticated, "Confirming Codex login...", true);
        var auth = await _host.CheckAuthenticationAsync(options, dir);
        await HandleAuthenticationResultAsync(auth);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] OnLoginClick failed: {ex.Message}");
        await UpdateAuthenticationStateAsync(true, _isAuthenticated, "Login failed. Check Diagnostics.", false);
      }
    }

    private async void OnLogoutClick(object sender, RoutedEventArgs e)
    {
      try
      {
        await EnsureWorkingDirectoryUpToDateAsync("logout-click");

        var host = _host;
        if (host == null)
          return;

        await UpdateAuthenticationStateAsync(true, _isAuthenticated, "Signing out of Codex...", true);
        var options = _options ?? new CodexOptions();
        var dir = _workingDir ?? string.Empty;

        var ok = await host.LogoutAsync(options, dir);
        if (!ok)
        {
          await UpdateAuthenticationStateAsync(true, _isAuthenticated, "codex logout failed. Check Diagnostics.", false);
          return;
        }

        await UpdateAuthenticationStateAsync(true, false, "Restarting Codex CLI...", true);
        var restarted = await RestartCliAsync();
        if (!restarted)
        {
          await UpdateAuthenticationStateAsync(true, false, "CLI restart failed after logout. See Diagnostics.", false);
          return;
        }

        await UpdateAuthenticationStateAsync(true, false, "Confirming Codex logout...", true);
        var auth = await _host.CheckAuthenticationAsync(options, dir);
        await HandleAuthenticationResultAsync(auth);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] OnLogoutClick failed: {ex.Message}");
        await UpdateAuthenticationStateAsync(true, false, "Logout failed. Check Diagnostics.", false);
      }
    }

    private async void HandleStderr(string line)
    {
      try
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[stderr] {line}");
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleStderr failed: {ex.Message}");
      }
    }

    private int _assistantChunkCounter;
    private readonly TelemetryTracker _telemetry = new();

    private async void HandleAgentMessageDelta(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var id = string.IsNullOrEmpty(evt.Id) ? "__unknown__" : evt.Id;
        var text = ExtractDeltaText(evt);
        if (string.IsNullOrEmpty(text))
          return;

        var turn = GetOrCreateAssistantTurn(id);
        AppendAssistantText(turn, text);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleAgentMessageDelta failed: {ex.Message}");
      }
    }

    private async void HandleAgentMessage(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var id = string.IsNullOrEmpty(evt.Id) ? "__unknown__" : evt.Id;
        var finalText = ExtractFinalText(evt);
        if (!_assistantTurns.TryGetValue(id, out var turn))
        {
          if (string.IsNullOrEmpty(finalText))
            return;
          turn = GetOrCreateAssistantTurn(id);
        }

        if (!string.IsNullOrEmpty(finalText))
        {
          turn.Buffer.Clear();
          AppendAssistantText(turn, finalText, isFinal: true);
        }
        else
        {
          turn.Bubble.Text = ChatTextUtilities.NormalizeAssistantText(turn.Buffer.ToString());
        }

        _assistantTurns.Remove(id);
        await LogAssistantTextAsync(turn.Bubble.Text);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleAgentMessage failed: {ex.Message}");
      }
    }

    private async void HandleTokenCount(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var (total, input, output) = ExtractTokenCounts(evt);
        if (total == null && input == null && output == null)
          return;
        UpdateTokenUsage(total, input, output);
        _telemetry.RecordTokens(total, input, output);
        UpdateTelemetryUi();
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleTokenCount failed: {ex.Message}");
      }
    }

    private async void HandleStreamError(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var message = ExtractStreamErrorMessage(evt);
        ShowStreamErrorBanner(message, !string.IsNullOrEmpty(_lastUserInput));
        if (!string.IsNullOrEmpty(evt.Id) && _assistantTurns.TryGetValue(evt.Id, out var turn))
        {
          if (!turn.Buffer.ToString().EndsWith("[stream interrupted]", StringComparison.Ordinal))
          {
            if (turn.Buffer.Length > 0)
              turn.Buffer.AppendLine().AppendLine();
            AppendAssistantText(turn, "[stream interrupted]", decorate: false);
          }
          _assistantTurns.Remove(evt.Id);
        }

        var btn = this.FindName("SendButton") as Button;
        var status = this.FindName("StatusText") as TextBlock;
        if (btn != null) btn.IsEnabled = true;
        if (status != null) status.Text = "Stream error";
        await VS.StatusBar.ShowMessageAsync("Codex stream error. You can retry.");
        _telemetry.CancelTurn();
        UpdateTelemetryUi();
        UpdateStreamingIndicator(false);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleStreamError failed: {ex.Message}");
      }
    }

    private async void HandleApplyPatchApproval(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var host = _host;
        if (host == null)
          return;

        var raw = evt.Raw ?? new JObject();
        var signature = BuildPatchSignature(raw);
        var callId = TryGetString(raw, "call_id") ?? evt.Id ?? string.Empty;
        if (string.IsNullOrEmpty(callId))
          return;

        var options = _options ?? new CodexOptions();
        EnqueueFullAccessBannerRefresh();
        if (TryResolvePatchApproval(options.Mode, signature, out var autoApproved, out var autoReason))
        {
          await host.SendAsync(CreatePatchApprovalSubmission(callId, autoApproved));
          await LogAutoApprovalAsync("patch", signature, autoApproved, autoReason);
          await VS.StatusBar.ShowMessageAsync($"Codex patch {(autoApproved ? "approved" : "denied")} ({autoReason}).");
          return;
        }

        var summary = TryGetString(raw, "summary") ?? "Apply patch from Codex?";
        var canRemember = !string.IsNullOrEmpty(signature) && ShouldOfferRemember(signature);
        EnqueueApprovalRequest(new ApprovalRequest(ApprovalKind.Patch, callId, summary, signature, canRemember));
        await VS.StatusBar.ShowMessageAsync("Codex awaiting patch approval.");
        return;
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleApplyPatchApproval failed: {ex.Message}");
      }
    }

    private async void HandleExecApproval(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var host = _host;
        if (host == null)
          return;

        var pane = await DiagnosticsPane.GetAsync();
        if (evt.Raw != null)
          await pane.WriteLineAsync($"[info] Exec approval request: {evt.Raw.ToString(Formatting.None)}");

        var raw = evt.Raw ?? new JObject();
        var (displayCommand, normalizedCommand) = ExtractExecCommandInfo(raw["command"]);
        var signature = string.IsNullOrEmpty(normalizedCommand) ? displayCommand : normalizedCommand;
        var callId = TryGetString(raw, "call_id") ?? evt.Id ?? string.Empty;
        if (string.IsNullOrEmpty(callId))
          return;

        var cwd = TryGetString(raw, "cwd") ?? string.Empty;
        var options = _options ?? new CodexOptions();
        EnqueueFullAccessBannerRefresh();

        if (TryResolveExecApproval(options.Mode, signature, out var autoApproved, out var autoReason))
        {
          await host.SendAsync(CreateExecApprovalSubmission(callId, autoApproved));
          await LogAutoApprovalAsync("exec", signature, autoApproved, autoReason);
          await VS.StatusBar.ShowMessageAsync($"Codex exec {(autoApproved ? "approved" : "denied")} ({autoReason}).");
          return;
        }

        var commandForPrompt = string.IsNullOrEmpty(displayCommand) ? (TryGetString(raw, "command") ?? "(unknown)") : displayCommand;
        var prompt = string.IsNullOrWhiteSpace(cwd)
          ? $"Approve exec?\n{commandForPrompt}"
          : $"Approve exec?\n{commandForPrompt}\nCWD: {cwd}";

        var canRemember = !string.IsNullOrEmpty(signature) && ShouldOfferRemember(signature);
        EnqueueApprovalRequest(new ApprovalRequest(ApprovalKind.Exec, callId, prompt, signature, canRemember));
        await VS.StatusBar.ShowMessageAsync("Codex awaiting exec approval.");
        return;
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleExecApproval failed: {ex.Message}");
      }
    }

    private async void HandleExecCommandBegin(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var eventId = GetExecEventId(evt);
        if (!string.IsNullOrEmpty(eventId) && !_execIdRemap.ContainsKey(eventId))
          _execIdRemap[eventId] = eventId;

        var commandToken = evt.Raw?["command"];
        var (displayCommand, normalizedCommand) = ExtractExecCommandInfo(commandToken);
        var cwd = NormalizeCwd(TryGetString(evt.Raw, "cwd"));
        var header = BuildExecHeader(displayCommand, cwd);

        string canonicalId = eventId;
        if (!string.IsNullOrEmpty(normalizedCommand) &&
            _execCommandIndex.TryGetValue(normalizedCommand, out var existingId))
        {
          canonicalId = existingId;
          if (!string.IsNullOrEmpty(eventId))
            _execIdRemap[eventId] = existingId;
        }

        if (string.IsNullOrEmpty(canonicalId))
        {
          canonicalId = RegisterExecFallbackId();
        }
        else
        {
          _execIdRemap[canonicalId] = canonicalId;
        }

        if (!string.IsNullOrEmpty(normalizedCommand) && !_execCommandIndex.ContainsKey(normalizedCommand))
          _execCommandIndex[normalizedCommand] = canonicalId;

        var previousHeader = _execTurns.TryGetValue(canonicalId, out var existingTurn)
          ? existingTurn.Header?.Text
          : null;

        var turn = GetOrCreateExecTurn(canonicalId, header, normalizedCommand);
        _execTurns[canonicalId] = turn;
        _lastExecFallbackId = canonicalId;

        var updatedHeader = turn.Header?.Text ?? header;
        if (!string.IsNullOrEmpty(updatedHeader) &&
            (string.IsNullOrEmpty(previousHeader) || !string.Equals(previousHeader, updatedHeader, StringComparison.Ordinal)))
        {
          await WriteExecDiagnosticsAsync(updatedHeader);
        }
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleExecCommandBegin failed: {ex.Message}");
      }
    }

    private async void HandleExecCommandOutputDelta(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var execId = ResolveExecId(evt);
        if (string.IsNullOrEmpty(execId))
        {
          execId = _lastExecFallbackId ?? RegisterExecFallbackId();
          var eventId = GetExecEventId(evt);
          if (!string.IsNullOrEmpty(eventId))
            _execIdRemap[eventId] = execId;
        }
        else
        {
          var eventId = GetExecEventId(evt);
          if (!string.IsNullOrEmpty(eventId) && !_execIdRemap.ContainsKey(eventId))
            _execIdRemap[eventId] = execId;
        }

        var outText = TryGetString(evt.Raw, "text") ?? TryGetString(evt.Raw, "chunk") ?? TryGetString(evt.Raw, "data") ?? string.Empty;
        var normalized = NormalizeExecChunk(outText);
        if (string.IsNullOrEmpty(normalized))
          return;

        var turn = GetOrCreateExecTurn(execId, header: null, normalizedCommand: null);
        AppendExecText(turn, normalized);
        await WriteExecDiagnosticsAsync(normalized);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleExecCommandOutputDelta failed: {ex.Message}");
      }
    }

    private async void HandleExecCommandEnd(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var execId = ResolveExecId(evt);
        if (string.IsNullOrEmpty(execId))
        {
          execId = _lastExecFallbackId ?? RegisterExecFallbackId();
          var eventId = GetExecEventId(evt);
          if (!string.IsNullOrEmpty(eventId))
            _execIdRemap[eventId] = execId;
        }
        else
        {
          var eventId = GetExecEventId(evt);
          if (!string.IsNullOrEmpty(eventId) && !_execIdRemap.ContainsKey(eventId))
            _execIdRemap[eventId] = execId;
        }

        if (_execTurns.TryGetValue(execId, out var turn))
        {
          AppendExecText(turn, "$ exec finished\n");
          _execTurns.Remove(execId);
          if (!string.IsNullOrEmpty(turn.NormalizedCommand) &&
              _execCommandIndex.TryGetValue(turn.NormalizedCommand, out var mappedId) &&
              string.Equals(mappedId, execId, StringComparison.Ordinal))
          {
            _execCommandIndex.Remove(turn.NormalizedCommand);
          }
        }
        await WriteExecDiagnosticsAsync("$ exec finished");
        if (_lastExecFallbackId == execId)
          _lastExecFallbackId = null;

        RemoveExecIdMappings(execId);
        foreach (var key in _execCommandIndex.Where(kvp => string.Equals(kvp.Value, execId, StringComparison.Ordinal)).Select(kvp => kvp.Key).ToList())
          _execCommandIndex.Remove(key);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleExecCommandEnd failed: {ex.Message}");
      }
    }

    private async void HandleTurnDiff(EventMsg evt)
    {
      try
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync("[diff] Received diff from Codex");
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleTurnDiff failed: {ex.Message}");
      }
    }

    private async void HandleTaskComplete(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var btn = this.FindName("SendButton") as Button;
        var status = this.FindName("StatusText") as TextBlock;
        if (btn != null) btn.IsEnabled = true;
        if (status != null) status.Text = string.Empty;
        HideStreamErrorBanner();
        UpdateStreamingIndicator(false);
        _telemetry.CompleteTurn();
        UpdateTelemetryUi();
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleTaskComplete failed: {ex.Message}");
      }
    }

    private void HandleStdout(string line)
    {
      var evt = EventParser.Parse(line);
      switch (evt.Kind)
      {
        case EventKind.SessionConfigured:
          CodexVS22.Core.CodexCliHost.LastRolloutPath = TryGetString(evt.Raw, "rollout_path");
          ConfigureHeartbeat(evt);
          break;
        case EventKind.AgentMessageDelta:
          HandleAgentMessageDelta(evt);
          break;
        case EventKind.AgentMessage:
          HandleAgentMessage(evt);
          break;
        case EventKind.TokenCount:
          HandleTokenCount(evt);
          break;
        case EventKind.StreamError:
          HandleStreamError(evt);
          break;
        case EventKind.ApplyPatchApprovalRequest:
          HandleApplyPatchApproval(evt);
          break;
        case EventKind.ExecApprovalRequest:
          HandleExecApproval(evt);
          break;
        case EventKind.ExecCommandBegin:
          HandleExecCommandBegin(evt);
          break;
        case EventKind.ExecCommandOutputDelta:
          HandleExecCommandOutputDelta(evt);
          break;
        case EventKind.ExecCommandEnd:
          HandleExecCommandEnd(evt);
          break;
        case EventKind.TurnDiff:
          HandleTurnDiff(evt);
          break;
        case EventKind.TaskComplete:
          HandleTaskComplete(evt);
          break;
        default:
          break;
      }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
      if (System.Windows.MessageBox.Show("Clear chat?", "Codex", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
      {
        if (this.FindName("Transcript") is StackPanel t)
        {
          t.Children.Clear();
        }
        if (this.FindName("InputBox") is TextBox box) box.Clear();
        _assistantTurns.Clear();
        _execTurns.Clear();
        _execCommandIndex.Clear();
        _execIdRemap.Clear();
        _rememberedExecApprovals.Clear();
        _rememberedPatchApprovals.Clear();
        ClearApprovalState(hideBanner: false);
        ShowApprovalBanner(null);
        _lastExecFallbackId = null;
        _lastUserInput = string.Empty;
        _assistantChunkCounter = 0;
        _telemetry.Reset();
        ClearTokenUsage();
        HideStreamErrorBanner();
        UpdateStreamingIndicator(false);
        FocusInputBox();
        UpdateTelemetryUi();
      }
    }

    private async void OnResetApprovalsClick(object sender, RoutedEventArgs e)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var execCount = _rememberedExecApprovals.Count;
        var patchCount = _rememberedPatchApprovals.Count;
        _rememberedExecApprovals.Clear();
        _rememberedPatchApprovals.Clear();

        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[info] Reset remembered approvals (exec={execCount}, patch={patchCount}).");
        await VS.StatusBar.ShowMessageAsync("Codex approvals reset for this session.");
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] Reset approvals failed: {ex.Message}");
      }
    }

    private void OnApprovalApproveClick(object sender, RoutedEventArgs e)
    {
      ThreadHelper.JoinableTaskFactory.RunAsync(async () => await ResolveActiveApprovalAsync(true));
    }

    private void OnApprovalDenyClick(object sender, RoutedEventArgs e)
    {
      ThreadHelper.JoinableTaskFactory.RunAsync(async () => await ResolveActiveApprovalAsync(false));
    }

    private async void OnCopyAllClick(object sender, RoutedEventArgs e)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var text = CollectTranscriptText();
        if (string.IsNullOrWhiteSpace(text))
          return;
        Clipboard.SetText(text);
        await VS.StatusBar.ShowMessageAsync("Transcript copied to clipboard.");
        if (sender is Button button)
        {
          AnimateButtonFeedback(button);
        }
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] Copy all failed: {ex.Message}");
      }
    }

    private string CollectTranscriptText()
    {
      if (this.FindName("Transcript") is not StackPanel transcript)
        return string.Empty;

      var builder = new StringBuilder();
      foreach (var child in transcript.Children)
      {
        if (child is FrameworkElement element)
        {
          var text = ExtractTextFromElement(element);
          if (string.IsNullOrWhiteSpace(text))
            continue;
          if (builder.Length > 0)
            builder.AppendLine().AppendLine();
          builder.Append(text.TrimEnd());
        }
      }

      return builder.ToString();
    }

    private static string ExtractTextFromElement(FrameworkElement element)
    {
      switch (element)
      {
        case null:
          return string.Empty;
        case TextBlock tb:
          return tb.Text ?? string.Empty;
        case Border border:
          return ExtractTextFromElement(border.Child as FrameworkElement);
        case StackPanel panel:
          return ExtractTextFromPanel(panel);
        default:
          return string.Empty;
      }
    }

    private static string ExtractTextFromPanel(StackPanel panel)
    {
      if (panel == null)
        return string.Empty;

      var builder = new StringBuilder();
      foreach (var child in panel.Children)
      {
        if (child is FrameworkElement element)
        {
          var text = ExtractTextFromElement(element);
          if (string.IsNullOrWhiteSpace(text))
            continue;
          if (builder.Length > 0)
            builder.AppendLine();
          builder.Append(text.TrimEnd());
        }
      }

      return builder.ToString();
    }

    private static void AnimateButtonFeedback(Button button)
    {
      var animation = new DoubleAnimation(1.0, 0.6, TimeSpan.FromMilliseconds(140))
      {
        AutoReverse = true,
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
      };
      button.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static void AnimateBubbleFeedback(TextBlock bubble)
    {
      var baseBrush = bubble.Background as SolidColorBrush;
      if (baseBrush == null || baseBrush.IsFrozen)
      {
        baseBrush = new SolidColorBrush(Colors.Transparent);
        bubble.Background = baseBrush;
      }

      var animation = new ColorAnimation
      {
        From = Colors.Transparent,
        To = Color.FromArgb(80, 0, 120, 215),
        Duration = TimeSpan.FromMilliseconds(120),
        AutoReverse = true,
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
      };

      baseBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    private sealed class TelemetryTracker
    {
      private int _turns;
      private int _totalTokens;
      private double _totalSeconds;
      private int _currentTokens;
      private DateTime? _turnStart;

      public void BeginTurn()
      {
        _turnStart = DateTime.UtcNow;
        _currentTokens = 0;
      }

      public void RecordTokens(int? total, int? input, int? output)
      {
        if (!_turnStart.HasValue)
          return;

        var candidate = 0;
        if (total.HasValue) candidate = Math.Max(candidate, total.Value);
        if (output.HasValue) candidate = Math.Max(candidate, output.Value);
        if (input.HasValue) candidate = Math.Max(candidate, input.Value);

        if (candidate > _currentTokens)
          _currentTokens = candidate;
      }

      public void CompleteTurn()
      {
        if (!_turnStart.HasValue)
          return;

        var elapsed = Math.Max(0.05, (DateTime.UtcNow - _turnStart.Value).TotalSeconds);
        _turns++;
        _totalTokens += _currentTokens;
        _totalSeconds += elapsed;
        _turnStart = null;
        _currentTokens = 0;
      }

      public void CancelTurn()
      {
        _turnStart = null;
        _currentTokens = 0;
      }

      public void Reset()
      {
        _turns = 0;
        _totalTokens = 0;
        _totalSeconds = 0;
        _currentTokens = 0;
        _turnStart = null;
      }

      public string GetSummary()
      {
        if (_turns == 0)
          return string.Empty;

        var avgTokens = (double)_totalTokens / _turns;
        var rate = _totalSeconds > 0 ? _totalTokens / _totalSeconds : 0;
        return $"Turns {_turns} • Avg {avgTokens:F1} tok • {rate:F1} tok/s";
      }
    }

    private void ApplyWindowPreferences()
    {
      Dispatcher.BeginInvoke(new Action(() =>
      {
        var window = Window.GetWindow(this);
        if (window == null)
          return;

        if (!ReferenceEquals(_hostWindow, window))
        {
          UnhookWindowEvents();
          _hostWindow = window;
        }

        ApplyWindowSettings(window);
        HookWindowEvents(window);
      }), DispatcherPriority.Background);
    }

    private void HookWindowEvents(Window window)
    {
      if (window == null || _windowEventsHooked)
        return;

      window.SizeChanged += OnHostWindowSizeChanged;
      window.LocationChanged += OnHostWindowLocationChanged;
      window.StateChanged += OnHostWindowStateChanged;
      _windowEventsHooked = true;
    }

    private void UnhookWindowEvents()
    {
      if (_hostWindow == null || !_windowEventsHooked)
        return;

      _hostWindow.SizeChanged -= OnHostWindowSizeChanged;
      _hostWindow.LocationChanged -= OnHostWindowLocationChanged;
      _hostWindow.StateChanged -= OnHostWindowStateChanged;
      _windowEventsHooked = false;
      _hostWindow = null;
    }

    private void ApplyWindowSettings(Window window)
    {
      if (window == null || _options == null)
        return;

      if (window.WindowState == WindowState.Normal)
      {
        if (_options.WindowWidth > 0)
          window.Width = _options.WindowWidth;
        if (_options.WindowHeight > 0)
          window.Height = _options.WindowHeight;

        if (!double.IsNaN(_options.WindowLeft))
          window.Left = _options.WindowLeft;
        if (!double.IsNaN(_options.WindowTop))
          window.Top = _options.WindowTop;
      }

      if (!string.IsNullOrWhiteSpace(_options.WindowState) &&
          Enum.TryParse(_options.WindowState, out WindowState state))
      {
        window.WindowState = state;
      }
    }

    private void OnHostWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
      if (_options == null)
        return;

      if (sender is Window window && window.WindowState == WindowState.Normal)
      {
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
          _options.WindowWidth = e.NewSize.Width;
          _options.WindowHeight = e.NewSize.Height;
          QueueOptionSave();
        }
      }
    }

    private void OnHostWindowLocationChanged(object sender, EventArgs e)
    {
      if (_options == null)
        return;

      if (sender is Window window && window.WindowState == WindowState.Normal)
      {
        _options.WindowLeft = window.Left;
        _options.WindowTop = window.Top;
        QueueOptionSave();
      }
    }

    private void OnHostWindowStateChanged(object sender, EventArgs e)
    {
      if (_options == null)
        return;

      if (sender is Window window)
      {
        _options.WindowState = window.WindowState.ToString();
        if (window.WindowState == WindowState.Normal)
        {
          _options.WindowWidth = window.Width;
          _options.WindowHeight = window.Height;
          _options.WindowLeft = window.Left;
          _options.WindowTop = window.Top;
        }
        QueueOptionSave();
      }
    }

    private async Task<string> DetermineInitialWorkingDirectoryAsync()
    {
      var resolution = await ResolveWorkingDirectoryAsync();
      await LogWorkingDirectoryResolutionAsync("initial load", resolution, previous: null, includeCandidates: true);

      var path = resolution?.Selected?.Path;
      if (string.IsNullOrEmpty(path))
        path = NormalizeDirectory(Environment.CurrentDirectory);

      return path;
    }

    private async Task<WorkingDirectoryResolution> ResolveWorkingDirectoryAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      var seeds = new List<CandidateSeed>();

      if (!string.IsNullOrEmpty(_lastKnownSolutionRoot))
        seeds.Add(new CandidateSeed("SolutionReadyHint", _lastKnownSolutionRoot, false));
      if (!string.IsNullOrEmpty(_lastKnownWorkspaceRoot))
        seeds.Add(new CandidateSeed("WorkspaceReadyHint", _lastKnownWorkspaceRoot, true));

      var dte = await VS.GetServiceAsync<DTE, DTE2>();
      var solutionFullDir = SafeInvoke(() => GetDirectoryFromFile(GetActiveSolutionFullName(dte)));
      TryAddCandidate(seeds, "DTE.Solution.FullName", () => solutionFullDir);
      TryAddCandidate(seeds, "TryFindSolutionDirectory(DTE.Solution.FullName)", () => TryFindSolutionDirectory(solutionFullDir));

      var solutionFileDir = SafeInvoke(() => GetDirectoryFromFile(GetActiveSolutionFileName(dte)));
      TryAddCandidate(seeds, "DTE.Solution.FileName", () => solutionFileDir);

      TryAddCandidate(seeds, "DTE.Solution.Properties.Path", () => GetDteSolutionProperty(dte, "Path"));

      var solutionItem = await SafeGetCurrentSolutionAsync();
      TryAddCandidate(seeds, "VS.Solutions.Current.FullPath", () => GetSolutionItemPath(solutionItem));

      var solutionDirFromService = await SafeInvokeAsync(() => GetSolutionDirectoryFromServiceAsync());
      TryAddCandidate(seeds, "IVsSolution.VSPROPID_SolutionDirectory", () => solutionDirFromService);

      var solutionRootDirectory = await SafeInvokeAsync(() => GetSolutionRootDirectoryAsync());
      TryAddCandidate(seeds, "IVsSolution.GetSolutionRootDirectory", () => solutionRootDirectory);

      var workspaceRoot = await SafeInvokeAsync(() => GetFolderWorkspaceRootAsync());
      TryAddCandidate(seeds, "FolderWorkspace.Current.Location", () => workspaceRoot, isWorkspaceRoot: true);

      var solutionInfo = await SafeInvokeTupleAsync(() => GetSolutionInfoAsync());
      TryAddCandidate(seeds, "IVsSolution.GetSolutionInfo.Directory", () => solutionInfo.Item1);
      TryAddCandidate(seeds, "IVsSolution.GetSolutionInfo.FileDir", () => GetDirectoryFromFile(solutionInfo.Item2));

      var activeProjectItem = await GetActiveProjectAsync();
      TryAddCandidate(seeds, "VS.Solutions.ActiveProject", () => GetSolutionItemPath(activeProjectItem));

      AddProjectDirectoryCandidates(seeds, dte);

      TryAddCandidate(seeds, "DTE.ActiveDocument", () => GetActiveDocumentDirectory(dte));

      var selectedItems = await GetActiveSolutionItemsAsync();
      foreach (var item in selectedItems)
      {
        var localItem = item;
        TryAddCandidate(seeds, $"VS.Solutions.ActiveItem:{localItem?.Type}", () => GetSolutionItemPath(localItem));
      }

      TryAddCandidate(seeds, "Environment.CurrentDirectory", () => Environment.CurrentDirectory);
      TryAddCandidate(seeds, "TryFindSolutionDirectory(Environment)", () => TryFindSolutionDirectory(Environment.CurrentDirectory));

      var analyzed = seeds
        .Select(AnalyzeCandidate)
        .ToList();

      var best = SelectBestCandidate(analyzed);

      return new WorkingDirectoryResolution(best, analyzed);
    }

    private async Task LogWorkingDirectoryResolutionAsync(
      string reason,
      WorkingDirectoryResolution resolution,
      string previous,
      bool includeCandidates)
    {
      try
      {
        var pane = await DiagnosticsPane.GetAsync();
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var bestPath = resolution?.Selected?.Path;
        var display = string.IsNullOrEmpty(bestPath) ? "<none>" : bestPath;
        var source = resolution?.Selected?.Source ?? "<unknown>";

        if (!string.IsNullOrEmpty(previous) && !PathsEqual(previous, bestPath))
        {
          await pane.WriteLineAsync($"[info] {timestamp} Working directory updated ({reason}): {display} (source: {source})");
          await pane.WriteLineAsync($"[info] {timestamp} Previous working directory: {previous}");
        }
        else
        {
          await pane.WriteLineAsync($"[info] {timestamp} Working directory ({reason}): {display} (source: {source})");
        }

        if (includeCandidates && resolution?.Candidates != null)
        {
          foreach (var candidate in resolution.Candidates)
          {
            var value = string.IsNullOrEmpty(candidate.Path) ? "<empty>" : candidate.Path;
            var labels = new List<string>
            {
              candidate.Exists ? "exists" : "missing",
              candidate.HasSolution ? "has .sln" : "no .sln",
              candidate.HasProject ? "has project" : "no project"
            };
            if (candidate.IsWorkspaceRoot)
              labels.Add("workspace-root");
            if (candidate.IsInsideExtension)
              labels.Add("extension-root");

            var status = string.Join(", ", labels);
            await pane.WriteLineAsync($"[debug] working dir candidate {candidate.Source}: {value} ({status})");
          }
        }
      }
      catch
      {
        // best effort logging only
      }
    }

    private async Task<string> GetSolutionDirectoryFromServiceAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var solutionService = await GetSolutionServiceAsync();
      if (solutionService == null)
        return string.Empty;

      try
      {
        solutionService.GetProperty((int)__VSPROPID.VSPROPID_SolutionDirectory, out var dirObj);
        if (dirObj is string dir)
          return dir;
      }
      catch
      {
        // ignore and fall back
      }

      return string.Empty;
    }

    private async Task<(string Directory, string File)> GetSolutionInfoAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var solutionService = await GetSolutionServiceAsync();
      if (solutionService == null)
        return (string.Empty, string.Empty);

      try
      {
        if (ErrorHandler.Succeeded(solutionService.GetSolutionInfo(out var dir, out var file, out _)))
          return (NormalizeDirectory(dir), file ?? string.Empty);
      }
      catch
      {
        // ignore and fall back
      }

      return (string.Empty, string.Empty);
    }

    private async Task<IVsSolution> GetSolutionServiceAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      if (_solutionService != null)
        return _solutionService;

      _solutionService = await VS.GetServiceAsync<SVsSolution, IVsSolution>();
      return _solutionService;
    }

    private static string GetDirectoryFromFile(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return string.Empty;

      try
      {
        if (Directory.Exists(path))
          return NormalizeDirectory(path);

        if (File.Exists(path))
        {
          var fileDirectory = Path.GetDirectoryName(path);
          return string.IsNullOrEmpty(fileDirectory) ? string.Empty : NormalizeDirectory(fileDirectory);
        }

        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(directory) ? string.Empty : NormalizeDirectory(directory);
      }
      catch
      {
        return string.Empty;
      }
    }

    private static string NormalizeDirectory(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return string.Empty;

      try
      {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      }
      catch
      {
        return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      }
    }

    private static bool PathsEqual(string left, string right)
    {
      var normalizedLeft = NormalizeDirectory(left);
      var normalizedRight = NormalizeDirectory(right);
      return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string TryFindSolutionDirectory(string start)
    {
      if (string.IsNullOrWhiteSpace(start))
        return string.Empty;

      try
      {
        var current = NormalizeDirectory(start);
        var depth = 0;
        while (!string.IsNullOrEmpty(current) && Directory.Exists(current) && depth < 6)
        {
          if (Directory.EnumerateFiles(current, "*.sln").Any())
            return current;

          var parent = Path.GetDirectoryName(current);
          if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            break;

          current = NormalizeDirectory(parent);
          depth++;
        }
      }
      catch
      {
        // ignore and fall back to the provided directory
      }

      return string.Empty;
    }

    private static WorkingDirectoryCandidate AnalyzeCandidate(CandidateSeed seed)
    {
      if (string.IsNullOrWhiteSpace(seed.Path) || seed.Path.StartsWith("<", StringComparison.Ordinal))
        return new WorkingDirectoryCandidate(seed.Source ?? string.Empty, string.Empty, false, false, false, -1, seed.IsWorkspaceRoot);

      var normalized = NormalizeDirectory(seed.Path);
      var exists = !string.IsNullOrEmpty(normalized) && Directory.Exists(normalized);
      var hasSolution = exists && DirectoryContainsFiles(normalized, "*.sln");
      var hasProject = exists && DirectoryContainsFiles(normalized, "*.csproj", "*.vbproj", "*.fsproj", "*.vcxproj", "*.vcproj");
      var depth = CalculatePathDepth(normalized);
      return new WorkingDirectoryCandidate(seed.Source ?? string.Empty, normalized, exists, hasSolution, hasProject, depth, seed.IsWorkspaceRoot);
    }

    private static void TryAddCandidate(List<CandidateSeed> list, string source, Func<string> resolver, bool isWorkspaceRoot = false)
    {
      if (list == null)
        return;

      string result;
      try
      {
        result = resolver?.Invoke() ?? string.Empty;
      }
      catch (COMException ex)
      {
        result = $"<COMException:{ex.ErrorCode:X8}>";
      }
      catch (Exception ex)
      {
        result = $"<Exception:{ex.GetType().Name}>";
      }

      list.Add(new CandidateSeed(source, result ?? string.Empty, isWorkspaceRoot));
    }

    private static string SafeInvoke(Func<string> resolver)
    {
      try
      {
        return resolver?.Invoke() ?? string.Empty;
      }
      catch (COMException ex)
      {
        return $"<COMException:{ex.ErrorCode:X8}>";
      }
      catch (Exception ex)
      {
        return $"<Exception:{ex.GetType().Name}>";
      }
    }

    private static async Task<string> SafeInvokeAsync(Func<Task<string>> resolver)
    {
      if (resolver == null)
        return string.Empty;

      try
      {
        return await resolver().ConfigureAwait(true) ?? string.Empty;
      }
      catch (COMException ex)
      {
        return $"<COMException:{ex.ErrorCode:X8}>";
      }
      catch (Exception ex)
      {
        return $"<Exception:{ex.GetType().Name}>";
      }
    }

    private static async Task<(string, string)> SafeInvokeTupleAsync(Func<Task<(string, string)>> resolver)
    {
      if (resolver == null)
        return (string.Empty, string.Empty);

      try
      {
        var result = await resolver().ConfigureAwait(true);
        return (result.Item1 ?? string.Empty, result.Item2 ?? string.Empty);
      }
      catch (COMException ex)
      {
        var marker = $"<COMException:{ex.ErrorCode:X8}>";
        return (marker, marker);
      }
      catch (Exception ex)
      {
        var marker = $"<Exception:{ex.GetType().Name}>";
        return (marker, marker);
      }
    }

    private static WorkingDirectoryCandidate SelectBestCandidate(List<WorkingDirectoryCandidate> candidates)
    {
      if (candidates == null || candidates.Count == 0)
        return null;

      bool OutsideExtension(WorkingDirectoryCandidate candidate)
        => !string.IsNullOrEmpty(candidate.Path) && !candidate.IsInsideExtension;

      bool Exists(WorkingDirectoryCandidate candidate)
        => !string.IsNullOrEmpty(candidate.Path) && candidate.Exists;

      WorkingDirectoryCandidate Pick(Func<WorkingDirectoryCandidate, bool> predicate)
      {
        var outside = candidates.FirstOrDefault(c => predicate(c) && OutsideExtension(c));
        if (outside != null)
          return outside;

        return candidates.FirstOrDefault(predicate);
      }

      var workspaceCandidate = Pick(c => Exists(c) && c.IsWorkspaceRoot);
      if (workspaceCandidate != null)
        return workspaceCandidate;

      var solutionSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {
        "SolutionReadyHint",
        "IVsSolution.GetSolutionInfo.Directory",
        "IVsSolution.VSPROPID_SolutionDirectory",
        "IVsSolution.GetSolutionInfo.FileDir",
        "IVsSolution.GetSolutionRootDirectory",
        "DTE.Solution.FullName",
        "DTE.Solution.FileName",
        "DTE.Solution.Properties.Path",
        "VS.Solutions.Current.FullPath"
      };

      var solutionCandidate = Pick(c => Exists(c) && solutionSources.Contains(c.Source));
      if (solutionCandidate != null)
        return solutionCandidate;

      var selectionCandidate = Pick(c => Exists(c) && (c.Source.StartsWith("VS.Solutions.ActiveItem", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Source, "VS.Solutions.ActiveProject", StringComparison.OrdinalIgnoreCase)));
      if (selectionCandidate != null)
        return selectionCandidate;

      var activeDocumentCandidate = Pick(c => Exists(c) && string.Equals(c.Source, "DTE.ActiveDocument", StringComparison.OrdinalIgnoreCase));
      if (activeDocumentCandidate != null)
        return activeDocumentCandidate;

      var existingOutside = candidates.FirstOrDefault(c => Exists(c) && OutsideExtension(c));
      if (existingOutside != null)
        return existingOutside;

      var existing = candidates.FirstOrDefault(Exists);
      if (existing != null)
        return existing;

      return candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c.Path));
    }

    private static int CalculatePathDepth(string path)
    {
      if (string.IsNullOrEmpty(path))
        return -1;

      return path.Count(ch => ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar);
    }

    private static bool DirectoryContainsFiles(string directory, params string[] patterns)
    {
      if (string.IsNullOrEmpty(directory) || patterns == null || patterns.Length == 0)
        return false;

      try
      {
        foreach (var pattern in patterns)
        {
          if (Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).Any())
            return true;
        }
      }
      catch
      {
        // ignore IO issues
      }

      return false;
    }

    private static string GetActiveSolutionFullName(DTE2 dte)
    {
      try { return dte?.Solution?.FullName ?? string.Empty; }
      catch { return string.Empty; }
    }

    private static string GetActiveSolutionFileName(DTE2 dte)
    {
      try { return dte?.Solution?.FileName ?? string.Empty; }
      catch { return string.Empty; }
    }

    private static string GetActiveDocumentDirectory(DTE2 dte)
    {
      try
      {
        var fullName = dte?.ActiveDocument?.FullName;
        if (string.IsNullOrWhiteSpace(fullName))
          return string.Empty;

        if (Directory.Exists(fullName))
          return NormalizeDirectory(fullName);

        if (File.Exists(fullName))
          return NormalizeDirectory(Path.GetDirectoryName(fullName) ?? string.Empty);

        return NormalizeDirectory(Path.GetDirectoryName(fullName) ?? string.Empty);
      }
      catch
      {
        return string.Empty;
      }
    }

    private static async Task<SolutionItem> SafeGetCurrentSolutionAsync()
    {
      try
      {
        return await VS.Solutions.GetCurrentSolutionAsync();
      }
      catch
      {
        return null;
      }
    }

    private static async Task<SolutionItem> GetActiveProjectAsync()
    {
      try
      {
        var items = await VS.Solutions.GetActiveItemsAsync();
        if (items != null)
        {
          foreach (var item in items)
          {
            if (item == null)
              continue;

            if (item.Type == SolutionItemType.Project || item.Type == SolutionItemType.PhysicalFolder || item.Type == SolutionItemType.Solution)
              return item;
          }
        }
      }
      catch
      {
      }

      return null;
    }

    private static async Task<IReadOnlyList<SolutionItem>> GetActiveSolutionItemsAsync()
    {
      try
      {
        var items = await VS.Solutions.GetActiveItemsAsync();
        var list = items?.ToList();
        return list != null && list.Count > 0 ? list : Array.Empty<SolutionItem>();
      }
      catch
      {
        return Array.Empty<SolutionItem>();
      }
    }

    private static string GetSolutionItemPath(SolutionItem item)
    {
      if (item == null)
        return string.Empty;

      var path = TryGetSolutionItemFullPath(item);
      if (!string.IsNullOrEmpty(path))
        return GetDirectoryFromFile(path);

      var parent = TryGetSolutionItemParent(item);
      while (parent != null)
      {
        path = TryGetSolutionItemFullPath(parent);
        if (!string.IsNullOrEmpty(path))
          return GetDirectoryFromFile(path);
        parent = TryGetSolutionItemParent(parent);
      }

      foreach (var child in TryGetSolutionItemChildren(item))
      {
        path = TryGetSolutionItemFullPath(child);
        if (!string.IsNullOrEmpty(path))
          return GetDirectoryFromFile(path);
      }

      return string.Empty;
    }

    private static string TryGetSolutionItemFullPath(SolutionItem item)
    {
      if (item == null)
        return string.Empty;

      try
      {
        var value = item.FullPath;
        if (!string.IsNullOrWhiteSpace(value))
          return value;
      }
      catch
      {
      }

      try
      {
        var type = item.GetType();
        var prop = type.GetProperty("FullPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop?.GetValue(item) is string alt && !string.IsNullOrWhiteSpace(alt))
          return alt;

        var physicalProp = type.GetProperty("PhysicalPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (physicalProp?.GetValue(item) is string physical && !string.IsNullOrWhiteSpace(physical))
          return physical;
      }
      catch
      {
      }

      return string.Empty;
    }

    private static SolutionItem TryGetSolutionItemParent(SolutionItem item)
    {
      if (item == null)
        return null;

      try { return item.Parent; }
      catch { }

      try
      {
        var prop = item.GetType().GetProperty("Parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop?.GetValue(item) is SolutionItem parent)
          return parent;
      }
      catch
      {
      }

      return null;
    }

    private static IEnumerable<SolutionItem> TryGetSolutionItemChildren(SolutionItem item)
    {
      if (item == null)
        return Array.Empty<SolutionItem>();

      var results = new List<SolutionItem>();
      try
      {
        var childrenProp = item.GetType().GetProperty("Children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (childrenProp != null && childrenProp.GetValue(item) is IEnumerable enumerable)
        {
          foreach (var childObj in enumerable)
          {
            if (childObj is SolutionItem child && child != null)
              results.Add(child);
          }
        }
      }
      catch
      {
      }

      return results.Count > 0 ? results : Array.Empty<SolutionItem>();
    }

    private static void AddProjectDirectoryCandidates(List<CandidateSeed> list, DTE2 dte)
    {
      if (list == null)
        return;

      var index = 0;
      foreach (var (name, path) in EnumerateSolutionProjectDirectories(dte))
      {
        var localPath = path;
        var label = string.IsNullOrEmpty(name) ? $"DTE.Project[{index++}]" : $"DTE.Project:{name}";
        TryAddCandidate(list, label, () => localPath);
      }
    }

    private static IEnumerable<(string Name, string Path)> EnumerateSolutionProjectDirectories(DTE2 dte)
    {
      if (dte?.Solution == null)
        yield break;

      foreach (var project in EnumerateProjects(dte.Solution))
      {
        var path = GetProjectDirectory(project);
        if (!string.IsNullOrEmpty(path))
          yield return (SafeGetProjectName(project), path);
      }
    }

    private static IEnumerable<DteProject> EnumerateProjects(DteSolution solution)
    {
      if (solution == null)
        yield break;

      DteProjects projects = null;
      try { projects = solution.Projects; }
      catch { }

      if (projects == null)
        yield break;

      foreach (DteProject project in projects)
      {
        if (project == null)
          continue;

        yield return project;

        foreach (var nested in EnumerateSubProjects(project))
          yield return nested;
      }
    }

    private static IEnumerable<DteProject> EnumerateSubProjects(DteProject project)
    {
      if (project == null)
        yield break;

      DteProjectItems items = null;
      try { items = project.ProjectItems; }
      catch { }

      if (items == null)
        yield break;

      foreach (DteProjectItem item in items)
      {
        DteProject subProject = null;
        try { subProject = item.SubProject; }
        catch { }

        if (subProject != null)
        {
          yield return subProject;

          foreach (var nested in EnumerateSubProjects(subProject))
            yield return nested;
        }
      }
    }

    private static string GetProjectDirectory(DteProject project)
    {
      if (project == null)
        return string.Empty;

      try
      {
        var fullName = project.FullName;
        if (!string.IsNullOrWhiteSpace(fullName))
        {
          if (Directory.Exists(fullName))
            return NormalizeDirectory(fullName);

          if (File.Exists(fullName))
            return NormalizeDirectory(Path.GetDirectoryName(fullName) ?? string.Empty);
        }
      }
      catch
      {
      }

      foreach (var propertyName in new[] { "FullPath", "ProjectHome", "ProjectDir" })
      {
        var candidate = GetProjectProperty(project, propertyName);
        if (!string.IsNullOrWhiteSpace(candidate))
          return NormalizeDirectory(candidate);
      }

      return string.Empty;
    }

    private static string GetProjectProperty(DteProject project, string propertyName)
    {
      if (project?.Properties == null || string.IsNullOrEmpty(propertyName))
        return string.Empty;

      try
      {
        var property = project.Properties.Item(propertyName);
        if (property?.Value is string value && !string.IsNullOrWhiteSpace(value))
          return value;
      }
      catch (ArgumentException)
      {
        // property not available
      }
      catch (COMException)
      {
      }
      catch
      {
      }

      return string.Empty;
    }

    private static string SafeGetProjectName(DteProject project)
    {
      try { return project?.Name ?? string.Empty; }
      catch { return string.Empty; }
    }

    private async Task<string> GetSolutionRootDirectoryAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var solutionService = await GetSolutionServiceAsync();
      var root = TryInvokeSolutionRootDirectory(solutionService);
      if (!string.IsNullOrEmpty(root))
        return NormalizeDirectory(root);

      return string.Empty;
    }

    private static string TryInvokeSolutionRootDirectory(IVsSolution solutionService)
    {
      if (solutionService == null)
        return string.Empty;

      try
      {
        var method = solutionService.GetType().GetMethod("GetSolutionRootDirectory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string).MakeByRefType() }, null);
        if (method != null)
        {
          var args = new object[] { string.Empty };
          var result = method.Invoke(solutionService, args);
          if (result is int hr && ErrorHandler.Succeeded(hr))
          {
            if (args[0] is string dir && !string.IsNullOrWhiteSpace(dir))
              return NormalizeDirectory(dir);
          }
        }
      }
      catch (TargetInvocationException tie) when (tie.InnerException is COMException)
      {
        // ignore COM failures
      }
      catch
      {
      }

      return string.Empty;
    }

    internal static async Task<EnvironmentSnapshot> CaptureEnvironmentSnapshotAsync(CancellationToken ct)
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
      var solutionRoot = TryGetSolutionRootDirectory();
      var workspaceRoot = TryGetFolderWorkspaceRootSynced();
      return new EnvironmentSnapshot(solutionRoot, workspaceRoot);
    }

    private static string TryGetSolutionRootDirectory()
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      if (ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) is IVsSolution solution &&
          ErrorHandler.Succeeded(solution.GetSolutionInfo(out var dir, out _, out _)) &&
          !string.IsNullOrWhiteSpace(dir))
      {
        return NormalizeDirectory(dir);
      }

      return string.Empty;
    }

    private static string TryGetFolderWorkspaceRootSynced()
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      try
      {
        static Type ResolveWorkspaceServiceType()
        {
          return Type.GetType("Microsoft.VisualStudio.Workspace.VSIntegration.Contracts.SVsFolderWorkspaceService, Microsoft.VisualStudio.Workspace.VSIntegration.Contracts", throwOnError: false)
                 ?? Type.GetType("Microsoft.VisualStudio.Workspace.VSIntegration.SVsFolderWorkspaceService, Microsoft.VisualStudio.Workspace.VSIntegration", throwOnError: false);
        }

        var serviceType = ResolveWorkspaceServiceType();
        if (serviceType == null)
          return string.Empty;

        var service = ServiceProvider.GlobalProvider?.GetService(serviceType);
        if (service == null)
          return string.Empty;

        var currentWorkspaceProp = service.GetType().GetProperty("CurrentWorkspace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var currentWorkspace = currentWorkspaceProp?.GetValue(service);
        if (currentWorkspace == null)
          return string.Empty;

        var locationProp = currentWorkspace.GetType().GetProperty("Location", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (locationProp?.GetValue(currentWorkspace) is string location && !string.IsNullOrWhiteSpace(location))
          return NormalizeDirectory(location);

        var rootProp = currentWorkspace.GetType().GetProperty("Root", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (rootProp?.GetValue(currentWorkspace) is string root && !string.IsNullOrWhiteSpace(root))
          return NormalizeDirectory(root);
      }
      catch
      {
      }

      return string.Empty;
    }

    private static async Task<string> GetFolderWorkspaceRootAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      return TryGetFolderWorkspaceRootSynced();
    }

    internal static UIContext TryGetFolderOpenUIContext()
    {
      var prop = typeof(KnownUIContexts).GetProperty("FolderOpenContext", BindingFlags.Public | BindingFlags.Static);
      if (prop?.GetValue(null) is UIContext contextFromProperty)
        return contextFromProperty;

      var candidateFieldNames = new[] { "FolderView", "FolderOpen", "OpenFolder" };
      foreach (var fieldName in candidateFieldNames)
      {
        var field = typeof(UIContextGuids80).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field?.GetValue(null) is string guidString && Guid.TryParse(guidString, out var guid))
          return UIContext.FromUIContextGuid(guid);
      }

      return null;
    }

    private string GetFolderWorkspaceLocation()
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      return TryGetFolderWorkspaceRootSynced();
    }

    private static string GetDteSolutionProperty(DTE2 dte, string propertyName)
    {
      if (dte?.Solution?.Properties == null || string.IsNullOrWhiteSpace(propertyName))
        return string.Empty;

      try
      {
        foreach (Property property in dte.Solution.Properties)
        {
          if (property == null)
            continue;

          string name = null;
          try
          {
            name = property.Name;
          }
          catch (COMException)
          {
            continue;
          }
          catch (Exception)
          {
            continue;
          }

          if (!string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase))
            continue;

          try
          {
            if (property.Value is string value && !string.IsNullOrWhiteSpace(value))
              return value;
          }
          catch (COMException)
          {
            continue;
          }
          catch (Exception)
          {
            continue;
          }
        }
      }
      catch
      {
        // ignore COM exceptions and fall back
      }

      return string.Empty;
    }

    private async Task AdviseSolutionEventsAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      await SubscribeUiContextsAsync();

      if (_solutionEvents != null)
        return;

      var solutionService = await GetSolutionServiceAsync();
      if (solutionService == null)
        return;

      var sink = new SolutionEventsSink(this);
      if (ErrorHandler.Succeeded(solutionService.AdviseSolutionEvents(sink, out var cookie)))
      {
        _solutionEvents = sink;
        _solutionEventsCookie = cookie;
        OnSolutionContextChanged("solution-events-advise");
      }
    }

    private async Task UnadviseSolutionEventsAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      if (_solutionService != null && _solutionEventsCookie != 0)
      {
        try
        {
          _solutionService.UnadviseSolutionEvents(_solutionEventsCookie);
        }
        catch
        {
          // ignore
        }
        _solutionEventsCookie = 0;
      }

      _solutionEvents = null;
      if (_solutionService != null)
      {
        _solutionService = null;
      }
    }

    private async Task SubscribeUiContextsAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      if (_solutionLoadedContext == null)
      {
        _solutionLoadedContext = KnownUIContexts.SolutionExistsAndFullyLoadedContext;
        _solutionLoadedContext.UIContextChanged += OnSolutionLoadedContextChanged;
        if (_solutionLoadedContext.IsActive)
          _ = ThreadHelper.JoinableTaskFactory.RunAsync(OnSolutionFullyLoadedAsync);
      }

      if (_folderOpenContext == null)
      {
        _folderOpenContext = TryGetFolderOpenUIContext();
        if (_folderOpenContext != null)
        {
          _folderOpenContext.UIContextChanged += OnFolderContextChanged;
          if (_folderOpenContext.IsActive && (_solutionLoadedContext == null || !_solutionLoadedContext.IsActive))
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(OnFolderWorkspaceReadyAsync);
        }
      }
    }

    private async Task UnsubscribeUiContextsAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      if (_solutionLoadedContext != null)
      {
        _solutionLoadedContext.UIContextChanged -= OnSolutionLoadedContextChanged;
        _solutionLoadedContext = null;
      }

      if (_folderOpenContext != null)
      {
        _folderOpenContext.UIContextChanged -= OnFolderContextChanged;
        _folderOpenContext = null;
      }
    }

    private void OnSolutionContextChanged(string reason)
    {
      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await EnsureWorkingDirectoryUpToDateAsync(reason));
    }

    private async Task EnsureWorkingDirectoryUpToDateAsync(string reason)
    {
      await _workingDirLock.WaitAsync();
      try
      {
        var resolution = await ResolveWorkingDirectoryAsync();
        var newPath = resolution?.Selected?.Path ?? string.Empty;
        if (string.IsNullOrEmpty(newPath) || PathsEqual(newPath, _workingDir))
          return;

        var previous = _workingDir;
        _workingDir = newPath;
        await LogWorkingDirectoryResolutionAsync(reason, resolution, previous, includeCandidates: true);

        if (_host == null || !_cliStarted)
          return;

        await UpdateAuthenticationStateAsync(_authKnown, _isAuthenticated, "Switching Codex to current solution...", true);
        var restarted = await RestartCliAsync();
        if (!restarted)
        {
          await UpdateAuthenticationStateAsync(true, false, "Failed to restart Codex CLI after solution change. Check Diagnostics.", false);
          return;
        }

        var options = _options ?? new CodexOptions();
        var auth = await _host.CheckAuthenticationAsync(options, _workingDir);
        await HandleAuthenticationResultAsync(auth);
      }
      finally
      {
        _workingDirLock.Release();
      }
    }

    private async Task OnSolutionReadyAsync(string path)
    {
      var normalized = NormalizeDirectory(path);
      if (string.IsNullOrEmpty(normalized) || IsInsideExtensionRoot(normalized))
        return;

      _waitingForSolutionLoad = false;
      _lastKnownSolutionRoot = normalized;
      _lastKnownWorkspaceRoot = string.Empty;

      await EnsureWorkingDirectoryUpToDateAsync("solution-ready");
    }

    private async Task OnWorkspaceReadyAsync(string path)
    {
      var normalized = NormalizeDirectory(path);
      if (string.IsNullOrEmpty(normalized) || IsInsideExtensionRoot(normalized))
        return;

      _lastKnownWorkspaceRoot = normalized;
      _lastKnownSolutionRoot = string.Empty;

      if (_solutionLoadedContext != null && _solutionLoadedContext.IsActive)
        return;

      await EnsureWorkingDirectoryUpToDateAsync("workspace-ready");
    }

    private void OnSolutionEventsSolutionOpened()
    {
      _waitingForSolutionLoad = true;
      _lastKnownWorkspaceRoot = string.Empty;

      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (_solutionLoadedContext != null && _solutionLoadedContext.IsActive)
          await OnSolutionFullyLoadedAsync();
      });
    }

    private void OnSolutionClosed()
    {
      _waitingForSolutionLoad = false;
      _lastKnownSolutionRoot = string.Empty;

      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (_folderOpenContext != null && _folderOpenContext.IsActive)
          await OnFolderWorkspaceReadyAsync();
        else
          await EnsureWorkingDirectoryUpToDateAsync("solution-closed");
      });
    }

    private async Task OnSolutionFullyLoadedAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      var (directory, file) = await GetSolutionInfoAsync();
      var candidate = !string.IsNullOrEmpty(directory) ? directory : GetDirectoryFromFile(file);
      if (string.IsNullOrEmpty(candidate))
      {
        var solutionItem = await SafeGetCurrentSolutionAsync();
        candidate = GetSolutionItemPath(solutionItem);
      }

      await OnSolutionReadyAsync(candidate);
    }

    private async Task OnFolderWorkspaceReadyAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var location = GetFolderWorkspaceLocation();
      await OnWorkspaceReadyAsync(location);
    }

    private void OnSolutionLoadedContextChanged(object sender, UIContextChangedEventArgs e)
    {
      if (e.Activated)
        _ = ThreadHelper.JoinableTaskFactory.RunAsync(OnSolutionFullyLoadedAsync);
    }

    private void OnFolderContextChanged(object sender, UIContextChangedEventArgs e)
    {
      if (!e.Activated)
        return;

      if (_solutionLoadedContext != null && _solutionLoadedContext.IsActive)
        return;

      _ = ThreadHelper.JoinableTaskFactory.RunAsync(OnFolderWorkspaceReadyAsync);
    }

    private async Task CleanupSolutionSubscriptionsAsync()
    {
      await UnadviseSolutionEventsAsync();
      await UnsubscribeUiContextsAsync();
    }

    private sealed class SolutionEventsSink : IVsSolutionEvents
    {
      private readonly WeakReference<MyToolWindowControl> _owner;

      public SolutionEventsSink(MyToolWindowControl owner)
      {
        _owner = new WeakReference<MyToolWindowControl>(owner);
      }

      private void Notify(string reason, Action<MyToolWindowControl> callback = null)
      {
        if (_owner.TryGetTarget(out var control))
        {
          control.OnSolutionContextChanged(reason);
          callback?.Invoke(control);
        }
      }

      public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
      {
        if (fAdded != 0)
          Notify("project-opened");
        return VSConstants.S_OK;
      }

      public int OnAfterCloseProject(IVsHierarchy pHierarchy, int fRemoved)
      {
        if (fRemoved != 0)
          Notify("project-closed");
        return VSConstants.S_OK;
      }

      public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;

      public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;

      public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
      {
        Notify("project-loaded");
        return VSConstants.S_OK;
      }

      public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

      public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
      {
        Notify("project-unload");
        return VSConstants.S_OK;
      }

      public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
      {
        Notify("solution-opened", control => control.OnSolutionEventsSolutionOpened());
        return VSConstants.S_OK;
      }

      public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;

      public int OnAfterCloseSolution(object pUnkReserved)
      {
        Notify("solution-closed", control => control.OnSolutionClosed());
        return VSConstants.S_OK;
      }

      public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;
    }

    private static string TryGetString(JObject obj, string name)
    {
      try { return obj?[name]?.ToString(); } catch { return null; }
    }

    private void ConfigureHeartbeat(EventMsg evt)
    {
      var state = ExtractHeartbeatState(evt.Raw);
      if (state == null)
      {
        StopHeartbeatTimer();
        return;
      }

      var needsRestart = true;
      lock (_heartbeatLock)
      {
        if (_heartbeatTimer != null && _heartbeatState != null)
        {
          var sameInterval = Math.Abs((_heartbeatState.Interval - state.Interval).TotalMilliseconds) < 1;
          var sameOp = string.Equals(_heartbeatState.OpType, state.OpType, StringComparison.OrdinalIgnoreCase);
          if (sameInterval && sameOp)
          {
            _heartbeatState = state;
            needsRestart = false;
          }
        }
      }

      if (!needsRestart)
        return;

      StartHeartbeatTimer(state);
    }

    private void StartHeartbeatTimer(HeartbeatState state)
    {
      if (state == null || state.OpTemplate == null)
        return;

      Timer oldTimer = null;
      HeartbeatState previousState = null;
      var intervalMs = (int)Math.Max(1, Math.Min(int.MaxValue, state.Interval.TotalMilliseconds));

      lock (_heartbeatLock)
      {
        oldTimer = _heartbeatTimer;
        previousState = _heartbeatState;
        _heartbeatState = state;
        _heartbeatTimer = new Timer(OnHeartbeatTimer, null, intervalMs, intervalMs);
      }

      oldTimer?.Dispose();

      var stateChanged = previousState == null
        || Math.Abs((previousState.Interval - state.Interval).TotalMilliseconds) >= 1
        || !string.Equals(previousState.OpType, state.OpType, StringComparison.OrdinalIgnoreCase);

      if (stateChanged)
      {
        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
          try
          {
            var pane = await DiagnosticsPane.GetAsync();
            await pane.WriteLineAsync(
              $"[info] Heartbeat enabled (interval={state.Interval.TotalSeconds:F1}s, op={state.OpType})");
          }
          catch
          {
          }
        });
      }
    }

    private void StopHeartbeatTimer()
    {
      Timer timerToDispose = null;
      var hadState = false;

      lock (_heartbeatLock)
      {
        if (_heartbeatTimer != null)
        {
          timerToDispose = _heartbeatTimer;
          _heartbeatTimer = null;
        }

        if (_heartbeatState != null)
        {
          hadState = true;
          _heartbeatState = null;
        }
      }

      timerToDispose?.Dispose();
      Interlocked.Exchange(ref _heartbeatSending, 0);

      if (hadState)
      {
        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
          try
          {
            var pane = await DiagnosticsPane.GetAsync();
            await pane.WriteLineAsync("[info] Heartbeat disabled");
          }
          catch
          {
          }
        });
      }
    }

    private void OnHeartbeatTimer(object state)
    {
      if (Interlocked.Exchange(ref _heartbeatSending, 1) == 1)
        return;

      HeartbeatState snapshot;
      lock (_heartbeatLock)
      {
        snapshot = _heartbeatState;
      }

      var host = _host;

      if (snapshot == null || host == null)
      {
        Interlocked.Exchange(ref _heartbeatSending, 0);
        return;
      }

      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
      {
        try
        {
          await SendHeartbeatAsync(host, snapshot);
        }
        finally
        {
          Interlocked.Exchange(ref _heartbeatSending, 0);
        }
      });
    }

    private async Task SendHeartbeatAsync(CodexCliHost host, HeartbeatState state)
    {
      try
      {
        var submission = CreateHeartbeatSubmission(state.OpTemplate);
        if (string.IsNullOrEmpty(submission))
          return;

        var ok = await host.SendAsync(submission);
        if (!ok)
        {
          var pane = await DiagnosticsPane.GetAsync();
          await pane.WriteLineAsync("[warn] Heartbeat send failed (SendAsync returned false).");
        }
      }
      catch (ObjectDisposedException)
      {
        StopHeartbeatTimer();
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[warn] Heartbeat send error: {ex.Message}");
      }
    }

    private static string CreateHeartbeatSubmission(JObject opTemplate)
    {
      if (opTemplate == null)
        return string.Empty;

      var op = opTemplate.DeepClone() as JObject;
      if (op == null)
        return string.Empty;

      var type = TryGetString(op, "type");
      if (string.IsNullOrWhiteSpace(type))
        return string.Empty;

      var submission = new JObject
      {
        ["id"] = Guid.NewGuid().ToString(),
        ["op"] = op
      };

      return submission.ToString(Formatting.None);
    }

    private static HeartbeatState ExtractHeartbeatState(JObject raw)
    {
      if (raw == null)
        return null;

      var heartbeatToken = FindHeartbeatToken(raw);
      var intervalMs = ExtractHeartbeatIntervalMs(raw, heartbeatToken);
      if (intervalMs <= 0)
        return null;

      var opTemplate = BuildHeartbeatOpTemplate(raw, heartbeatToken);
      if (opTemplate == null)
        return null;

      var opType = TryGetString(opTemplate, "type");
      if (string.IsNullOrWhiteSpace(opType))
        return null;

      var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 1000));
      return new HeartbeatState(interval, opTemplate, opType);
    }

    private static JToken FindHeartbeatToken(JObject root)
    {
      if (root == null)
        return null;

      foreach (var path in new[]
      {
        "heartbeat",
        "session.heartbeat",
        "session.capabilities.heartbeat",
        "session.protocol_features.heartbeat",
        "capabilities.heartbeat",
        "protocol_features.heartbeat",
        "features.heartbeat",
        "settings.heartbeat"
      })
      {
        var token = SafeSelectToken(root, path);
        if (token != null)
          return token;
      }

      return null;
    }

    private static int ExtractHeartbeatIntervalMs(JObject root, JToken heartbeatToken)
    {
      if (heartbeatToken != null)
      {
        if (heartbeatToken.Type == JTokenType.Integer || heartbeatToken.Type == JTokenType.Float)
        {
          var direct = ValueAsInt(heartbeatToken);
          if (direct > 0)
            return direct;
        }

        if (heartbeatToken is JObject heartbeatObj)
        {
          foreach (var name in new[] { "interval_ms", "intervalMs", "interval" })
          {
            var value = ValueAsInt(heartbeatObj[name]);
            if (value > 0)
              return value;
          }

          if (heartbeatObj["config"] is JObject configObj)
          {
            foreach (var name in new[] { "interval_ms", "intervalMs" })
            {
              var value = ValueAsInt(configObj[name]);
              if (value > 0)
                return value;
            }
          }
        }
      }

      foreach (var token in new[]
      {
        root,
        root?["session"] as JObject
      })
      {
        if (token == null)
          continue;

        foreach (var name in new[]
        {
          "heartbeat_interval_ms",
          "heartbeatIntervalMs",
          "keep_alive_ms",
          "keepAliveMs",
          "keepalive_ms"
        })
        {
          var value = ValueAsInt(token[name]);
          if (value > 0)
            return value;
        }
      }

      return 0;
    }

    private static JObject BuildHeartbeatOpTemplate(JObject root, JToken heartbeatToken)
    {
      JObject opTemplate = null;
      if (heartbeatToken is JObject heartbeatObj)
      {
        if (heartbeatObj["op"] is JObject opObj)
          opTemplate = opObj.DeepClone() as JObject;
        else if (heartbeatObj["op_template"] is JObject opTemplateObj)
          opTemplate = opTemplateObj.DeepClone() as JObject;
        else if (heartbeatObj["opTemplate"] is JObject opTemplateCamel)
          opTemplate = opTemplateCamel.DeepClone() as JObject;
      }

      if (opTemplate != null)
      {
        var type = TryGetString(opTemplate, "type");
        if (string.IsNullOrWhiteSpace(type))
        {
          var fallback = ExtractOpTypeFromToken(heartbeatToken as JObject) ?? DetermineFallbackOpType(root);
          if (string.IsNullOrWhiteSpace(fallback))
            return null;
          opTemplate["type"] = fallback;
        }

        return opTemplate;
      }

      var opType = ExtractOpTypeFromToken(heartbeatToken as JObject) ?? DetermineFallbackOpType(root);
      if (string.IsNullOrWhiteSpace(opType))
        return null;

      return new JObject { ["type"] = opType };
    }

    private static string ExtractOpTypeFromToken(JObject token)
    {
      if (token == null)
        return null;

      var opType = TryGetString(token, "op_type") ?? TryGetString(token, "opType");
      if (!string.IsNullOrWhiteSpace(opType))
        return opType;

      if (token["op"] is JObject op)
      {
        opType = TryGetString(op, "type");
        if (!string.IsNullOrWhiteSpace(opType))
          return opType;
      }

      return null;
    }

    private static string DetermineFallbackOpType(JObject root)
    {
      var supported = ExtractSupportedOps(root);
      if (supported.Contains("heartbeat"))
        return "heartbeat";
      if (supported.Contains("noop"))
        return "noop";
      return null;
    }

    private static IReadOnlyCollection<string> ExtractSupportedOps(JObject root)
    {
      var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (root == null)
        return result;

      foreach (var name in new[]
      {
        "supported_ops",
        "supportedOps",
        "supported_operations",
        "supportedOperations",
        "allowed_ops",
        "allowedOps"
      })
      {
        foreach (var token in FindTokensByName(root, name))
        {
          if (token == null)
            continue;

          if (token.Type == JTokenType.Array)
          {
            foreach (var item in token)
            {
              var value = item?.ToString();
              if (!string.IsNullOrWhiteSpace(value))
                result.Add(value.Trim());
            }
          }
          else
          {
            var value = token.ToString();
            if (!string.IsNullOrWhiteSpace(value))
              result.Add(value.Trim());
          }
        }
      }

      return result;
    }

    private static IEnumerable<JToken> FindTokensByName(JToken token, string name)
    {
      if (token == null)
        yield break;

      if (token.Type == JTokenType.Object)
      {
        var obj = (JObject)token;
        foreach (var property in obj.Properties())
        {
          if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            yield return property.Value;

          foreach (var child in FindTokensByName(property.Value, name))
            yield return child;
        }
      }
      else if (token.Type == JTokenType.Array)
      {
        foreach (var item in (JArray)token)
        {
          foreach (var child in FindTokensByName(item, name))
            yield return child;
        }
      }
    }

    private static JToken SafeSelectToken(JObject obj, string path)
    {
      try { return obj?.SelectToken(path, false); }
      catch { return null; }
    }

    private static int ValueAsInt(JToken token)
    {
      if (token == null || token.Type == JTokenType.Null)
        return 0;

      if (token.Type == JTokenType.Integer)
        return token.Value<int>();

      if (token.Type == JTokenType.Float)
        return (int)Math.Round(token.Value<double>());

      var text = token.ToString();
      if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        return parsed;

      return 0;
    }

    private string ResolveExecId(EventMsg evt)
    {
      var id = GetExecEventId(evt);
      if (string.IsNullOrEmpty(id))
        return string.Empty;

      if (_execIdRemap.TryGetValue(id, out var mapped))
        return mapped;

      return id;
    }

    private static string GetExecEventId(EventMsg evt)
    {
      var callId = TryGetString(evt.Raw, "call_id");
      if (!string.IsNullOrEmpty(callId))
        return callId;
      if (!string.IsNullOrEmpty(evt.Id))
        return evt.Id;
      return string.Empty;
    }

    private string RegisterExecFallbackId()
    {
      var id = Guid.NewGuid().ToString();
      _execIdRemap[id] = id;
      _lastExecFallbackId = id;
      return id;
    }

    private static string BuildExecHeader(string command, string cwd)
    {
      var hasCommand = !string.IsNullOrWhiteSpace(command);
      var hasCwd = !string.IsNullOrWhiteSpace(cwd);

      if (hasCommand && hasCwd)
        return $"$ {command} (cwd: {cwd})";
      if (hasCommand)
        return $"$ {command}";
      if (hasCwd)
        return $"cwd: {cwd}";
      return "$ exec";
    }

    private static (string display, string normalized) ExtractExecCommandInfo(JToken commandToken)
    {
      if (commandToken == null)
        return (string.Empty, string.Empty);

      if (commandToken.Type == JTokenType.Array)
      {
        var array = (JArray)commandToken;
        var parts = array.Select(t => TrimQuotes(t?.ToString() ?? string.Empty)).Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (parts.Count == 0)
          return (string.Empty, string.Empty);

        if (parts.Count >= 3 && string.Equals(parts[1], "-lc", StringComparison.OrdinalIgnoreCase))
        {
          var script = parts[2];
          return (script, script);
        }

        var joined = string.Join(" ", parts);
        var tail = parts.LastOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? joined;
        return (joined, tail);
      }

      if (commandToken.Type == JTokenType.Object)
      {
        return (commandToken.ToString(Formatting.None), string.Empty);
      }

      var text = TrimQuotes(commandToken.ToString());
      return (text, text);
    }

    private static string TrimQuotes(string text)
    {
      if (string.IsNullOrEmpty(text))
        return string.Empty;

      var trimmed = text.Trim();
      if (trimmed.Length >= 2)
      {
        var first = trimmed[0];
        var last = trimmed[trimmed.Length - 1];
        if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
          return trimmed.Substring(1, trimmed.Length - 2);
      }

      return trimmed;
    }

    private static bool ShouldOfferRemember(string signature)
      => !string.IsNullOrWhiteSpace(signature);

    private void RememberExecDecision(string signature, bool approved)
    {
      if (string.IsNullOrWhiteSpace(signature))
        return;
      _rememberedExecApprovals[signature] = approved;
    }

    private void RememberPatchDecision(string signature, bool approved)
    {
      if (string.IsNullOrWhiteSpace(signature))
        return;
      _rememberedPatchApprovals[signature] = approved;
    }

    private void EnqueueApprovalRequest(ApprovalRequest request)
    {
      if (request == null)
        return;
      _approvalQueue.Enqueue(request);
      if (_activeApproval == null)
        ThreadHelper.JoinableTaskFactory.RunAsync(DisplayNextApprovalAsync);
    }

    private async Task DisplayNextApprovalAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      if (_activeApproval != null)
        return;
      if (_approvalQueue.Count == 0)
      {
        ShowApprovalBanner(null);
        return;
      }

      _activeApproval = _approvalQueue.Dequeue();
      ShowApprovalBanner(_activeApproval);
    }

    private void ShowApprovalBanner(ApprovalRequest request)
    {
      if (this.FindName("ApprovalPromptBanner") is not Border banner ||
          this.FindName("ApprovalPromptText") is not TextBlock text ||
          this.FindName("ApprovalRememberCheckBox") is not CheckBox remember ||
          this.FindName("ApprovalApproveButton") is not Button approve ||
          this.FindName("ApprovalDenyButton") is not Button deny)
      {
        return;
      }

      if (request == null)
      {
        banner.Visibility = Visibility.Collapsed;
        remember.Visibility = Visibility.Collapsed;
        remember.IsChecked = false;
        approve.IsEnabled = false;
        deny.IsEnabled = false;
        return;
      }

      banner.Visibility = Visibility.Visible;
      text.Text = request.Message;
      remember.Visibility = request.CanRemember ? Visibility.Visible : Visibility.Collapsed;
      remember.IsChecked = false;
      approve.IsEnabled = true;
      deny.IsEnabled = true;
    }

    private async Task ResolveActiveApprovalAsync(bool approved)
    {
      ApprovalRequest request;
      bool rememberChecked = false;

      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      request = _activeApproval;
      if (request == null)
        return;

      if (this.FindName("ApprovalRememberCheckBox") is CheckBox rememberCheck)
        rememberChecked = request.CanRemember && rememberCheck.IsChecked == true;

      _activeApproval = null;
      ShowApprovalBanner(null);

      if (rememberChecked)
      {
        if (request.Kind == ApprovalKind.Exec)
          RememberExecDecision(request.Signature, approved);
        else
          RememberPatchDecision(request.Signature, approved);
      }

      var host = _host;
      if (host != null)
      {
        if (request.Kind == ApprovalKind.Exec)
        {
          await host.SendAsync(CreateExecApprovalSubmission(request.CallId, approved));
          if (!approved)
            await LogManualApprovalAsync("exec", request.Signature, approved);
        }
        else
        {
          await host.SendAsync(CreatePatchApprovalSubmission(request.CallId, approved));
          if (!approved)
            await LogManualApprovalAsync("patch", request.Signature, approved);
        }
      }

      await DisplayNextApprovalAsync();
    }

    private void ClearApprovalState(bool hideBanner = true)
    {
      _approvalQueue.Clear();
      _activeApproval = null;

      if (!hideBanner)
        return;

      ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
      {
        try
        {
          await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
          ShowApprovalBanner(null);
        }
        catch
        {
          // ignore if control already disposed
        }
      });
    }

    private bool TryResolveExecApproval(CodexOptions.ApprovalMode mode, string signature, out bool approved, out string reason)
    {
      approved = false;
      reason = string.Empty;

      if (mode == CodexOptions.ApprovalMode.Agent || mode == CodexOptions.ApprovalMode.AgentFullAccess)
      {
        approved = true;
        reason = mode == CodexOptions.ApprovalMode.Agent ? "Agent mode" : "Agent full access";
        if (!string.IsNullOrWhiteSpace(signature))
          _rememberedExecApprovals[signature] = approved;
        return true;
      }

      if (!string.IsNullOrWhiteSpace(signature) && _rememberedExecApprovals.TryGetValue(signature, out approved))
      {
        reason = "remembered";
        return true;
      }

      return false;
    }

    private bool TryResolvePatchApproval(CodexOptions.ApprovalMode mode, string signature, out bool approved, out string reason)
    {
      approved = false;
      reason = string.Empty;

      if (mode == CodexOptions.ApprovalMode.AgentFullAccess)
      {
        approved = true;
        reason = "Agent full access";
        if (!string.IsNullOrWhiteSpace(signature))
          _rememberedPatchApprovals[signature] = approved;
        return true;
      }

      if (!string.IsNullOrWhiteSpace(signature) && _rememberedPatchApprovals.TryGetValue(signature, out approved))
      {
        reason = "remembered";
        return true;
      }

      return false;
    }

    private static async Task LogAutoApprovalAsync(string kind, string signature, bool approved, string reason)
    {
      try
      {
        var pane = await DiagnosticsPane.GetAsync();
        var decision = approved ? "approved" : "denied";
        var signaturePart = string.IsNullOrWhiteSpace(signature) ? string.Empty : $" [{signature}]";
        var reasonPart = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" via {reason}";
        await pane.WriteLineAsync($"[info] Auto-{decision} {kind}{signaturePart}{reasonPart}");
      }
      catch
      {
        // diagnostics best effort
      }
    }

    private static async Task LogManualApprovalAsync(string kind, string signature, bool approved)
    {
      try
      {
        var pane = await DiagnosticsPane.GetAsync();
        var decision = approved ? "approved" : "denied";
        var signaturePart = string.IsNullOrWhiteSpace(signature) ? string.Empty : $" [{signature}]";
        await pane.WriteLineAsync($"[info] Manual-{decision} {kind}{signaturePart}");
      }
      catch
      {
        // diagnostics best effort
      }
    }

    private static string BuildPatchSignature(JObject raw)
    {
      if (raw == null)
        return string.Empty;

      var summary = TryGetString(raw, "summary");
      if (!string.IsNullOrWhiteSpace(summary))
        return summary;

      if (raw["files"] is JArray files && files.Count > 0)
      {
        var names = files
          .Select(token => TrimQuotes(token?.ToString() ?? string.Empty))
          .Where(s => !string.IsNullOrWhiteSpace(s));
        var joined = string.Join("|", names);
        if (!string.IsNullOrWhiteSpace(joined))
          return joined;
      }

      return TryGetString(raw, "call_id") ?? string.Empty;
    }

    private async Task UpdateFullAccessBannerAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      var banner = this.FindName("FullAccessBanner") as Border;
      var text = this.FindName("FullAccessText") as TextBlock;
      if (banner == null || text == null)
        return;

      var mode = _options?.Mode ?? CodexOptions.ApprovalMode.Chat;
      var show = mode == CodexOptions.ApprovalMode.AgentFullAccess;
      banner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
      if (show)
      {
        text.Text = "Full Access mode is enabled. Codex may auto-approve exec commands and patches.";
      }
    }

    private void EnqueueFullAccessBannerRefresh()
    {
      ThreadHelper.JoinableTaskFactory.RunAsync(UpdateFullAccessBannerAsync);
    }

    private static string NormalizeCwd(string cwd)
    {
      if (string.IsNullOrWhiteSpace(cwd))
        return string.Empty;

      var normalized = cwd.Trim();
      if (normalized.EndsWith("/.", StringComparison.Ordinal) || normalized.EndsWith("\\.", StringComparison.Ordinal))
      {
        normalized = normalized.Length > 2
          ? normalized.Substring(0, normalized.Length - 2)
          : normalized.Substring(0, normalized.Length - 1);
      }

      return normalized;
    }

    internal readonly struct EnvironmentSnapshot
    {
      public static readonly EnvironmentSnapshot Empty = new(string.Empty, string.Empty);

      public EnvironmentSnapshot(string solutionRoot, string workspaceRoot)
      {
        SolutionRoot = string.IsNullOrWhiteSpace(solutionRoot) ? string.Empty : solutionRoot;
        WorkspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot) ? string.Empty : workspaceRoot;
      }

      public string SolutionRoot { get; }
      public string WorkspaceRoot { get; }
    }

    private void RemoveExecIdMappings(string canonicalId)
    {
      if (string.IsNullOrEmpty(canonicalId))
        return;

      var keysToRemove = _execIdRemap
        .Where(kvp => string.Equals(kvp.Value, canonicalId, StringComparison.Ordinal))
        .Select(kvp => kvp.Key)
        .ToList();

      foreach (var key in keysToRemove)
        _execIdRemap.Remove(key);
    }

    private static void AppendExecText(ExecTurn turn, string text)
    {
      if (turn == null || string.IsNullOrEmpty(text))
        return;

      turn.Buffer.Append(text);
      if (!text.EndsWith("\n", StringComparison.Ordinal))
        turn.Buffer.Append('\n');
      turn.Body.Text = turn.Buffer.ToString();
    }

    private AssistantTurn GetOrCreateAssistantTurn(string id)
    {
      if (_assistantTurns.TryGetValue(id, out var turn))
        return turn;

      var bubble = CreateAssistantBubble();
      turn = new AssistantTurn(bubble);
      AttachCopyContextMenu(bubble);
      _assistantTurns[id] = turn;
      return turn;
    }

    private TextBlock CreateAssistantBubble()
      => CreateChatBubble("assistant");

    private void AppendAssistantText(AssistantTurn turn, string delta, bool isFinal = false, bool decorate = true)
      {
        if (turn == null || string.IsNullOrEmpty(delta))
          return;

        if (turn.Buffer.Length > 0 && !turn.Buffer.ToString().EndsWith("\n", StringComparison.Ordinal))
          turn.Buffer.AppendLine();

        turn.Buffer.Append(delta);
        var cleaned = ChatTextUtilities.NormalizeAssistantText(turn.Buffer.ToString());
        turn.Bubble.Text = cleaned;

        if (!decorate)
          return;

        _assistantChunkCounter++;
        if (!isFinal && _assistantChunkCounter % 5 == 0)
        {
          turn.Bubble.Text += "\n";
        }
      }

    private void AttachCopyContextMenu(TextBlock bubble)
    {
      if (bubble == null)
        return;

      var menu = new ContextMenu();
      var item = new MenuItem
      {
        Header = "Copy Message",
        Tag = bubble
      };
      item.Click += OnCopyMessageMenuItemClick;
      menu.Items.Add(item);
      bubble.ContextMenu = menu;
    }

    private void UpdateStreamingIndicator(bool streaming)
    {
      if (this.FindName("StreamingIndicator") is not ProgressBar indicator)
        return;
      indicator.Visibility = streaming ? Visibility.Visible : Visibility.Collapsed;
      if (!streaming)
        indicator.BeginAnimation(ProgressBar.OpacityProperty, null);
    }

    private void FocusInputBox()
    {
      Dispatcher.BeginInvoke(new Action(() =>
      {
        if (this.FindName("InputBox") is TextBox input)
        {
          if (!input.IsKeyboardFocusWithin)
            input.Focus();
        }
      }), DispatcherPriority.Input);
    }

    private async void OnCopyMessageMenuItemClick(object sender, RoutedEventArgs e)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (sender is MenuItem item && item.Tag is TextBlock bubble)
        {
          var text = bubble.Text ?? string.Empty;
          if (string.IsNullOrWhiteSpace(text))
            return;
          Clipboard.SetText(text);
          await VS.StatusBar.ShowMessageAsync("Message copied to clipboard.");
          AnimateBubbleFeedback(bubble);
        }
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] Copy message failed: {ex.Message}");
      }
    }

    private TextBlock CreateChatBubble(string role, string initialText = "")
    {
      if (this.FindName("Transcript") is not StackPanel transcript)
        throw new InvalidOperationException("Transcript panel missing");

      var margin = role switch
      {
        "assistant" => new Thickness(60, 4, 0, 4),
        _ => new Thickness(0, 4, 60, 4)
      };
      var alignment = role switch
      {
        "assistant" => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Left
      };

      var container = new Border
      {
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(10),
        Margin = margin,
        BorderThickness = new Thickness(1),
        MaxWidth = 520,
        HorizontalAlignment = alignment
      };
      container.SetResourceReference(Border.BorderBrushProperty, VsBrushes.ToolWindowBorderKey);
      ApplyChatBubbleBrush(container, role);

      var bubble = new TextBlock
      {
        Text = initialText,
        Tag = role,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Left,
        Visibility = Visibility.Visible
      };
      bubble.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);
      AttachCopyContextMenu(bubble);

      container.Child = bubble;
      transcript.Children.Add(container);
      return bubble;
    }

    private ExecTurn CreateExecTurn(string headerText, string normalizedCommand)
    {
      if (this.FindName("Transcript") is not StackPanel transcript)
        throw new InvalidOperationException("Transcript panel missing");

      var container = new Border
      {
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(10),
        Margin = new Thickness(20, 4, 80, 4),
        BorderThickness = new Thickness(1),
        MaxWidth = 600,
        HorizontalAlignment = HorizontalAlignment.Left
      };
      ApplyExecBubbleBrush(container);

      var panel = new StackPanel();
      var headerTextValue = string.IsNullOrWhiteSpace(headerText) ? "$ exec" : headerText.Trim();
      TextBlock headerBlock = null;

      if (!string.IsNullOrEmpty(headerTextValue))
      {
        headerBlock = new TextBlock
        {
          Text = headerTextValue,
          FontWeight = FontWeights.SemiBold,
          TextWrapping = TextWrapping.Wrap,
          Margin = new Thickness(0, 0, 0, 4)
        };
        headerBlock.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);
        panel.Children.Add(headerBlock);
      }

      var bodyBlock = new TextBlock
      {
        Text = string.Empty,
        Tag = "exec",
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Consolas"),
        Visibility = Visibility.Visible
      };
      bodyBlock.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);
      AttachCopyContextMenu(bodyBlock);

      panel.Children.Add(bodyBlock);
      container.Child = panel;
      transcript.Children.Add(container);

      return new ExecTurn(container, bodyBlock, headerBlock, normalizedCommand);
    }

    private ExecTurn GetOrCreateExecTurn(string id, string header, string normalizedCommand)
    {
      if (string.IsNullOrEmpty(id))
        id = RegisterExecFallbackId();

      if (!_execTurns.TryGetValue(id, out var turn))
      {
        turn = CreateExecTurn(header, normalizedCommand);
        _execTurns[id] = turn;
      }
      else if (!string.IsNullOrEmpty(header) && turn.Header != null)
      {
        if (string.IsNullOrEmpty(turn.Header.Text) || string.Equals(turn.Header.Text, "$ exec", StringComparison.Ordinal))
          turn.Header.Text = header;
      }

      if (!string.IsNullOrEmpty(normalizedCommand) && string.IsNullOrEmpty(turn.NormalizedCommand))
        turn.NormalizedCommand = normalizedCommand;

      return turn;
    }

    private void ApplyExecBubbleBrush(Border container)
    {
      container.SetResourceReference(Border.BorderBrushProperty, VsBrushes.ToolWindowBorderKey);
      const double opacity = 0.65;
      if (TryFindResource(VsBrushes.CommandBarGradientBeginKey) is SolidColorBrush brush)
      {
        var clone = brush.Clone();
        clone.Opacity = opacity;
        container.Background = clone;
      }
      else
      {
        container.Background = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 255, 255, 255));
      }
    }

    private void ApplyChatBubbleBrush(Border container, string role)
    {
      var key = role == "assistant" ? VsBrushes.CommandBarGradientEndKey : VsBrushes.CommandBarGradientBeginKey;
      var opacity = role == "assistant" ? 0.88 : 0.72;

      if (TryFindResource(key) is SolidColorBrush brush)
      {
        var clone = brush.Clone();
        clone.Opacity = opacity;
        container.Background = clone;
      }
      else
      {
        container.SetResourceReference(Border.BackgroundProperty, key);
        container.Opacity = opacity;
      }
    }

    private static string ExtractDeltaText(EventMsg evt)
    {
      var direct = TryGetString(evt.Raw, "text_delta");
      if (!string.IsNullOrEmpty(direct))
        return ChatTextUtilities.StripAnsi(direct);

      var text = CollectText(evt.Raw?["delta"] ?? evt.Raw?["message"]);
      if (!string.IsNullOrEmpty(text))
        return ChatTextUtilities.StripAnsi(text);

      text = CollectText(evt.Raw);
      return ChatTextUtilities.StripAnsi(text);
    }

    private static string ExtractFinalText(EventMsg evt)
    {
      var direct = TryGetString(evt.Raw, "text");
      if (!string.IsNullOrEmpty(direct))
        return ChatTextUtilities.StripAnsi(direct);

      var text = CollectText(evt.Raw?["message"]);
      if (!string.IsNullOrEmpty(text))
        return ChatTextUtilities.StripAnsi(text);

      text = CollectText(evt.Raw);
      return ChatTextUtilities.StripAnsi(text);
    }

    private static string ExtractStreamErrorMessage(EventMsg evt)
    {
      var obj = evt.Raw;
      string message = TryGetString(obj, "message")
        ?? TryGetString(obj, "error")
        ?? TryGetString(obj, "detail")
        ?? TryGetString(obj, "description");

      if (string.IsNullOrWhiteSpace(message) && obj?["error"] is JObject errorObj)
      {
        message = TryGetString(errorObj, "message") ?? TryGetString(errorObj, "detail");
      }

      return string.IsNullOrWhiteSpace(message) ? "Stream error" : message.Trim();
    }

    private static (int? total, int? input, int? output) ExtractTokenCounts(EventMsg evt)
    {
      var obj = evt.Raw ?? new JObject();
      var total = ResolveTokenValue(obj, "total", "total_tokens");
      var input = ResolveTokenValue(obj, "input", "prompt", "input_tokens");
      var output = ResolveTokenValue(obj, "output", "completion", "output_tokens");
      return (total, input, output);
    }

    private static int? ResolveTokenValue(JObject source, params string[] names)
    {
      if (source == null)
        return null;

      foreach (var name in names)
      {
        if (TryReadInt(source[name], out var value))
          return value;
      }

      foreach (var container in new[] { "counts", "usage", "token_counts" })
      {
        if (source[container] is JObject nested)
        {
          foreach (var name in names)
          {
            if (TryReadInt(nested[name], out var value))
              return value;
          }
        }
      }

      return null;
    }

    private static bool TryReadInt(JToken token, out int value)
    {
      value = 0;
      if (token == null)
        return false;
      if (token.Type == JTokenType.Integer)
      {
        value = token.Value<int>();
        return true;
      }
      if (token.Type == JTokenType.Float)
      {
        value = (int)Math.Round(token.Value<double>());
        return true;
      }
      var text = token.ToString();
      return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private void UpdateTokenUsage(int? total, int? input, int? output)
    {
      if (this.FindName("TokenUsageText") is not TextBlock block)
        return;

      var builder = new StringBuilder();
      if (total.HasValue)
        builder.Append($"total {total.Value}");

      var ioParts = new List<string>();
      if (input.HasValue)
        ioParts.Add($"in {input.Value}");
      if (output.HasValue)
        ioParts.Add($"out {output.Value}");

      if (ioParts.Count > 0)
      {
        if (builder.Length > 0)
          builder.Append(' ');
        builder.Append('(');
        builder.Append(string.Join(", ", ioParts));
        builder.Append(')');
      }

      if (builder.Length == 0)
      {
        block.Text = string.Empty;
        block.Visibility = Visibility.Collapsed;
        return;
      }

      block.Text = $"Tokens: {builder}";
      block.Visibility = Visibility.Visible;
    }

    private void ClearTokenUsage()
    {
      if (this.FindName("TokenUsageText") is not TextBlock block)
        return;
      block.Text = string.Empty;
      block.Visibility = Visibility.Collapsed;
    }

    private void UpdateTelemetryUi()
    {
      if (this.FindName("TelemetryText") is not TextBlock block)
        return;

      var summary = _telemetry.GetSummary();
      if (string.IsNullOrEmpty(summary))
      {
        block.Text = string.Empty;
        block.Visibility = Visibility.Collapsed;
      }
      else
      {
        block.Text = summary;
        block.Visibility = Visibility.Visible;
      }
    }

    private void ShowStreamErrorBanner(string message, bool canRetry)
    {
      if (this.FindName("StreamErrorText") is TextBlock text)
        text.Text = string.IsNullOrWhiteSpace(message) ? "Stream error" : message;
      if (this.FindName("StreamRetryButton") is Button retry)
        retry.IsEnabled = canRetry;
      if (this.FindName("StreamErrorBanner") is Border banner)
        banner.Visibility = Visibility.Visible;
    }

    private void HideStreamErrorBanner()
    {
      if (this.FindName("StreamErrorBanner") is Border banner)
        banner.Visibility = Visibility.Collapsed;
    }

    public void AppendSelectionToInput(string text)
    {
      if (string.IsNullOrWhiteSpace(text)) return;
      var box = this.FindName("InputBox") as TextBox;
      if (box != null)
      {
        if (!string.IsNullOrEmpty(box.Text))
          box.Text += "\n";
        box.Text += text;
        box.Focus();
        box.CaretIndex = box.Text.Length;
      }
    }

    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key != Key.Enter)
        return;

      var modifiers = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows);
      if (modifiers != ModifierKeys.None)
        return;

      e.Handled = true;
      var box = sender as TextBox;
      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await SendUserInputAsync(box?.Text, fromRetry: false));
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
      try
      {
        var box = this.FindName("InputBox") as TextBox;
        await SendUserInputAsync(box?.Text, fromRetry: false);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] OnSendClick failed: {ex.Message}");
      }
    }

    private async Task SendUserInputAsync(string text, bool fromRetry)
    {
      var payloadText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
      if (string.IsNullOrEmpty(payloadText))
        return;

      await EnsureWorkingDirectoryUpToDateAsync(fromRetry ? "stream-retry" : "send-user-input");

      var host = _host;
      if (host == null)
        return;

      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      ClearTokenUsage();
      HideStreamErrorBanner();

      if (!fromRetry)
      {
        _ = CreateChatBubble("user", payloadText);
      }

      var btn = this.FindName("SendButton") as Button;
      var status = this.FindName("StatusText") as TextBlock;

      var json = ChatTextUtilities.CreateUserInputSubmission(payloadText);
      var pane = await DiagnosticsPane.GetAsync();
      await pane.WriteLineAsync($"[debug] submission {json}");

      var ok = await host.SendAsync(json);

      if (!ok)
      {
        if (btn != null) btn.IsEnabled = true;
        if (status != null) status.Text = "Send failed";
        _telemetry.CancelTurn();
        UpdateTelemetryUi();
        UpdateStreamingIndicator(false);
        return;
      }

      if (btn != null) btn.IsEnabled = false;
      if (status != null) status.Text = "Streaming...";
      UpdateStreamingIndicator(true);
      _telemetry.BeginTurn();
      UpdateTelemetryUi();
      if (!fromRetry)
        _lastUserInput = payloadText;

      if (!fromRetry && this.FindName("InputBox") is TextBox input)
        input.Clear();
    }

    private void OnStreamRetryClick(object sender, RoutedEventArgs e)
    {
      var text = _lastUserInput;
      if (string.IsNullOrEmpty(text))
        return;
      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await SendUserInputAsync(text, fromRetry: true));
    }

    private void OnStreamDismissClick(object sender, RoutedEventArgs e)
    {
      HideStreamErrorBanner();
    }

    private static string CreateUserInputSubmission(string text)
    {
      var submission = new JObject
      {
        ["id"] = Guid.NewGuid().ToString(),
        ["op"] = new JObject
        {
          ["type"] = "user_input",
          ["items"] = new JArray
          {
            new JObject
            {
              ["type"] = "text",
              ["text"] = text
            }
          }
        }
      };
      return submission.ToString(Formatting.None);
    }

    private static string CreateExecApprovalSubmission(string requestId, bool approved)
    {
      var decision = approved ? "approved" : "denied";
      var callId = requestId ?? string.Empty;
      var submissionId = !string.IsNullOrEmpty(callId)
        ? $"{callId}:exec_{Interlocked.Increment(ref _approvalCounter)}"
        : Guid.NewGuid().ToString();

      var submission = new JObject
      {
        ["id"] = submissionId,
        ["op"] = new JObject
        {
          ["type"] = "exec_approval",
          ["id"] = callId,
          ["call_id"] = callId,
          ["decision"] = decision,
          ["approved"] = approved
        }
      };
      return submission.ToString(Formatting.None);
    }
    private static string CreatePatchApprovalSubmission(string requestId, bool approved)
    {
      var decision = approved ? "approved" : "denied";
      var callId = requestId ?? string.Empty;
      var submissionId = !string.IsNullOrEmpty(callId)
        ? $"{callId}:patch_{Interlocked.Increment(ref _approvalCounter)}"
        : Guid.NewGuid().ToString();

      var submission = new JObject
      {
        ["id"] = submissionId,
        ["op"] = new JObject
        {
          ["type"] = "patch_approval",
          ["id"] = callId,
          ["call_id"] = callId,
          ["decision"] = decision,
          ["approved"] = approved
        }
      };
      return submission.ToString(Formatting.None);
    }
    private static async Task LogAssistantTextAsync(string text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return;

      try
      {
        var pane = await DiagnosticsPane.GetAsync();
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        await pane.WriteLineAsync($"[assistant] {timestamp} {text}");
      }
      catch
      {
        // best effort logging
      }
    }

    private static async Task WriteExecDiagnosticsAsync(string text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return;

      try
      {
        var pane = await DiagnosticsPane.GetAsync();
        using var reader = new StringReader(text);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
          if (string.IsNullOrWhiteSpace(line))
            continue;
          await pane.WriteLineAsync($"[exec] {line.TrimEnd()}");
        }
      }
      catch
      {
        // diagnostics logging is best effort
      }
    }

    private static string CollectText(JToken token)
    {
      if (token == null)
        return string.Empty;

      return token.Type switch
      {
        JTokenType.String => token.ToString(),
        JTokenType.Object => CollectTextFromObject((JObject)token),
        JTokenType.Array => string.Concat(token.Children().Select(CollectText)),
        _ => string.Empty
      };
    }

    private static string CollectTextFromObject(JObject obj)
    {
      if (obj == null)
        return string.Empty;

      if (obj["text"] is JToken textToken && textToken.Type == JTokenType.String)
        return textToken.ToString();

      if (obj["value"] is JToken valueToken && valueToken.Type == JTokenType.String)
        return valueToken.ToString();

      foreach (var key in new[] { "content", "message", "delta", "data" })
      {
        var child = obj[key];
        var text = CollectText(child);
        if (!string.IsNullOrEmpty(text))
          return text;
      }

      return string.Empty;
    }

    private static readonly Regex Base64Regex = new(@"^[A-Za-z0-9+/=\r\n]+$", RegexOptions.Compiled);

    private static string NormalizeExecChunk(string value)
    {
      if (string.IsNullOrEmpty(value))
        return string.Empty;

      var normalized = value.Replace("\r\n", "\n");
      if (TryDecodeBase64Chunk(normalized, out var decoded))
        return decoded;

      return normalized;
    }

    private static bool TryDecodeBase64Chunk(string value, out string decoded)
    {
      decoded = string.Empty;
      if (string.IsNullOrWhiteSpace(value))
        return false;

      var trimmed = value.Trim();
      if (trimmed.Length < 8 || trimmed.Length % 4 != 0)
        return false;
      if (!Base64Regex.IsMatch(trimmed))
        return false;

      try
      {
        var bytes = Convert.FromBase64String(trimmed);
        if (bytes.Length == 0)
          return false;
        decoded = Encoding.UTF8.GetString(bytes);
        if (string.IsNullOrEmpty(decoded))
          return false;
        return true;
      }
      catch
      {
        return false;
      }
    }

  }
}
