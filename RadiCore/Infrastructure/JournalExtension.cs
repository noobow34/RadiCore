using System.Runtime.CompilerServices;

namespace RadiCore.Infrastructure
{
    public static class JournalExtension
    {
        private static readonly ConditionalWeakTable<object, string> _instanceIdList = [];

        // ── ジョブログ収集バッファ ─────────────────────────────────
        // ジョブインスタンスをキーに、そのジョブが出力したログ行を蓄積する
        private static readonly ConditionalWeakTable<object, List<string>> _jobLogBuffer = [];

        public static void JournalWriteLine(this object obj, string value)
        {
            if (!_instanceIdList.TryGetValue(obj, out string? instanceId))
            {
                instanceId = Ulid.NewUlid().ToString();
                _instanceIdList.Add(obj, instanceId);
            }
            var line = $"【{obj.GetType().Name}:{instanceId}】{value}";
            Console.WriteLine(line);

            // ジョブログバッファには時刻付きで追記
            if (_jobLogBuffer.TryGetValue(obj, out var buf))
                buf.Add($"[{DateTime.Now:HH:mm:ss}]{line}");
        }

        /// <summary>このオブジェクトのログ収集を開始する</summary>
        public static void JournalBeginCapture(this object obj)
        {
            // 既存バッファがあればクリア、なければ新規作成
            if (_jobLogBuffer.TryGetValue(obj, out var existing))
                existing.Clear();
            else
                _jobLogBuffer.Add(obj, []);
        }

        /// <summary>収集したログ行を返す（収集していない場合は空リスト）</summary>
        public static List<string> JournalGetCaptured(this object obj)
        {
            if (_jobLogBuffer.TryGetValue(obj, out var buf))
                return new List<string>(buf);
            return [];
        }
    }
}