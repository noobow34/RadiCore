using RadiCore.Data;
using SlackNet.Blocks;
using SlackNet.WebApi;

namespace RadiCore.Infrastructure
{
    /// <summary>Slack 通知メッセージの組み立て</summary>
    public static class SlackNotifier
    {
        /// <summary>録音完了通知を Block Kit（テーブル）で組み立てる</summary>
        /// <param name="reservation">録音元の予約</param>
        /// <param name="recording">保存された録音</param>
        /// <param name="imageUrl">番組画像URL（あればサムネイルとして表示）</param>
        /// <param name="autoDeleted">前回分の録音を自動削除したか</param>
        public static Message BuildRecordingCompleted(Reservation reservation, Recording recording, string? imageUrl, bool autoDeleted)
        {
            string programName = recording.ProgramName ?? "（番組名不明）";
            string stationName = recording.StationName ?? recording.StationId;
            var duration = recording.EndTime - recording.StartTime;

            // 番組画像・番組名・放送局を1行にまとめる。
            // section の accessory は必ず右端に描画されるため、画像を左に出すには context の画像要素にする
            var headlineElements = new List<IContextElement>();
            if (!string.IsNullOrWhiteSpace(imageUrl)
                && Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                headlineElements.Add(new Image { ImageUrl = uri.ToString(), AltText = programName });
            }
            headlineElements.Add(new Markdown { Text = $"*{Escape(programName)}*　{Escape(stationName)}" });

            List<IList<TableCell>> rows =
            [
                Row("項目", "内容"),
                Row("放送日時", $"{recording.StartTime:yyyy/MM/dd(ddd) HH:mm} - {recording.EndTime:HH:mm}（{(int)duration.TotalMinutes}分）"),
            ];
            if (!string.IsNullOrWhiteSpace(recording.CastName))
                rows.Add(Row("出演", recording.CastName));
            rows.Add(Row("ファイル", recording.FileName));
            rows.Add(Row("サイズ", recording.FileSizeText));
            rows.Add(Row("予約", reservation.ScheduleText));

            var footer = $"録音ID: #{recording.Id} ・ 予約ID: #{reservation.Id} ・ 保存: {recording.CreatedAt.ToLocalTime():yyyy/MM/dd HH:mm}";
            if (autoDeleted)
                footer += " ・ :wastebasket: 前回分の録音を自動削除しました";

            return new Message
            {
                // 通知バナー・検索用のフォールバックテキスト
                Text = $"録音完了 {programName}（{recording.StartTime:yyyy/MM/dd HH:mm}-{recording.EndTime:HH:mm} {stationName}）",
                Blocks =
                [
                    new HeaderBlock { Text = new PlainText { Text = ":white_check_mark: 録音完了", Emoji = true } },
                    new ContextBlock { Elements = headlineElements },
                    new TableBlock
                    {
                        Rows = rows,
                        ColumnSettings =
                        [
                            new TableColumnSettings { Align = ColumnAlignment.Left, IsWrapped = false },
                            new TableColumnSettings { Align = ColumnAlignment.Left, IsWrapped = true },
                        ]
                    },
                    new ContextBlock { Elements = [new Markdown { Text = footer }] },
                ]
            };
        }

        private static IList<TableCell> Row(string label, string value) =>
        [
            new RawTextCell { Text = label },
            new RawTextCell { Text = value },
        ];

        /// <summary>mrkdwn で特別扱いされる文字をエスケープする</summary>
        private static string Escape(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
