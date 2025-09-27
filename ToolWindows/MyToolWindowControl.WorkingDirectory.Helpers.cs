using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private static string GetDirectoryFromFile(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return string.Empty;

      try
      {
        if (Directory.Exists(path))
          return NormalizeDirectory(path);

        if (File.Exists(path))
        {
          var fileDirectory = Path.GetDirectoryName(path);
          return string.IsNullOrEmpty(fileDirectory) ? string.Empty : NormalizeDirectory(fileDirectory);
        }

        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(directory) ? string.Empty : NormalizeDirectory(directory);
      }
      catch
      {
        return string.Empty;
      }
    }

    private static string NormalizeDirectory(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return string.Empty;

      try
      {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      }
      catch
      {
        return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      }
    }

    private static bool PathsEqual(string left, string right)
    {
      var normalizedLeft = NormalizeDirectory(left);
      var normalizedRight = NormalizeDirectory(right);
      return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string TryFindSolutionDirectory(string start)
    {
      if (string.IsNullOrWhiteSpace(start))
        return string.Empty;

      try
      {
        var current = NormalizeDirectory(start);
        var depth = 0;
        while (!string.IsNullOrEmpty(current) && Directory.Exists(current) && depth < 6)
        {
          if (Directory.EnumerateFiles(current, "*.sln").Any())
            return current;

          var parent = Path.GetDirectoryName(current);
          if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            break;

          current = NormalizeDirectory(parent);
          depth++;
        }
      }
      catch
      {
        // ignore and fall back to the provided directory
      }

      return string.Empty;
    }

    private static WorkingDirectoryCandidate AnalyzeCandidate(CandidateSeed seed)
    {
      if (string.IsNullOrWhiteSpace(seed.Path) || seed.Path.StartsWith("<", StringComparison.Ordinal))
        return new WorkingDirectoryCandidate(seed.Source ?? string.Empty, string.Empty, false, false, false, -1, seed.IsWorkspaceRoot);

      var normalized = NormalizeDirectory(seed.Path);
      var exists = !string.IsNullOrEmpty(normalized) && Directory.Exists(normalized);
      var hasSolution = exists && DirectoryContainsFiles(normalized, "*.sln");
      var hasProject = exists && DirectoryContainsFiles(normalized, "*.csproj", "*.vbproj", "*.fsproj", "*.vcxproj", "*.vcproj");
      var depth = CalculatePathDepth(normalized);
      return new WorkingDirectoryCandidate(seed.Source ?? string.Empty, normalized, exists, hasSolution, hasProject, depth, seed.IsWorkspaceRoot);
    }

    private static void TryAddCandidate(List<CandidateSeed> list, string source, Func<string> resolver, bool isWorkspaceRoot = false)
    {
      if (list == null)
        return;

      string result;
      try
      {
        result = resolver?.Invoke() ?? string.Empty;
      }
      catch (COMException ex)
      {
        result = $"<COMException:{ex.ErrorCode:X8}>";
      }
      catch (Exception ex)
      {
        result = $"<Exception:{ex.GetType().Name}>";
      }

      list.Add(new CandidateSeed(source, result ?? string.Empty, isWorkspaceRoot));
    }

    private static string SafeInvoke(Func<string> resolver)
    {
      try
      {
        return resolver?.Invoke() ?? string.Empty;
      }
      catch (COMException ex)
      {
        return $"<COMException:{ex.ErrorCode:X8}>";
      }
      catch (Exception ex)
      {
        return $"<Exception:{ex.GetType().Name}>";
      }
    }

    private static async Task<string> SafeInvokeAsync(Func<Task<string>> resolver)
    {
      if (resolver == null)
        return string.Empty;

      try
      {
        return await resolver().ConfigureAwait(true) ?? string.Empty;
      }
      catch (COMException ex)
      {
        return $"<COMException:{ex.ErrorCode:X8}>";
      }
      catch (Exception ex)
      {
        return $"<Exception:{ex.GetType().Name}>";
      }
    }

    private static async Task<(string, string)> SafeInvokeTupleAsync(Func<Task<(string, string)>> resolver)
    {
      if (resolver == null)
        return (string.Empty, string.Empty);

      try
      {
        var result = await resolver().ConfigureAwait(true);
        return (result.Item1 ?? string.Empty, result.Item2 ?? string.Empty);
      }
      catch (COMException ex)
      {
        var marker = $"<COMException:{ex.ErrorCode:X8}>";
        return (marker, marker);
      }
      catch (Exception ex)
      {
        var marker = $"<Exception:{ex.GetType().Name}>";
        return (marker, marker);
      }
    }


  }
}
