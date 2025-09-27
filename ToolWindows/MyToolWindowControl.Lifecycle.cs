using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Threading;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
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
      _selectedApprovalMode = _options?.Mode ?? CodexOptions.ApprovalMode.Chat;
      _execConsolePreferredHeight = Math.Max(80, _options?.ExecConsoleHeight ?? 180.0);
      ApplyExecConsoleToggleState();
      if (_options != null)
      {
        _options.DefaultModel = _selectedModel;
        _options.DefaultReasoning = _selectedReasoning;
        _options.Mode = _selectedApprovalMode;
      }

      await InitializeSelectorsAsync();
      await UpdateFullAccessBannerAsync();
      ApplyWindowPreferences();
      InitializeMcpToolsUi();
      
      // Restore last used tool and prompt
      await RestoreLastUsedItemsAsync();

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

      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await RequestMcpToolsAsync("tool-window-loaded"));
      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await RequestCustomPromptsAsync("tool-window-loaded"));
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

  }
}
