using Microsoft.Extensions.Caching.Memory;

namespace RadiCore.Infrastructure
{
    /// <summary>
    /// 番組画像URLが今も配信されているかを判定する。
    /// 古い番組の画像は radiko 側から消えていることがあり、消えたURLで img タグを出すと
    /// ブラウザが 404 を引くため、表示前に HEAD で存在を確認する。
    /// </summary>
    public class ImageAvailabilityService
    {
        /// <summary>存在を確認できたURLのキャッシュ保持時間</summary>
        private static readonly TimeSpan AvailableTtl = TimeSpan.FromHours(12);

        /// <summary>確認できなかったURLのキャッシュ保持時間（通信エラーで長く伏せないよう短め）</summary>
        private static readonly TimeSpan UnavailableTtl = TimeSpan.FromMinutes(30);

        /// <summary>1本あたりの確認タイムアウト</summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 確認全体にかける上限時間。配信元が無応答のとき、URL数に比例して画面表示が
        /// 止まるのを防ぐ。間に合わなかったURLはこの描画では表示せず、判定もキャッシュしない
        /// （次の表示で再確認され、確認できた分から順に出るようになる）。
        /// </summary>
        private static readonly TimeSpan TotalBudget = TimeSpan.FromSeconds(1.5);

        private const int MaxParallelism = 8;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;

        public ImageAvailabilityService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        /// <summary>与えられたURLのうち、実際に配信されているものだけを返す</summary>
        public async Task<ISet<string>> FilterAvailableAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
        {
            var targets = urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct()
                .Where(IsHttpUrl)
                .ToList();

            var available = new System.Collections.Concurrent.ConcurrentBag<string>();
            using var throttle = new SemaphoreSlim(MaxParallelism);

            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TotalBudget);

            await Task.WhenAll(targets.Select(async url =>
            {
                try
                {
                    await throttle.WaitAsync(budget.Token);
                }
                catch (OperationCanceledException)
                {
                    // 時間切れ。順番待ちのまま確認できなかったURLは表示しない
                    return;
                }

                try
                {
                    if (await IsAvailableAsync(url, budget.Token))
                        available.Add(url);
                }
                finally
                {
                    throttle.Release();
                }
            }));

            return available.ToHashSet();
        }

        private async Task<bool> IsAvailableAsync(string url, CancellationToken budgetToken)
        {
            string cacheKey = $"image-available:{url}";
            if (_cache.TryGetValue(cacheKey, out bool cached))
                return cached;

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(budgetToken);
                timeout.CancelAfter(RequestTimeout);

                // 本文は不要なので HEAD で確認する
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await _httpClientFactory.CreateClient()
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                bool available = response.IsSuccessStatusCode;
                _cache.Set(cacheKey, available, available ? AvailableTtl : UnavailableTtl);
                return available;
            }
            catch
            {
                // 全体の時間切れによる打ち切りは「画像が消えた」とは限らないのでキャッシュしない。
                // それ以外（1本あたりのタイムアウト・通信エラー）は短期間だけ伏せる
                if (!budgetToken.IsCancellationRequested)
                    _cache.Set(cacheKey, false, UnavailableTtl);

                return false;
            }
        }

        private static bool IsHttpUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
