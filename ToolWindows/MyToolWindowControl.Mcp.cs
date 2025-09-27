using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using CodexVS22.Core;
using CodexVS22.Core.Protocol;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private async void HandleListMcpTools(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var tools = ExtractMcpTools(evt.Raw);

        _mcpTools.Clear();
        foreach (var tool in tools)
          _mcpTools.Add(tool);

        UpdateMcpToolsUi();

        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[info] MCP tools received: {_mcpTools.Count}.");
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleListMcpTools failed: {ex.Message}");
      }
    }

    private async void HandleListCustomPrompts(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var prompts = ExtractCustomPrompts(evt.Raw);

        _customPrompts.Clear();
        _customPromptIndex.Clear();
        foreach (var prompt in prompts)
        {
          _customPrompts.Add(prompt);
          _customPromptIndex[prompt.Id] = prompt;
        }

        UpdateCustomPromptsUi();

        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[info] Custom prompts received: {_customPrompts.Count}.");
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleListCustomPrompts failed: {ex.Message}");
      }
    }

    private async void HandleToolCallBegin(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var raw = evt.Raw ?? new JObject();
        var callId = ResolveToolCallId(evt);
        var toolName = TryGetString(raw, "tool_name", "tool", "name", "id");
        var server = TryGetString(raw, "server", "provider", "source");
        var status = TryGetString(raw, "status", "state");
        var detail = FormatToolArguments(raw);

        var run = EnsureToolRun(callId, toolName, server);
        run.UpdateRunning(status, detail);
        callId = run.CallId;

        UpdateMcpToolRunsUi();

        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[info] Tool call started: {run.ToolName} ({callId})");
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleToolCallBegin failed: {ex.Message}");
      }
    }

    private async void HandleToolCallOutput(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var raw = evt.Raw ?? new JObject();
        var callId = ResolveToolCallId(evt);
        var toolName = TryGetString(raw, "tool_name", "tool", "name", "id");
        var server = TryGetString(raw, "server", "provider", "source");
        var run = EnsureToolRun(callId, toolName, server);
        callId = run.CallId;

        var output = ExtractToolOutputText(raw);
        if (!string.IsNullOrWhiteSpace(output))
          run.AppendOutput(output);

        UpdateMcpToolRunsUi();
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleToolCallOutput failed: {ex.Message}");
      }
    }

    private async void HandleToolCallEnd(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var raw = evt.Raw ?? new JObject();
        var callId = ResolveToolCallId(evt);
        var toolName = TryGetString(raw, "tool_name", "tool", "name", "id");
        var server = TryGetString(raw, "server", "provider", "source");
        var run = EnsureToolRun(callId, toolName, server);
        callId = run.CallId;

        var status = TryGetString(raw, "status", "state", "result", "outcome");
        var success = TryGetBoolean(raw, "success", "ok", "completed") ?? InterpretToolStatus(status);
        var detail = ExtractToolCompletionDetail(raw);

        run.Complete(status, success, detail);
        UpdateMcpToolRunsUi();

        var pane = await DiagnosticsPane.GetAsync();
        var outcome = success == false ? "failed" : "completed";
        await pane.WriteLineAsync($"[info] Tool call {outcome}: {run.ToolName} ({callId})");
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleToolCallEnd failed: {ex.Message}");
      }
    }

    private async void HandleTurnDiff(EventMsg evt)
    {
      try
      {
        var docs = DiffUtilities.ExtractDocuments(evt.Raw);
        if (docs.Count == 0)
        {
          var pane = await DiagnosticsPane.GetAsync();
          await pane.WriteLineAsync("[warn] TurnDiff event missing diff content; nothing to display.");
          return;
        }

        var filtered = await ProcessDiffDocumentsAsync(docs);
        if (filtered.Count == 0)
        {
          await UpdateDiffTreeAsync(filtered);
          var pane = await DiagnosticsPane.GetAsync();
          await pane.WriteLineAsync("[info] Codex diff contained no textual changes after filtering.");
          if (this.FindName("StatusText") is TextBlock status)
            status.Text = "Codex diff contained no textual changes.";
          return;
        }

        await UpdateDiffTreeAsync(filtered);

        foreach (var doc in filtered)
          await ShowDiffAsync(doc);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleTurnDiff failed: {ex.Message}");
      }
    }

    private async Task UpdateDiffTreeAsync(IReadOnlyList<DiffDocument> docs)
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      if (this.FindName("DiffTreeContainer") is not Border container ||
          this.FindName("DiffTreeView") is not TreeView tree ||
          this.FindName("DiffSelectionSummary") is not TextBlock summary ||
          this.FindName("DiscardPatchButton") is not Button discardButton)
        return;

      if (docs == null || docs.Count == 0)
      {
        _diffDocuments.Clear();
        _diffTreeRoots.Clear();
        _diffTotalLeafCount = 0;
        tree.ItemsSource = null;
        container.Visibility = Visibility.Collapsed;
        summary.Text = string.Empty;
        summary.Visibility = Visibility.Collapsed;
        discardButton.Visibility = Visibility.Collapsed;
        return;
      }

      _diffDocuments = docs
        .GroupBy(doc => NormalizeDiffPath(doc.Path))
        .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

      _suppressDiffSelectionUpdate = true;
      try
      {
        var (roots, leafCount) = BuildDiffTree(docs, HandleDiffSelectionChanged);
        _diffTotalLeafCount = leafCount;
        _diffTreeRoots = new ObservableCollection<DiffTreeItem>(roots);
        tree.ItemsSource = _diffTreeRoots;
        container.Visibility = Visibility.Visible;
        discardButton.Visibility = Visibility.Visible;
      }
      finally
      {
        _suppressDiffSelectionUpdate = false;
      }

      UpdateDiffSelectionSummary();
    }

    private async Task<List<DiffDocument>> ProcessDiffDocumentsAsync(IReadOnlyList<DiffDocument> docs)
    {
      var filtered = new List<DiffDocument>();
      if (docs == null)
        return filtered;

      var pane = await DiagnosticsPane.GetAsync();
      foreach (var doc in docs)
      {
        if (doc.IsBinary)
        {
          await pane.WriteLineAsync($"[warn] Skipping binary diff for {doc.Path}; manual review recommended.");
          continue;
        }

        if (doc.IsEmpty)
        {
          await pane.WriteLineAsync($"[info] Empty diff for {doc.Path}; nothing to display.");
          continue;
        }

        filtered.Add(doc);
      }

      return filtered;
    }

  }
}
