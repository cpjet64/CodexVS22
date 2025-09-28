# Member Visibility Inventory

## MyToolWindowControl.Approvals.cs
- public: 0
- internal: 0
- protected: 0
- private: 15

## MyToolWindowControl.Authentication.cs
- public: 0
- internal: 0
- protected: 0
- private: 9

## MyToolWindowControl.Exec.Helpers.cs
- public: 0
- internal: 0
- protected: 0
- private: 8

## MyToolWindowControl.Exec.cs
- public: 0
- internal: 0
- protected: 0
- private: 4

## MyToolWindowControl.Heartbeat.Helpers.cs
- public: 0
- internal: 0
- protected: 0
- private: 10

## MyToolWindowControl.Heartbeat.Models.cs
- public: 4
- internal: 0
- protected: 0
- private: 0
### Public Members
- MyToolWindowControl.Heartbeat.Models.cs:10 | public HeartbeatState(TimeSpan interval, JObject opTemplate, string opType)
- MyToolWindowControl.Heartbeat.Models.cs:17 | public TimeSpan Interval { get; }
- MyToolWindowControl.Heartbeat.Models.cs:19 | public JObject OpTemplate { get; }
- MyToolWindowControl.Heartbeat.Models.cs:21 | public string OpType { get; }

## MyToolWindowControl.Heartbeat.cs
- public: 0
- internal: 0
- protected: 0
- private: 5

## MyToolWindowControl.JsonHelpers.cs
- public: 0
- internal: 0
- protected: 0
- private: 4

## MyToolWindowControl.Lifecycle.cs
- public: 0
- internal: 2
- protected: 0
- private: 5
### Internal Members
- MyToolWindowControl.Lifecycle.cs:10 | internal static void SignalEnvironmentReady(EnvironmentSnapshot snapshot)
- MyToolWindowControl.Lifecycle.cs:18 | internal static Task WaitForUiContextAsync(UIContext context, CancellationToken ct)

## MyToolWindowControl.Mcp.Helpers.cs
- public: 0
- internal: 0
- protected: 0
- private: 4

## MyToolWindowControl.Mcp.cs
- public: 0
- internal: 0
- protected: 0
- private: 8

## MyToolWindowControl.Options.cs
- public: 0
- internal: 0
- protected: 0
- private: 8

## MyToolWindowControl.Telemetry.cs
- public: 14
- internal: 0
- protected: 0
- private: 15
### Public Members
- MyToolWindowControl.Telemetry.cs:26 | public void BeginTurn()
- MyToolWindowControl.Telemetry.cs:32 | public void RecordTokens(int? total, int? input, int? output)
- MyToolWindowControl.Telemetry.cs:46 | public void CompleteTurn()
- MyToolWindowControl.Telemetry.cs:59 | public void CancelTurn()
- MyToolWindowControl.Telemetry.cs:65 | public void BeginPatch()
- MyToolWindowControl.Telemetry.cs:70 | public void CompletePatch(bool success, double durationSeconds)
- MyToolWindowControl.Telemetry.cs:85 | public void CancelPatch()
- MyToolWindowControl.Telemetry.cs:90 | public void BeginExec(string id, string command)
- MyToolWindowControl.Telemetry.cs:98 | public void CompleteExec(string id, int exitCode)
- MyToolWindowControl.Telemetry.cs:114 | public void CancelExec(string id)
- MyToolWindowControl.Telemetry.cs:122 | public void RecordToolInvocation()
- MyToolWindowControl.Telemetry.cs:127 | public void RecordPromptInsert()
- MyToolWindowControl.Telemetry.cs:132 | public void Reset()
- MyToolWindowControl.Telemetry.cs:151 | public string GetSummary()

## MyToolWindowControl.Transcript.cs
- public: 0
- internal: 0
- protected: 0
- private: 6

