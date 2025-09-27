using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private async Task<IVsSolution> GetSolutionServiceAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      if (_solutionService != null)
        return _solutionService;

      _solutionService = await VS.GetServiceAsync<SVsSolution, IVsSolution>();
      return _solutionService;
    }

    private async Task<string> GetSolutionDirectoryFromServiceAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      var solution = await GetSolutionServiceAsync();
      if (solution == null)
        return string.Empty;

      try
      {
        var hr = solution.GetSolutionInfo(out var directory, out var solutionFile, out _);
        if (!ErrorHandler.Succeeded(hr))
          return string.Empty;

        if (!string.IsNullOrWhiteSpace(directory))
          return NormalizeDirectory(directory);

        if (!string.IsNullOrWhiteSpace(solutionFile))
          return GetDirectoryFromFile(solutionFile);
      }
      catch
      {
      }

      return string.Empty;
    }

    private async Task<(string Directory, string File)> GetSolutionInfoAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      var solution = await GetSolutionServiceAsync();
      if (solution == null)
        return (string.Empty, string.Empty);

      try
      {
        var hr = solution.GetSolutionInfo(out var directory, out var solutionFile, out _);
        if (!ErrorHandler.Succeeded(hr))
          return (string.Empty, string.Empty);

        var normalizedDir = string.IsNullOrWhiteSpace(directory)
          ? string.Empty
          : NormalizeDirectory(directory);
        return (normalizedDir, solutionFile ?? string.Empty);
      }
      catch
      {
        return (string.Empty, string.Empty);
      }
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
  }
}
