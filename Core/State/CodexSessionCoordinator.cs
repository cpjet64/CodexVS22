using System;
using CodexVS22.Core.Cli;
using CodexVS22.Shared.Cli;
using CodexVS22.Shared.Options;

namespace CodexVS22.Core.State
{
    /// <summary>
    /// Coordinates workspace, options, and CLI session state between foundational services.
    /// </summary>
    public sealed class CodexSessionCoordinator : ICodexSessionCoordinator, IDisposable
    {
        private readonly ICodexCliHost _host;
        private readonly CliSessionService _sessionService;
        private readonly ICliMessageRouter _router;
        private readonly ICodexOptionsProvider _optionsProvider;
        private readonly ICodexSessionStore _sessionStore;
        private readonly IWorkspaceContextStore _workspaceStore;
        private readonly IOptionsCache _optionsCache;
        private readonly object _gate = new();
        private bool _initialized;

        public CodexSessionCoordinator(
            ICodexCliHost host,
            CliSessionService sessionService,
            ICliMessageRouter router,
            ICodexOptionsProvider optionsProvider,
            ICodexSessionStore sessionStore,
            IWorkspaceContextStore workspaceStore,
            IOptionsCache optionsCache)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _workspaceStore = workspaceStore ?? throw new ArgumentNullException(nameof(workspaceStore));
            _optionsCache = optionsCache ?? throw new ArgumentNullException(nameof(optionsCache));
        }

        public ICodexSessionStore SessionStore => _sessionStore;

        public IWorkspaceContextStore WorkspaceStore => _workspaceStore;

        public IOptionsCache OptionsCache => _optionsCache;

        public void Initialize()
        {
            lock (_gate)
            {
                if (_initialized)
                    return;

                _host.StateChanged += OnHostStateChanged;
                _router.EnvelopeReceived += OnEnvelopeReceived;
                _sessionService.DiagnosticReceived += OnDiagnosticReceived;
                _optionsProvider.OptionsChanged += OnOptionsChanged;
                _workspaceStore.WorkspaceChanged += OnWorkspaceChanged;

                _initialized = true;
            }

            RefreshOptions();
        }

        public void RefreshOptions()
        {
            var options = _optionsProvider.GetCurrentOptions();
            _optionsCache.Update(options);
        }

        public void ApplyWorkspace(string solutionPath, string workspaceRoot, WorkspaceTransitionKind transitionKind = WorkspaceTransitionKind.WorkspaceChanged)
        {
            _workspaceStore.UpdateWorkspace(solutionPath, workspaceRoot, transitionKind);
        }

        public void ResetSession(SessionResetReason reason = SessionResetReason.Manual)
        {
            _sessionStore.Reset(reason);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (!_initialized)
                    return;

                _initialized = false;
                _host.StateChanged -= OnHostStateChanged;
                _router.EnvelopeReceived -= OnEnvelopeReceived;
                _sessionService.DiagnosticReceived -= OnDiagnosticReceived;
                _optionsProvider.OptionsChanged -= OnOptionsChanged;
                _workspaceStore.WorkspaceChanged -= OnWorkspaceChanged;
            }
        }

        private void OnHostStateChanged(object sender, CliHostStateChangedEventArgs e)
        {
            _sessionStore.UpdateHostState(e.State);
        }

        private void OnEnvelopeReceived(object sender, CliEnvelope envelope)
        {
            _sessionStore.RecordEnvelope(envelope);
        }

        private void OnDiagnosticReceived(object sender, CliDiagnostic diagnostic)
        {
            _sessionStore.RecordDiagnostic(diagnostic);
        }

        private void OnOptionsChanged(object sender, EventArgs e)
        {
            RefreshOptions();
            _sessionStore.Reset(SessionResetReason.OptionsChanged);
        }

        private void OnWorkspaceChanged(object sender, WorkspaceContextChangedEventArgs e)
        {
            _sessionStore.Reset(SessionResetReason.WorkspaceChanged);
            RefreshOptions();
        }
    }

    public interface ICodexSessionCoordinator : IDisposable
    {
        ICodexSessionStore SessionStore { get; }

        IWorkspaceContextStore WorkspaceStore { get; }

        IOptionsCache OptionsCache { get; }

        void Initialize();

        void RefreshOptions();

        void ApplyWorkspace(string solutionPath, string workspaceRoot, WorkspaceTransitionKind transitionKind = WorkspaceTransitionKind.WorkspaceChanged);

        void ResetSession(SessionResetReason reason = SessionResetReason.Manual);
    }
}
