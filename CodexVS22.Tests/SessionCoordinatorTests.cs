using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodexVS22.Core.Cli;
using CodexVS22.Core.State;
using CodexVS22.Shared.Cli;
using CodexVS22.Shared.Options;

namespace CodexVS22.Tests
{
  internal static partial class Program
  {
    private static void SessionCoordinator_TracksHostStateAndResetsOnOptionChange()
    {
      var host = new StubCliHost();
      var router = new DefaultCliMessageRouter();
      var optionsProvider = new MutableOptionsProvider();
      var serializer = new CliSubmissionFactory();

      using var service = new CliSessionService(host, router, serializer, optionsProvider);
      var sessionStore = new CodexSessionStore();
      var workspaceStore = new WorkspaceContextStore();
      var optionsCache = new OptionsCache();

      using var coordinator = new CodexSessionCoordinator(host, service, router, optionsProvider, sessionStore, workspaceStore, optionsCache);
      coordinator.Initialize();

      var connect = service.ConnectAsync(new CodexOptions(), Environment.CurrentDirectory, CancellationToken.None)
        .GetAwaiter().GetResult();
      AssertTrue(connect.IsSuccess, "Connect should succeed");

      host.EmitState(CliHostState.Connected);
      AssertTrue(sessionStore.Current.HostState == CliHostState.Connected, "Host state should propagate");

      router.RouteAsync(CliEnvelope.FromRaw("{\"event\":{\"type\":\"workspace_ready\"}}"))
        .GetAwaiter().GetResult();
      AssertEqual(1, sessionStore.Current.LastEnvelope.Count, "Envelope count should increment");
      AssertEqual("workspace_ready", sessionStore.Current.LastEnvelope.EventType, "Envelope type should track");

      host.EmitDiagnostic(CliDiagnostic.Info("Test", "diagnostic-line"));
      var afterDiagnostic = sessionStore.Current;
      var diagnosticSummary = afterDiagnostic.LastDiagnostic;
      if (!diagnosticSummary.HasValue)
        throw new InvalidOperationException("Diagnostic should record");
      AssertEqual("Test", diagnosticSummary.Value.Category, "Diagnostic category mismatch");

      var beforeOptionsSession = afterDiagnostic.SessionId;
      var globalCli = Path.Combine(Environment.CurrentDirectory, "global-codex.exe");
      var solutionCli = Path.Combine(Environment.CurrentDirectory, "solution-codex.exe");
      optionsProvider.SetOptions(new CodexOptions
      {
        CliExecutable = globalCli,
        SolutionCliExecutable = solutionCli,
        UseWsl = false,
        SolutionUseWsl = true,
        DefaultModel = "gpt-test"
      });
      optionsProvider.NotifyChanged();

      var expectedEffective = Path.GetFullPath(solutionCli);
      AssertEqual(expectedEffective, optionsCache.Current.EffectiveCliExecutable, "Effective CLI path mismatch");
      AssertTrue(optionsCache.Current.EffectiveUseWsl, "Effective UseWsl should honor override");
      AssertTrue(optionsCache.Current.HasSolutionOverrides, "Options cache should mark solution overrides");
      AssertTrue(sessionStore.Current.SessionId != beforeOptionsSession, "Session should reset after options change");

      var beforeWorkspaceSession = sessionStore.Current.SessionId;
      var workspaceRoot = Path.Combine(Environment.CurrentDirectory, "repo-workspace");
      var solutionPath = Path.Combine(workspaceRoot, "solution.sln");
      coordinator.ApplyWorkspace(solutionPath, workspaceRoot, WorkspaceTransitionKind.SolutionLoaded);
      AssertTrue(sessionStore.Current.SessionId != beforeWorkspaceSession, "Session should reset after workspace change");
      AssertTrue(workspaceStore.Current.HasSolution, "Workspace should report solution present");
      AssertEqual(Path.GetFullPath(solutionPath), workspaceStore.Current.SolutionPath, "Workspace solution path should normalize");
    }

    private static void WorkspaceContextStore_NormalizesPathsAndRaisesEvents()
    {
      var store = new WorkspaceContextStore();
      var transitions = new List<WorkspaceTransitionKind>();
      store.WorkspaceChanged += (_, args) => transitions.Add(args.Transition);

      var workspace = Path.Combine(Path.GetTempPath(), "codex-workspace");
      var solutionPath = Path.Combine(workspace, "project.sln");
      store.UpdateWorkspace(solutionPath, workspace, WorkspaceTransitionKind.SolutionLoaded);

      AssertTrue(store.Current.HasWorkspace, "Workspace root should be set");
      AssertTrue(store.Current.HasSolution, "Solution path should be set");
      AssertEqual(Path.GetFullPath(solutionPath), store.Current.SolutionPath, "Solution path should normalize");
      AssertEqual(Path.GetFullPath(workspace), store.Current.WorkspaceRoot, "Workspace path should normalize");
      AssertEqual(1, transitions.Count, "Should raise a single change event");
      AssertTrue(transitions[0] == WorkspaceTransitionKind.SolutionLoaded, "Transition kind mismatch");

      store.Reset();
      AssertTrue(store.Current.SolutionPath.Length == 0, "Reset should clear solution path");
      AssertTrue(store.Current.WorkspaceRoot.Length == 0, "Reset should clear workspace path");
      AssertEqual(2, transitions.Count, "Reset should raise second event");
      AssertTrue(transitions[^1] == WorkspaceTransitionKind.Reset, "Reset event should indicate reset transition");
    }

