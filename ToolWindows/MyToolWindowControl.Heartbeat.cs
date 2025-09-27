using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
  }
}
