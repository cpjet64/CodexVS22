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
  private static readonly List<string> Failures = new();

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
    RunTest(nameof(PatchApply_WithMatchingOriginal_WritesFile), PatchApply_WithMatchingOriginal_WritesFile);
    RunTest(nameof(PatchApply_WithMismatchedOriginal_ReportsConflict), PatchApply_WithMismatchedOriginal_ReportsConflict);

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
    try
    {
      action();
    }
    catch (Exception ex)
    {
      Failures.Add($"{name} failed: {ex.Message}");
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

  private static void PatchApply_WithMatchingOriginal_WritesFile()
  {
    var path = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}.txt");
    try
    {
      File.WriteAllText(path, "line1\nline2\n");
      var result = DiffUtilities.ApplyPatchToFileForTests(path, "line1\nline2\n", "line1\nline2\nline3\n");
      AssertEqual(DiffUtilities.NormalizeForComparison("line1\nline2\nline3\n"), DiffUtilities.NormalizeForComparison(File.ReadAllText(path)), "Patched file content mismatch");
      if (result != DiffUtilities.PatchApplyResult.Applied)
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
      if (result != DiffUtilities.PatchApplyResult.Conflict)
        throw new InvalidOperationException("Expected conflict when originals differ");
      AssertEqual(DiffUtilities.NormalizeForComparison("current\n"), DiffUtilities.NormalizeForComparison(File.ReadAllText(path)), "File should remain unchanged after conflict");
    }
    finally
    {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  private static void AssertEqual(string expected, string actual, string message)
  {
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
      throw new InvalidOperationException($"{message} (expected: '{expected}', actual: '{actual}')");
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
