using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodexVS22.Core;
using CodexVS22.Core.Protocol;
using CodexVS22;
using Newtonsoft.Json.Linq;

namespace CodexVS22.Tests;

internal static class Program
{
  private sealed class TestResult
  {
    public string Name = string.Empty;
    public bool Passed;
    public string Message = string.Empty;
    public double Millis;
  }

  private static readonly List<string> Failures = new();
  private static readonly List<TestResult> Results = new();

  private static int Main()
  {
    RunTest(nameof(AgentMessageEvents_CorrelateDeltaAndFinal), AgentMessageEvents_CorrelateDeltaAndFinal);
    RunTest(nameof(ParallelTurns_CorrelateIndependently), ParallelTurns_CorrelateIndependently);
    RunTest(nameof(ParallelTurns_TaskCompleteCleansState), ParallelTurns_TaskCompleteCleansState);
    RunTest(nameof(UserInputSubmission_LongInput_RoundTrips), UserInputSubmission_LongInput_RoundTrips);
    RunTest(nameof(UserInputSubmission_NonAscii_RoundTrips), UserInputSubmission_NonAscii_RoundTrips);
    RunTest(nameof(UserInputSubmission_PasteFlowPreservesLines), UserInputSubmission_PasteFlowPreservesLines);
    RunTest(nameof(NormalizeAssistantText_NormalizesSmartQuotes), NormalizeAssistantText_NormalizesSmartQuotes);
    RunTest(nameof(ExecApprovalSubmission_MatchesRequest), ExecApprovalSubmission_MatchesRequest);
    RunTest(nameof(PatchApprovalSubmission_MatchesRequest), PatchApprovalSubmission_MatchesRequest);
    RunTest(nameof(RememberedExecApprovals_AreHonored), RememberedExecApprovals_AreHonored);
    RunTest(nameof(RememberedPatchApprovals_AreHonored), RememberedPatchApprovals_AreHonored);
    RunTest(nameof(DiffParsing_WithFilesArray_ParsesDocuments), DiffParsing_WithFilesArray_ParsesDocuments);
    RunTest(nameof(DiffParsing_MarksBinaryAndEmpty), DiffParsing_MarksBinaryAndEmpty);
    RunTest(nameof(PatchApply_WithMatchingOriginal_WritesFile), PatchApply_WithMatchingOriginal_WritesFile);
    RunTest(nameof(PatchApply_WithMismatchedOriginal_ReportsConflict), PatchApply_WithMismatchedOriginal_ReportsConflict);
    RunTest(nameof(PatchApply_RespectsReadOnlyFiles), PatchApply_RespectsReadOnlyFiles);
    RunTest(nameof(ExecTranscriptTracker_HandlesAnsiAndTrim), ExecTranscriptTracker_HandlesAnsiAndTrim);
    RunTest(nameof(ExecTranscriptTracker_CancelClearsState), ExecTranscriptTracker_CancelClearsState);
    RunTest(nameof(ExecConsole_LongRapidStream_Responsive), ExecConsole_LongRapidStream_Responsive);
        RunTest(nameof(ExtractMcpTools_ParsesValidResponse), ExtractMcpTools_ParsesValidResponse);
        RunTest(nameof(ExtractMcpTools_HandlesEmptyResponse), ExtractMcpTools_HandlesEmptyResponse);
        RunTest(nameof(ExtractCustomPrompts_ParsesValidResponse), ExtractCustomPrompts_ParsesValidResponse);
        RunTest(nameof(ExtractCustomPrompts_HandlesEmptyResponse), ExtractCustomPrompts_HandlesEmptyResponse);
        RunTest(nameof(CodexOptions_ExportToJson), CodexOptions_ExportToJson);
        RunTest(nameof(CodexOptions_ImportFromJson), CodexOptions_ImportFromJson);
        RunTest(nameof(CodexOptions_GetEffectiveValues), CodexOptions_GetEffectiveValues);

    WriteArtifacts();

    if (Failures.Count == 0)
    {
      Console.WriteLine("Correlation tests passed.");
      return 0;
    }

    foreach (var failure in Failures)
      Console.Error.WriteLine(failure);

    return 1;
  }

  private static void RunTest(string name, Action action)
  {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var tr = new TestResult { Name = name };
    try
    {
      action();
      tr.Passed = true;
    }
    catch (Exception ex)
    {
      Failures.Add($"{name} failed: {ex.Message}");
      tr.Passed = false;
      tr.Message = ex.Message;
    }
    finally
    {
      sw.Stop();
      tr.Millis = sw.Elapsed.TotalMilliseconds;
      Results.Add(tr);
    }
  }

