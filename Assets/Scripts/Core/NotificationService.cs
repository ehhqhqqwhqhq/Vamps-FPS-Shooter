using System;
using System.Collections.Generic;

namespace Vamp.Core
{
    public enum NotificationKind
    {
        Info,
        FriendRequest,
        FriendAccepted,
        PartyInvite,
        FriendOnline,
        MatchFound,
        Achievement,
        LevelUp,
        Unlock,
        Challenge,
        Warning,
        Error
    }

    public struct GameNotification
    {
        public NotificationKind Kind;
        public string Title;
        public string Message;
        public DateTime Time;
    }

    /// <summary>Queue of toast notifications (friend requests, level ups, unlocks, match found...). UI subscribes.</summary>
    public sealed class NotificationService
    {
        private readonly List<GameNotification> _history = new List<GameNotification>();

        public event Action<GameNotification> Posted;
        public IReadOnlyList<GameNotification> History { get { return _history; } }
        public int UnreadCount { get; private set; }

        public void Push(NotificationKind kind, string title, string message = "")
        {
            var n = new GameNotification { Kind = kind, Title = title, Message = message, Time = DateTime.Now };
            _history.Add(n);
            if (_history.Count > 50) _history.RemoveAt(0);
            UnreadCount++;
            if (Posted != null) Posted(n);
        }

        public void MarkAllRead() { UnreadCount = 0; }
    }
}
