using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CodexVS22.Core.Chat
{
    public enum ChatRole
    {
        User = 0,
        Assistant = 1,
        Status = 2
    }

    public enum ChatSegmentKind
    {
        PlainText = 0,
        Code = 1,
        Markdown = 2
    }

    public sealed class ChatTurnId : IEquatable<ChatTurnId>
    {
        public ChatTurnId(string eventId, int index)
        {
            EventId = string.IsNullOrWhiteSpace(eventId) ? string.Empty : eventId.Trim();
            Index = index < 0 ? 0 : index;
        }

        public string EventId { get; }

        public int Index { get; }

        public bool Equals(ChatTurnId? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(EventId, other.EventId, StringComparison.Ordinal) && Index == other.Index;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ChatTurnId);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((EventId != null ? StringComparer.Ordinal.GetHashCode(EventId) : 0) * 397) ^ Index;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", EventId, Index);
        }
    }

    public sealed class ChatSegmentModel
    {
        public ChatSegmentModel(ChatSegmentKind kind, string text)
        {
            Kind = kind;
            Text = text ?? string.Empty;
        }

        public ChatSegmentKind Kind { get; }

        public string Text { get; }
    }

    public sealed class ChatTurnModel
    {
        public ChatTurnModel(ChatTurnId id, ChatRole role, DateTime timestampUtc)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Role = role;
            TimestampUtc = timestampUtc;
            Segments = new ObservableCollection<ChatSegmentModel>();
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public ChatTurnId Id { get; }

        public ChatRole Role { get; }

        public DateTime TimestampUtc { get; }

        public bool IsStreaming { get; set; }

        public string StreamingBuffer { get; set; } = string.Empty;

        public ObservableCollection<ChatSegmentModel> Segments { get; }

        public IDictionary<string, string> Metadata { get; }
    }

    public sealed class ChatMessageDelta
    {
        public ChatMessageDelta(ChatTurnId turnId, string deltaText, ChatSegmentKind segmentKind, bool isFinal)
        {
            TurnId = turnId ?? throw new ArgumentNullException(nameof(turnId));
            DeltaText = deltaText ?? string.Empty;
            SegmentKind = segmentKind;
            IsFinal = isFinal;
            TimestampUtc = DateTime.UtcNow;
        }

        public ChatTurnId TurnId { get; }

        public string DeltaText { get; }

        public ChatSegmentKind SegmentKind { get; }

        public bool IsFinal { get; }

        public DateTime TimestampUtc { get; }
    }
}