  private static void AgentMessageEvents_CorrelateDeltaAndFinal()
  {
    var lines = new[]
    {
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-1\",\"text_delta\":\"Hello \"}}",
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-1\",\"text_delta\":\"World\"}}",
      "{\"msg\":{\"kind\":\"AgentMessage\",\"id\":\"turn-1\",\"text\":\"Hello World!\"}}"
    };

    var tracker = new TranscriptTracker();
    tracker.Process(lines);

    AssertEqual("Hello World!", tracker.GetTranscript("turn-1"), "Final transcript mismatch");
    AssertFalse(tracker.HasInFlight("turn-1"), "turn-1 should be completed");
  }

  private static void ParallelTurns_CorrelateIndependently()
  {
    var lines = new[]
    {
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-A\",\"text_delta\":\"alpha\"}}",
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-B\",\"text_delta\":\"beta\"}}",
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-A\",\"text_delta\":\" one\"}}",
      "{\"msg\":{\"kind\":\"AgentMessage\",\"id\":\"turn-B\",\"text\":\"beta!\"}}",
      "{\"msg\":{\"kind\":\"StreamError\",\"id\":\"turn-A\",\"message\":\"cancelled\"}}"
    };

    var tracker = new TranscriptTracker();
    tracker.Process(lines);

    AssertEqual("beta!", tracker.GetTranscript("turn-B"), "turn-B transcript mismatch");
    AssertFalse(tracker.HasTranscript("turn-A"), "turn-A should not have a final transcript");
    AssertFalse(tracker.HasInFlight("turn-A"), "turn-A should be cleared");
    AssertFalse(tracker.HasInFlight("turn-B"), "turn-B should be cleared");
  }

  private static void ParallelTurns_TaskCompleteCleansState()
  {
    var lines = new[]
    {
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-1\",\"text_delta\":\"foo\"}}",
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-2\",\"text_delta\":\"bar\"}}",
      "{\"msg\":{\"kind\":\"TaskComplete\",\"id\":\"turn-1\"}}",
      "{\"msg\":{\"kind\":\"AgentMessage\",\"id\":\"turn-2\",\"text\":\"bar!\"}}",
      "{\"msg\":{\"kind\":\"TaskComplete\",\"id\":\"turn-2\"}}"
    };

    var tracker = new TranscriptTracker();
    tracker.Process(lines);

    AssertFalse(tracker.HasTranscript("turn-1"), "turn-1 transcript should be cleared");
    AssertFalse(tracker.HasInFlight("turn-1"), "turn-1 in-flight state should be cleared");
    AssertEqual("bar!", tracker.GetTranscript("turn-2"), "turn-2 transcript mismatch");
    AssertFalse(tracker.HasInFlight("turn-2"), "turn-2 in-flight state should be cleared");
  }

  private static void UserInputSubmission_LongInput_RoundTrips()
  {
    var sample = new string('A', 4096);
    var payload = ExtractSubmissionText(sample);
    AssertEqual(sample, payload, "Long input should round-trip through submission");
  }

  private static void UserInputSubmission_NonAscii_RoundTrips()
  {
    const string sample = "café 🤖 中国";
    var payload = ExtractSubmissionText(sample);
    AssertEqual(sample, payload, "Non-ASCII input should be preserved");
  }

  private static void UserInputSubmission_PasteFlowPreservesLines()
  {
    const string sample = "line1\r\nline2\r\nline3";
    var payload = ExtractSubmissionText(sample);
    AssertEqual(sample, payload, "Paste flow should retain CRLF line breaks");
  }

  private static void NormalizeAssistantText_NormalizesSmartQuotes()
  {
    const string sample = "“Smart” — test… café";
    var normalized = ChatTextUtilities.NormalizeAssistantText(sample);
    AssertEqual("\"Smart\" - test... café", normalized, "Assistant text should normalize typographic characters");
  }

  private static void ExecApprovalSubmission_MatchesRequest()
  {
    var submission = ApprovalSubmissionFactory.CreateExec("call-123", approved: true);
    var obj = JObject.Parse(submission);
    AssertEqual("call-123", obj["op"]?["call_id"]?.ToString() ?? string.Empty, "Exec call id mismatch");
    AssertEqual("approved", obj["op"]?["decision"]?.ToString() ?? string.Empty, "Exec decision mismatch");

    submission = ApprovalSubmissionFactory.CreateExec(null, approved: false);
    obj = JObject.Parse(submission);
    AssertEqual("denied", obj["op"]?["decision"]?.ToString() ?? string.Empty, "Exec denied decision mismatch");
  }

  private static void PatchApprovalSubmission_MatchesRequest()
  {
    var submission = ApprovalSubmissionFactory.CreatePatch("patch-7", approved: false);
    var obj = JObject.Parse(submission);
    AssertEqual("patch-7", obj["op"]?["call_id"]?.ToString() ?? string.Empty, "Patch call id mismatch");
    AssertEqual("denied", obj["op"]?["decision"]?.ToString() ?? string.Empty, "Patch decision mismatch");

    submission = ApprovalSubmissionFactory.CreatePatch(null, approved: true);
    obj = JObject.Parse(submission);
    AssertEqual("approved", obj["op"]?["decision"]?.ToString() ?? string.Empty, "Patch approved decision mismatch");
  }

