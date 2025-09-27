using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using System.Linq;
using CodexVS22.Core;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
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
          await host.SendAsync(ApprovalSubmissionFactory.CreateExec(request.CallId, approved));
          if (!approved)
            await LogManualApprovalAsync("exec", request.Signature, approved);
        }
        else
        {
          await host.SendAsync(ApprovalSubmissionFactory.CreatePatch(request.CallId, approved));
          if (approved)
            await ApplySelectedDiffsAsync();
          else
            await LogManualApprovalAsync("patch", request.Signature, approved);
          _lastPatchCallId = request.CallId ?? string.Empty;
          _lastPatchSignature = request.Signature ?? string.Empty;
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

  }
}


