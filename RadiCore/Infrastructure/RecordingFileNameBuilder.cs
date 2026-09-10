using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RadiCore.Infrastructure
{
    /// <summary>録音ファイル名テンプレートで使えるプレースホルダーの定義（設定画面の説明表示に使用）</summary>
    /// <param name="Token">テンプレートに書く文字列（例: <c>{program}</c>）</param>
    /// <param name="Description">説明</param>
    /// <param name="SupportsFormat">「:書式」による書式指定に対応するか</param>
    public record FileNamePlaceholder(string Token, string Description, bool SupportsFormat);

    /// <summary>ファイル名テンプレートの展開に使う録音のメタデータ</summary>
    public record RecordingFileNameMetadata
    {
        /// <summary>番組名</summary>
        public string? ProgramName { get; init; }
        /// <summary>出演者</summary>
        public string? CastName { get; init; }
        /// <summary>放送局名</summary>
        public string? StationName { get; init; }
        /// <summary>放送局ID</summary>
        public string? StationId { get; init; }
        /// <summary>番組ID</summary>
        public string? ProgramId { get; init; }
        /// <summary>録音開始日時</summary>
        public DateTime StartTime { get; init; }
        /// <summary>録音終了日時</summary>
        public DateTime EndTime { get; init; }
        /// <summary>予約ID</summary>
        public int ReservationId { get; init; }
    }

    /// <summary>
    /// 録音ファイル名テンプレートを実際のファイル名へ展開する。
    /// テンプレートは拡張子を除いた部分を表し、展開後に必ず .m4a を付与する。
    /// </summary>
    public static class RecordingFileNameBuilder
    {
        /// <summary>録音ファイルの拡張子</summary>
        public const string Extension = ".m4a";

        /// <summary>拡張子を除いたファイル名の最大バイト数（UTF-8。Linuxの255バイト制限に対する余裕を持たせる）</summary>
        public const int MaxFileNameBytes = 200;

        /// <summary>テンプレート未設定時に使うファイル名</summary>
        public const string DefaultTemplate = "{date:yyyyMMdd}_{program}";

        /// <summary>設定画面のプレビューに使うサンプルメタデータ</summary>
        public static readonly RecordingFileNameMetadata SampleMetadata = new()
        {
            ProgramName   = "サンプル番組",
            CastName      = "サンプル出演者",
            StationName   = "サンプル放送局",
            StationId     = "SAMPLE",
            ProgramId     = "2026091013000000",
            StartTime     = new DateTime(2026, 9, 10, 13, 0, 0),
            EndTime       = new DateTime(2026, 9, 10, 15, 0, 0),
            ReservationId = 1,
        };

        /// <summary>使用できるプレースホルダーの一覧</summary>
        public static readonly IReadOnlyList<FileNamePlaceholder> Placeholders =
        [
            new("{program}",       "番組名",                                    false),
            new("{cast}",          "出演者",                                    false),
            new("{station}",       "放送局名",                                  false),
            new("{stationId}",     "放送局ID",                                  false),
            new("{programId}",     "番組ID",                                    false),
            new("{date}",          "放送日（既定: yyyyMMdd）",                  true),
            new("{start}",         "録音開始日時（既定: HHmm）",                true),
            new("{end}",           "録音終了日時（既定: HHmm）",                true),
            new("{reservationId}", "予約ID",                                    false),
        ];

        private static readonly Regex PlaceholderRegex =
            new(@"\{(?<name>[A-Za-z][A-Za-z0-9]*)(?::(?<format>[^{}]*))?\}", RegexOptions.Compiled);

        /// <summary>
        /// テンプレートを展開し、拡張子付きのファイル名を返す。
        /// テンプレートが空、または展開結果が空になる場合は既定のテンプレートを使う。
        /// </summary>
        public static string Build(string? template, RecordingFileNameMetadata metadata)
        {
            string name = Sanitize(Expand(template, metadata));

            if (name.Length == 0)
                name = Sanitize(Expand(DefaultTemplate, metadata));

            if (name.Length == 0)
                name = $"recording_{metadata.StartTime:yyyyMMddHHmmss}";

            return name + Extension;
        }

        /// <summary>
        /// テンプレートに未知のプレースホルダーが含まれていないか検証する。
        /// </summary>
        /// <param name="unknownTokens">未知のプレースホルダー（例: <c>{foo}</c>）</param>
        public static bool TryValidate(string? template, out List<string> unknownTokens)
        {
            unknownTokens = [];
            if (string.IsNullOrWhiteSpace(template))
                return true;

            foreach (Match m in PlaceholderRegex.Matches(template))
            {
                string name = m.Groups["name"].Value;
                if (ResolveValue(name, m.Groups["format"].Success ? m.Groups["format"].Value : null, SampleMetadata) == null)
                {
                    string token = $"{{{name}}}";
                    if (!unknownTokens.Contains(token))
                        unknownTokens.Add(token);
                }
            }

            return unknownTokens.Count == 0;
        }

        /// <summary>プレースホルダーを実際の値へ置換する（サニタイズ前）</summary>
        private static string Expand(string? template, RecordingFileNameMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(template))
                return "";

            return PlaceholderRegex.Replace(template, m =>
            {
                string name = m.Groups["name"].Value;
                string? format = m.Groups["format"].Success ? m.Groups["format"].Value : null;
                // 未知のプレースホルダーはそのまま残し、設定ミスに気付けるようにする
                return ResolveValue(name, format, metadata) ?? m.Value;
            });
        }

        /// <summary>プレースホルダー1個分の値を返す。未知の名前の場合は null</summary>
        private static string? ResolveValue(string name, string? format, RecordingFileNameMetadata m) => name.ToLowerInvariant() switch
        {
            "program"       => m.ProgramName ?? "",
            "cast"          => m.CastName    ?? "",
            "station"       => m.StationName ?? "",
            "stationid"     => m.StationId   ?? "",
            "programid"     => m.ProgramId   ?? "",
            "date"          => m.StartTime.ToString(EmptyToNull(format) ?? "yyyyMMdd", CultureInfo.InvariantCulture),
            "start"         => m.StartTime.ToString(EmptyToNull(format) ?? "HHmm",     CultureInfo.InvariantCulture),
            "end"           => m.EndTime.ToString(EmptyToNull(format)   ?? "HHmm",     CultureInfo.InvariantCulture),
            "reservationid" => m.ReservationId.ToString(CultureInfo.InvariantCulture),
            _               => null,
        };

        private static string? EmptyToNull(string? value) => string.IsNullOrEmpty(value) ? null : value;

        /// <summary>ファイル名として使えない文字を除去し、長さを制限する</summary>
        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);

            // 末尾のドット・空白はWindowsで扱えないため除去する
            string result = Truncate(sb.ToString().Trim()).TrimEnd('.', ' ');

            // 先頭のハイフンはffmpegにオプションとして解釈されるため、
            // Windowsの予約デバイス名（CON, COM1 など）はファイル名にできないため退避する
            if (result.StartsWith('-') || ReservedNames.Contains(result.Split('.')[0]))
                result = "_" + result;

            return result;
        }

        /// <summary>Windowsで使用できない予約デバイス名</summary>
        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        /// <summary>UTF-8バイト数が上限を超えないよう、文字境界を保って切り詰める</summary>
        private static string Truncate(string name)
        {
            if (Encoding.UTF8.GetByteCount(name) <= MaxFileNameBytes)
                return name;

            var sb = new StringBuilder();
            int bytes = 0;
            foreach (var rune in name.EnumerateRunes())
            {
                int runeBytes = rune.Utf8SequenceLength;
                if (bytes + runeBytes > MaxFileNameBytes)
                    break;

                sb.Append(rune.ToString());
                bytes += runeBytes;
            }

            return sb.ToString();
        }
    }
}
