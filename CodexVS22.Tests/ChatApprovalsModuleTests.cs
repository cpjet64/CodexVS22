using System;
using CodexVS22.Core.Approvals;
using CodexVS22.Core.Chat;

namespace CodexVS22.Tests
{
  internal static partial class Program
  {
    private static void ChatTranscriptReducer_FinalizesStreamingTurn()
    {
      var reducer = new ChatTranscriptReducer();
      var turnId = new ChatTurnId("turn-1", 1);

      var streaming = reducer.Reduce(new ChatMessageDelta(turnId, "Hello ", ChatSegmentKind.Markdown, isFinal: false));
      AssertTrue(streaming.IsStreaming, "Turn should remain streaming before final delta");
      AssertEqual(0, streaming.Segments.Count, "Streaming turn should not finalize segments early");

      var finalized = reducer.Reduce(new ChatMessageDelta(turnId, "world", ChatSegmentKind.Markdown, isFinal: true));
      AssertFalse(finalized.IsStreaming, "Turn should stop streaming on final delta");
      AssertEqual(1, finalized.Segments.Count, "Finalized turn should create one transcript segment");
      AssertEqual("Hello world", finalized.Segments[0].Text, "Finalized transcript text mismatch");
      AssertEqual(1, reducer.Turns.Count, "Reducer should keep single turn for same turn id");
    }

    private static void ApprovalService_QueuesAndResolvesInOrder()
    {
      var service = new ApprovalService();
      string firstPrompt = string.Empty;
      string secondPrompt = string.Empty;
      var resolvedCount = 0;

      service.PromptRaised += (_, prompt) =>
      {
        if (string.IsNullOrEmpty(firstPrompt))
        {
          firstPrompt = prompt.Request.CallId;
        }
        else
        {
          secondPrompt = prompt.Request.CallId;
        }
      };

      service.ApprovalResolved += (_, __) => resolvedCount++;

      service.QueueAsync(new PendingApproval("call-1", ApprovalType.Exec, "sig-1", "Run command?")).GetAwaiter().GetResult();
      service.QueueAsync(new PendingApproval("call-2", ApprovalType.Patch, "sig-2", "Apply patch?")).GetAwaiter().GetResult();

      AssertEqual("call-1", firstPrompt, "First queued approval should be active prompt");
      AssertEqual(2, service.PendingCount, "Pending count should include active + queued approvals");

      service.ResolveAsync("call-1", ApprovalDecision.Approved, rememberDecision: false).GetAwaiter().GetResult();
      AssertEqual("call-2", secondPrompt, "Second prompt should become active after first resolution");

      service.ResolveAsync("call-2", ApprovalDecision.Denied, rememberDecision: false).GetAwaiter().GetResult();
      AssertEqual(2, resolvedCount, "Both queued approvals should be resolved");
      AssertEqual(0, service.PendingCount, "Pending approvals should be empty after resolving queue");
    }

    private static void ApprovalService_RememberedDecisionSkipsQueue()
    {
      var service = new ApprovalService();
      var resolvedCount = 0;
      var promptCount = 0;

      service.ApprovalResolved += (_, approval) =>
      {
        resolvedCount++;
        AssertEqual("approved", approval.Metadata["decision"], "Remembered decision metadata mismatch");
      };

      service.PromptRaised += (_, __) => promptCount++;

      service.QueueAsync(new PendingApproval("call-1", ApprovalType.Exec, "same-signature", "Prompt one")).GetAwaiter().GetResult();
      service.ResolveAsync("call-1", ApprovalDecision.Approved, rememberDecision: true).GetAwaiter().GetResult();
      service.QueueAsync(new PendingApproval("call-2", ApprovalType.Exec, "same-signature", "Prompt two")).GetAwaiter().GetResult();

      AssertEqual(2, resolvedCount, "Second request should auto-resolve from remembered decision");
      AssertEqual(1, promptCount, "Remembered decision should bypass second UI prompt");
      AssertEqual(0, service.PendingCount, "No pending approvals expected when remembered decision applies");
    }
  }
}