## MyToolWindowControl.Types.cs
- public: 53
- internal: 0
- protected: 0
- private: 13
### Public Members
- MyToolWindowControl.Types.cs:14 | public AssistantTurn(ChatBubbleElements elements)
- MyToolWindowControl.Types.cs:21 | public Border Container { get; }
- MyToolWindowControl.Types.cs:22 | public TextBlock Header { get; }
- MyToolWindowControl.Types.cs:23 | public TextBlock Bubble { get; }
- MyToolWindowControl.Types.cs:24 | public StringBuilder Buffer { get; } = new StringBuilder();
- MyToolWindowControl.Types.cs:29 | public ChatBubbleElements(Border container, TextBlock header, TextBlock body)
- MyToolWindowControl.Types.cs:36 | public Border Container { get; }
- MyToolWindowControl.Types.cs:37 | public TextBlock Header { get; }
- MyToolWindowControl.Types.cs:38 | public TextBlock Body { get; }
- MyToolWindowControl.Types.cs:49 | public ApprovalRequest(ApprovalKind kind, string callId, string message, string signature, bool canRemember)
- MyToolWindowControl.Types.cs:58 | public ApprovalKind Kind { get; }
- MyToolWindowControl.Types.cs:59 | public string CallId { get; }
- MyToolWindowControl.Types.cs:60 | public string Message { get; }
- MyToolWindowControl.Types.cs:61 | public string Signature { get; }
- MyToolWindowControl.Types.cs:62 | public bool CanRemember { get; }
- MyToolWindowControl.Types.cs:67 | public ExecTurn(Border container, TextBlock body, TextBlock header, Button cancelButton, Button copyButton, Button clearButton, Button exportButton, string normalizedCommand)
- MyToolWindowControl.Types.cs:80 | public Border Container { get; }
- MyToolWindowControl.Types.cs:81 | public TextBlock Body { get; }
- MyToolWindowControl.Types.cs:82 | public TextBlock Header { get; }
- MyToolWindowControl.Types.cs:83 | public Button CancelButton { get; }
- MyToolWindowControl.Types.cs:84 | public Button CopyButton { get; }
- MyToolWindowControl.Types.cs:85 | public Button ClearButton { get; }
- MyToolWindowControl.Types.cs:86 | public Button ExportButton { get; }
- MyToolWindowControl.Types.cs:87 | public Brush DefaultForeground { get; }
- MyToolWindowControl.Types.cs:88 | public string ExecId { get; set; } = string.Empty;
- MyToolWindowControl.Types.cs:89 | public bool CancelRequested { get; set; }
- MyToolWindowControl.Types.cs:90 | public bool IsRunning { get; set; }
- MyToolWindowControl.Types.cs:91 | public string NormalizedCommand { get; set; }
- MyToolWindowControl.Types.cs:92 | public StringBuilder Buffer { get; } = new StringBuilder();
- MyToolWindowControl.Types.cs:97 | public McpToolInfo(string name, string description, string server)
- MyToolWindowControl.Types.cs:104 | public string Name { get; }
- MyToolWindowControl.Types.cs:105 | public string Description { get; }
- MyToolWindowControl.Types.cs:106 | public string Server { get; }
- MyToolWindowControl.Types.cs:118 | public McpToolRun(string callId, string toolName, string server)
- MyToolWindowControl.Types.cs:128 | public string CallId { get; }
- MyToolWindowControl.Types.cs:129 | public string ToolName { get; }
- MyToolWindowControl.Types.cs:130 | public string Server { get; }
- MyToolWindowControl.Types.cs:131 | public DateTimeOffset StartedUtc { get; }
- MyToolWindowControl.Types.cs:133 | public string StatusDisplay
- MyToolWindowControl.Types.cs:139 | public string Detail
- MyToolWindowControl.Types.cs:145 | public string TimingDisplay
- MyToolWindowControl.Types.cs:151 | public bool IsRunning
- MyToolWindowControl.Types.cs:157 | public event PropertyChangedEventHandler PropertyChanged;
- MyToolWindowControl.Types.cs:159 | public void UpdateRunning(string statusText, string detail)
- MyToolWindowControl.Types.cs:177 | public void AppendOutput(string text)
- MyToolWindowControl.Types.cs:188 | public void Complete(string statusText, bool? success, string detail)
- MyToolWindowControl.Types.cs:238 | public CustomPromptInfo(string id, string name, string description, string body, string source)
- MyToolWindowControl.Types.cs:247 | public string Id { get; }
- MyToolWindowControl.Types.cs:248 | public string Name { get; }
- MyToolWindowControl.Types.cs:249 | public string Description { get; }
- MyToolWindowControl.Types.cs:250 | public string Body { get; }
- MyToolWindowControl.Types.cs:251 | public string Source { get; }
- MyToolWindowControl.Types.cs:253 | public string SourceDisplay => string.IsNullOrEmpty(Source) ? string.Empty : Source;

## MyToolWindowControl.Windowing.cs
- public: 0
- internal: 0
- protected: 0
- private: 7

## MyToolWindowControl.WorkingDirectory.Environment.cs
- public: 0
- internal: 2
- protected: 0
- private: 10
### Internal Members
- MyToolWindowControl.WorkingDirectory.Environment.cs:128 | internal static async Task<EnvironmentSnapshot> CaptureEnvironmentSnapshotAsync(CancellationToken ct)
- MyToolWindowControl.WorkingDirectory.Environment.cs:194 | internal static UIContext TryGetFolderOpenUIContext()

## MyToolWindowControl.WorkingDirectory.Helpers.cs
- public: 0
- internal: 0
- protected: 0
- private: 9