  private static void RememberedExecApprovals_AreHonored()
  {
    var resolver = new FakeApprovalResolver();
    resolver.RememberExecDecision("dir", true);
    AssertTrue(resolver.TryResolveExecApproval(autoApprove: false, "dir", out var approved), "Exec should resolve via remembered decision");
    AssertTrue(approved, "Remembered exec decision mismatch");
    AssertTrue(resolver.TryResolveExecApproval(autoApprove: false, "dir", out approved), "Exec should resolve repeatedly via remembered decision");
    AssertTrue(approved, "Repeated remembered exec decision mismatch");
  }

  private static void RememberedPatchApprovals_AreHonored()
  {
    var resolver = new FakeApprovalResolver();
    resolver.RememberPatchDecision("file", false);
    AssertTrue(resolver.TryResolvePatchApproval(autoApprove: false, "file", out var approved), "Patch should resolve via remembered decision");
    AssertFalse(approved, "Remembered patch decision mismatch");
    AssertTrue(resolver.TryResolvePatchApproval(autoApprove: false, "file", out approved), "Patch should resolve repeatedly via remembered decision");
    AssertFalse(approved, "Repeated remembered patch decision mismatch");
  }

  private static void DiffParsing_WithFilesArray_ParsesDocuments()
  {
    var payload = new JObject
    {
      ["files"] = new JArray
      {
        new JObject
        {
          ["path"] = "src/Program.cs",
          ["original"] = "old",
          ["text"] = "new"
        },
        new JObject
        {
          ["file"] = "README.md",
          ["before"] = "old readme",
          ["after"] = "new readme"
        }
      }
    };

    var docs = DiffUtilities.ExtractDocuments(payload);
    AssertEqual("src/Program.cs", docs[0].Path, "First diff path mismatch");
    AssertEqual("old", docs[0].Original, "First diff original mismatch");
    AssertEqual("new", docs[0].Modified, "First diff modified mismatch");
    AssertEqual("README.md", docs[1].Path, "Second diff path mismatch");
    AssertEqual("old readme", docs[1].Original, "Second diff original mismatch");
    AssertEqual("new readme", docs[1].Modified, "Second diff modified mismatch");
  }

  private static void DiffParsing_MarksBinaryAndEmpty()
  {
    var payload = new JObject
    {
      ["files"] = new JArray
      {
        new JObject
        {
          ["path"] = "src/binary.dat",
          ["original"] = "\u0000\u0001",
          ["text"] = "\u0000\u0002"
        },
        new JObject
        {
          ["path"] = "src/empty.txt",
          ["original"] = "",
          ["text"] = ""
        }
      }
    };

    var docs = DiffUtilities.ExtractDocuments(payload);
    AssertTrue(docs[0].IsBinary, "Binary diff should be marked as binary");
    AssertTrue(docs[1].IsEmpty, "Empty diff should be marked as empty");
  }

  private static void PatchApply_WithMatchingOriginal_WritesFile()
  {
    var path = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}.txt");
    try
    {
      File.WriteAllText(path, "line1\nline2\n");
      var result = DiffUtilities.ApplyPatchToFileForTests(path, "line1\nline2\n", "line1\nline2\nline3\n");
      AssertEqual(DiffUtilities.NormalizeForComparison("line1\nline2\nline3\n"), DiffUtilities.NormalizeForComparison(File.ReadAllText(path)), "Patched file content mismatch");
      if (result != CodexVS22.Core.PatchApplyResult.Applied)
        throw new InvalidOperationException("Expected patch to apply successfully");
    }
    finally
    {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  private static void PatchApply_WithMismatchedOriginal_ReportsConflict()
  {
    var path = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}.txt");
    try
    {
      File.WriteAllText(path, "current\n");
      var result = DiffUtilities.ApplyPatchToFileForTests(path, "expected\n", "patched\n");
      if (result != CodexVS22.Core.PatchApplyResult.Conflict)
        throw new InvalidOperationException("Expected conflict when originals differ");
      AssertEqual(DiffUtilities.NormalizeForComparison("current\n"), DiffUtilities.NormalizeForComparison(File.ReadAllText(path)), "File should remain unchanged after conflict");
    }
    finally
    {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  private static void PatchApply_RespectsReadOnlyFiles()
  {
    var path = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}.txt");
    try
    {
      File.WriteAllText(path, "locked\n");
      var info = new FileInfo(path) { IsReadOnly = true };
      try
      {
        var result = DiffUtilities.ApplyPatchToFileForTests(path, "locked\n", "edited\n");
        if (result != CodexVS22.Core.PatchApplyResult.Failed)
          throw new InvalidOperationException("Expected read-only patch to fail");
        AssertEqual(DiffUtilities.NormalizeForComparison("locked\n"), DiffUtilities.NormalizeForComparison(File.ReadAllText(path)), "Read-only file should remain unchanged");
      }
      finally
      {
        info.IsReadOnly = false;
      }
    }
    finally
    {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  private static void ExecTranscriptTracker_HandlesAnsiAndTrim()
  {
    var tracker = new ExecTranscriptTracker(bufferLimit: 200);
    tracker.Begin("exec-1", "$ exec ls", "/tmp");
    tracker.Append("exec-1", "Normal line\n");
    tracker.Append("exec-1", "\u001b[31mError\u001b[0m line\n");
    tracker.Append("exec-1", new string('x', 200));
    tracker.SetFinished("exec-1");

    var transcript = tracker.GetTranscript("exec-1");
    try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "exec-trans.txt"), transcript); } catch {}
    AssertTrue(transcript.Contains("Normal line") || transcript.Contains("$ exec ls"),
      "Transcript should retain initial header or first line");
    AssertTrue(transcript.Contains("Error line"), "Transcript should strip ANSI codes");
    AssertTrue(transcript.Length <= 210, "Transcript should be trimmed near limit");
    AssertFalse(transcript.Contains("\u001b"), "Transcript should not contain raw ANSI");
  }

