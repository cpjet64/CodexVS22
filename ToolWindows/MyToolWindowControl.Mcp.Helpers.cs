using System;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using CodexVS22.Core.Protocol;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private void InitializeMcpToolsUi()
    {
      if (FindName("McpToolsList") is ItemsControl list)
        list.ItemsSource = _mcpTools;

      if (FindName("McpToolRunsList") is ItemsControl runs)
        runs.ItemsSource = _mcpToolRuns;

      if (FindName("CustomPromptsList") is ItemsControl prompts)
        prompts.ItemsSource = _customPrompts;

      UpdateMcpToolsUi();
      UpdateMcpToolRunsUi();
      UpdateCustomPromptsUi();
    }

    private static string ResolveToolCallId(EventMsg evt)
    {
      if (evt == null)
        return string.Empty;

      return TryGetString(evt.Raw, "call_id", "tool_call_id", "toolCallId", "id")
        ?? evt.Id
        ?? string.Empty;
    }

    private McpToolRun EnsureToolRun(string callId, string toolName, string server)
    {
      var key = string.IsNullOrEmpty(callId) ? Guid.NewGuid().ToString() : callId;

      if (_mcpToolRunIndex.TryGetValue(key, out var existing))
        return existing;

      var run = new McpToolRun(key, toolName, server);
      _mcpToolRunIndex[key] = run;
      _mcpToolRuns.Insert(0, run);
      TrimMcpToolRunsIfNeeded();
      return run;
    }

    private void TrimMcpToolRunsIfNeeded()
    {
      while (_mcpToolRuns.Count > MaxMcpToolRuns)
      {
        var lastIndex = _mcpToolRuns.Count - 1;
        var last = _mcpToolRuns[lastIndex];
        _mcpToolRuns.RemoveAt(lastIndex);
        if (last != null)
          _mcpToolRunIndex.Remove(last.CallId);
      }
    }
  }
}
