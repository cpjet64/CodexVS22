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
    }
}
