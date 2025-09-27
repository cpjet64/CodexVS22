using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using CodexVS22.Core;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private async Task RefreshWorkingDirectoryAsync(string reason)
    {
      await EnsureWorkingDirectoryUpToDateAsync(reason);
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

    private async Task LogWorkingDirectoryResolutionAsync(string reason, WorkingDirectoryResolution resolution, string previous, bool includeCandidates)
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
        // diagnostics best effort
      }
    }
  }
}
