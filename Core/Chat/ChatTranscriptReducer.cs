using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CodexVS22.Core.Chat
{
    public sealed class ChatTranscriptReducer
    {
        private readonly object _gate = new();
        private readonly Dictionary<ChatTurnId, ChatTurnModel> _turnIndex = new();
        private readonly ObservableCollection<ChatTurnModel> _turns = new();

        public ReadOnlyObservableCollection<ChatTurnModel> Turns { get; }

        public ChatTranscriptReducer()
        {
            Turns = new ReadOnlyObservableCollection<ChatTurnModel>(_turns);
        }

        public ChatTurnModel Reduce(ChatMessageDelta delta)
        {
            if (delta is null)
            {
                throw new ArgumentNullException(nameof(delta));
            }

            lock (_gate)
            {
                if (!_turnIndex.TryGetValue(delta.TurnId, out var turn))
                {
                    turn = new ChatTurnModel(delta.TurnId, ChatRole.Assistant, delta.TimestampUtc);
                    _turnIndex.Add(delta.TurnId, turn);
                    _turns.Add(turn);
                }

                turn.IsStreaming = !delta.IsFinal;
                if (!string.IsNullOrEmpty(delta.DeltaText))
                {
                    turn.StreamingBuffer += delta.DeltaText;
                }

                if (delta.IsFinal)
                {
                    var finalizedText = Core.ChatTextUtilities.NormalizeAssistantText(turn.StreamingBuffer);
                    turn.Segments.Clear();
                    turn.Segments.Add(new ChatSegmentModel(delta.SegmentKind, finalizedText));
                    turn.StreamingBuffer = string.Empty;
                }

                return turn;
            }
        }

        public ChatTurnModel AddUserPrompt(string text)
        {
            lock (_gate)
            {
                var id = new ChatTurnId("user-" + (_turns.Count + 1), _turns.Count + 1);
                var turn = new ChatTurnModel(id, ChatRole.User, DateTime.UtcNow);
                turn.Segments.Add(new ChatSegmentModel(ChatSegmentKind.PlainText, text ?? string.Empty));
                _turnIndex[id] = turn;
                _turns.Add(turn);
                return turn;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _turnIndex.Clear();
                _turns.Clear();
            }
        }

        public IReadOnlyCollection<ChatTurnModel> Snapshot()
        {
            lock (_gate)
            {
                return _turns.ToList();
            }
        }
    }
}