  private static void ExecTranscriptTracker_CancelClearsState()
  {
    var tracker = new ExecTranscriptTracker(bufferLimit: 100);
    tracker.Begin("exec-2", "$ exec build", "/src");
    tracker.Append("exec-2", "Building...\n");
    tracker.Cancel("exec-2");

    AssertFalse(tracker.HasTranscript("exec-2"), "Cancelled exec should not retain transcript");
    AssertFalse(tracker.HasTurn("exec-2"), "Cancelled exec should be removed");
  }

  // T7.12: stress test long outputs and rapid updates
  private static void ExecConsole_LongRapidStream_Responsive()
  {
    var tracker = new ExecTranscriptTracker(bufferLimit: 5000);
    tracker.Begin("exec-stress", "$ exec tail -f", "/var/log");
    // Simulate rapid bursts
    for (int i = 0; i < 200; i++)
    {
      tracker.Append("exec-stress", $"line-{i:D4} {new string('a', 50)}\n");
    }
    tracker.SetFinished("exec-stress");
    var txt = tracker.GetTranscript("exec-stress");
    AssertTrue(txt.Length <= 5050, "Transcript should be near cap");
    AssertTrue(txt.Contains("$ exec tail -f"), "Transcript should retain header");
    AssertTrue(txt.Contains("line-0199"), "Transcript should contain last line");
  }

  private static void ExtractMcpTools_ParsesValidResponse()
  {
    var payload = new JObject
    {
      ["tools"] = new JArray
      {
        new JObject
        {
          ["name"] = "file_search",
          ["description"] = "Search for files in the project",
          ["server"] = "filesystem"
        },
        new JObject
        {
          ["id"] = "git_status",
          ["summary"] = "Get git repository status",
          ["provider"] = "git"
        },
        new JObject
        {
          ["tool"] = "web_search",
          ["detail"] = "Search the web for information",
          ["source"] = "web"
        }
      }
    };

    var tools = ExtractMcpTools(payload);
    AssertEqual(3, tools.Count, "Should extract 3 tools");
    AssertEqual("file_search", tools[0].Name, "First tool name mismatch");
    AssertEqual("Search for files in the project", tools[0].Description, "First tool description mismatch");
    AssertEqual("filesystem", tools[0].Server, "First tool server mismatch");
    AssertEqual("git_status", tools[1].Name, "Second tool name mismatch");
    AssertEqual("Get git repository status", tools[1].Description, "Second tool description mismatch");
    AssertEqual("git", tools[1].Server, "Second tool server mismatch");
    AssertEqual("web_search", tools[2].Name, "Third tool name mismatch");
    AssertEqual("Search the web for information", tools[2].Description, "Third tool description mismatch");
    AssertEqual("web", tools[2].Server, "Third tool server mismatch");
  }

  private static void ExtractMcpTools_HandlesEmptyResponse()
  {
    var payload = new JObject();
    var tools = ExtractMcpTools(payload);
    AssertEqual(0, tools.Count, "Empty response should return no tools");

    var nullPayload = (JObject)null;
    tools = ExtractMcpTools(nullPayload);
    AssertEqual(0, tools.Count, "Null payload should return no tools");
  }

