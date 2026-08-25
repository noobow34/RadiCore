namespace RadiCore.ViewModel
{
    public class ProgramItemViewModel
    {
        public required long ProgramId  { get; set; }
        public DateTime StartTime       { get; set; }
        public DateTime EndTime         { get; set; }
        public string   Title           { get; set; } = "";
        public string?  CastName        { get; set; }
        public string?  Description     { get; set; }

        public bool   IsNow      => DateTime.Now >= StartTime && DateTime.Now < EndTime;
        public string TimeRange  => $"{StartTime:HH:mm} – {EndTime:HH:mm}";
    }
}
