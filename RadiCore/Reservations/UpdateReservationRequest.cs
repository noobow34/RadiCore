namespace RadiCore.Reservations
{
    public class UpdateReservationRequest
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;

        public string? CastName { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public bool IsEdited { get; set; }

        public bool AutoDeletePrevious { get; set; }
    }
}
