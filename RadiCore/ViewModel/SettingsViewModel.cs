using RadiCore.Infrastructure;

namespace RadiCore.ViewModel
{
    public class SettingsViewModel
    {
        public int RefreshHour   { get; set; }
        public int RefreshMinute { get; set; }
        public int ParallelCount { get; set; }

        public int DefaultRefreshHour   => AppSettingsService.DefaultRefreshHour;
        public int DefaultRefreshMinute => AppSettingsService.DefaultRefreshMinute;
        public int DefaultParallelCount => AppSettingsService.DefaultParallelCount;

        public int MinRefreshHour   => AppSettingsService.MinRefreshHour;
        public int MaxRefreshHour   => AppSettingsService.MaxRefreshHour;
        public int MinRefreshMinute => AppSettingsService.MinRefreshMinute;
        public int MaxRefreshMinute => AppSettingsService.MaxRefreshMinute;
        public int MinParallelCount => AppSettingsService.MinParallelCount;
        public int MaxParallelCount => AppSettingsService.MaxParallelCount;
    }
}
