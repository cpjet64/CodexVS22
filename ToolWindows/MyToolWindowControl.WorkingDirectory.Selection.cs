using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using EnvDTE80;
namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private static WorkingDirectoryCandidate SelectBestCandidate(List<WorkingDirectoryCandidate> candidates)
    {
      if (candidates == null || candidates.Count == 0)
        return null;

      bool OutsideExtension(WorkingDirectoryCandidate candidate)
        => !string.IsNullOrEmpty(candidate.Path) && !candidate.IsInsideExtension;

      bool Exists(WorkingDirectoryCandidate candidate)
        => !string.IsNullOrEmpty(candidate.Path) && candidate.Exists;

      WorkingDirectoryCandidate Pick(Func<WorkingDirectoryCandidate, bool> predicate)
      {
        var outside = candidates.FirstOrDefault(c => predicate(c) && OutsideExtension(c));
        if (outside != null)
          return outside;

        return candidates.FirstOrDefault(predicate);
      }

      var workspaceCandidate = Pick(c => Exists(c) && c.IsWorkspaceRoot);
      if (workspaceCandidate != null)
        return workspaceCandidate;

      var solutionSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {
        "SolutionReadyHint",
        "IVsSolution.GetSolutionInfo.Directory",
        "IVsSolution.VSPROPID_SolutionDirectory",
        "IVsSolution.GetSolutionInfo.FileDir",
        "IVsSolution.GetSolutionRootDirectory",
        "DTE.Solution.FullName",
        "DTE.Solution.FileName",
        "DTE.Solution.Properties.Path",
        "VS.Solutions.Current.FullPath"
      };

      var solutionCandidate = Pick(c => Exists(c) && solutionSources.Contains(c.Source));
      if (solutionCandidate != null)
        return solutionCandidate;

      var selectionCandidate = Pick(c => Exists(c) &&
        (c.Source.StartsWith("VS.Solutions.ActiveItem", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(c.Source, "VS.Solutions.ActiveProject", StringComparison.OrdinalIgnoreCase)));
      if (selectionCandidate != null)
        return selectionCandidate;

      var activeDocumentCandidate = Pick(c => Exists(c) &&
        string.Equals(c.Source, "DTE.ActiveDocument", StringComparison.OrdinalIgnoreCase));
      if (activeDocumentCandidate != null)
        return activeDocumentCandidate;

      var existingOutside = candidates.FirstOrDefault(c => Exists(c) && OutsideExtension(c));
      if (existingOutside != null)
        return existingOutside;

      var existing = candidates.FirstOrDefault(Exists);
      if (existing != null)
        return existing;

      return candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c.Path));
    }

    private static int CalculatePathDepth(string path)
    {
      if (string.IsNullOrEmpty(path))
        return -1;

      return path.Count(ch => ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar);
    }

    private static bool DirectoryContainsFiles(string directory, params string[] patterns)
    {
      if (string.IsNullOrEmpty(directory) || patterns == null || patterns.Length == 0)
        return false;

      try
      {
        foreach (var pattern in patterns)
        {
          if (Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).Any())
            return true;
        }
      }
      catch
      {
        // ignore IO issues
      }

      return false;
    }

    private static string GetActiveSolutionFullName(DTE2 dte)
    {
      try { return dte?.Solution?.FullName ?? string.Empty; }
      catch { return string.Empty; }
    }

    private static string GetActiveSolutionFileName(DTE2 dte)
    {
      try { return dte?.Solution?.FileName ?? string.Empty; }
      catch { return string.Empty; }
    }

    private static string GetActiveDocumentDirectory(DTE2 dte)
    {
      try
      {
        var fullName = dte?.ActiveDocument?.FullName;
        if (string.IsNullOrWhiteSpace(fullName))
          return string.Empty;

        if (Directory.Exists(fullName))
          return NormalizeDirectory(fullName);

        if (File.Exists(fullName))
          return NormalizeDirectory(Path.GetDirectoryName(fullName) ?? string.Empty);

        return NormalizeDirectory(Path.GetDirectoryName(fullName) ?? string.Empty);
      }
      catch
      {
        return string.Empty;
      }
    }

    private static async Task<SolutionItem> SafeGetCurrentSolutionAsync()
    {
      try
      {
        return await VS.Solutions.GetCurrentSolutionAsync();
      }
      catch
      {
        return null;
      }
    }

    private static async Task<SolutionItem> GetActiveProjectAsync()
    {
      try
      {
        var items = await VS.Solutions.GetActiveItemsAsync();
        if (items != null)
        {
          foreach (var item in items)
          {
            if (item == null)
              continue;

            if (item.Type == SolutionItemType.Project ||
                item.Type == SolutionItemType.PhysicalFolder ||
                item.Type == SolutionItemType.Solution)
              return item;
          }
        }
      }
      catch
      {
      }

      return null;
    }

    private static async Task<IReadOnlyList<SolutionItem>> GetActiveSolutionItemsAsync()
    {
      try
      {
        var items = await VS.Solutions.GetActiveItemsAsync();
        var list = items?.ToList();
        return list != null && list.Count > 0 ? list : Array.Empty<SolutionItem>();
      }
      catch
      {
        return Array.Empty<SolutionItem>();
      }
    }

    private static string GetSolutionItemPath(SolutionItem item)
    {
      if (item == null)
        return string.Empty;

      var path = TryGetSolutionItemFullPath(item);
      if (!string.IsNullOrEmpty(path))
        return GetDirectoryFromFile(path);

      var parent = TryGetSolutionItemParent(item);
      while (parent != null)
      {
        path = TryGetSolutionItemFullPath(parent);
        if (!string.IsNullOrEmpty(path))
          return GetDirectoryFromFile(path);
        parent = TryGetSolutionItemParent(parent);
      }

      foreach (var child in TryGetSolutionItemChildren(item))
      {
        path = TryGetSolutionItemFullPath(child);
        if (!string.IsNullOrEmpty(path))
          return GetDirectoryFromFile(path);
      }

      return string.Empty;
    }

    private static string TryGetSolutionItemFullPath(SolutionItem item)
    {
      if (item == null)
        return string.Empty;

      try
      {
        var value = item.FullPath;
        if (!string.IsNullOrWhiteSpace(value))
          return value;
      }
      catch
      {
      }

      try
      {
        var type = item.GetType();
        var prop = type.GetProperty("FullPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop?.GetValue(item) is string alt && !string.IsNullOrWhiteSpace(alt))
          return alt;

        var physicalProp = type.GetProperty("PhysicalPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (physicalProp?.GetValue(item) is string physical && !string.IsNullOrWhiteSpace(physical))
          return physical;
      }
      catch
      {
      }

      return string.Empty;
    }

    private static SolutionItem TryGetSolutionItemParent(SolutionItem item)
    {
      if (item == null)
        return null;

      try { return item.Parent; }
      catch { }

      try
      {
        var prop = item.GetType().GetProperty("Parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop?.GetValue(item) is SolutionItem parent)
          return parent;
      }
      catch
      {
      }

      return null;
    }

    private static IEnumerable<SolutionItem> TryGetSolutionItemChildren(SolutionItem item)
    {
      if (item == null)
        return Array.Empty<SolutionItem>();

      var results = new List<SolutionItem>();
      try
      {
        var childrenProp = item.GetType().GetProperty("Children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (childrenProp != null && childrenProp.GetValue(item) is IEnumerable enumerable)
        {
          foreach (var childObj in enumerable)
          {
            if (childObj is SolutionItem child && child != null)
              results.Add(child);
          }
        }
      }
      catch
      {
      }

      return results.Count > 0 ? results : Array.Empty<SolutionItem>();
    }
  }
}
