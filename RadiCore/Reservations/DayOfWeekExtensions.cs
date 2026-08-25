namespace RadiCore.Reservations
{
    public static class DayOfWeekExtensions
    {
        public static string ToJapanese(this DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Sunday    => "日",
                DayOfWeek.Monday    => "月",
                DayOfWeek.Tuesday   => "火",
                DayOfWeek.Wednesday => "水",
                DayOfWeek.Thursday  => "木",
                DayOfWeek.Friday    => "金",
                DayOfWeek.Saturday  => "土",
                _ => ""
            };
        }
    }
}
