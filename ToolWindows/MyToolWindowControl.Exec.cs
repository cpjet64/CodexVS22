using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using CodexVS22.Core;
using CodexVS22.Core.Protocol;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
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
          await host.SendAsync(ApprovalSubmissionFactory.CreateExec(callId, autoApproved));
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

        UpdateExecCancelState(turn, running: true);
        _telemetry.BeginExec(canonicalId, normalizedCommand);

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

        var exitCode = 0;
        if (evt.Raw is JObject raw)
          exitCode = TryGetInt(raw, "exit_code", "code", "status") ?? 0;

        if (_execTurns.TryGetValue(execId, out var turn))
        {
          AppendExecText(turn, "$ exec finished\n");
          UpdateExecCancelState(turn, running: false);
          _execTurns.Remove(execId);
          if (!string.IsNullOrEmpty(turn.NormalizedCommand) &&
              _execCommandIndex.TryGetValue(turn.NormalizedCommand, out var mappedId) &&
              string.Equals(mappedId, execId, StringComparison.Ordinal))
          {
            _execCommandIndex.Remove(turn.NormalizedCommand);
          }
        }
        _telemetry.CompleteExec(execId, exitCode);
        await WriteExecDiagnosticsAsync("$ exec finished");
        if (_lastExecFallbackId == execId)
          _lastExecFallbackId = null;

        RemoveExecIdMappings(execId);
        foreach (var key in _execCommandIndex.Where(kvp => string.Equals(kvp.Value, execId, StringComparison.Ordinal)).Select(kvp => kvp.Key).ToList())
          _execCommandIndex.Remove(key);

        UpdateTelemetryUi();
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleExecCommandEnd failed: {ex.Message}");
      }
    }
  }
}
