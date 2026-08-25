using RadiCore.Data;
using System.Text.Json;

namespace RadiCore.Infrastructure
{
    public class AppSettingsService
    {
        public const string KeyRefreshHour    = "RefreshHour";
        public const string KeyRefreshMinute  = "RefreshMinute";
        public const string KeyParallelCount  = "ParallelCount";
        public const string KeyLastRefreshLog = "LastRefreshLog";

        public const int DefaultRefreshHour   = 6;
        public const int DefaultRefreshMinute = 0;
        public const int DefaultParallelCount = 10;

        public const int MinRefreshHour   = 0;
        public const int MaxRefreshHour   = 23;
        public const int MinRefreshMinute = 0;
        public const int MaxRefreshMinute = 59;
        public const int MinParallelCount = 1;
        public const int MaxParallelCount = 50;

        private readonly RadiCoreContext _db;

        public AppSettingsService(RadiCoreContext db)
        {
            _db = db;
        }

        public int RefreshHour   => GetInt(KeyRefreshHour,   DefaultRefreshHour);
        public int RefreshMinute => GetInt(KeyRefreshMinute, DefaultRefreshMinute);
        public int ParallelCount => GetInt(KeyParallelCount, DefaultParallelCount);

        public async Task SetAsync(string key, string value)
        {
            var setting = await _db.AppSettings.FindAsync(key);
            if (setting == null)
                _db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            else
                setting.Value = value;

            await _db.SaveChangesAsync();
        }

        /// <summary>番組表更新ジョブの開始を記録する（実行中状態）</summary>
        public async Task SaveRefreshLogStartAsync(DateTimeOffset startedAt)
        {
            await SaveRefreshLogAsync(new RefreshLog
            {
                IsRunning = true,
                StartedAt = startedAt,
            });
        }

        /// <summary>番組表更新ログを保存する</summary>
        public async Task SaveRefreshLogAsync(RefreshLog log)
        {
            var json = JsonSerializer.Serialize(log);
            await SetAsync(KeyLastRefreshLog, json);
        }

        /// <summary>番組表更新ログを取得する。未実行の場合は null を返す</summary>
        public RefreshLog? GetLastRefreshLog()
        {
            var setting = _db.AppSettings.Find(KeyLastRefreshLog);
            if (setting == null) return null;
            try { return JsonSerializer.Deserialize<RefreshLog>(setting.Value); }
            catch { return null; }
        }

        private int GetInt(string key, int defaultValue)
        {
            var setting = _db.AppSettings.Find(key);
            if (setting != null && int.TryParse(setting.Value, out int v))
                return v;
            return defaultValue;
        }
    }

    /// <summary>番組表更新ジョブの実行結果ログ</summary>
    public class RefreshLog
    {
        /// <summary>実行中フラグ（完了時は false）</summary>
        public bool IsRunning { get; set; }
        /// <summary>実行開始日時（JST）</summary>
        public DateTimeOffset StartedAt  { get; set; }
        /// <summary>実行終了日時（JST）</summary>
        public DateTimeOffset FinishedAt { get; set; }
        /// <summary>成功 / 失敗</summary>
        public bool Succeeded { get; set; }
        /// <summary>ログ行（JournalWriteLine の出力と同形式）</summary>
        public List<string> Lines { get; set; } = [];
    }
}
