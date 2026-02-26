using System;
using System.Collections.ObjectModel;
using CodexVS22.Core.Chat;

namespace CodexVS22.ToolWindows.CodexToolWindow.ViewModels
{
    public class ChatTranscriptViewModel
    {
        private readonly ChatTranscriptReducer _reducer;

        public ChatTranscriptViewModel(ChatTranscriptReducer reducer = null)
        {
            _reducer = reducer ?? new ChatTranscriptReducer();
            TranscriptItems = new ObservableCollection<string>();
        }

        public ObservableCollection<string> TranscriptItems { get; }

        public bool IsStreaming { get; private set; }

        public void AddUserPrompt(string text)
        {
            var userTurn = _reducer.AddUserPrompt(text ?? string.Empty);
            var promptText = userTurn.Segments.Count > 0 ? userTurn.Segments[0].Text : string.Empty;
            TranscriptItems.Add(promptText);
        }

        public void ApplyDelta(string eventId, int index, string deltaText, bool isFinal)
        {
            var turn = _reducer.Reduce(new ChatMessageDelta(new ChatTurnId(eventId, index), deltaText, ChatSegmentKind.Markdown, isFinal));
            IsStreaming = turn.IsStreaming;

            if (!isFinal)
            {
                return;
            }

            var finalText = turn.Segments.Count > 0 ? turn.Segments[0].Text : string.Empty;
            TranscriptItems.Add(finalText);
        }

        public void Reset()
        {
            _reducer.Reset();
            TranscriptItems.Clear();
            IsStreaming = false;
        }
    }
}