    private static void OptionsCache_DerivesEffectiveValues()
    {
      var cache = new OptionsCache();
      OptionsCacheSnapshot? observed = null;
      cache.OptionsChanged += (_, args) => observed = args.Current;

      var globalOptionPath = Path.Combine(Environment.CurrentDirectory, "global-cli.exe");
      var solutionOptionPath = Path.Combine(Environment.CurrentDirectory, "solution-cli.exe");
      var options = new CodexOptions
      {
        CliExecutable = globalOptionPath,
        SolutionCliExecutable = solutionOptionPath,
        UseWsl = false,
        SolutionUseWsl = true,
        DefaultModel = "gpt-override",
        DefaultReasoning = "high",
        AutoOpenPatchedFiles = false,
        AutoHideExecConsole = true,
        ExecConsoleVisible = true,
        ExecConsoleHeight = 420,
        ExecOutputBufferLimit = 9000,
        LastUsedTool = "lint",
        LastUsedPrompt = "refactor"
      };

      cache.Update(options);

      AssertTrue(observed is not null, "Options cache should raise change event");
      AssertEqual(Path.GetFullPath(solutionOptionPath), cache.Current.EffectiveCliExecutable, "Effective CLI mismatch");
      AssertTrue(cache.Current.EffectiveUseWsl, "Effective WSL override expected");
      AssertEqual("gpt-override", cache.Current.DefaultModel, "Default model mismatch");
      AssertTrue(cache.Current.HasSolutionOverrides, "Should report solution overrides");

      cache.Reset();
      AssertTrue(cache.Current.HasSolutionOverrides == false, "Reset should clear overrides flag");
      AssertTrue(string.IsNullOrEmpty(cache.Current.EffectiveCliExecutable), "Reset should clear effective CLI path");
    }

    private sealed class MutableOptionsProvider : ICodexOptionsProvider
    {
      private CodexOptions _options = new();

      public event EventHandler OptionsChanged;

      public CodexOptions GetCurrentOptions()
      {
        return _options;
      }

      public void SetOptions(CodexOptions options)
      {
        _options = options ?? new CodexOptions();
      }

      public void NotifyChanged()
      {
        OptionsChanged?.Invoke(this, EventArgs.Empty);
      }
    }

    private sealed class StubCliHost : ICodexCliHost
    {
      public CliHostState State { get; private set; } = CliHostState.Stopped;

      public event EventHandler<CliHostStateChangedEventArgs> StateChanged;

      public event EventHandler<CliEnvelope> EnvelopeReceived;

      public event EventHandler<CliDiagnostic> DiagnosticReceived;

      public Task<CliConnectionResult> ConnectAsync(CliConnectionRequest request, CancellationToken cancellationToken)
      {
        EmitState(CliHostState.Connected);
        return Task.FromResult(CliConnectionResult.Success);
      }

      public Task DisconnectAsync(CancellationToken cancellationToken)
      {
        EmitState(CliHostState.Stopped);
        return Task.CompletedTask;
      }

      public Task<bool> SendAsync(string payload, CancellationToken cancellationToken = default)
      {
        return Task.FromResult(!string.IsNullOrWhiteSpace(payload));
      }

      public Task<CodexAuthenticationResult> CheckAuthenticationAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(new CodexAuthenticationResult(true, string.Empty));
      }

      public Task<bool> LoginAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(true);
      }

      public Task<bool> LogoutAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(true);
      }

      public Task<CliHeartbeatInfo> EnsureHeartbeatAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(CliHeartbeatInfo.Empty);
      }

      public Task<CliHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(new CliHealthSnapshot(0, TimeSpan.Zero, State));
      }

      public void EmitState(CliHostState state)
      {
        State = state;
        StateChanged?.Invoke(this, new CliHostStateChangedEventArgs(state));
      }

      public void EmitDiagnostic(CliDiagnostic diagnostic)
      {
        DiagnosticReceived?.Invoke(this, diagnostic);
      }

      public void Dispose()
      {
      }
    }
  }
}
