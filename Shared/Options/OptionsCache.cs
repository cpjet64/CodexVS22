using System;
using System.IO;
using global::CodexVS22;

namespace CodexVS22.Shared.Options
{
    /// <summary>
    /// Caches the latest Codex options snapshot with derived effective values for downstream consumers.
    /// </summary>
    public sealed class OptionsCache : IOptionsCache
    {
        private readonly object _gate = new();
        private OptionsCacheSnapshot _snapshot = OptionsCacheSnapshot.CreateInitial();

        public event EventHandler<OptionsCacheChangedEventArgs> OptionsChanged;

        public OptionsCacheSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    return _snapshot;
                }
            }
        }

        public void Update(CodexOptions options)
        {
            var snapshot = OptionsCacheSnapshot.FromOptions(options ?? new CodexOptions(), DateTimeOffset.UtcNow);
            Apply(snapshot, OptionsChangeKind.Refreshed);
        }

        public void Reset()
        {
            Apply(OptionsCacheSnapshot.CreateInitial(), OptionsChangeKind.Reset);
        }

        private void Apply(OptionsCacheSnapshot next, OptionsChangeKind kind)
        {
            OptionsCacheSnapshot previous;
            bool changed;
            lock (_gate)
            {
                previous = _snapshot;
                changed = !Equals(previous, next);
                if (!changed)
                    return;

                _snapshot = next;
            }

            OptionsChanged?.Invoke(this, new OptionsCacheChangedEventArgs(previous, next, kind));
        }
    }

    public interface IOptionsCache
    {
        event EventHandler<OptionsCacheChangedEventArgs> OptionsChanged;

        OptionsCacheSnapshot Current { get; }

        void Update(CodexOptions options);

        void Reset();
    }

    public sealed class OptionsCacheChangedEventArgs : EventArgs
    {
        public OptionsCacheChangedEventArgs(OptionsCacheSnapshot previous, OptionsCacheSnapshot current, OptionsChangeKind kind)
        {
            Previous = previous;
            Current = current;
            Kind = kind;
        }

        public OptionsCacheSnapshot Previous { get; }

        public OptionsCacheSnapshot Current { get; }

        public OptionsChangeKind Kind { get; }
    }

    public enum OptionsChangeKind
    {
        Unknown,
        Refreshed,
        Reset
    }

    public sealed record OptionsCacheSnapshot
    {
        public Guid Version { get; init; } = Guid.NewGuid();

        public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

        public string CliExecutable { get; init; } = string.Empty;

        public bool UseWsl { get; init; }

        public bool OpenOnStartup { get; init; }

        public CodexOptions.ApprovalMode ApprovalMode { get; init; } = CodexOptions.ApprovalMode.Chat;

        public CodexOptions.SandboxPolicyMode SandboxPolicy { get; init; } = CodexOptions.SandboxPolicyMode.Moderate;

        public string DefaultModel { get; init; } = string.Empty;

        public string DefaultReasoning { get; init; } = string.Empty;

        public bool AutoOpenPatchedFiles { get; init; }

        public bool AutoHideExecConsole { get; init; }

        public bool ExecConsoleVisible { get; init; } = true;

        public double ExecConsoleHeight { get; init; }

        public int ExecOutputBufferLimit { get; init; }

        public double WindowWidth { get; init; }

        public double WindowHeight { get; init; }

        public double WindowLeft { get; init; } = double.NaN;

        public double WindowTop { get; init; } = double.NaN;

        public string WindowState { get; init; } = "Normal";

        public string LastUsedTool { get; init; } = string.Empty;

        public string LastUsedPrompt { get; init; } = string.Empty;

        public string SolutionCliExecutable { get; init; } = string.Empty;

        public bool? SolutionUseWsl { get; init; }

        public string EffectiveCliExecutable { get; init; } = string.Empty;

        public bool EffectiveUseWsl { get; init; }

        public bool HasSolutionOverrides { get; init; }

        public static OptionsCacheSnapshot CreateInitial()
        {
            return FromOptions(new CodexOptions(), DateTimeOffset.UtcNow);
        }

        public static OptionsCacheSnapshot FromOptions(CodexOptions source, DateTimeOffset capturedAt)
        {
            if (source == null)
                source = new CodexOptions();

            static string TrimOrEmpty(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

            var cliExecutable = NormalizePath(TrimOrEmpty(source.CliExecutable));
            var solutionExecutable = NormalizePath(TrimOrEmpty(source.SolutionCliExecutable));
            var effectiveExecutable = string.IsNullOrEmpty(solutionExecutable) ? cliExecutable : solutionExecutable;
            var effectiveUseWsl = source.SolutionUseWsl ?? source.UseWsl;

            return new OptionsCacheSnapshot
            {
                Version = Guid.NewGuid(),
                CapturedAt = capturedAt,
                CliExecutable = cliExecutable,
                UseWsl = source.UseWsl,
                OpenOnStartup = source.OpenOnStartup,
                ApprovalMode = source.Mode,
                SandboxPolicy = source.SandboxPolicy,
                DefaultModel = TrimOrEmpty(source.DefaultModel),
                DefaultReasoning = TrimOrEmpty(source.DefaultReasoning),
                AutoOpenPatchedFiles = source.AutoOpenPatchedFiles,
                AutoHideExecConsole = source.AutoHideExecConsole,
                ExecConsoleVisible = source.ExecConsoleVisible,
                ExecConsoleHeight = source.ExecConsoleHeight,
                ExecOutputBufferLimit = source.ExecOutputBufferLimit,
                WindowWidth = source.WindowWidth,
                WindowHeight = source.WindowHeight,
                WindowLeft = source.WindowLeft,
                WindowTop = source.WindowTop,
                WindowState = TrimOrEmpty(source.WindowState),
                LastUsedTool = TrimOrEmpty(source.LastUsedTool),
                LastUsedPrompt = TrimOrEmpty(source.LastUsedPrompt),
                SolutionCliExecutable = solutionExecutable,
                SolutionUseWsl = source.SolutionUseWsl,
                EffectiveCliExecutable = effectiveExecutable,
                EffectiveUseWsl = effectiveUseWsl,
                HasSolutionOverrides = !string.IsNullOrEmpty(solutionExecutable) || source.SolutionUseWsl.HasValue
            };
        }

        private static string NormalizePath(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                return Path.GetFullPath(value);
            }
            catch
            {
                return value;
            }
        }
    }
}
