using System;

namespace HDRReset
{
    internal enum RefreshType
    {
        Startup,
        Automatic,
        Manual
    }

    internal enum RefreshError
    {
        None,
        GetPhysicalMonitorCountFailed,
        GetPhysicalMonitorFailed,
        SetHdrModeFailed,
        NoPhysicalMonitor,
        UnexpectedException
    }

    internal sealed class RefreshResult
    {
        public RefreshType Type { get; set; }

        public DateTime RefreshTime { get; set; }

        public DateTime NextRefreshTime { get; set; }

        public bool Success { get; set; }

        public RefreshError Error { get; set; }

        public int? Win32ErrorCode { get; set; }

        public string? MonitorDescription { get; set; }

        public byte VcpCode { get; set; }

        public uint VcpValue { get; set; }

        public string? ExceptionMessage { get; set; }
    }
}