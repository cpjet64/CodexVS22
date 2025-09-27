using System;



using System.Collections;



using System.Collections.Generic;



using System.Collections.ObjectModel;



using System.ComponentModel;



using System.Globalization;



using System.IO;



using System.Linq;



using System.Reflection;



using System.Runtime.InteropServices;



using System.Text;



using System.Threading;



using System.Threading.Tasks;



using System.Windows;



using System.Windows.Automation;



using System.Windows.Controls;



using System.Windows.Controls.Primitives;



using System.Windows.Documents;



using System.Windows.Input;



using System.Windows.Media;



using System.Windows.Media.Animation;



using Community.VisualStudio.Toolkit;



using EnvDTE;



using EnvDTE80;



using CodexVS22.Core;



using CodexVS22.Core.Protocol;



// Bring nested DiffUtilities types into scope explicitly.



using CodexVS22.Core;



using Microsoft.VisualStudio;



using Microsoft.VisualStudio.Shell;



using Microsoft.VisualStudio.Shell.Interop;



using Microsoft.VisualStudio.Threading;



using Newtonsoft.Json;



using Newtonsoft.Json.Linq;



using System.Text.RegularExpressions;



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



    private readonly List<ExecTurn> _execConsoleTurns = new();



    private readonly ObservableCollection<McpToolInfo> _mcpTools = new();



    private readonly ObservableCollection<McpToolRun> _mcpToolRuns = new();



    private readonly ObservableCollection<CustomPromptInfo> _customPrompts = new();



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



    private double _execConsolePreferredHeight = 180.0;



    private bool _suppressExecToggleEvent;



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



    private readonly Dictionary<string, McpToolRun> _mcpToolRunIndex = new(StringComparer.Ordinal);



    private readonly Dictionary<string, CustomPromptInfo> _customPromptIndex = new(StringComparer.Ordinal);



    private const int MaxMcpToolRuns = 20;



    private static int _environmentReadyInitialized;



    private DateTime _lastMcpToolsRefresh = DateTime.MinValue;



    private DateTime _lastPromptsRefresh = DateTime.MinValue;



    private const int RefreshDebounceSeconds = 2;







    private readonly object _heartbeatLock = new();



    private Timer _heartbeatTimer;



    private HeartbeatState _heartbeatState;



    private int _heartbeatSending;



    private bool _initializingSelectors;



    private string _selectedModel = DefaultModelName;



    private string _selectedReasoning = DefaultReasoningValue;



    private CodexOptions.ApprovalMode _selectedApprovalMode = CodexOptions.ApprovalMode.Chat;



    private System.Windows.Window _hostWindow;



    private bool _windowEventsHooked;



    private ObservableCollection<DiffTreeItem> _diffTreeRoots = new();



    private Dictionary<string, DiffDocument> _diffDocuments = new(StringComparer.OrdinalIgnoreCase);



    private bool _suppressDiffSelectionUpdate;



    private int _diffTotalLeafCount;



    private bool _patchApplyInProgress;



    private DateTime? _patchApplyStartedAt;



    private int _patchApplyExpectedFiles;



    private string _lastPatchCallId = string.Empty;



    private string _lastPatchSignature = string.Empty;



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







    private static readonly CodexOptions.ApprovalMode[] ApprovalModeOptions =



    {



      CodexOptions.ApprovalMode.Chat,



      CodexOptions.ApprovalMode.Agent,



      CodexOptions.ApprovalMode.AgentFullAccess



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



































































    public MyToolWindowControl()



    {



      InitializeComponent();



    }







    public static MyToolWindowControl Current { get; private set; }







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



        _lastPatchCallId = callId;



        _lastPatchSignature = signature ?? string.Empty;



        if (TryResolvePatchApproval(options.Mode, signature, out var autoApproved, out var autoReason))



        {



          await host.SendAsync(ApprovalSubmissionFactory.CreatePatch(callId, autoApproved));



          if (autoApproved)



            await ApplySelectedDiffsAsync();



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







    private async void HandlePatchApplyBegin(EventMsg evt)



    {



      try



      {



        var raw = evt.Raw ?? new JObject();



        var summary = TryGetString(raw, "summary") ?? "Applying Codex patch...";



        var total = TryGetInt(raw, "total", "count", "files") ?? 0;



        await BeginPatchApplyProgressAsync(summary, total);



      }



      catch (Exception ex)



      {



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[error] HandlePatchApplyBegin failed: {ex.Message}");



      }



    }







    private async void HandlePatchApplyEnd(EventMsg evt)



    {



      try



      {



        var raw = evt.Raw ?? new JObject();



        var success = TryGetBoolean(raw, "success", "ok", "completed") ?? true;



        var applied = TryGetInt(raw, "applied", "succeeded", "files_applied") ?? 0;



        var failed = TryGetInt(raw, "failed", "errors", "files_failed") ?? 0;



        var message = TryGetString(raw, "message") ?? TryGetString(raw, "description");



        await CompletePatchApplyProgressAsync(success, applied, failed, message);



      }



      catch (Exception ex)



      {



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[error] HandlePatchApplyEnd failed: {ex.Message}");



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



          _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await RequestMcpToolsAsync("session-configured"));



          _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await RequestCustomPromptsAsync("session-configured"));



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



        case EventKind.ListMcpTools:



          HandleListMcpTools(evt);



          break;



        case EventKind.ListCustomPrompts:



          HandleListCustomPrompts(evt);



          break;



        case EventKind.ToolCallBegin:



          HandleToolCallBegin(evt);



          break;



        case EventKind.ToolCallOutput:



          HandleToolCallOutput(evt);



          break;



        case EventKind.ToolCallEnd:



          HandleToolCallEnd(evt);



          break;



        case EventKind.TurnDiff:



          HandleTurnDiff(evt);



          break;



        case EventKind.PatchApplyBegin:



          HandlePatchApplyBegin(evt);



          break;



        case EventKind.PatchApplyEnd:



          HandlePatchApplyEnd(evt);



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



        ResetTranscript();



        if (this.FindName("InputBox") is TextBox box) box.Clear();



        _assistantTurns.Clear();



        _execTurns.Clear();



        _execConsoleTurns.Clear();



        _execCommandIndex.Clear();



        _execIdRemap.Clear();



        _rememberedExecApprovals.Clear();



        _rememberedPatchApprovals.Clear();



        _mcpToolRuns.Clear();



        _mcpToolRunIndex.Clear();



        UpdateMcpToolRunsUi();



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



        ScrollTranscriptToEnd();



      }



    }







    public sealed class DiffTreeItem : INotifyPropertyChanged



    {



      private readonly Action<DiffTreeItem> _onCheckChanged;



      private bool? _isChecked = true;



      private bool _isExpanded;



      private bool _isUpdating;







      public DiffTreeItem(string name, string relativePath, bool isDirectory, DiffDocument document, Action<DiffTreeItem> onCheckChanged)



      {



        Name = string.IsNullOrWhiteSpace(name) ? "(file)" : name;



        RelativePath = relativePath ?? string.Empty;



        IsDirectory = isDirectory;



        Document = document;



        _onCheckChanged = onCheckChanged;



        Children = new ObservableCollection<DiffTreeItem>();



        _isExpanded = isDirectory;



      }







      public event PropertyChangedEventHandler PropertyChanged;







      public string Name { get; }



      public string RelativePath { get; }



      public bool IsDirectory { get; }



      public DiffDocument Document { get; private set; }



      public ObservableCollection<DiffTreeItem> Children { get; }



      public DiffTreeItem Parent { get; private set; }







      public bool? IsChecked



      {



        get => _isChecked;



        set => SetIsChecked(value, updateChildren: true, updateParent: true);



      }







      public bool IsExpanded



      {



        get => _isExpanded;



        set



        {



          if (_isExpanded == value)



            return;



          _isExpanded = value;



          OnPropertyChanged(nameof(IsExpanded));



        }



      }







      internal void SetParent(DiffTreeItem parent)



      {



        Parent = parent;



      }







      internal void SetDocument(DiffDocument document)



      {



        if (document == null)



          return;



        Document = document;



      }







      internal void SetIsChecked(bool? value, bool updateChildren, bool updateParent)



      {



        if (_isUpdating)



          return;







        if (_isChecked == value)



        {



          if (updateChildren && value.HasValue && IsDirectory)



          {



            foreach (var child in Children)



              child.SetIsChecked(value, updateChildren: true, updateParent: false);



          }



          return;



        }







        _isUpdating = true;



        try



        {



          _isChecked = value;



          OnPropertyChanged(nameof(IsChecked));







          if (updateChildren && value.HasValue && IsDirectory)



          {



            foreach (var child in Children)



              child.SetIsChecked(value, updateChildren: true, updateParent: false);



          }







          if (updateParent && Parent != null)



            Parent.SynchronizeCheckStateFromChildren();



        }



        finally



        {



          _isUpdating = false;



        }







        _onCheckChanged?.Invoke(this);



      }







      internal void SynchronizeCheckStateFromChildren()



      {



        if (!IsDirectory || Children.Count == 0)



          return;







        var allChecked = true;



        var allUnchecked = true;







        foreach (var child in Children)



        {



          var state = child.IsChecked;



          if (state != true)



            allChecked = false;



          if (state != false)



            allUnchecked = false;



          if (!allChecked && !allUnchecked)



            break;



        }







        bool? newValue = allChecked ? true : allUnchecked ? false : (bool?)null;



        if (_isChecked != newValue)



        {



          _isChecked = newValue;



          OnPropertyChanged(nameof(IsChecked));



          _onCheckChanged?.Invoke(this);



        }







        Parent?.SynchronizeCheckStateFromChildren();



      }







      private void OnPropertyChanged(string propertyName)



        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));



    }







    private static (List<DiffTreeItem> roots, int leafCount) BuildDiffTree(



      IReadOnlyList<DiffDocument> docs,



      Action<DiffTreeItem> onCheckChanged)



    {



      var roots = new List<DiffTreeItem>();



      var map = new Dictionary<string, DiffTreeItem>(StringComparer.OrdinalIgnoreCase);



      var leaves = 0;







      foreach (var doc in docs)



      {



        var normalizedPath = NormalizeDiffPath(doc.Path);



        var segments = SplitDiffPath(normalizedPath);



        if (segments.Length == 0)



          segments = new[] { string.IsNullOrEmpty(normalizedPath) ? "codex.diff" : normalizedPath };







        DiffTreeItem parent = null;



        string key = string.Empty;







        for (var i = 0; i < segments.Length; i++)



        {



          var segment = segments[i];



          var isLast = i == segments.Length - 1;



          key = string.IsNullOrEmpty(key) ? segment : $"{key}/{segment}";







          if (!map.TryGetValue(key, out var node))



          {



            var relativePath = string.IsNullOrEmpty(normalizedPath) ? segment : key;



            var document = isLast ? doc : null;



            node = new DiffTreeItem(segment, relativePath, !isLast, document, onCheckChanged);



            if (parent == null)



              InsertDiffNodeInOrder(roots, node);



            else



              InsertDiffNodeInOrder(parent.Children, node);







            node.SetParent(parent);



            map[key] = node;



          }



          else if (isLast)



          {



            node.SetDocument(doc);



          }







          if (isLast)



            leaves++;







          parent = node;



        }



      }







      return (roots, leaves);



    }







    private static void InsertDiffNodeInOrder(IList<DiffTreeItem> collection, DiffTreeItem node)



    {



      var index = 0;



      while (index < collection.Count && CompareDiffNodes(collection[index], node) <= 0)



        index++;



      collection.Insert(index, node);



    }







    private static int CompareDiffNodes(DiffTreeItem left, DiffTreeItem right)



    {



      if (left == null && right == null)



        return 0;



      if (left == null)



        return 1;



      if (right == null)



        return -1;



      if (left.IsDirectory != right.IsDirectory)



        return left.IsDirectory ? -1 : 1;



      return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);



    }







    private static string NormalizeDiffPath(string path)



    {



      if (string.IsNullOrWhiteSpace(path))



        return "codex.diff";







      var normalized = path.Replace('\\', '/').Trim();



      while (normalized.StartsWith("./", StringComparison.Ordinal))



        normalized = normalized.Length > 2 ? normalized.Substring(2) : string.Empty;



      normalized = normalized.Trim('/');



      if (string.IsNullOrWhiteSpace(normalized))



        normalized = Path.GetFileName(path) ?? "codex.diff";



      return normalized;



    }







    private static string[] SplitDiffPath(string normalizedPath)



    {



      if (string.IsNullOrWhiteSpace(normalizedPath))



        return Array.Empty<string>();



      return normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);



    }







    private void HandleDiffSelectionChanged(DiffTreeItem _)



    {



      if (_suppressDiffSelectionUpdate)



        return;



      UpdateDiffSelectionSummary();



    }







    private void UpdateDiffSelectionSummary()



    {



      if (_suppressDiffSelectionUpdate)



        return;







      if (this.FindName("DiffSelectionSummary") is not TextBlock summary)



        return;







      if (_diffTreeRoots == null || _diffTreeRoots.Count == 0)



      {



        summary.Text = string.Empty;



        summary.Visibility = Visibility.Collapsed;



        return;



      }







      var (selected, total) = CountSelectedDiffFiles();



      if (total == 0)



      {



        summary.Text = string.Empty;



        summary.Visibility = Visibility.Collapsed;



        return;



      }







      summary.Text = $"Selected {selected} of {total} files.";



      summary.Visibility = Visibility.Visible;



    }







    private (int selected, int total) CountSelectedDiffFiles()



    {



      var selected = 0;



      var total = 0;







      foreach (var root in _diffTreeRoots)



        AccumulateDiffLeafCounts(root, ref selected, ref total);







      return (selected, total);



    }







    private static void AccumulateDiffLeafCounts(DiffTreeItem item, ref int selected, ref int total)



    {



      if (item == null)



        return;







      if (!item.IsDirectory)



      {



        total++;



        if (item.IsChecked == true)



          selected++;



        return;



      }







      foreach (var child in item.Children)



        AccumulateDiffLeafCounts(child, ref selected, ref total);



    }







    private IReadOnlyList<DiffDocument> GetSelectedDiffDocuments()



    {



      var results = new List<DiffDocument>();



      foreach (var root in _diffTreeRoots)



        CollectSelectedDocuments(root, results);







      if (results.Count == 0 && _diffDocuments.Count > 0)



        results.AddRange(_diffDocuments.Values);







      return results;



    }







    private static void CollectSelectedDocuments(DiffTreeItem item, ICollection<DiffDocument> results)



    {



      if (item == null)



        return;







      if (!item.IsDirectory)



      {



        if (item.IsChecked == true && item.Document != null)



          results.Add(item.Document);



        return;



      }







      foreach (var child in item.Children)



        CollectSelectedDocuments(child, results);



    }







    private async Task<bool> TryDiscardPendingPatchAsync()



    {



      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();







      if (_activeApproval?.Kind == ApprovalKind.Patch)



      {



        await ResolveActiveApprovalAsync(false);



        return true;



      }







      if (_approvalQueue.Count == 0)



        return false;







      var handled = false;



      var pending = new Queue<ApprovalRequest>();



      var host = _host;







      while (_approvalQueue.Count > 0)



      {



        var request = _approvalQueue.Dequeue();



        if (!handled && request.Kind == ApprovalKind.Patch)



        {



          if (host != null)



            await host.SendAsync(ApprovalSubmissionFactory.CreatePatch(request.CallId, false));



          await LogManualApprovalAsync("patch", request.Signature, false);



          handled = true;



          continue;



        }







        pending.Enqueue(request);



      }







      while (pending.Count > 0)



        _approvalQueue.Enqueue(pending.Dequeue());







      if (handled)



      {



        await DisplayNextApprovalAsync();



        _lastPatchCallId = string.Empty;



        _lastPatchSignature = string.Empty;



      }







      return handled;



    }







    private async Task DiscardPatchAsync()



    {



      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();







      if (_patchApplyInProgress)



      {



        await VS.StatusBar.ShowMessageAsync("Codex patch is currently applying. Please wait for completion.");



        return;



      }







      var handled = await TryDiscardPendingPatchAsync();







      if (!handled && !string.IsNullOrEmpty(_lastPatchCallId))



      {



        var host = _host;



        if (host != null)



        {



          await host.SendAsync(ApprovalSubmissionFactory.CreatePatch(_lastPatchCallId, false));



          await LogManualApprovalAsync("patch", _lastPatchSignature, false);



        }



        handled = true;



      }







      if (!handled && (_diffTreeRoots == null || _diffTreeRoots.Count == 0))



      {



        await VS.StatusBar.ShowMessageAsync("No Codex patch to discard.");



        return;



      }







      await CompletePatchApplyProgressAsync(false, 0, 0, handled ? "Codex patch discarded." : "Codex patch dismissed.", recordTelemetry: false);



      await UpdateDiffTreeAsync(Array.Empty<DiffDocument>());



      _lastPatchCallId = string.Empty;



      _lastPatchSignature = string.Empty;



      await VS.StatusBar.ShowMessageAsync("Codex patch discarded.");



    }







    private async void OnDiscardPatchClick(object sender, RoutedEventArgs e)



    {



      try



      {



        await DiscardPatchAsync();



      }



      catch (Exception ex)



      {



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[error] Discard patch failed: {ex.Message}");



      }



    }







    private async Task BeginPatchApplyProgressAsync(string summary, int totalFiles)



    {



      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();







      _patchApplyInProgress = true;



      _patchApplyStartedAt = DateTime.UtcNow;



      _patchApplyExpectedFiles = Math.Max(totalFiles, 0);



      _telemetry.BeginPatch();







      if (string.IsNullOrWhiteSpace(summary))



        summary = _patchApplyExpectedFiles > 0



          ? $"Applying Codex patch ({_patchApplyExpectedFiles} files)..."



          : "Applying Codex patch...";







      if (this.FindName("StatusText") is TextBlock status)



        status.Text = summary;







      if (this.FindName("DiscardPatchButton") is Button discardButton)



        discardButton.IsEnabled = false;







      await VS.StatusBar.ShowMessageAsync(summary);



      UpdateTelemetryUi();







      try



      {



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[patch] begin: {summary}");



      }



      catch



      {



        // diagnostics are best effort



      }



    }







    private async Task CompletePatchApplyProgressAsync(bool success, int applied, int failed, string messageOverride = null, bool recordTelemetry = true)



    {



      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();







      var elapsed = _patchApplyStartedAt.HasValue



        ? (DateTime.UtcNow - _patchApplyStartedAt.Value).TotalSeconds



        : (double?)null;







      _patchApplyInProgress = false;



      _patchApplyStartedAt = null;



      _patchApplyExpectedFiles = 0;



      _lastPatchCallId = string.Empty;



      _lastPatchSignature = string.Empty;







      if (applied < 0) applied = 0;



      if (failed < 0) failed = 0;







      string message = string.IsNullOrWhiteSpace(messageOverride) ? null : messageOverride.Trim();



      if (string.IsNullOrEmpty(message))



      {



        var duration = elapsed.HasValue ? $" in {elapsed.Value:F1}s" : string.Empty;



        if (success)



        {



          var filesPart = applied > 0 ? $"{applied} file{(applied == 1 ? string.Empty : "s")}" : "files";



          message = $"Codex patch applied ({filesPart}{duration}).";



        }



        else



        {



          var appliedPart = applied > 0 ? $"{applied} applied" : "0 applied";



          var failedPart = failed > 0 ? $", {failed} failed" : string.Empty;



          message = $"Codex patch failed ({appliedPart}{failedPart}{duration}).";



        }



      }







      if (this.FindName("StatusText") is TextBlock status)



        status.Text = message;







      if (this.FindName("DiscardPatchButton") is Button discardButton)



        discardButton.IsEnabled = true;







      await VS.StatusBar.ShowMessageAsync(message);







      if (recordTelemetry)



      {



        var duration = elapsed ?? 0.0;



        _telemetry.CompletePatch(success, duration);



      }



      else



      {



        _telemetry.CancelPatch();



      }



      UpdateTelemetryUi();







      try



      {



        var pane = await DiagnosticsPane.GetAsync();



        var state = success ? "success" : "failure";



        await pane.WriteLineAsync($"[patch] {state}: applied={applied}, failed={failed}{(elapsed.HasValue ? $", elapsed={elapsed.Value:F2}s" : string.Empty)}");



        if (!string.IsNullOrEmpty(messageOverride))



          await pane.WriteLineAsync($"[patch] detail: {messageOverride}");



      }



      catch



      {



        // diagnostics best effort



      }



    }







    private async Task<bool> ApplySelectedDiffsAsync()



    {



      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();







      var documents = GetSelectedDiffDocuments();



      if (documents == null || documents.Count == 0)



      {



        await VS.StatusBar.ShowMessageAsync("No files selected for Codex patch.");



        return false;



      }







      var applySummary = documents.Count == 1



        ? $"Applying Codex patch to {documents[0].Path}..."



        : $"Applying Codex patch ({documents.Count} files)...";







      await BeginPatchApplyProgressAsync(applySummary, documents.Count);







      var applied = 0;



      var failures = new List<string>();



      var conflicts = new List<string>();



      var openDocuments = _options?.AutoOpenPatchedFiles ?? true;







      foreach (var document in documents)



      {



        if (document == null || string.IsNullOrEmpty(document.Modified))



        {



          failures.Add(document?.Path ?? "(unknown)");



          continue;



        }







        var fullPath = ResolveDiffFullPath(document.Path);



        if (string.IsNullOrEmpty(fullPath))



        {



          failures.Add(document.Path);



          continue;



        }







        try



        {



          var result = await ApplyDocumentTextAsync(fullPath, document, openDocuments);



          switch (result)



          {



            case PatchApplyResult.Applied:



              applied++;



              break;



            case PatchApplyResult.Conflict:



              conflicts.Add(fullPath);



              break;



            default:



              failures.Add(fullPath);



              break;



          }



        }



        catch (Exception ex)



        {



          failures.Add($"{fullPath}: {ex.Message}");



        }



      }







      if (failures.Count > 0 || conflicts.Count > 0)



      {



        try



        {



          var pane = await DiagnosticsPane.GetAsync();



          foreach (var failure in failures)



            await pane.WriteLineAsync($"[error] Failed to apply Codex patch: {failure}");



          foreach (var conflict in conflicts)



            await pane.WriteLineAsync($"[warn] Patch conflict for {conflict}; file differs from expected base. Manual merge recommended.");



        }



        catch



        {



          // diagnostics best effort



        }



      }







      var success = failures.Count == 0 && conflicts.Count == 0;



      string messageOverride = null;



      if (!success)



      {



        if (conflicts.Count > 0 && failures.Count == 0)



        {



          messageOverride = conflicts.Count == 1



            ? "Codex patch encountered a conflict; please merge manually."



            : $"Codex patch encountered {conflicts.Count} conflicts; please merge manually.";



        }



        else if (conflicts.Count > 0)



        {



          messageOverride = $"Codex patch completed with {conflicts.Count} conflicts and {failures.Count} errors.";



        }



        else if (failures.Count > 0)



        {



          messageOverride = failures.Count == 1



            ? "Codex patch failed for 1 file."



            : $"Codex patch failed for {failures.Count} files.";



        }



      }







      await CompletePatchApplyProgressAsync(success, applied, failures.Count + conflicts.Count, messageOverride);







      return success;



    }







    private async Task<PatchApplyResult> ApplyDocumentTextAsync(string fullPath, DiffDocument document, bool openDocument)



    {



      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();







      if (string.IsNullOrWhiteSpace(fullPath))



        return PatchApplyResult.Failed;







      var normalizedPath = NormalizeDirectory(fullPath);



      var directory = Path.GetDirectoryName(normalizedPath);



      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))



        Directory.CreateDirectory(directory);







      if (!File.Exists(normalizedPath))



        File.WriteAllText(normalizedPath, string.Empty, Encoding.UTF8);







      if (IsFileReadOnly(normalizedPath))



      {



        await LogPatchReadOnlyAsync(normalizedPath);



        return PatchApplyResult.Failed;



      }







      DocumentView documentView = null;



      if (openDocument)



      {



        try



        {



          documentView = await VS.Documents.OpenAsync(normalizedPath);



        }



        catch



        {



          documentView = null;



        }



      }



      else



      {



        try



        {



          documentView = await VS.Documents.GetDocumentViewAsync(normalizedPath);



        }



        catch



        {



          documentView = null;



        }



      }







      var buffer = documentView?.TextBuffer;



      var newText = DiffUtilities.NormalizeFileContent(document?.Modified ?? string.Empty);







      var currentText = buffer != null



        ? buffer.CurrentSnapshot.GetText()



        : File.Exists(normalizedPath) ? File.ReadAllText(normalizedPath) : string.Empty;







      if (!string.IsNullOrEmpty(document?.Original))



      {



        var normalizedCurrent = DiffUtilities.NormalizeForComparison(currentText);



        var normalizedOriginal = DiffUtilities.NormalizeForComparison(document.Original);



        if (!string.Equals(normalizedCurrent, normalizedOriginal, StringComparison.Ordinal))



        {



          return PatchApplyResult.Conflict;



        }



      }











      if (buffer != null)



      {



        using var edit = buffer.CreateEdit();



        edit.Replace(0, buffer.CurrentSnapshot.Length, newText);



        var appliedSnapshot = edit.Apply();

        if (appliedSnapshot == null)

          return PatchApplyResult.Failed;



          return PatchApplyResult.Failed;







        if (!openDocument && documentView?.WindowFrame == null)



        {



          try



          {



            File.WriteAllText(normalizedPath, buffer.CurrentSnapshot.GetText(), Encoding.UTF8);



          }



          catch



          {



            // if writing fails, keep buffer dirty and continue



          }



        }



        return PatchApplyResult.Applied;



      }







      File.WriteAllText(normalizedPath, newText, Encoding.UTF8);







      if (openDocument)



      {



        try



        {



          await VS.Documents.OpenAsync(normalizedPath);



        }



        catch



        {



          // best effort



        }



      }







      return PatchApplyResult.Applied;



    }







    private string ResolveDiffFullPath(string path)



    {



      if (string.IsNullOrWhiteSpace(path))



        return string.Empty;







      if (Path.IsPathRooted(path))



        return NormalizeDirectory(path);







      var relative = ConvertDiffPathToPlatform(path);







      foreach (var baseDir in new[] { _workingDir, _lastKnownWorkspaceRoot, _lastKnownSolutionRoot })



      {



        var candidate = CombineWithBaseDirectory(baseDir, relative);



        if (!string.IsNullOrEmpty(candidate) && (File.Exists(candidate) || Directory.Exists(Path.GetDirectoryName(candidate) ?? string.Empty)))



          return candidate;



      }







      var fallback = CombineWithBaseDirectory(_workingDir, relative);



      return string.IsNullOrEmpty(fallback) ? NormalizeDirectory(relative) : fallback;



    }







    private static string CombineWithBaseDirectory(string baseDir, string relativePath)



    {



      if (string.IsNullOrWhiteSpace(relativePath))



        return string.Empty;



      if (string.IsNullOrWhiteSpace(baseDir))



        return NormalizeDirectory(relativePath);







      try



      {



        return NormalizeDirectory(Path.Combine(baseDir, relativePath));



      }



      catch



      {



        return NormalizeDirectory(relativePath);



      }



    }







    private static string ConvertDiffPathToPlatform(string path)



    {



      if (string.IsNullOrWhiteSpace(path))



        return string.Empty;



      var normalized = NormalizeDiffPath(path);



      return normalized.Replace('/', Path.DirectorySeparatorChar);



    }







    private static bool IsFileReadOnly(string path)



    {



      try



      {



        var info = new FileInfo(path);



        return info.Exists && info.IsReadOnly;



      }



      catch



      {



        return false;



      }



    }







    private static async Task LogPatchReadOnlyAsync(string path)



    {



      try



      {



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[warn] Skipping patch for {path}: file is read-only or locked.");



      }



      catch



      {



        // diagnostics best effort



      }



    }







    private async void OnExecCancelClick(object sender, RoutedEventArgs e)



    {



      if (sender is not Button button)



        return;







      var execId = button.Tag as string;



      if (string.IsNullOrEmpty(execId))



        return;







      button.IsEnabled = false;



      if (_execTurns.TryGetValue(execId, out var turn))



        turn.CancelRequested = true;







      if (this.FindName("StatusText") is TextBlock status)



        status.Text = "Cancelling exec...";







      var ok = await SendExecCancelAsync(execId);



      if (!ok)



      {



        if (_execTurns.TryGetValue(execId, out var retryTurn))



          retryTurn.CancelRequested = false;



        button.IsEnabled = true;



        if (this.FindName("StatusText") is TextBlock statusRetry)



          statusRetry.Text = "Exec cancel failed";



      }



      else



      {



        _telemetry.CancelExec(execId);



        UpdateTelemetryUi();



      }



    }







    private async void OnExecCopyAllClick(object sender, RoutedEventArgs e)



    {



      if (sender is not Button button || button.Tag is not ExecTurn turn)



        return;







      try



      {



        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();







        var text = ChatTextUtilities.StripAnsi(turn.Buffer.ToString()).TrimEnd('\n');



        if (string.IsNullOrWhiteSpace(text))



        {



          await VS.StatusBar.ShowMessageAsync("Exec output is empty.");



          return;



        }







        Clipboard.SetText(text);



        await VS.StatusBar.ShowMessageAsync("Exec output copied to clipboard.");



      }



      catch (Exception ex)



      {



        try



        {



          var pane = await DiagnosticsPane.GetAsync();



          await pane.WriteLineAsync($"[error] Exec copy failed: {ex.Message}");



        }



        catch



        {



          // diagnostics best effort



        }



      }



    }







    private async void OnExecClearClick(object sender, RoutedEventArgs e)



    {



      if (sender is not Button button || button.Tag is not ExecTurn turn)



        return;







      turn.Buffer.Clear();



      RenderAnsiText(turn.Body, string.Empty, turn.DefaultForeground ?? turn.Body.Foreground);







      if (this.FindName("StatusText") is TextBlock status)



        status.Text = "Exec output cleared.";







      try



      {



        await VS.StatusBar.ShowMessageAsync("Exec output cleared.");



      }



      catch



      {



        // status bar optional



      }



    }







    private void OnExecConsoleToggleChanged(object sender, RoutedEventArgs e)



    {



      if (_suppressExecToggleEvent)



        return;







      if (FindName("ExecConsoleToggle") is not ToggleButton toggle)



        return;







      var visible = toggle.IsChecked == true;



      if (_options != null)



        _options.ExecConsoleVisible = visible;







      foreach (var turn in _execConsoleTurns)



        ApplyExecConsoleVisibility(turn);



      UpdateMcpToolsUi();



    }







    private void OnRefreshMcpToolsClick(object sender, RoutedEventArgs e)



    {



      var now = DateTime.UtcNow;



      var timeSinceLastRefresh = now - _lastMcpToolsRefresh;



      



      if (timeSinceLastRefresh.TotalSeconds < RefreshDebounceSeconds)



      {



        // Show debounce message



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 



        {



          await VS.StatusBar.ShowMessageAsync($"Please wait {RefreshDebounceSeconds - (int)timeSinceLastRefresh.TotalSeconds} seconds before refreshing again");



        });



        return;



      }



      



      _lastMcpToolsRefresh = now;



      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await RequestMcpToolsAsync("user-refresh"));



    }







    private void OnMcpHelpClick(object sender, RoutedEventArgs e)



    {



      try



      {



        // Open MCP documentation in browser



        var url = "https://codex.anthropic.com/docs/mcp";



        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo



        {



          FileName = url,



          UseShellExecute = true



        });



        



        // Log telemetry for help link click



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 



        {



          await LogTelemetryAsync("mcp_help_clicked", new Dictionary<string, object>



          {



            ["url"] = url



          });



        });



      }



      catch (Exception ex)



      {



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 



        {



          await VS.StatusBar.ShowMessageAsync($"Failed to open MCP documentation: {ex.Message}");



        });



      }



    }







    private void OnRefreshPromptsClick(object sender, RoutedEventArgs e)



    {



      var now = DateTime.UtcNow;



      var timeSinceLastRefresh = now - _lastPromptsRefresh;



      



      if (timeSinceLastRefresh.TotalSeconds < RefreshDebounceSeconds)



      {



        // Show debounce message



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 



        {



          await VS.StatusBar.ShowMessageAsync($"Please wait {RefreshDebounceSeconds - (int)timeSinceLastRefresh.TotalSeconds} seconds before refreshing again");



        });



        return;



      }



      



      _lastPromptsRefresh = now;



      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await RequestCustomPromptsAsync("user-refresh"));



    }







    private void OnCustomPromptClick(object sender, MouseButtonEventArgs e)



    {



      if (sender is not Border border || border.DataContext is not CustomPromptInfo prompt)



        return;







      try



      {



        // Insert the prompt body into the input box



        if (FindName("InputBox") is TextBox inputBox)



        {



          var currentText = inputBox.Text ?? string.Empty;



          var promptText = prompt.Body ?? string.Empty;



          



          if (string.IsNullOrWhiteSpace(currentText))



          {



            inputBox.Text = promptText;



          }



          else



          {



            // Insert at cursor position or append



            var cursorPosition = inputBox.CaretIndex;



            inputBox.Text = currentText.Insert(cursorPosition, promptText);



            inputBox.CaretIndex = cursorPosition + promptText.Length;



          }



          



          inputBox.Focus();



          



              // Track last used prompt



              _options.LastUsedPrompt = prompt.Id;



              _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>



              {






              });







              // Record telemetry for prompt insertion



              _telemetry.RecordPromptInsert();







              // Log telemetry for prompt insertion



              _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>



              {



                await LogTelemetryAsync("prompt_inserted", new Dictionary<string, object>



                {



                  ["prompt_id"] = prompt.Id,



                  ["prompt_name"] = prompt.Name,



                  ["prompt_source"] = prompt.Source



                });



              });



        }



      }



      catch (Exception ex)



      {



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 



        {



          await VS.StatusBar.ShowMessageAsync($"Failed to insert prompt: {ex.Message}");



        });



      }



    }







    private void OnMcpToolClick(object sender, MouseButtonEventArgs e)



    {



      if (sender is not Border border || border.DataContext is not McpToolInfo tool)



        return;







      try



      {



        // Track last used tool



        _options.LastUsedTool = tool.Name;



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>



        {






        });







        // Record telemetry for tool invocation



        _telemetry.RecordToolInvocation();







        // Log telemetry for tool selection



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>



        {



          await LogTelemetryAsync("tool_selected", new Dictionary<string, object>



          {



            ["tool_name"] = tool.Name,



            ["tool_server"] = tool.Server



          });



        });



        



        // Show a message about the tool



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 



        {



          await VS.StatusBar.ShowMessageAsync($"Selected tool: {tool.Name}");



        });



      }



      catch (Exception ex)



      {



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 



        {



          await VS.StatusBar.ShowMessageAsync($"Failed to select tool: {ex.Message}");



        });



      }



    }







    private void OnMcpToolMouseEnter(object sender, MouseEventArgs e)



    {



      if (sender is not Border border || border.DataContext is not McpToolInfo tool)



        return;







      try



      {



        // Show tool details on hover



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 



        {



          await VS.StatusBar.ShowMessageAsync($"Tool: {tool.Name} - {tool.Description}");



        });



      }



      catch



      {



        // Ignore errors in hover



      }



    }







    private void OnMcpToolMouseLeave(object sender, MouseEventArgs e)



    {



      if (sender is not Border border)



        return;







      try



      {



        // Clear status bar message



        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 



        {



          await VS.StatusBar.ShowMessageAsync("");



        });



      }



      catch



      {



        // Ignore errors in hover



      }



    }







    private void OnCustomPromptMouseEnter(object sender, MouseEventArgs e)



    {



      if (sender is not Border border || border.DataContext is not CustomPromptInfo prompt)



        return;







      try



      {



        // Show preview of the prompt body



        if (border.FindName("PromptPreview") is TextBlock preview)



        {



          preview.Visibility = Visibility.Visible;



        }



      }



      catch



      {



        // Ignore errors in hover preview



      }



    }







    private void OnCustomPromptMouseLeave(object sender, MouseEventArgs e)



    {



      if (sender is not Border border)



        return;







      try



      {



        // Hide preview of the prompt body



        if (border.FindName("PromptPreview") is TextBlock preview)



        {



          preview.Visibility = Visibility.Collapsed;



        }



      }



      catch



      {



        // Ignore errors in hover preview



      }



    }







    private async void OnExecExportClick(object sender, RoutedEventArgs e)



    {



      if (sender is not Button button || button.Tag is not ExecTurn turn)



        return;







      try



      {



        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();







        var buffer = ChatTextUtilities.StripAnsi(turn.Buffer.ToString());



        if (string.IsNullOrWhiteSpace(buffer))



        {



          await VS.StatusBar.ShowMessageAsync("Exec output is empty.");



          return;



        }







        var dialog = new Microsoft.Win32.SaveFileDialog



        {



          Title = "Export Exec Output",



          Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",



          FileName = BuildExecExportFileName(turn)



        };







        var result = dialog.ShowDialog();



        if (result != true || string.IsNullOrWhiteSpace(dialog.FileName))



          return;







        File.WriteAllText(dialog.FileName, buffer);



        await VS.StatusBar.ShowMessageAsync($"Exec output saved to {Path.GetFileName(dialog.FileName)}.");



      }



      catch (Exception ex)



      {



        try



        {



          var pane = await DiagnosticsPane.GetAsync();



          await pane.WriteLineAsync($"[error] Exec export failed: {ex.Message}");



        }



        catch



        {



          // diagnostics best effort



        }



      }



    }







    private void OnDiffTreeCheckBoxClick(object sender, RoutedEventArgs e)



    {



      if (sender is not CheckBox checkBox || checkBox.DataContext is not DiffTreeItem item)



        return;







      var next = item.IsChecked != true;



      _suppressDiffSelectionUpdate = true;



      try



      {



        item.SetIsChecked(next, updateChildren: true, updateParent: true);



      }



      finally



      {



        _suppressDiffSelectionUpdate = false;



      }







      HandleDiffSelectionChanged(item);



      e.Handled = true;



    }







    private static string ExtractDocumentText(JObject container, string[] keys)



    {



      if (container == null)



        return string.Empty;







      foreach (var key in keys)



      {



        if (container.TryGetValue(key, out var token))



        {



          var text = CollectTokenText(token);



          if (!string.IsNullOrEmpty(text))



            return text;



        }







        if (container[key] is JObject nested)



        {



          var text = TryGetString(nested, "text") ?? TryGetString(nested, "value");



          if (!string.IsNullOrEmpty(text))



            return text;



        }



      }







      return string.Empty;



    }







    private static string ExtractNestedText(JObject parent, params string[] keys)



    {



      if (parent == null || keys == null || keys.Length == 0)



        return string.Empty;







      JToken current = parent;



      foreach (var key in keys)



      {



        if (current is not JObject obj || !obj.TryGetValue(key, out current))



          return string.Empty;



      }







      return CollectTokenText(current);



    }







    private static string CollectTokenText(JToken token)



    {



      if (token == null)



        return string.Empty;







      return token.Type switch



      {



        JTokenType.String => token.ToString(),



        JTokenType.Object => TryGetString((JObject)token, "text") ?? TryGetString((JObject)token, "value") ?? string.Empty,



        JTokenType.Array => string.Concat(token.Children().Select(CollectTokenText)),



        _ => token.ToString()



      };



    }







    private async Task ShowDiffAsync(DiffDocument doc)



    {



      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();







      IVsDifferenceService diffService;



      try



      {



        diffService = await VS.GetRequiredServiceAsync<SVsDifferenceService, IVsDifferenceService>();



      }



      catch (Exception ex)



      {



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[error] Unable to acquire diff service: {ex.Message}");



        return;



      }







      var originalFile = CreateTempDiffFile(doc.Path, doc.Original, "original");



      var modifiedFile = CreateTempDiffFile(doc.Path, doc.Modified, "modified");







      var caption = $"Codex Diff - {doc.Path}";



      var tooltip = caption;



      var originalLabel = $"{doc.Path} (current)";



      var modifiedLabel = $"{doc.Path} (Codex)";







      const __VSDIFFSERVICEOPTIONS options =



        __VSDIFFSERVICEOPTIONS.VSDIFFOPT_LeftFileIsTemporary |



        __VSDIFFSERVICEOPTIONS.VSDIFFOPT_RightFileIsTemporary;



      try



      {



        diffService.OpenComparisonWindow2(



          originalFile,



          modifiedFile,



          caption,



          tooltip,



          originalLabel,



          modifiedLabel,



          string.Empty,



          string.Empty,



          (uint)options);







        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[diff] Opened diff for {doc.Path}");



      }



      catch (Exception ex)



      {



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[error] Failed to open diff viewer for {doc.Path}: {ex.Message}");



      }



    }







    private static string CreateTempDiffFile(string displayPath, string contents, string suffix)



    {



      var safeName = Path.GetFileName(displayPath);



      if (string.IsNullOrEmpty(safeName))



        safeName = "codex";







      foreach (var invalid in Path.GetInvalidFileNameChars())



        safeName = safeName.Replace(invalid, '_');







      var dir = Path.Combine(Path.GetTempPath(), "CodexVS22", "Diffs");



      Directory.CreateDirectory(dir);







      var filePath = Path.Combine(dir, $"{safeName}.{suffix}.{Guid.NewGuid():N}.tmp");



      File.WriteAllText(filePath, contents ?? string.Empty, Encoding.UTF8);



      return filePath;



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







    private static readonly Regex AnsiCodeRegex = new("\x1B\\[(?<code>[0-9;]*)m", RegexOptions.Compiled);







    private static readonly Brush[] AnsiBrushes =



    {



      Brushes.DimGray,      // black



      Brushes.IndianRed,    // red



      Brushes.SeaGreen,     // green



      Brushes.Goldenrod,    // yellow



      Brushes.SteelBlue,    // blue



      Brushes.Orchid,       // magenta



      Brushes.Teal,         // cyan



      Brushes.Gainsboro     // white



    };







    private static readonly Brush[] AnsiBrightBrushes =



    {



      Brushes.LightGray,



      Brushes.Red,



      Brushes.LimeGreen,



      Brushes.Yellow,



      Brushes.DeepSkyBlue,



      Brushes.MediumOrchid,



      Brushes.Aqua,



      Brushes.White



    };







    private void OnExecContainerPreviewMouseWheel(object sender, MouseWheelEventArgs e)



    {



      if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)



        return;







      if (sender is not Border border || border.Tag is not ExecTurn turn)



        return;







      e.Handled = true;







      var delta = e.Delta > 0 ? 20 : -20;



      var newHeight = Math.Max(80, Math.Min(600, border.MaxHeight + delta));



      border.MaxHeight = newHeight;



      _execConsolePreferredHeight = newHeight;







      if (_options != null)



        _options.ExecConsoleHeight = newHeight;







      ApplyExecConsoleVisibility(turn);



    }







    private void ApplyExecConsoleVisibility(ExecTurn turn)



    {



      if (turn?.Container == null)



        return;







      var show = ShouldShowExecTurn(turn?.IsRunning ?? false);



      turn.Container.Visibility = show ? Visibility.Visible : Visibility.Collapsed;







      if (show)



        turn.Container.MaxHeight = _execConsolePreferredHeight;



    }







    private bool ShouldShowExecTurn(bool running)



    {



      if (running)



        return true;







      if (_options?.AutoHideExecConsole ?? false)



        return false;







      return _options?.ExecConsoleVisible ?? true;



    }







    private async Task RequestMcpToolsAsync(string reason)



    {



      var host = _host;



      if (host == null)



        return;







      try



      {



        var submission = new JObject



        {



          ["id"] = Guid.NewGuid().ToString(),



          ["op"] = new JObject



          {



            ["type"] = "list_mcp_tools"



          }



        };







        var json = submission.ToString(Formatting.None);



        var ok = await host.SendAsync(json);



        var pane = await DiagnosticsPane.GetAsync();



        if (ok)



          await pane.WriteLineAsync($"[info] Requested MCP tools ({reason}).");



        else



          await pane.WriteLineAsync($"[warn] Failed to request MCP tools ({reason}).");



      }



      catch (Exception ex)



      {



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[error] RequestMcpToolsAsync failed: {ex.Message}");



      }



    }







    private async Task RequestCustomPromptsAsync(string reason)



    {



      var host = _host;



      if (host == null)



        return;







      try



      {



        var submission = new JObject



        {



          ["id"] = Guid.NewGuid().ToString(),



          ["op"] = new JObject



          {



            ["type"] = "list_custom_prompts"



          }



        };







        var json = submission.ToString(Formatting.None);



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[info] Requesting custom prompts ({reason}).");



        await host.SendAsync(json);



      }



      catch (Exception ex)



      {



        var pane = await DiagnosticsPane.GetAsync();



        await pane.WriteLineAsync($"[error] RequestCustomPromptsAsync failed: {ex.Message}");



      }



    }







    private void ApplyExecBufferLimit(ExecTurn turn)



    {



      if (turn?.Buffer == null)



        return;







      var limit = _options?.ExecOutputBufferLimit ?? 0;



      if (limit <= 0)



        return;







      if (turn.Buffer.Length <= limit)



        return;







      var excess = turn.Buffer.Length - limit;



      if (excess < limit / 5)



        excess = limit / 5;







      turn.Buffer.Remove(0, excess);



    }







    private static string BuildExecExportFileName(ExecTurn turn)



    {



      var source = !string.IsNullOrWhiteSpace(turn?.NormalizedCommand)



        ? turn.NormalizedCommand



        : turn?.ExecId;







      var safe = SanitizeFileName(source);



      if (string.IsNullOrEmpty(safe))



        safe = "codex-exec";







      var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);



      return $"{safe}-{timestamp}.txt";



    }







    private static string SanitizeFileName(string name)



    {



      if (string.IsNullOrWhiteSpace(name))



        return string.Empty;







      var invalid = Path.GetInvalidFileNameChars();



      var builder = new StringBuilder(name.Length);



      foreach (var ch in name)



      {



        if (invalid.Contains(ch) || char.IsControl(ch))



        {



          builder.Append('-');



          continue;



        }







        builder.Append(ch);



      }







      var sanitized = builder.ToString().Trim('-');



      if (sanitized.Length > 80)



        sanitized = sanitized.Substring(0, 80);







      return sanitized;



    }







    private void UpdateMcpToolsUi()



    {



      if (FindName("McpToolsContainer") is not Border container ||



          FindName("McpToolsEmptyText") is not StackPanel emptyPanel ||



          FindName("McpToolsList") is not ItemsControl list)



        return;







      if (list.ItemsSource != _mcpTools)



        list.ItemsSource = _mcpTools;







      if (_mcpTools.Count == 0)



      {



        emptyPanel.Visibility = Visibility.Visible;



        container.Visibility = Visibility.Visible;



      }



      else



      {



        emptyPanel.Visibility = Visibility.Collapsed;



        container.Visibility = Visibility.Visible;



      }







      UpdateMcpToolRunsUi();



    }







    private void UpdateMcpToolRunsUi()



    {



      if (FindName("McpToolRunsContainer") is not Border container ||



          FindName("McpToolRunsList") is not ItemsControl list)



        return;







      if (list.ItemsSource != _mcpToolRuns)



        list.ItemsSource = _mcpToolRuns;







      container.Visibility = _mcpToolRuns.Count > 0



        ? Visibility.Visible



        : Visibility.Collapsed;



    }







    private void UpdateCustomPromptsUi()



    {



      if (FindName("CustomPromptsContainer") is not Border container ||



          FindName("CustomPromptsList") is not ItemsControl list ||



          FindName("CustomPromptsEmptyText") is not TextBlock empty)



        return;







      if (list.ItemsSource != _customPrompts)



        list.ItemsSource = _customPrompts;







      if (_customPrompts.Count == 0)



      {



        empty.Visibility = Visibility.Visible;



        container.Visibility = Visibility.Visible;



      }



      else



      {



        empty.Visibility = Visibility.Collapsed;



        container.Visibility = Visibility.Visible;



      }



    }







    private void AppendExecText(ExecTurn turn, string text)



    {



      if (turn == null || string.IsNullOrEmpty(text))



        return;







      turn.Buffer.Append(text);



      if (!text.EndsWith("\n", StringComparison.Ordinal))



        turn.Buffer.Append('\n');







      ApplyExecBufferLimit(turn);







      var bufferText = turn.Buffer.ToString();



      RenderAnsiText(turn.Body, bufferText, turn.DefaultForeground ?? turn.Body.Foreground);



    }







    private static void RenderAnsiText(TextBlock block, string text, Brush defaultBrush)



    {



      if (block == null)



        return;







      block.Inlines.Clear();







      if (string.IsNullOrEmpty(text))



        return;







      var currentBrush = defaultBrush;



      var isBold = false;



      var lastIndex = 0;







      foreach (Match match in AnsiCodeRegex.Matches(text))



      {



        if (match.Index > lastIndex)



        {



          var segment = text.Substring(lastIndex, match.Index - lastIndex);



          AppendAnsiSegment(block, segment, currentBrush, isBold);



        }







        var codes = match.Groups["code"].Value;



        UpdateAnsiState(codes, defaultBrush, ref currentBrush, ref isBold);



        lastIndex = match.Index + match.Length;



      }







      if (lastIndex < text.Length)



      {



        var tail = text.Substring(lastIndex);



        AppendAnsiSegment(block, tail, currentBrush, isBold);



      }



    }







    private static void AppendAnsiSegment(TextBlock block, string segment, Brush brush, bool bold)



    {



      if (block == null || string.IsNullOrEmpty(segment))



        return;







      var sanitized = segment.Replace("\r", string.Empty);



      if (sanitized.Length == 0)



        return;







      var run = new Run(sanitized)



      {



        Foreground = brush,



        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal



      };



      block.Inlines.Add(run);



    }







    private static void UpdateAnsiState(string codes, Brush defaultBrush, ref Brush brush, ref bool bold)



    {



      if (defaultBrush == null)



        defaultBrush = Brushes.Gainsboro;







      if (string.IsNullOrEmpty(codes))



      {



        brush = defaultBrush;



        bold = false;



        return;



      }







      var parts = codes.Split(';');



      foreach (var part in parts)



      {



        if (string.IsNullOrWhiteSpace(part))



        {



          brush = defaultBrush;



          bold = false;



          continue;



        }







        if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))



          continue;







        switch (code)



        {



          case 0:



            brush = defaultBrush;



            bold = false;



            break;



          case 1:



            bold = true;



            break;



          case 22:



            bold = false;



            break;



          case 39:



            brush = defaultBrush;



            break;



          default:



            if ((code >= 30 && code <= 37) || (code >= 90 && code <= 97))



            {



              var resolved = ResolveAnsiBrush(code);



              if (resolved != null)



                brush = resolved;



            }



            break;



        }



      }



    }







    private static Brush ResolveAnsiBrush(int code)



    {



      var bright = false;



      if (code >= 90 && code <= 97)



      {



        bright = true;



        code -= 60;



      }







      var index = code - 30;



      if (index < 0 || index >= AnsiBrushes.Length)



        return null;







      return bright ? AnsiBrightBrushes[index] : AnsiBrushes[index];



    }







    private void UpdateExecCancelState(ExecTurn turn, bool running)



    {



      if (turn == null)



        return;







      turn.IsRunning = running;



      turn.CancelRequested = false;







      if (turn.CancelButton != null)



      {



        turn.CancelButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;



        turn.CancelButton.IsEnabled = true;



      }







      ApplyExecConsoleVisibility(turn);







      if (running)



        ScrollTranscriptToEnd();



    }







    private AssistantTurn GetOrCreateAssistantTurn(string id)



    {



      if (_assistantTurns.TryGetValue(id, out var turn))



        return turn;







      var elements = CreateAssistantBubble();



      turn = new AssistantTurn(elements);



      _assistantTurns[id] = turn;



      return turn;



    }







    private ChatBubbleElements CreateAssistantBubble()



      => CreateChatBubble("assistant", string.Empty, DateTime.UtcNow);







    private void AppendAssistantText(AssistantTurn turn, string delta, bool isFinal = false, bool decorate = true)



    {



      if (turn == null || string.IsNullOrEmpty(delta))



        return;







      if (turn.Buffer.Length > 0 && !turn.Buffer.ToString().EndsWith("\n", StringComparison.Ordinal))



        turn.Buffer.AppendLine();







      turn.Buffer.Append(delta);



      var cleaned = ChatTextUtilities.NormalizeAssistantText(turn.Buffer.ToString());



      turn.Bubble.Text = cleaned;



      UpdateBubbleAutomation(turn.Header, turn.Bubble, cleaned);



      ScrollTranscriptToEnd();







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



          if (bubble.Tag is ExecTurn execTurn)



          {



            var buffer = execTurn.Buffer.ToString();



            text = ChatTextUtilities.StripAnsi(buffer).TrimEnd('\n');



          }



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







    private ChatBubbleElements CreateChatBubble(string role, string initialText = "", DateTime? timestamp = null)



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







      var when = (timestamp ?? DateTime.UtcNow).ToLocalTime();



      var headerText = $"{GetRoleDisplayName(role)} — {when.ToString("t", CultureInfo.CurrentCulture)}";







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







      var header = new TextBlock



      {



        Text = headerText,



        FontWeight = FontWeights.SemiBold,



        Margin = new Thickness(0, 0, 0, 4)



      };



      header.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);







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







      var layout = new StackPanel



      {



        Orientation = Orientation.Vertical



      };



      layout.Children.Add(header);



      layout.Children.Add(bubble);







      container.Child = layout;



      transcript.Children.Add(container);



      UpdateBubbleAutomation(header, bubble, initialText);



      ScrollTranscriptToEnd();







      return new ChatBubbleElements(container, header, bubble);



    }







    private static string GetRoleDisplayName(string role)



      => string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) ? "Codex" : "You";







    private void UpdateBubbleAutomation(TextBlock header, TextBlock bubble, string bodyText)



    {



      if (header == null || bubble == null)



        return;







      var sanitized = string.IsNullOrWhiteSpace(bodyText)



        ? string.Empty



        : bodyText.Replace('\r', ' ').Replace('\n', ' ').Trim();







      var name = string.IsNullOrWhiteSpace(sanitized)



        ? header.Text ?? string.Empty



        : $"{header.Text}: {sanitized}";







      AutomationProperties.SetName(bubble, name);



      AutomationProperties.SetName(header, header.Text ?? string.Empty);







      if (header.Parent is FrameworkElement element)



        AutomationProperties.SetName(element, name);



    }







    private void ScrollTranscriptToEnd()



    {



      if (this.FindName("TranscriptScrollViewer") is not ScrollViewer viewer)



        return;







      viewer.Dispatcher.BeginInvoke(new Action(() =>



      {



        viewer.UpdateLayout();



        viewer.ScrollToEnd();



      }), DispatcherPriority.Background);



    }







    private void ResetTranscript(bool includeWelcome = true)



    {



      if (this.FindName("Transcript") is not StackPanel transcript)



        return;







      transcript.Children.Clear();







      if (!includeWelcome)



        return;







      var welcome = new TextBlock



      {



        Text = "Welcome to Codex for Visual Studio",



        TextWrapping = TextWrapping.Wrap



      };



      welcome.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);



      transcript.Children.Add(welcome);



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



        HorizontalAlignment = HorizontalAlignment.Left,



        Visibility = Visibility.Collapsed



      };



      ApplyExecBubbleBrush(container);







      var panel = new StackPanel();



      var headerTextValue = string.IsNullOrWhiteSpace(headerText) ? "$ exec" : headerText.Trim();







      Button CreateHeaderButton(string accessText, RoutedEventHandler handler, double minWidth, Thickness? margin = null)



      {



        var button = new Button



        {



          Content = new AccessText { Text = accessText },



          Margin = margin ?? new Thickness(6, 0, 0, 4),



          MinWidth = minWidth,



          Height = 24,



          HorizontalAlignment = HorizontalAlignment.Left



        };



        button.Click += handler;



        return button;



      }







      var cancelButton = CreateHeaderButton("_Cancel", OnExecCancelClick, 70, new Thickness(8, 0, 0, 4));



      cancelButton.Visibility = Visibility.Collapsed;







      var copyButton = CreateHeaderButton("Cop_y All", OnExecCopyAllClick, 80);



      var clearButton = CreateHeaderButton("C_lear Output", OnExecClearClick, 100);



      var exportButton = CreateHeaderButton("_Export", OnExecExportClick, 80);







      TextBlock headerBlock = null;







      if (!string.IsNullOrEmpty(headerTextValue))



      {



        headerBlock = new TextBlock



        {



          Text = headerTextValue,



          FontWeight = FontWeights.SemiBold,



          TextWrapping = TextWrapping.Wrap,



          Margin = new Thickness(0, 0, 0, 4),



          VerticalAlignment = VerticalAlignment.Center



        };



        headerBlock.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);







        var headerRow = new StackPanel



        {



          Orientation = Orientation.Horizontal,



          VerticalAlignment = VerticalAlignment.Center



        };







        headerRow.Children.Add(headerBlock);



        headerRow.Children.Add(cancelButton);



        headerRow.Children.Add(copyButton);



        headerRow.Children.Add(clearButton);



        headerRow.Children.Add(exportButton);



        panel.Children.Add(headerRow);



      }



      else



      {



        var buttonRow = new StackPanel



        {



          Orientation = Orientation.Horizontal,



          HorizontalAlignment = HorizontalAlignment.Left,



          Margin = new Thickness(0, 0, 0, 4)



        };



        buttonRow.Children.Add(cancelButton);



        buttonRow.Children.Add(copyButton);



        buttonRow.Children.Add(clearButton);



        buttonRow.Children.Add(exportButton);



        panel.Children.Add(buttonRow);



      }







      var bodyBlock = new TextBlock



      {



        Text = string.Empty,



        TextWrapping = TextWrapping.Wrap,



        FontFamily = new FontFamily("Consolas"),



        Visibility = Visibility.Visible



      };



      bodyBlock.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);



      AttachCopyContextMenu(bodyBlock);







      panel.Children.Add(bodyBlock);



      container.Child = panel;



      transcript.Children.Add(container);







      var execTurn = new ExecTurn(container, bodyBlock, headerBlock, cancelButton, copyButton, clearButton, exportButton, normalizedCommand);



      bodyBlock.Tag = execTurn;



      copyButton.Tag = execTurn;



      clearButton.Tag = execTurn;



      exportButton.Tag = execTurn;



      container.Tag = execTurn;



      container.MaxHeight = _execConsolePreferredHeight;



      container.PreviewMouseWheel += OnExecContainerPreviewMouseWheel;







      _execConsoleTurns.Add(execTurn);



      if (_execConsoleTurns.Count > 50)



        _execConsoleTurns.RemoveAt(0);



      ApplyExecConsoleVisibility(execTurn);







      return execTurn;



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







      turn.ExecId = id;



      if (turn.CancelButton != null)



        turn.CancelButton.Tag = id;







      if (turn.CopyButton != null)



        turn.CopyButton.Tag = turn;







      if (turn.ClearButton != null)



        turn.ClearButton.Tag = turn;







      if (turn.ExportButton != null)



        turn.ExportButton.Tag = turn;







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







    private static IReadOnlyList<McpToolInfo> ExtractMcpTools(JObject obj)



    {



      var results = new List<McpToolInfo>();



      if (obj == null)



        return results;







      var toolsToken = obj["tools"] ?? obj["items"];



      if (toolsToken is not JArray array)



        return results;







      foreach (var token in array)



      {



        if (token is not JObject toolObj)



          continue;







        var name = TryGetString(toolObj, "name", "id", "tool");



        var description = TryGetString(toolObj, "description", "summary", "detail");



        var server = TryGetString(toolObj, "server", "provider", "source");



        results.Add(new McpToolInfo(name, description, server));



      }







      return results;



    }







    private static IReadOnlyList<CustomPromptInfo> ExtractCustomPrompts(JObject obj)



    {



      var results = new List<CustomPromptInfo>();



      if (obj == null)



        return results;







      var promptsToken = obj["prompts"] ?? obj["items"] ?? obj["data"];



      if (promptsToken is not JArray array)



        return results;







      foreach (var token in array)



      {



        if (token is not JObject promptObj)



          continue;







        var id = TryGetString(promptObj, "id", "prompt_id", "promptId", "name");



        var name = TryGetString(promptObj, "name", "title", "label");



        var description = TryGetString(promptObj, "description", "summary", "detail", "notes");



        var body = TryGetString(promptObj, "body", "content", "text", "prompt");



        var source = TryGetString(promptObj, "source", "provider", "server", "scope");







        results.Add(new CustomPromptInfo(id, name, description, body, source));



      }







      return results;



    }







    private static string FormatToolArguments(JObject raw)



    {



      if (raw == null)



        return string.Empty;







      var direct = TryGetString(raw, "arguments_preview", "input_preview", "input_summary");



      if (!string.IsNullOrWhiteSpace(direct))



        return direct.Trim();







      var token = raw["arguments"] ?? raw["args"] ?? raw["input"] ?? raw["parameters"];



      var text = FormatCompactText(token);



      if (string.IsNullOrEmpty(text))



        return string.Empty;







      return text.StartsWith("Args:", StringComparison.OrdinalIgnoreCase)



        ? text



        : $"Args: {text}";



    }







    private static string ExtractToolOutputText(JObject raw)



    {



      if (raw == null)



        return string.Empty;







      var direct = TryGetString(raw, "text", "delta", "chunk", "output", "message", "value");



      if (!string.IsNullOrWhiteSpace(direct))



        return direct.Trim();







      var token = raw["output"] ?? raw["result"] ?? raw["data"] ?? raw["response"];



      return FormatCompactText(token);



    }







    private static string ExtractToolCompletionDetail(JObject raw)



    {



      if (raw == null)



        return string.Empty;







      var direct = TryGetString(raw, "detail", "message", "result_text", "result", "output", "error");



      if (!string.IsNullOrWhiteSpace(direct))



        return direct.Trim();







      var token = raw["result"] ?? raw["output"] ?? raw["response"];



      return FormatCompactText(token);



    }







    private static bool? InterpretToolStatus(string status)



    {



      if (string.IsNullOrWhiteSpace(status))



        return null;







      var normalized = status.Trim().ToLowerInvariant();



      if (normalized is "success" or "succeeded" or "ok" or "completed" or "complete" or "done")



        return true;



      if (normalized is "fail" or "failed" or "error" or "errored" or "cancelled" or "canceled" or "aborted" or "timeout")



        return false;



      return null;



    }







    private static string FormatCompactText(JToken token, int maxLength = 200)



    {



      if (token == null)



        return string.Empty;







      string text = token.Type switch



      {



        JTokenType.String => token.ToString(),



        JTokenType.Integer or JTokenType.Float or JTokenType.Boolean => token.ToString(),



        _ => token.ToString(Formatting.None)



      };







      if (string.IsNullOrWhiteSpace(text))



        return string.Empty;







      text = Regex.Replace(text, "\\s+", " ").Trim();



      if (text.Length > maxLength)



        text = text.Substring(0, maxLength) + "...";



      return text;



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







    private async Task<bool> SendExecCancelAsync(string execId)



    {



      var host = _host;



      if (host == null || string.IsNullOrEmpty(execId))



        return false;







      var submission = new JObject



      {



        ["id"] = Guid.NewGuid().ToString(),



        ["op"] = new JObject



        {



          ["type"] = "exec_cancel",



          ["id"] = execId,



          ["call_id"] = execId



        }



      };







      var json = submission.ToString(Formatting.None);



      var pane = await DiagnosticsPane.GetAsync();



      await pane.WriteLineAsync($"[debug] exec cancel submission {json}");







      var ok = await host.SendAsync(json);



      if (ok)



      {



        await pane.WriteLineAsync($"[info] Requested cancel for exec {execId}");



      }



      else



      {



        await pane.WriteLineAsync($"[warn] Failed to send exec cancel for {execId}");



      }







      return ok;



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







    private static async Task LogTelemetryAsync(string eventName, Dictionary<string, object> properties = null)



    {



      try



      {



        var pane = await DiagnosticsPane.GetAsync();



        var timestamp = DateTime.Now.ToString("HH:mm:ss");



        var props = properties != null ? string.Join(", ", properties.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "";



        var message = string.IsNullOrEmpty(props) ? eventName : $"{eventName} ({props})";



        await pane.WriteLineAsync($"[telemetry] {timestamp} {message}");



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



























