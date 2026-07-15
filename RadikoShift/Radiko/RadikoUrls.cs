namespace RadikoShift.Radiko
{
    /// <summary>
    /// Radiko API の URL 定数
    /// </summary>
    public static class RadikoUrls
    {
        /// <summary>週刊番組表</summary>
        public const string WeeklyTimeTable = "https://radiko.jp/v3/program/station/weekly/[stationCode].xml";

        /// <summary>地域判定用</summary>
        public const string AreaCheck = "http://radiko.jp/area/";

        /// <summary>ログイン</summary>
        public const string Login = "https://radiko.jp/ap/member/webapi/member/login";

        /// <summary>ログインチェック</summary>
        public const string LoginCheck = "https://radiko.jp/ap/member/webapi/member/login/check";

        /// <summary>放送局一覧（全国）</summary>
        public const string StationListFull = "https://radiko.jp/v3/station/region/full.xml";

        /// <summary>放送局一覧（都道府県ごと）</summary>
        public const string StationListPref = "https://radiko.jp/v3/station/list/[AREA].xml";

        /// <summary>プレミアムログイン（radiko_session取得用）</summary>
        public const string PremiumLogin = "https://radiko.jp/v4/api/member/login";

        /// <summary>プレミアムログアウト</summary>
        public const string PremiumLogout = "https://radiko.jp/v4/api/member/logout";

        /// <summary>認証1（AuthToken/PartialKeyの元情報を取得）</summary>
        public const string Auth1 = "https://radiko.jp/v2/api/auth1";

        /// <summary>認証2（エリアID判定）</summary>
        public const string Auth2 = "https://radiko.jp/v2/api/auth2";

        /// <summary>タイムフリー再生用HLSプレイリストURL</summary>
        public const string StationStream = "https://radiko.jp/v3/station/stream/pc_html5/[stationCode].xml";
    }
}