## MyToolWindowControl.WorkingDirectory.Models.cs
- public: 16
- internal: 0
- protected: 0
- private: 0
### Public Members
- MyToolWindowControl.WorkingDirectory.Models.cs:10 | public CandidateSeed(string source, string path, bool isWorkspaceRoot)
- MyToolWindowControl.WorkingDirectory.Models.cs:17 | public string Source { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:19 | public string Path { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:21 | public bool IsWorkspaceRoot { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:26 | public WorkingDirectoryCandidate(string source, string path, bool exists, bool hasSolution,
- MyToolWindowControl.WorkingDirectory.Models.cs:39 | public string Source { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:41 | public string Path { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:43 | public bool Exists { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:45 | public bool HasSolution { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:47 | public bool HasProject { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:49 | public int Depth { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:51 | public bool IsWorkspaceRoot { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:53 | public bool IsInsideExtension { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:58 | public WorkingDirectoryResolution(WorkingDirectoryCandidate selected,
- MyToolWindowControl.WorkingDirectory.Models.cs:65 | public WorkingDirectoryCandidate Selected { get; }
- MyToolWindowControl.WorkingDirectory.Models.cs:67 | public IReadOnlyList<WorkingDirectoryCandidate> Candidates { get; }

## MyToolWindowControl.WorkingDirectory.Projects.cs
- public: 0
- internal: 0
- protected: 0
- private: 7

## MyToolWindowControl.WorkingDirectory.Selection.cs
- public: 0
- internal: 0
- protected: 0
- private: 13

## MyToolWindowControl.WorkingDirectory.SolutionEvents.cs
- public: 12
- internal: 0
- protected: 0
- private: 3
### Public Members
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:21 | public SolutionEventsSink(MyToolWindowControl owner)
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:35 | public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:42 | public int OnAfterCloseProject(IVsHierarchy pHierarchy, int fRemoved)
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:49 | public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:51 | public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:53 | public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:59 | public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:61 | public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:67 | public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:73 | public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:75 | public int OnAfterCloseSolution(object pUnkReserved)
- MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:81 | public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;

## MyToolWindowControl.WorkingDirectory.Subscriptions.cs
- public: 0
- internal: 0
- protected: 0
- private: 14

## MyToolWindowControl.WorkingDirectory.cs
- public: 0
- internal: 0
- protected: 0
- private: 4

## MyToolWindowControl.xaml.cs
- public: 17
- internal: 5
- protected: 0
- private: 201
### Public Members
- MyToolWindowControl.xaml.cs:1257 | public MyToolWindowControl()
- MyToolWindowControl.xaml.cs:1297 | public static MyToolWindowControl Current { get; private set; }
- MyToolWindowControl.xaml.cs:4121 | public DiffTreeItem(string name, string relativePath, bool isDirectory, DiffDocument document, Action<DiffTreeItem> onCheckChanged)
- MyToolWindowControl.xaml.cs:4209 | public event PropertyChangedEventHandler PropertyChanged;
- MyToolWindowControl.xaml.cs:4225 | public string Name { get; }
- MyToolWindowControl.xaml.cs:4233 | public string RelativePath { get; }
- MyToolWindowControl.xaml.cs:4241 | public bool IsDirectory { get; }
- MyToolWindowControl.xaml.cs:4249 | public DiffDocument Document { get; private set; }
- MyToolWindowControl.xaml.cs:4257 | public ObservableCollection<DiffTreeItem> Children { get; }
- MyToolWindowControl.xaml.cs:4265 | public DiffTreeItem Parent { get; private set; }
- MyToolWindowControl.xaml.cs:4281 | public bool? IsChecked
- MyToolWindowControl.xaml.cs:4329 | public bool IsExpanded
- MyToolWindowControl.xaml.cs:14917 | public static readonly EnvironmentSnapshot Empty = new(string.Empty, string.Empty);
- MyToolWindowControl.xaml.cs:14933 | public EnvironmentSnapshot(string solutionRoot, string workspaceRoot)
- MyToolWindowControl.xaml.cs:14981 | public string SolutionRoot { get; }
- MyToolWindowControl.xaml.cs:14989 | public string WorkspaceRoot { get; }
- MyToolWindowControl.xaml.cs:24021 | public void AppendSelectionToInput(string text)
### Internal Members
- MyToolWindowControl.xaml.cs:4425 | internal void SetParent(DiffTreeItem parent)
- MyToolWindowControl.xaml.cs:4465 | internal void SetDocument(DiffDocument document)
- MyToolWindowControl.xaml.cs:4521 | internal void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
- MyToolWindowControl.xaml.cs:4825 | internal void SynchronizeCheckStateFromChildren()
- MyToolWindowControl.xaml.cs:14901 | internal readonly struct EnvironmentSnapshot
