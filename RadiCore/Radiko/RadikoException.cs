namespace RadiCore.Radiko
{
    /// <summary>Radiko APIとのやり取り（ログイン・認証・録音）に関するエラー</summary>
    public class RadikoException : Exception
    {
        public RadikoException(string message) : base(message) { }
        public RadikoException(string message, Exception? innerException) : base(message, innerException) { }
    }
}
