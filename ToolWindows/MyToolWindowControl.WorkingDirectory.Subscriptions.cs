using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using CodexVS22.Core;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
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
  }
}
