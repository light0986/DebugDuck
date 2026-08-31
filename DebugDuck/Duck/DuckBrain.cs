using System;
using System.Linq;

namespace DebugDuck.Duck
{
    public sealed class DuckReply
    {
        public DuckReply(string text, DuckState reaction)
        {
            Text = text;
            Reaction = reaction;
        }

        public string Text { get; }
        public DuckState Reaction { get; }
    }

    /// <summary>
    /// 小黃鴨除錯法：鴨子只會「嘎」。
    /// 台詞只有 5 種：嘎 / 嘎嘎 / 嘎嘎嘎 / ? / !
    /// </summary>
    public sealed class DuckBrain
    {
        private static readonly string[] Quacks = { "嘎", "嘎嘎", "嘎嘎嘎" };

        private readonly Random _rng = new Random();
        private string _lastQuack = "";

        public DuckReply Respond(string input)
        {
            var text = (input ?? "").Trim();
            var lower = text.ToLowerInvariant();

            // 你自己想通了 → ！
            if (ContainsAny(lower, "解決", "搞定", "找到了", "原來", "懂了", "是這個",
                                   "fixed", "solved", "got it", "works now", "nailed it"))
                return new DuckReply("!", DuckState.Happy);

            // 你在問問題 → ？（把問題丟回給你）
            if (text.Length == 0 ||
                text.Contains("?") || text.Contains("？") ||
                ContainsAny(lower, "為什麼", "為何", "怎麼", "怎會", "how", "why", "what"))
                return new DuckReply("?", DuckState.Listen);

            // 其他 → 嘎（繼續講）
            return new DuckReply(NextQuack(), DuckState.Talk);
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            return needles.Any(n => haystack.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private string NextQuack()
        {
            var q = Quacks[_rng.Next(Quacks.Length)];
            if (q == _lastQuack) q = Quacks[(Array.IndexOf(Quacks, q) + 1) % Quacks.Length];
            _lastQuack = q;
            return q;
        }
    }
}
