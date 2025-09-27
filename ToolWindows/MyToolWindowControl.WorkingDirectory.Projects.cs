using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using DteProject = EnvDTE.Project;
using DteProjects = EnvDTE.Projects;
using DteProjectItem = EnvDTE.ProjectItem;
using DteProjectItems = EnvDTE.ProjectItems;
using DteSolution = EnvDTE.Solution;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private static IEnumerable<DteProject> EnumerateProjects(DteSolution solution)
    {
      if (solution == null)
        yield break;

      DteProjects projects = null;
      try { projects = solution.Projects; }
      catch { }

      if (projects == null)
        yield break;

      foreach (DteProject project in projects)
      {
        if (project == null)
          continue;

        yield return project;

        foreach (var nested in EnumerateSubProjects(project))
          yield return nested;
      }
    }

    private static IEnumerable<DteProject> EnumerateSubProjects(DteProject project)
    {
      if (project == null)
        yield break;

      DteProjectItems items = null;
      try { items = project.ProjectItems; }
      catch { }

      if (items == null)
        yield break;

      foreach (DteProjectItem item in items)
      {
        DteProject subProject = null;
        try { subProject = item.SubProject; }
        catch { }

        if (subProject != null)
        {
          yield return subProject;

          foreach (var nested in EnumerateSubProjects(subProject))
            yield return nested;
        }
      }
    }

    private static string GetProjectDirectory(DteProject project)
    {
      if (project == null)
        return string.Empty;

      try
      {
        var fullName = project.FullName;
        if (!string.IsNullOrWhiteSpace(fullName))
        {
          if (Directory.Exists(fullName))
            return NormalizeDirectory(fullName);

          if (File.Exists(fullName))
            return NormalizeDirectory(Path.GetDirectoryName(fullName) ?? string.Empty);
        }
      }
      catch
      {
      }

      foreach (var propertyName in new[] { "FullPath", "ProjectHome", "ProjectDir" })
      {
        var candidate = GetProjectProperty(project, propertyName);
        if (!string.IsNullOrWhiteSpace(candidate))
          return NormalizeDirectory(candidate);
      }

      return string.Empty;
    }

    private static string GetProjectProperty(DteProject project, string propertyName)
    {
      if (project?.Properties == null || string.IsNullOrEmpty(propertyName))
        return string.Empty;

      try
      {
        var property = project.Properties.Item(propertyName);
        if (property?.Value is string value && !string.IsNullOrWhiteSpace(value))
          return value;
      }
      catch (ArgumentException)
      {
        // property not available
      }
      catch (COMException)
      {
      }
      catch
      {
      }

      return string.Empty;
    }

    private static string SafeGetProjectName(DteProject project)
    {
      try { return project?.Name ?? string.Empty; }
      catch { return string.Empty; }
    }


    private static void AddProjectDirectoryCandidates(List<CandidateSeed> list, DTE2 dte)
    {
      if (list == null)
        return;

      var index = 0;
      foreach (var (name, path) in EnumerateSolutionProjectDirectories(dte))
      {
        var localPath = path;
        var label = string.IsNullOrEmpty(name) ? $"DTE.Project[{index++}]" : $"DTE.Project:{name}";
        TryAddCandidate(list, label, () => localPath);
      }
    }

    private static IEnumerable<(string Name, string Path)> EnumerateSolutionProjectDirectories(DTE2 dte)
    {
      if (dte?.Solution == null)
        yield break;

      foreach (var project in EnumerateProjects(dte.Solution))
      {
        var path = GetProjectDirectory(project);
        if (!string.IsNullOrEmpty(path))
          yield return (SafeGetProjectName(project), path);
      }
    }
  }
}






