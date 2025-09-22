using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace CodexVS22
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class CodexOptions : DialogPage
    {
        public enum ApprovalMode
        {
            Chat,
            Agent,
            AgentFullAccess
        }
        [Category("Codex")]
        [DisplayName("CLI Executable Path")]
        [Description("Full path to 'codex'. Empty to use PATH.")]
        public string CliExecutable { get; set; } = string.Empty;

        [Category("Codex")]
        [DisplayName("Use WSL")]
        [Description("Run 'codex proto' via WSL if native Windows is unstable.")]
        public bool UseWsl { get; set; } = false;

        [Category("Codex")]
        [DisplayName("Open on Startup")]
        [Description("Focus the Codex tool window when the package loads.")]
        public bool OpenOnStartup { get; set; } = false;

        [Category("Codex")]
        [DisplayName("Approval Mode")]
        [Description("Chat, Agent, or Agent (Full Access) for exec/patch.")]
        public ApprovalMode Mode { get; set; } = ApprovalMode.Chat;

        [Category("Codex")]
        [DisplayName("Default Model")]
        [Description("Model identifier to request for new chats.")]
        public string DefaultModel { get; set; } = "gpt-4.1";

        [Category("Codex")]
        [DisplayName("Default Reasoning Effort")]
        [Description("Reasoning effort to request (none, medium, high).")]
        public string DefaultReasoning { get; set; } = "medium";

        [Category("Codex")]
        [DisplayName("Window Width")]
        [Description("Last width of the Codex tool window (when floating).")]
        public double WindowWidth { get; set; } = 600;

        [Category("Codex")]
        [DisplayName("Window Height")]
        [Description("Last height of the Codex tool window (when floating).")]
        public double WindowHeight { get; set; } = 700;

        [Category("Codex")]
        [DisplayName("Window Left")]
        [Description("Last X position of the Codex tool window (when floating).")]
        public double WindowLeft { get; set; } = double.NaN;

        [Category("Codex")]
        [DisplayName("Window Top")]
        [Description("Last Y position of the Codex tool window (when floating).")]
        public double WindowTop { get; set; } = double.NaN;

        [Category("Codex")]
        [DisplayName("Window State")]
        [Description("Last window state (Normal, Maximized).")]
        public string WindowState { get; set; } = "Normal";
    }
}
