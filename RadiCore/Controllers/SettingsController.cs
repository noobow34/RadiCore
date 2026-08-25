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
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Save(int refreshHour, int refreshMinute, int parallelCount)
        {
            refreshHour   = Math.Clamp(refreshHour,   AppSettingsService.MinRefreshHour,   AppSettingsService.MaxRefreshHour);
            refreshMinute = Math.Clamp(refreshMinute, AppSettingsService.MinRefreshMinute, AppSettingsService.MaxRefreshMinute);
            parallelCount = Math.Clamp(parallelCount, AppSettingsService.MinParallelCount, AppSettingsService.MaxParallelCount);

            await _settings.SetAsync(AppSettingsService.KeyRefreshHour,   refreshHour.ToString());
            await _settings.SetAsync(AppSettingsService.KeyRefreshMinute, refreshMinute.ToString());
            await _settings.SetAsync(AppSettingsService.KeyParallelCount, parallelCount.ToString());

            await _scheduler.RescheduleRefreshJobAsync(refreshHour, refreshMinute, parallelCount);
            this.JournalWriteLine($"設定変更: 番組表更新時刻={refreshHour:D2}:{refreshMinute:D2} 並列数={parallelCount}");

            return Json(new { success = true, message = "設定を保存しました。" });
        }

        [HttpPost]
        public async Task<IActionResult> RunNow()
        {
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