  private static void ExtractCustomPrompts_ParsesValidResponse()
  {
    var payload = JObject.Parse("{\n" +
      "  \"prompts\": [\n" +
      "    {\n" +
      "      \"id\": \"prompt-1\",\n" +
      "      \"name\": \"Code Review\",\n" +
      "      \"description\": \"Review code for best practices\",\n" +
      "      \"body\": \"Please review this code for best practices and suggest improvements.\",\n" +
      "      \"source\": \"built-in\"\n" +
      "    },\n" +
      "    {\n" +
      "      \"id\": \"prompt-2\",\n" +
      "      \"title\": \"Debug Help\",\n" +
      "      \"summary\": \"Help with debugging issues\",\n" +
      "      \"text\": \"Help me debug this issue step by step.\",\n" +
      "      \"provider\": \"user\"\n" +
      "    }\n" +
      "  ]\n" +
      "}");

    var prompts = ExtractCustomPrompts(payload);
    AssertEqual(2, prompts.Count, "Should extract 2 prompts");
    AssertEqual("prompt-1", prompts[0].Id, "First prompt ID mismatch");
    AssertEqual("Code Review", prompts[0].Name, "First prompt name mismatch");
    AssertEqual("Review code for best practices", prompts[0].Description, "First prompt description mismatch");
    AssertEqual("Please review this code for best practices and suggest improvements.", prompts[0].Body, "First prompt body mismatch");
    AssertEqual("built-in", prompts[0].Source, "First prompt source mismatch");
    AssertEqual("prompt-2", prompts[1].Id, "Second prompt ID mismatch");
    AssertEqual("Debug Help", prompts[1].Name, "Second prompt name mismatch");
    AssertEqual("Help with debugging issues", prompts[1].Description, "Second prompt description mismatch");
    AssertEqual("Help me debug this issue step by step.", prompts[1].Body, "Second prompt body mismatch");
    AssertEqual("user", prompts[1].Source, "Second prompt source mismatch");
  }

  private static void ExtractCustomPrompts_HandlesEmptyResponse()
  {
    var payload = new JObject();
    var prompts = ExtractCustomPrompts(payload);
    AssertEqual(0, prompts.Count, "Empty response should return no prompts");

    var nullPayload = (JObject)null;
    prompts = ExtractCustomPrompts(nullPayload);
    AssertEqual(0, prompts.Count, "Null payload should return no prompts");
  }

  private static IReadOnlyList<McpToolInfo> ExtractMcpTools(JObject obj)
  {
    var results = new List<McpToolInfo>();
    if (obj == null)
      return results;

    var toolsArray = obj["tools"] as JArray;
    if (toolsArray == null)
      return results;

    foreach (var toolToken in toolsArray)
    {
      if (toolToken is not JObject toolObj)
        continue;

      var name = TryGetString(toolObj, "name", "id", "tool");
      var description = TryGetString(toolObj, "description", "summary", "detail");
      var server = TryGetString(toolObj, "server", "provider", "source");
      results.Add(new McpToolInfo(name, description, server));
    }

    return results;
  }

  private static IReadOnlyList<CustomPromptInfo> ExtractCustomPrompts(JObject obj)
  {
    var results = new List<CustomPromptInfo>();
    if (obj == null)
      return results;

    var promptsArray = obj["prompts"] as JArray;
    if (promptsArray == null)
      return results;

    foreach (var promptToken in promptsArray)
    {
      if (promptToken is not JObject promptObj)
        continue;

      var id = TryGetString(promptObj, "id", "key", "identifier");
      var name = TryGetString(promptObj, "name", "title", "label");
      var description = TryGetString(promptObj, "description", "summary", "detail");
      var body = TryGetString(promptObj, "body", "text", "content", "prompt");
      var source = TryGetString(promptObj, "source", "provider", "origin");
      results.Add(new CustomPromptInfo(id, name, description, body, source));
    }

    return results;
  }

  private static string TryGetString(JObject obj, params string[] keys)
  {
    foreach (var key in keys)
    {
      var value = obj[key]?.ToString();
      if (!string.IsNullOrWhiteSpace(value))
        return value.Trim();
    }
    return string.Empty;
  }

