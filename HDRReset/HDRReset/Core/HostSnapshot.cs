namespace HDRReset
{
    internal sealed class HostSnapshot
    {
        public byte HdrMode { get; set; }
        public int RefreshIntervalSeconds { get; set; }
        public System.DateTime NextRefreshTime { get; set; }
        public RefreshResult[] Records { get; set; } = System.Array.Empty<RefreshResult>();
    }
}
