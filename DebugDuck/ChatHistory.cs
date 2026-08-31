using System;
using System.Collections.Generic;

namespace DebugDuck
{
    /// <summary>玩家送出的一則訊息。</summary>
    public sealed class ChatEntry
    {
        public DateTime TimestampUtc { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    /// 背景聊天歷史。聊天室畫面每次送出就清空，但玩家講過的每一句都留在這裡。
    /// 鴨子的回應「不」記錄。
    /// 之後的「提取訊息」功能就從這裡拿資料。
    /// </summary>
    public static class ChatHistory
    {
        private static readonly List<ChatEntry> _entries = new List<ChatEntry>();

        public static IReadOnlyList<ChatEntry> Entries => _entries;

        public static int Count => _entries.Count;

        public static void AddPlayerMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _entries.Add(new ChatEntry { TimestampUtc = DateTime.UtcNow, Text = text.Trim() });
        }

        public static void Clear() => _entries.Clear();
    }
}
