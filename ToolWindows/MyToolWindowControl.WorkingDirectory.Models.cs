using System;
using System.Collections.Generic;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private sealed class CandidateSeed
    {
      public CandidateSeed(string source, string path, bool isWorkspaceRoot)
      {
        Source = source ?? string.Empty;
        Path = path ?? string.Empty;
        IsWorkspaceRoot = isWorkspaceRoot;
      }

      public string Source { get; }

      public string Path { get; }

      public bool IsWorkspaceRoot { get; }
    }

    private sealed class WorkingDirectoryCandidate
    {
      public WorkingDirectoryCandidate(string source, string path, bool exists, bool hasSolution,
        bool hasProject, int depth, bool isWorkspaceRoot)
      {
        Source = source ?? string.Empty;
        Path = path ?? string.Empty;
        Exists = exists;
        HasSolution = hasSolution;
        HasProject = hasProject;
        Depth = depth;
        IsWorkspaceRoot = isWorkspaceRoot;
        IsInsideExtension = IsInsideExtensionRoot(path);
      }

      public string Source { get; }

      public string Path { get; }

      public bool Exists { get; }

      public bool HasSolution { get; }

      public bool HasProject { get; }

      public int Depth { get; }

      public bool IsWorkspaceRoot { get; }

      public bool IsInsideExtension { get; }
    }

    private sealed class WorkingDirectoryResolution
    {
      public WorkingDirectoryResolution(WorkingDirectoryCandidate selected,
        IReadOnlyList<WorkingDirectoryCandidate> candidates)
      {
        Selected = selected;
        Candidates = candidates ?? Array.Empty<WorkingDirectoryCandidate>();
      }

      public WorkingDirectoryCandidate Selected { get; }

      public IReadOnlyList<WorkingDirectoryCandidate> Candidates { get; }
    }
  }
}
