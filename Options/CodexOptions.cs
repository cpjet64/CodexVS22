using System.ComponentModel;
using Microsoft.VisualStudio.Shell;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Text;
using System.Collections.Generic;
using System.Linq;

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

        public enum SandboxPolicyMode
        {
            Strict,
            Moderate,
            Permissive
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
        [DisplayName("Sandbox Policy")]
        [Description("Security policy for code execution: Strict (most secure), Moderate (balanced), Permissive (least secure).")]
        public SandboxPolicyMode SandboxPolicy { get; set; } = SandboxPolicyMode.Moderate;

        [Category("Codex")]
        [DisplayName("Default Model")]
        [Description("Model identifier to request for new chats.")]
        public string DefaultModel { get; set; } = "gpt-4.1";

        [Category("Codex")]
        [DisplayName("Default Reasoning Effort")]
        [Description("Reasoning effort to request (none, medium, high).")]
        public string DefaultReasoning { get; set; } = "medium";

        [Category("Codex")]
        [DisplayName("Auto-open patched files")]
        [Description("Automatically open documents after Codex applies patches.")]
        [DefaultValue(true)]
        public bool AutoOpenPatchedFiles { get; set; } = true;

        [Category("Codex")]
        [DisplayName("Auto-hide exec console")]
        [Description("Collapse exec output after commands finish to reduce clutter.")]
        [DefaultValue(false)]
        public bool AutoHideExecConsole { get; set; } = false;

        [Category("Codex")]
        [DisplayName("Exec console visible")]
        [Description("Internal flag that stores the last visibility of the exec console.")]
        [DefaultValue(true)]
        public bool ExecConsoleVisible { get; set; } = true;

        [Category("Codex")]
        [DisplayName("Exec console height")]
        [Description("Last height of the exec console area in device independent pixels.")]
        [DefaultValue(180.0)]
        public double ExecConsoleHeight { get; set; } = 180.0;

        [Category("Codex")]
        [DisplayName("Exec output buffer limit (characters)")]
        [Description("Maximum characters to retain per exec turn before older content is trimmed (0 = unlimited).")]
        [DefaultValue(200000)]
        public int ExecOutputBufferLimit { get; set; } = 200000;

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

        [Category("Codex")]
        [DisplayName("Last Used Tool")]
        [Description("ID of the last used MCP tool for quick access.")]
        public string LastUsedTool { get; set; } = string.Empty;

        [Category("Codex")]
        [DisplayName("Last Used Prompt")]
        [Description("ID of the last used custom prompt for quick access.")]
        public string LastUsedPrompt { get; set; } = string.Empty;

        [Category("Codex")]
        [DisplayName("Solution-Specific CLI Path")]
        [Description("Override CLI path for this solution only. Leave empty to use global setting.")]
        public string SolutionCliExecutable { get; set; } = string.Empty;

        [Category("Codex")]
        [DisplayName("Solution-Specific WSL Setting")]
        [Description("Override WSL setting for this solution only. Leave empty to use global setting.")]
        public bool? SolutionUseWsl { get; set; } = null;

        public override void SaveSettingsToStorage()
        {
            // Log option changes with timestamp
            LogOptionChanges();

            // Validate all settings before saving
            ValidateSettings();

            // Validate CLI path before saving
            var effectiveCliPath = GetEffectiveCliExecutable();
            if (!string.IsNullOrEmpty(effectiveCliPath))
            {
                if (!File.Exists(effectiveCliPath))
                {
                    // Log warning but don't prevent saving
                    System.Diagnostics.Debug.WriteLine($"Warning: CLI executable not found at {effectiveCliPath}");
                }
                else
                {
                    // Validate CLI version
                    try
                    {
                        var version = GetCliVersion(effectiveCliPath);
                        if (!string.IsNullOrEmpty(version))
                        {
                            System.Diagnostics.Debug.WriteLine($"CLI version: {version}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Failed to get CLI version: {ex.Message}");
                    }
                }
            }

            base.SaveSettingsToStorage();
        }

        private void LogOptionChanges()
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var changes = new List<string>();

            // Log all current option values for diagnostics
            changes.Add($"CLI Executable: '{CliExecutable}'");
            changes.Add($"Use WSL: {UseWsl}");
            changes.Add($"Open on Startup: {OpenOnStartup}");
            changes.Add($"Approval Mode: {Mode}");
            changes.Add($"Sandbox Policy: {SandboxPolicy}");
            changes.Add($"Default Model: '{DefaultModel}'");
            changes.Add($"Default Reasoning: '{DefaultReasoning}'");
            changes.Add($"Auto Open Patched Files: {AutoOpenPatchedFiles}");
            changes.Add($"Auto Hide Exec Console: {AutoHideExecConsole}");
            changes.Add($"Exec Console Visible: {ExecConsoleVisible}");
            changes.Add($"Exec Console Height: {ExecConsoleHeight}");
            changes.Add($"Exec Output Buffer Limit: {ExecOutputBufferLimit}");
            changes.Add($"Window Width: {WindowWidth}");
            changes.Add($"Window Height: {WindowHeight}");
            changes.Add($"Window Left: {WindowLeft}");
            changes.Add($"Window Top: {WindowTop}");
            changes.Add($"Window State: '{WindowState}'");
            changes.Add($"Last Used Tool: '{LastUsedTool}'");
            changes.Add($"Last Used Prompt: '{LastUsedPrompt}'");
            changes.Add($"Solution CLI Executable: '{SolutionCliExecutable}'");
            changes.Add($"Solution Use WSL: {SolutionUseWsl}");

            var logMessage = $"[{timestamp}] Codex Options Changed:\n{string.Join("\n", changes)}";
            System.Diagnostics.Debug.WriteLine(logMessage);
        }

        private void ValidateSettings()
        {
            // Validate CLI executable path
            if (!string.IsNullOrEmpty(CliExecutable) && !File.Exists(CliExecutable))
            {
                System.Diagnostics.Debug.WriteLine($"Warning: CLI executable not found at {CliExecutable}. Please check the path or leave empty to use PATH.");
            }

            // Validate solution-specific CLI executable path
            if (!string.IsNullOrEmpty(SolutionCliExecutable) && !File.Exists(SolutionCliExecutable))
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Solution-specific CLI executable not found at {SolutionCliExecutable}. Please check the path or leave empty to use global setting.");
            }

            // Validate model name
            if (string.IsNullOrWhiteSpace(DefaultModel))
            {
                DefaultModel = "gpt-4.1";
                System.Diagnostics.Debug.WriteLine("Warning: Default model was empty, reset to 'gpt-4.1'");
            }

            // Validate reasoning effort
            if (string.IsNullOrWhiteSpace(DefaultReasoning) || 
                !new[] { "none", "medium", "high" }.Contains(DefaultReasoning.ToLowerInvariant()))
            {
                DefaultReasoning = "medium";
                System.Diagnostics.Debug.WriteLine("Warning: Default reasoning was invalid, reset to 'medium'. Valid values are: none, medium, high");
            }

            // Validate window dimensions
            if (WindowWidth < 300 || WindowWidth > 2000)
            {
                WindowWidth = Math.Max(300, Math.Min(2000, WindowWidth));
                System.Diagnostics.Debug.WriteLine($"Warning: Window width was out of range, clamped to {WindowWidth}. Valid range: 300-2000");
            }

            if (WindowHeight < 200 || WindowHeight > 1500)
            {
                WindowHeight = Math.Max(200, Math.Min(1500, WindowHeight));
                System.Diagnostics.Debug.WriteLine($"Warning: Window height was out of range, clamped to {WindowHeight}. Valid range: 200-1500");
            }

            // Validate exec console height
            if (ExecConsoleHeight < 50 || ExecConsoleHeight > 800)
            {
                ExecConsoleHeight = Math.Max(50, Math.Min(800, ExecConsoleHeight));
                System.Diagnostics.Debug.WriteLine($"Warning: Exec console height was out of range, clamped to {ExecConsoleHeight}. Valid range: 50-800");
            }

            // Validate output buffer limit
            if (ExecOutputBufferLimit < 0 || ExecOutputBufferLimit > 10000000)
            {
                ExecOutputBufferLimit = Math.Max(0, Math.Min(10000000, ExecOutputBufferLimit));
                System.Diagnostics.Debug.WriteLine($"Warning: Exec output buffer limit was out of range, clamped to {ExecOutputBufferLimit}. Valid range: 0-10000000");
            }

            // Validate window state
            if (string.IsNullOrWhiteSpace(WindowState) || 
                !new[] { "Normal", "Maximized", "Minimized" }.Contains(WindowState))
            {
                WindowState = "Normal";
                System.Diagnostics.Debug.WriteLine("Warning: Window state was invalid, reset to 'Normal'. Valid values are: Normal, Maximized, Minimized");
            }
        }

        public string GetEffectiveCliExecutable()
        {
            return !string.IsNullOrEmpty(SolutionCliExecutable) ? SolutionCliExecutable : CliExecutable;
        }

        public bool GetEffectiveUseWsl()
        {
            return SolutionUseWsl ?? UseWsl;
        }

        public void ResetToDefaults()
        {
            CliExecutable = string.Empty;
            UseWsl = false;
            OpenOnStartup = false;
            Mode = ApprovalMode.Chat;
            SandboxPolicy = SandboxPolicyMode.Moderate;
            DefaultModel = "gpt-4.1";
            DefaultReasoning = "medium";
            AutoOpenPatchedFiles = true;
            AutoHideExecConsole = false;
            ExecConsoleVisible = true;
            ExecConsoleHeight = 180.0;
            ExecOutputBufferLimit = 200000;
            WindowWidth = 600;
            WindowHeight = 700;
            WindowLeft = double.NaN;
            WindowTop = double.NaN;
            WindowState = "Normal";
            LastUsedTool = string.Empty;
            LastUsedPrompt = string.Empty;
            SolutionCliExecutable = string.Empty;
            SolutionUseWsl = null;

            System.Diagnostics.Debug.WriteLine("Codex options reset to defaults");
        }

        public string ExportToJson()
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                return JsonConvert.SerializeObject(this, settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting options to JSON: {ex.Message}");
                return string.Empty;
            }
        }

        public bool ImportFromJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return false;

                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };

                var imported = JsonConvert.DeserializeObject<CodexOptions>(json, settings);
                if (imported == null)
                    return false;

                // Copy properties from imported object
                CliExecutable = imported.CliExecutable ?? string.Empty;
                UseWsl = imported.UseWsl;
                OpenOnStartup = imported.OpenOnStartup;
                Mode = imported.Mode;
                SandboxPolicy = imported.SandboxPolicy;
                DefaultModel = imported.DefaultModel ?? "gpt-4.1";
                DefaultReasoning = imported.DefaultReasoning ?? "medium";
                AutoOpenPatchedFiles = imported.AutoOpenPatchedFiles;
                AutoHideExecConsole = imported.AutoHideExecConsole;
                ExecConsoleVisible = imported.ExecConsoleVisible;
                ExecConsoleHeight = imported.ExecConsoleHeight;
                ExecOutputBufferLimit = imported.ExecOutputBufferLimit;
                WindowWidth = imported.WindowWidth;
                WindowHeight = imported.WindowHeight;
                WindowLeft = imported.WindowLeft;
                WindowTop = imported.WindowTop;
                WindowState = imported.WindowState ?? "Normal";
                LastUsedTool = imported.LastUsedTool ?? string.Empty;
                LastUsedPrompt = imported.LastUsedPrompt ?? string.Empty;
                SolutionCliExecutable = imported.SolutionCliExecutable ?? string.Empty;
                SolutionUseWsl = imported.SolutionUseWsl;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing options from JSON: {ex.Message}");
                return false;
            }
        }

        private string GetCliVersion(string cliPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit(5000); // 5 second timeout
                        if (process.ExitCode == 0)
                        {
                            return process.StandardOutput.ReadToEnd().Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting CLI version: {ex.Message}");
            }

            return string.Empty;
        }
    }
}
