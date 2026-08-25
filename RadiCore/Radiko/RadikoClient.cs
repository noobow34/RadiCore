using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RadiCore.Data;

namespace RadiCore.Radiko
{
    public class RadikoClient
    {
        /// <summary>認証キー（https://radiko.jp/apps/js/playerCommon.js より）</summary>
        private const string AuthKeyValue = "bcd151073c03b352e1ef2fd66c32209da9ca0afa";
        /// <summary>
        /// ログインし、認証済み HttpClient を返す。
        /// メール・パスが空の場合は未ログイン状態の HttpClient を返す。
        /// </summary>
        public static async Task<HttpClient> CreateHttpClient(
            string email, string pass,
            DecompressionMethods decompression = DecompressionMethods.GZip | DecompressionMethods.Deflate)
        {
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                UseCookies             = true,
                CookieContainer        = cookieContainer,
                AutomaticDecompression = decompression
            };
            var client = new HttpClient(handler);

            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(pass))
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "mail", email },
                    { "pass",  pass  }
                });
                var res = await client.PostAsync(RadikoUrls.Login, content);
                await res.Content.ReadAsStringAsync();

                res = await client.GetAsync(RadikoUrls.LoginCheck);
                await res.Content.ReadAsStringAsync();
            }

            return client;
        }

        /// <summary>放送局一覧を取得する</summary>
        public static async Task<List<Station>> GetStations(bool login, HttpClient httpClient)
        {
            var xmlUrl = RadikoUrls.StationListFull;

            if (!login)
            {
                var text = await httpClient.GetStringAsync(RadikoUrls.AreaCheck).ConfigureAwait(false);
                var m = Regex.Match(text, @"JP[0-9]+");
                if (m.Success)
                    xmlUrl = RadikoUrls.StationListPref.Replace("[AREA]", m.Value);
            }

            var res = new List<Station>();
            var stream = await httpClient.GetStreamAsync(xmlUrl).ConfigureAwait(false);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None).ConfigureAwait(false);

            int stationOrder = 1;
            foreach (var stations in doc.Descendants("stations"))
            {
                var regionId   = stations.Attribute("region_id")?.Value   ?? "";
                var regionName = stations.Attribute("region_name")?.Value ?? "";

                foreach (var station in stations.Descendants("station"))
                {
                    var code   = station.Descendants("id").FirstOrDefault()?.Value      ?? "";
                    var name   = station.Descendants("name").FirstOrDefault()?.Value    ?? "";
                    var areaId = station.Descendants("area_id").FirstOrDefault()?.Value ?? "";

                    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
                        continue;

                    res.Add(new Station
                    {
                        Id           = code,
                        RegionId     = regionId,
                        RegionName   = regionName,
                        Name         = name,
                        AreaCode     = areaId,
                        DisplayOrder = stationOrder++
                    });
                }
            }

            var sorted = res
                .GroupBy(s => s.AreaCode)
                .OrderBy(g => g.Min(x => x.DisplayOrder))
                .SelectMany(g => g.OrderBy(x => x.DisplayOrder))
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
                sorted[i].DisplayOrder = i + 1;

            return sorted;
        }

        /// <summary>番組表を取得する</summary>
        public static async Task<List<Data.Program>> GetPrograms(Station station, HttpClient httpClient)
        {
            var stream = await httpClient.GetStreamAsync(
                RadikoUrls.WeeklyTimeTable.Replace("[stationCode]", station.Id));

            var doc = XDocument.Load(stream);

            return doc.Descendants("prog")
                .Select(prog => new Data.Program
                {
                    Id          = station.Id + prog.Attribute("ft")?.Value + prog.Attribute("to")?.Value,
                    StartTime   = DateTime.ParseExact(prog.Attribute("ft")?.Value!, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal),
                    EndTime     = DateTime.ParseExact(prog.Attribute("to")?.Value!, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal),
                    Title       = prog.Element("title")?.Value.Trim(),
                    CastName    = prog.Element("pfm")?.Value.Trim(),
                    Description = prog.Element("info")?.Value.Trim(),
                    StationId   = station.Id,
                    ImageUrl    = prog.Element("img")?.Value.Trim(),
                })
                .ToList();
        }

        /// <summary>
        /// ラジコプレミアムにログインし、HLS認証(auth2)で使用するradiko_sessionを取得する
        /// </summary>
        public static async Task<(string RadikoSession, bool IsAreaFree)> LoginPremiumAsync(string mail, string password, HttpClient httpClient)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "mail", mail },
                { "pass", password }
            });

            var res = await httpClient.PostAsync(RadikoUrls.PremiumLogin, content);
            var json = await res.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string radikoSession = root.TryGetProperty("radiko_session", out var sessionEl)
                ? (sessionEl.ValueKind == JsonValueKind.String ? sessionEl.GetString() ?? "" : sessionEl.ToString())
                : "";

            bool isAreaFree = root.TryGetProperty("areafree", out var areafreeEl)
                && (areafreeEl.ValueKind == JsonValueKind.String ? areafreeEl.GetString() : areafreeEl.ToString()) == "1";

            if (string.IsNullOrEmpty(radikoSession))
                throw new RadikoException("Radikoプレミアムログインに失敗しました");

            return (radikoSession, isAreaFree);
        }

        /// <summary>ラジコプレミアムからログアウトする（失敗しても無視する）</summary>
        public static async Task LogoutPremiumAsync(string radikoSession, HttpClient httpClient)
        {
            if (string.IsNullOrEmpty(radikoSession))
                return;

            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "radiko_session", radikoSession }
                });
                await httpClient.PostAsync(RadikoUrls.PremiumLogout, content);
            }
            catch
            {
                // ログアウト失敗は無視する
            }
        }

        /// <summary>
        /// タイムフリー再生に必要なAuthTokenとエリアIDを取得する(auth1 -&gt; auth2)
        /// </summary>
        public static async Task<(string AuthToken, string AreaId)> AuthAsync(string radikoSession, HttpClient httpClient)
        {
            // Authorize 1
            using var auth1Req = new HttpRequestMessage(HttpMethod.Get, RadikoUrls.Auth1);
            auth1Req.Headers.Add("X-Radiko-App", "pc_html5");
            auth1Req.Headers.Add("X-Radiko-App-Version", "0.0.1");
            auth1Req.Headers.Add("X-Radiko-Device", "pc");
            auth1Req.Headers.Add("X-Radiko-User", "dummy_user");

            using var auth1Res = await httpClient.SendAsync(auth1Req);

            if (!auth1Res.Headers.TryGetValues("X-Radiko-AuthToken", out var authTokenValues) ||
                !auth1Res.Headers.TryGetValues("X-Radiko-KeyOffset", out var keyOffsetValues) ||
                !auth1Res.Headers.TryGetValues("X-Radiko-KeyLength", out var keyLengthValues))
            {
                throw new RadikoException("auth1に失敗しました（レスポンスヘッダー不足）");
            }

            string authToken = authTokenValues.First();
            int keyOffset = int.Parse(keyOffsetValues.First());
            int keyLength = int.Parse(keyLengthValues.First());

            // Partial key (AUTHKEY_VALUEの一部をBase64化)
            var keyBytes = Encoding.ASCII.GetBytes(AuthKeyValue);
            string partialKey = Convert.ToBase64String(keyBytes, keyOffset, keyLength);

            // Authorize 2
            string auth2Url = RadikoUrls.Auth2;
            if (!string.IsNullOrEmpty(radikoSession))
                auth2Url += $"?radiko_session={radikoSession}";

            using var auth2Req = new HttpRequestMessage(HttpMethod.Get, auth2Url);
            auth2Req.Headers.Add("X-Radiko-Device", "pc");
            auth2Req.Headers.Add("X-Radiko-User", "dummy_user");
            auth2Req.Headers.Add("X-Radiko-AuthToken", authToken);
            auth2Req.Headers.Add("X-Radiko-PartialKey", partialKey);

            using var auth2Res = await httpClient.SendAsync(auth2Req);
            string body = (await auth2Res.Content.ReadAsStringAsync()).Replace("\r", "").Trim();

            if (string.IsNullOrEmpty(body) || body == "OUT")
                throw new RadikoException("auth2に失敗しました（エリア判定不可、または日本国外からのアクセス）");

            string areaId = body.Split('\n')[0].Split(',')[0];

            return (authToken, areaId);
        }

        /// <summary>タイムフリー再生用のHLSプレイリストURL一覧を取得する</summary>
        public static async Task<List<string>> GetHlsPlaylistUrlsAsync(string stationId, bool isAreaFree, HttpClient httpClient)
        {
            var xml = await httpClient.GetStringAsync(RadikoUrls.StationStream.Replace("[stationCode]", stationId));
            var doc = XDocument.Parse(xml);
            string areafreeValue = isAreaFree ? "1" : "0";

            return doc.Descendants("url")
                .Where(u => (string?)u.Attribute("timefree") == "1" && (string?)u.Attribute("areafree") == areafreeValue)
                .Select(u => u.Element("playlist_create_url")?.Value.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!)
                .ToList();
        }
    }
}
