using RadiCore.Reservations;

namespace RadiCore.ViewModel
{
    public class ReservationListViewModel
    {
        public int             Id          { get; set; }
        public string?         ProgramName { get; set; }
        public string          StationId   { get; set; } = null!;
        public RepeatType      RepeatType  { get; set; }
        public DayOfWeek?      RepeatDays  { get; set; }
        public DateOnly?       TargetDate  { get; set; }
        public TimeOnly        StartTime   { get; set; }
        public TimeOnly        EndTime     { get; set; }
        public ReservationStatus Status    { get; set; }

        public string ScheduleText => RepeatType switch
        {
            RepeatType.Once    => $"{TargetDate:yyyy/MM/dd} {StartTime:HH:mm} - {EndTime:HH:mm}",
            RepeatType.Daily   => $"毎日 {StartTime:HH:mm} - {EndTime:HH:mm}",
            RepeatType.Weekly  => $"毎週 {RepeatDays} {StartTime:HH:mm} - {EndTime:HH:mm}",
            _ => "-"
        };
    }
}
