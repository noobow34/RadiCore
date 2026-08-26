using RadiCore.Data;

namespace RadiCore.ViewModel
{
    public class LibraryListViewModel
    {
        public int         Id                { get; set; }
        public string?     ProgramName       { get; set; }
        public string?     CastName          { get; set; }
        public string      StationName       { get; set; } = null!;
        public DateTime    StartTime         { get; set; }
        public DateTime    EndTime           { get; set; }
        public string      FileName          { get; set; } = null!;
        public long        FileSize          { get; set; }

        /// <summary>番組画像URL。配信されていない場合は null（img タグを出さない）</summary>
        public string?     ImageUrl          { get; set; }

        public Reservation? ParentReservation { get; set; }

        public string TimeRange    => $"{StartTime:yyyy/MM/dd(ddd) HH:mm} - {EndTime:HH:mm}";
        public string FileSizeText => $"{FileSize / 1024 / 1024.0:F2} MB";
    }
}