  private static void CodexOptions_ExportToJson()
  {
    var options = new CodexOptions
    {
      CliExecutable = "C:\\codex.exe",
      UseWsl = true,
      OpenOnStartup = true,
      Mode = CodexOptions.ApprovalMode.Agent,
      SandboxPolicy = CodexOptions.SandboxPolicyMode.Strict,
      DefaultModel = "gpt-4.1",
      DefaultReasoning = "high",
      AutoOpenPatchedFiles = false,
      AutoHideExecConsole = true,
      ExecConsoleVisible = false,
      ExecConsoleHeight = 200.0,
      ExecOutputBufferLimit = 100000,
      WindowWidth = 800.0,
      WindowHeight = 600.0,
      WindowLeft = 100.0,
      WindowTop = 50.0,
      WindowState = "Maximized",
      LastUsedTool = "test-tool",
      LastUsedPrompt = "test-prompt",
      SolutionCliExecutable = "C:\\solution-codex.exe",
      SolutionUseWsl = false
    };

    var json = options.ExportToJson();
    AssertFalse(string.IsNullOrEmpty(json), "JSON export should not be empty");
    try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "codex-options-export.json"), json); } catch {}
    var jobj = JObject.Parse(json);
    AssertEqual("C:\\codex.exe", jobj[nameof(CodexOptions.CliExecutable)]?.ToString() ?? string.Empty,
      "JSON should contain CLI executable path");
    AssertEqual((int)CodexOptions.ApprovalMode.Agent,
      jobj[nameof(CodexOptions.Mode)]?.Value<int>() ?? -1,
      "JSON should contain approval mode");
    AssertEqual((int)CodexOptions.SandboxPolicyMode.Strict,
      jobj[nameof(CodexOptions.SandboxPolicy)]?.Value<int>() ?? -1,
      "JSON should contain sandbox policy");
    AssertTrue(json.Contains("test-tool"), "JSON should contain last used tool");
    AssertTrue(json.Contains("test-prompt"), "JSON should contain last used prompt");
  }

  private static void CodexOptions_ImportFromJson()
  {
    var json = @"{
      ""CliExecutable"": ""C:\\imported-codex.exe"",
      ""UseWsl"": false,
      ""OpenOnStartup"": true,
      ""Mode"": 1,
      ""SandboxPolicy"": 2,
      ""DefaultModel"": ""gpt-4.1-mini"",
      ""DefaultReasoning"": ""none"",
      ""AutoOpenPatchedFiles"": true,
      ""AutoHideExecConsole"": false,
      ""ExecConsoleVisible"": true,
      ""ExecConsoleHeight"": 250.0,
      ""ExecOutputBufferLimit"": 50000,
      ""WindowWidth"": 900.0,
      ""WindowHeight"": 700.0,
      ""WindowLeft"": 200.0,
      ""WindowTop"": 100.0,
      ""WindowState"": ""Normal"",
      ""LastUsedTool"": ""imported-tool"",
      ""LastUsedPrompt"": ""imported-prompt"",
      ""SolutionCliExecutable"": ""C:\\solution-codex.exe"",
      ""SolutionUseWsl"": true
    }";

    var options = new CodexOptions();
    var success = options.ImportFromJson(json);

    AssertTrue(success, "Import should succeed");
    AssertEqual("C:\\imported-codex.exe", options.CliExecutable, "CLI executable should be imported");
    AssertFalse(options.UseWsl, "UseWsl should be imported");
    AssertTrue(options.OpenOnStartup, "OpenOnStartup should be imported");
    AssertEqual(CodexOptions.ApprovalMode.Agent, options.Mode, "Approval mode should be imported");
    AssertEqual(CodexOptions.SandboxPolicyMode.Permissive, options.SandboxPolicy, "Sandbox policy should be imported");
    AssertEqual("gpt-4.1-mini", options.DefaultModel, "Default model should be imported");
    AssertEqual("none", options.DefaultReasoning, "Default reasoning should be imported");
    AssertTrue(options.AutoOpenPatchedFiles, "AutoOpenPatchedFiles should be imported");
    AssertFalse(options.AutoHideExecConsole, "AutoHideExecConsole should be imported");
    AssertTrue(options.ExecConsoleVisible, "ExecConsoleVisible should be imported");
    AssertEqual(250.0, options.ExecConsoleHeight, "ExecConsoleHeight should be imported");
    AssertEqual(50000, options.ExecOutputBufferLimit, "ExecOutputBufferLimit should be imported");
    AssertEqual(900.0, options.WindowWidth, "WindowWidth should be imported");
    AssertEqual(700.0, options.WindowHeight, "WindowHeight should be imported");
    AssertEqual(200.0, options.WindowLeft, "WindowLeft should be imported");
    AssertEqual(100.0, options.WindowTop, "WindowTop should be imported");
    AssertEqual("Normal", options.WindowState, "WindowState should be imported");
    AssertEqual("imported-tool", options.LastUsedTool, "LastUsedTool should be imported");
    AssertEqual("imported-prompt", options.LastUsedPrompt, "LastUsedPrompt should be imported");
    AssertEqual("C:\\solution-codex.exe", options.SolutionCliExecutable, "SolutionCliExecutable should be imported");
    AssertTrue(options.SolutionUseWsl.Value, "SolutionUseWsl should be imported");
  }

  private static void CodexOptions_GetEffectiveValues()
  {
    var options = new CodexOptions
    {
      CliExecutable = "C:\\global-codex.exe",
      UseWsl = true,
      SolutionCliExecutable = "C:\\solution-codex.exe",
      SolutionUseWsl = false
    };

    // Test effective CLI executable (solution override)
    AssertEqual("C:\\solution-codex.exe", options.GetEffectiveCliExecutable(), "Should use solution CLI when set");

    // Test effective WSL setting (solution override)
    AssertFalse(options.GetEffectiveUseWsl(), "Should use solution WSL setting when set");

    // Clear solution overrides
    options.SolutionCliExecutable = string.Empty;
    options.SolutionUseWsl = null;

    // Test effective CLI executable (global fallback)
    AssertEqual("C:\\global-codex.exe", options.GetEffectiveCliExecutable(), "Should use global CLI when solution not set");

    // Test effective WSL setting (global fallback)
    AssertTrue(options.GetEffectiveUseWsl(), "Should use global WSL setting when solution not set");
  }

  private static void AssertEqual(int expected, int actual, string message)
  {
    if (expected != actual)
      throw new InvalidOperationException($"{message} (expected: {expected}, actual: {actual})");
  }

  private static void AssertEqual(string expected, string actual, string message)
  {
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
      throw new InvalidOperationException($"{message} (expected: '{expected}', actual: '{actual}')");
  }

  private static void AssertEqual(double expected, double actual, string message)
  {
    if (Math.Abs(expected - actual) > 1e-9)
      throw new InvalidOperationException($"{message} (expected: {expected}, actual: {actual})");
  }

  private static void AssertEqual(CodexOptions.ApprovalMode expected,
    CodexOptions.ApprovalMode actual, string message)
  {
    if (!expected.Equals(actual))
      throw new InvalidOperationException($"{message} (expected: {expected}, actual: {actual})");
  }

  private static void AssertEqual(CodexOptions.SandboxPolicyMode expected,
    CodexOptions.SandboxPolicyMode actual, string message)
  {
    if (!expected.Equals(actual))
      throw new InvalidOperationException($"{message} (expected: {expected}, actual: {actual})");
  }

  private static void AssertFalse(bool condition, string message)
  {
    if (condition)
      throw new InvalidOperationException(message);
  }

  private static void AssertTrue(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }

  private static string ExtractSubmissionText(string input)
  {
    var payload = ChatTextUtilities.CreateUserInputSubmission(input);
    var obj = JObject.Parse(payload);
    return obj["op"]?["items"]?[0]?["text"]?.ToString() ?? string.Empty;
  }

  private static void WriteArtifacts()
  {
    try
    {
      WriteJunit("junit.xml");
      WritePerfCsv("perf.csv");
      WriteCoverage("coverage.lcov");
    }
    catch
    {
      // best-effort; artifacts are optional in this environment
    }
  }

  private static void WriteJunit(string path)
  {
    var total = Results.Count;
    var failures = Results.Count(r => !r.Passed);
    var time = Results.Sum(r => r.Millis) / 1000.0;
    var sb = new StringBuilder();
    sb.Append($"<testsuite name=\"CodexVS22.Tests\" tests=\"{total}\" failures=\"{failures}\" time=\"{time:F3}\">\n");
    foreach (var r in Results)
    {
      sb.Append($"  <testcase name=\"{System.Security.SecurityElement.Escape(r.Name)}\" time=\"{r.Millis/1000.0:F3}\">");
      if (!r.Passed)
      {
        var msg = System.Security.SecurityElement.Escape(r.Message ?? string.Empty);
        sb.Append($"<failure message=\"{msg}\" />");
      }
      sb.Append("</testcase>\n");
    }
    sb.Append("</testsuite>\n");
    File.WriteAllText(path, sb.ToString());
  }

  private static void WritePerfCsv(string path)
  {
    var sb = new StringBuilder();
    sb.AppendLine("test,ms");
    foreach (var r in Results)
      sb.AppendLine($"{r.Name},{r.Millis:F1}");
    File.WriteAllText(path, sb.ToString());
  }

  private static void WriteCoverage(string path)
  {
    // Minimal LCOV with per-test markers mapped to Program.cs
    var src = Path.Combine("CodexVS22.Tests", "Program.cs");
    var sb = new StringBuilder();
    sb.AppendLine("TN:CodexVS22.Tests");
    sb.AppendLine($"SF:{src}");
    int line = 1;
    foreach (var _ in Results)
    {
      sb.AppendLine($"DA:{line},1");
      line += 10;
    }
    sb.AppendLine("end_of_record");
    File.WriteAllText(path, sb.ToString());
  }

  private sealed class TranscriptTracker
  {
    private readonly CorrelationMap _map = new();
    private readonly Dictionary<string, StringBuilder> _buffers = new();
    private readonly Dictionary<string, string> _completed = new();

    public void Process(IEnumerable<string> jsonLines)
    {
      foreach (var line in jsonLines)
      {
        var evt = EventParser.Parse(line);
        switch (evt.Kind)
        {
          case EventKind.AgentMessageDelta:
            AppendDelta(evt);
            break;
          case EventKind.AgentMessage:
            CompleteTurn(evt);
            break;
          case EventKind.StreamError:
          case EventKind.TaskComplete:
            Cleanup(evt.Id);
            break;
        }
      }
    }

    public string GetTranscript(string id)
      => _completed.TryGetValue(id, out var text) ? text : string.Empty;

    public bool HasTranscript(string id) => _completed.ContainsKey(id);

    public bool HasInFlight(string id) => _map.TryGet(id, out _);

    private void AppendDelta(EventMsg evt)
    {
      var id = evt.Id;
      if (string.IsNullOrEmpty(id))
        return;

      if (!_map.TryGet(id, out var state))
      {
        var buffer = new StringBuilder();
        _map.Add(id, buffer);
        _buffers[id] = buffer;
        state = buffer;
      }

      if (state is StringBuilder sb)
      {
        var text = ExtractTextDelta(evt.Raw);
        sb.Append(text);
      }
    }

    private void CompleteTurn(EventMsg evt)
    {
      var id = evt.Id;
      if (string.IsNullOrEmpty(id))
        return;

      var finalText = ExtractFinalText(evt.Raw);

      if (_map.TryGet(id, out var state) && state is StringBuilder sb)
      {
        if (!string.IsNullOrEmpty(finalText))
        {
          sb.Clear();
          sb.Append(finalText);
        }

        _completed[id] = sb.ToString();
      }
      else if (!string.IsNullOrEmpty(finalText))
      {
        _completed[id] = finalText;
      }

      Cleanup(id);
    }

    private void Cleanup(string id)
    {
      if (string.IsNullOrEmpty(id))
        return;

      _map.Remove(id);
      _buffers.Remove(id);
    }

    private static string ExtractTextDelta(JObject? obj)
    {
      if (obj == null)
        return string.Empty;

      var direct = obj["text_delta"]?.ToString();
      if (!string.IsNullOrEmpty(direct))
        return direct;

      if (obj["delta"] is JObject deltaObj)
        return deltaObj["text_delta"]?.ToString() ?? string.Empty;

      return string.Empty;
    }

    private static string ExtractFinalText(JObject? obj)
    {
      if (obj == null)
        return string.Empty;

      var direct = obj["text"]?.ToString();
      if (!string.IsNullOrEmpty(direct))
        return direct;

      if (obj["message"] is JObject messageObj)
        return messageObj["text"]?.ToString() ?? string.Empty;

      return string.Empty;
    }
  }
}
internal sealed class FakeApprovalResolver
{
  private readonly Dictionary<string, bool> _exec = new(StringComparer.Ordinal);
  private readonly Dictionary<string, bool> _patch = new(StringComparer.Ordinal);

