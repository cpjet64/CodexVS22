using System;
using System.IO;

namespace CodexVS22.Core.State
{
    /// <summary>
    /// Stores the active workspace (solution or folder) context in a threadsafe observable snapshot.
    /// </summary>
    public sealed class WorkspaceContextStore : IWorkspaceContextStore
    {
        private readonly object _gate = new();
        private WorkspaceContextSnapshot _snapshot = WorkspaceContextSnapshot.CreateInitial();

        public event EventHandler<WorkspaceContextChangedEventArgs> WorkspaceChanged;

        public WorkspaceContextSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    return _snapshot;
                }
            }
        }

        public void UpdateWorkspace(string solutionPath, string workspaceRoot, WorkspaceTransitionKind transitionKind = WorkspaceTransitionKind.Unknown)
        {
            var normalizedSolution = NormalizePath(solutionPath);
            var normalizedWorkspace = NormalizePath(workspaceRoot);
            var now = DateTimeOffset.UtcNow;

            Apply(
                snapshot =>
                {
                    if (snapshot.SolutionPath.Equals(normalizedSolution, StringComparison.OrdinalIgnoreCase) &&
                        snapshot.WorkspaceRoot.Equals(normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
                    {
                        return snapshot;
                    }

                    return snapshot with
                    {
                        SolutionPath = normalizedSolution,
                        WorkspaceRoot = normalizedWorkspace,
                        UpdatedAt = now,
                        UpdateId = Guid.NewGuid()
                    };
                },
                transitionKind);
        }

        public void Reset(WorkspaceTransitionKind transitionKind = WorkspaceTransitionKind.Reset)
        {
            Apply(_ => WorkspaceContextSnapshot.CreateInitial(), transitionKind);
        }

        private void Apply(Func<WorkspaceContextSnapshot, WorkspaceContextSnapshot> mutator, WorkspaceTransitionKind transitionKind)
        {
            if (mutator == null)
                throw new ArgumentNullException(nameof(mutator));

            WorkspaceContextSnapshot previous;
            WorkspaceContextSnapshot updated;
            bool changed;
            lock (_gate)
            {
                previous = _snapshot;
                updated = mutator(previous);
                changed = !Equals(updated, previous);
                if (!changed)
                    return;

                _snapshot = updated;
            }

            WorkspaceChanged?.Invoke(this, new WorkspaceContextChangedEventArgs(previous, updated, transitionKind));
        }

        private static string NormalizePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var trimmed = value.Trim();
            try
            {
                return Path.GetFullPath(trimmed);
            }
            catch
            {
                return trimmed;
            }
        }
    }

    public interface IWorkspaceContextStore
    {
        event EventHandler<WorkspaceContextChangedEventArgs> WorkspaceChanged;

        WorkspaceContextSnapshot Current { get; }

        void UpdateWorkspace(string solutionPath, string workspaceRoot, WorkspaceTransitionKind transitionKind = WorkspaceTransitionKind.Unknown);

        void Reset(WorkspaceTransitionKind transitionKind = WorkspaceTransitionKind.Reset);
    }

    public sealed class WorkspaceContextChangedEventArgs : EventArgs
    {
        public WorkspaceContextChangedEventArgs(WorkspaceContextSnapshot previous, WorkspaceContextSnapshot current, WorkspaceTransitionKind transitionKind)
        {
            Previous = previous;
            Current = current;
            Transition = transitionKind;
        }

        public WorkspaceContextSnapshot Previous { get; }

        public WorkspaceContextSnapshot Current { get; }

        public WorkspaceTransitionKind Transition { get; }
    }

    public enum WorkspaceTransitionKind
    {
        Unknown,
        Initialized,
        SolutionLoaded,
        SolutionClosed,
        FolderOpened,
        FolderClosed,
        WorkspaceChanged,
        Reset
    }

    public sealed record WorkspaceContextSnapshot
    {
        public Guid UpdateId { get; init; } = Guid.NewGuid();

        public string SolutionPath { get; init; } = string.Empty;

        public string WorkspaceRoot { get; init; } = string.Empty;

        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

        public bool HasSolution => !string.IsNullOrEmpty(SolutionPath);

        public bool HasWorkspace => !string.IsNullOrEmpty(WorkspaceRoot);

        public string ActiveRoot => HasWorkspace ? WorkspaceRoot : SolutionPath;

        public static WorkspaceContextSnapshot CreateInitial()
        {
            return new WorkspaceContextSnapshot
            {
                UpdateId = Guid.NewGuid(),
                SolutionPath = string.Empty,
                WorkspaceRoot = string.Empty,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
