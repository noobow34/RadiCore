using RadiCore.Infrastructure;

namespace RadiCore.ViewModel
{
    public class SettingsViewModel
    {
        public int RefreshHour   { get; set; }
        public int RefreshMinute { get; set; }
        public int ParallelCount { get; set; }
        public string FileNameTemplate { get; set; } = AppSettingsService.DefaultFileNameTemplate;

        /// <summary>スケジューラが一時停止中か</summary>
        public bool SchedulerPaused { get; set; }

        public int DefaultRefreshHour   => AppSettingsService.DefaultRefreshHour;
        public int DefaultRefreshMinute => AppSettingsService.DefaultRefreshMinute;
        public int DefaultParallelCount => AppSettingsService.DefaultParallelCount;
        public string DefaultFileNameTemplate => AppSettingsService.DefaultFileNameTemplate;

        public int MinRefreshHour   => AppSettingsService.MinRefreshHour;
        public int MaxRefreshHour   => AppSettingsService.MaxRefreshHour;
        public int MinRefreshMinute => AppSettingsService.MinRefreshMinute;
        public int MaxRefreshMinute => AppSettingsService.MaxRefreshMinute;
        public int MinParallelCount => AppSettingsService.MinParallelCount;
        public int MaxParallelCount => AppSettingsService.MaxParallelCount;
        public int MaxFileNameTemplateLength => AppSettingsService.MaxFileNameTemplateLength;

        /// <summary>ファイル名テンプレートで使えるプレースホルダーの一覧</summary>
        public IReadOnlyList<FileNamePlaceholder> FileNamePlaceholders => RecordingFileNameBuilder.Placeholders;
    }
}
