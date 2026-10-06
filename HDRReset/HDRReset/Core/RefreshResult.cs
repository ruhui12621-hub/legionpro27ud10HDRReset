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
        SetHdrPhotoFailed,
        NoPhysicalMonitor,
        UnexpectedException
    }

    internal sealed class RefreshResult
    {
        public RefreshType Type { get; init; }

        public DateTime RefreshTime { get; init; }

        public DateTime NextRefreshTime { get; init; }

        public bool Success { get; init; }

        public RefreshError Error { get; init; }

        public int? Win32ErrorCode { get; init; }

        public string? MonitorDescription { get; init; }

        public byte VcpCode { get; init; }

        public uint VcpValue { get; init; }

        public string? ExceptionMessage { get; init; }
    }
}