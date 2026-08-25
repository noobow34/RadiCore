namespace RadiCore.ViewModel
{
    public class ProgramFilterViewModel
    {
        public string SelectedRegion  { get; set; } = "";
        public string SelectedArea    { get; set; } = "";
        public string SelectedStation { get; set; } = "";
        public string MinDate         { get; set; } = "";
        public string MaxDate         { get; set; } = "";
        public DateTime SelectedDate  { get; set; } = DateTime.Today;

        public List<IdNamePair> Regions  { get; set; } = new();
        public List<IdNamePair> Areas    { get; set; } = new();
        public List<IdNamePair> Stations { get; set; } = new();
    }
}
