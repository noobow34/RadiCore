namespace RadikoShift.Reservations
{
    public class CreateReservationRequest
    {
        public long ProgramId { get; set; }

        public string Title { get; set; } = null!;

        public string? CastName { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public RepeatType RepeatType { get; set; }

        public bool IsEdited { get; set; }

        public bool AutoDeletePrevious { get; set; }
    }
}