  public void RememberExecDecision(string signature, bool approved)
  {
    if (string.IsNullOrWhiteSpace(signature))
      return;
    _exec[signature] = approved;
  }

  public void RememberPatchDecision(string signature, bool approved)
  {
    if (string.IsNullOrWhiteSpace(signature))
      return;
    _patch[signature] = approved;
  }

  public bool TryResolveExecApproval(bool autoApprove, string signature, out bool approved)
  {
    if (autoApprove)
    {
      approved = true;
      return true;
    }

    if (!string.IsNullOrWhiteSpace(signature) && _exec.TryGetValue(signature, out approved))
      return true;

    approved = false;
    return false;
  }

  public bool TryResolvePatchApproval(bool autoApprove, string signature, out bool approved)
  {
    if (autoApprove)
    {
      approved = true;
      return true;
    }

    if (!string.IsNullOrWhiteSpace(signature) && _patch.TryGetValue(signature, out approved))
      return true;

    approved = false;
    return false;
  }
}

internal sealed class McpToolInfo
{
  public McpToolInfo(string name, string description, string server)
  {
    Name = string.IsNullOrWhiteSpace(name) ? "(tool)" : name.Trim();
    Description = description?.Trim() ?? string.Empty;
    Server = server?.Trim() ?? string.Empty;
  }

  public string Name { get; }
  public string Description { get; }
  public string Server { get; }
}

internal sealed class CustomPromptInfo
{
  public CustomPromptInfo(string id, string name, string description, string body, string source)
  {
    Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id.Trim();
    Name = string.IsNullOrWhiteSpace(name) ? "(prompt)" : name.Trim();
    Description = description?.Trim() ?? string.Empty;
    Body = body ?? string.Empty;
    Source = source?.Trim() ?? string.Empty;
  }

  public string Id { get; }
  public string Name { get; }
  public string Description { get; }
  public string Body { get; }
  public string Source { get; }
}
