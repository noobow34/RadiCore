using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RadiCore.Infrastructure;
using RadiCore.ViewModel;

namespace RadiCore.Controllers
{
    public class SettingsController : Controller
    {
        private readonly AppSettingsService _settings;
        private readonly QuartzScheduler _scheduler;

        public SettingsController(AppSettingsService settings, QuartzScheduler scheduler)
        {
            _settings  = settings;
            _scheduler = scheduler;
        }

        public IActionResult Index()
        {
            var vm = new SettingsViewModel
            {
                RefreshHour   = _settings.RefreshHour,
                RefreshMinute = _settings.RefreshMinute,
                ParallelCount = _settings.ParallelCount,
                FileNameTemplate = _settings.FileNameTemplate,
                SchedulerPaused  = _scheduler.IsPaused,
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Save(int refreshHour, int refreshMinute, int parallelCount, string? fileNameTemplate)
        {
            refreshHour   = Math.Clamp(refreshHour,   AppSettingsService.MinRefreshHour,   AppSettingsService.MaxRefreshHour);
            refreshMinute = Math.Clamp(refreshMinute, AppSettingsService.MinRefreshMinute, AppSettingsService.MaxRefreshMinute);
            parallelCount = Math.Clamp(parallelCount, AppSettingsService.MinParallelCount, AppSettingsService.MaxParallelCount);

            fileNameTemplate = (fileNameTemplate ?? "").Trim();
            if (fileNameTemplate.Length == 0)
                fileNameTemplate = AppSettingsService.DefaultFileNameTemplate;

            if (fileNameTemplate.Length > AppSettingsService.MaxFileNameTemplateLength)
                return Json(new { success = false, message = $"ファイル名テンプレートは{AppSettingsService.MaxFileNameTemplateLength}文字以内で入力してください。" });

            if (!RecordingFileNameBuilder.TryValidate(fileNameTemplate, out var unknownTokens))
                return Json(new { success = false, message = $"使用できないプレースホルダーが含まれています: {string.Join(", ", unknownTokens)}" });

            await _settings.SetAsync(AppSettingsService.KeyRefreshHour,      refreshHour.ToString());
            await _settings.SetAsync(AppSettingsService.KeyRefreshMinute,    refreshMinute.ToString());
            await _settings.SetAsync(AppSettingsService.KeyParallelCount,    parallelCount.ToString());
            await _settings.SetAsync(AppSettingsService.KeyFileNameTemplate, fileNameTemplate);

            await _scheduler.RescheduleRefreshJobAsync(refreshHour, refreshMinute, parallelCount);
            this.JournalWriteLine($"設定変更: 番組表更新時刻={refreshHour:D2}:{refreshMinute:D2} 並列数={parallelCount} ファイル名テンプレート={fileNameTemplate}");

            return Json(new { success = true, message = "設定を保存しました。" });
        }

        /// <summary>ファイル名テンプレートをサンプルデータで展開したプレビューを返す</summary>
        [HttpGet]
        public IActionResult PreviewFileName(string? fileNameTemplate)
        {
            fileNameTemplate = (fileNameTemplate ?? "").Trim();
            if (fileNameTemplate.Length == 0)
                fileNameTemplate = AppSettingsService.DefaultFileNameTemplate;

            bool valid = RecordingFileNameBuilder.TryValidate(fileNameTemplate, out var unknownTokens);

            return Json(new
            {
                valid,
                fileName = RecordingFileNameBuilder.Build(fileNameTemplate, RecordingFileNameBuilder.SampleMetadata),
                message  = valid ? "" : $"使用できないプレースホルダー: {string.Join(", ", unknownTokens)}",
            });
        }

        /// <summary>録音・番組表更新のスケジューラ全体を一時停止／再開する</summary>
        [HttpPost]
        public async Task<IActionResult> SetSchedulerPaused(bool paused)
        {
            if (paused)
                await _scheduler.PauseAllAsync();
            else
                await _scheduler.ResumeAllAsync();

            // プロセス再起動後も状態を維持するため設定として保存する
            await _settings.SetAsync(AppSettingsService.KeySchedulerPaused, paused.ToString());
            this.JournalWriteLine($"設定変更: スケジューラ一時停止={paused}");

            return Json(new
            {
                success = true,
                paused,
                message = paused
                    ? "録音・番組表更新を一時停止しました。"
                    : "録音・番組表更新を再開しました。",
            });
        }

        [HttpPost]
        public async Task<IActionResult> RunNow()
        {
            if (_scheduler.IsPaused)
                return Json(new { success = false, message = "一時停止中は実行できません。先に再開してください。" });

            await _scheduler.TriggerRefreshJobNowAsync(_settings.ParallelCount);
            this.JournalWriteLine("番組表更新を手動実行");
            return Json(new { success = true, message = "番組表更新を開始しました。完了までしばらくお待ちください。" });
        }
        [HttpGet]
        public IActionResult GetLastRefreshLog()
        {
            var log = _settings.GetLastRefreshLog();
            if (log == null)
                return Json(new { exists = false });

            return Json(new
            {
                exists     = true,
                isRunning  = log.IsRunning,
                succeeded  = log.Succeeded,
                startedAt  = log.StartedAt.ToString("yyyy/MM/dd HH:mm:ss"),
                finishedAt = log.FinishedAt.ToString("yyyy/MM/dd HH:mm:ss"),
                elapsed    = (log.FinishedAt - log.StartedAt).ToString(@"mm\:ss\.ff"),
                lines      = log.Lines,
            });
        }
    }
}
