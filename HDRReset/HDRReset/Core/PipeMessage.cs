namespace HDRReset
{
    internal sealed class PipeMessage
    {
        public string Type { get; set; } = string.Empty;
        public HostSnapshot? Snapshot { get; set; }
        public RefreshResult? Result { get; set; }
        public string? Language { get; set; }
        public byte Mode { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
    }
}
