namespace RadiCore.ViewModel
{
    /// <summary>汎用ID・名前ペア（セレクトボックス等で使用）</summary>
    public class IdNamePair
    {
        public string? Id           { get; set; }
        public string? Name         { get; set; }
        public int     DisplayOrder { get; set; }
    }
}
