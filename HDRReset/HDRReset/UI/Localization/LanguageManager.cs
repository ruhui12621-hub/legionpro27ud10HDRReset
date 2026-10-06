using System;
using System.IO;
using System.Windows;

namespace HDRReset
{
    internal static class LanguageManager
    {
        private const string Chinese = "zh-CN";
        private const string English = "en-US";

        private static readonly string SettingsDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "HDRReset");

        private static readonly string LanguageFile =
            Path.Combine(
                SettingsDirectory,
                "language.txt");

        public static string CurrentLanguage { get; private set; }
            = Chinese;

        public static event EventHandler? LanguageChanged;

        // ============================================================
        // 初始化语言
        // ============================================================

        public static void Initialize()
        {
            string language = LoadLanguage();

            if (language != Chinese &&
                language != English)
            {
                language = Chinese;
            }

            ApplyLanguage(language, false);
        }

        // ============================================================
        // 设置语言
        // ============================================================

        public static void SetLanguage(
            string language)
        {
            if (language != Chinese &&
                language != English)
            {
                language = Chinese;
            }

            if (CurrentLanguage == language)
                return;

            ApplyLanguage(language, true);
        }

        // ============================================================
        // 应用资源字典
        // ============================================================

        private static void ApplyLanguage(
            string language,
            bool save)
        {
            ResourceDictionary dictionary =
                new ResourceDictionary
                {
                    Source = new Uri(
                        $"UI/Localization/{language}.xaml",
                        UriKind.Relative)
                };

            System.Windows.Application.Current
                .Resources
                .MergedDictionaries
                .Clear();

            System.Windows.Application.Current
                .Resources
                .MergedDictionaries
                .Add(dictionary);

            CurrentLanguage = language;

            if (save)
            {
                SaveLanguage(language);
            }

            LanguageChanged?.Invoke(
                null,
                EventArgs.Empty);
        }

        // ============================================================
        // 读取语言
        // ============================================================

        private static string LoadLanguage()
        {
            try
            {
                if (File.Exists(LanguageFile))
                {
                    string language =
                        File.ReadAllText(LanguageFile)
                            .Trim();

                    if (!string.IsNullOrWhiteSpace(language))
                    {
                        return language;
                    }
                }
            }
            catch
            {
                // 读取失败时使用中文。
            }

            return Chinese;
        }

        // ============================================================
        // 保存语言
        // ============================================================

        private static void SaveLanguage(
            string language)
        {
            try
            {
                Directory.CreateDirectory(
                    SettingsDirectory);

                File.WriteAllText(
                    LanguageFile,
                    language);
            }
            catch
            {
                // 保存失败不影响程序运行。
            }
        }

        // ============================================================
        // 刷新类型翻译
        // ============================================================

        public static string GetRefreshTypeText(
            RefreshType type)
        {
            string key =
                type switch
                {
                    RefreshType.Startup =>
                        "RefreshTypeStartup",

                    RefreshType.Automatic =>
                        "RefreshTypeAutomatic",

                    RefreshType.Manual =>
                        "RefreshTypeManual",

                    _ =>
                        "RefreshTypeUnknown"
                };

            return GetString(key);
        }

        // ============================================================
        // 错误翻译
        // ============================================================

        public static string GetErrorText(
            RefreshResult result)
        {
            string text =
                result.Error switch
                {
                    RefreshError
                        .GetPhysicalMonitorCountFailed =>
                        GetString(
                            "ErrorGetPhysicalMonitorCountFailed"),

                    RefreshError
                        .GetPhysicalMonitorFailed =>
                        GetString(
                            "ErrorGetPhysicalMonitorFailed"),

                    RefreshError
                        .SetHdrPhotoFailed =>
                        GetString(
                            "ErrorSetHdrPhotoFailed"),

                    RefreshError
                        .NoPhysicalMonitor =>
                        GetString(
                            "ErrorNoPhysicalMonitor"),

                    RefreshError
                        .UnexpectedException =>
                        GetString(
                            "ErrorUnexpectedException"),

                    _ =>
                        GetString(
                            "ErrorUnknown")
                };

            // --------------------------------------------------------
            // SetVCPFeature 失败
            // --------------------------------------------------------

            if (result.Error ==
                RefreshError.SetHdrPhotoFailed)
            {
                text = string.Format(
                    text,
                    result.MonitorDescription
                    ?? GetString("UnknownMonitor"));

                text += " " +
                        string.Format(
                            GetString("Win32ErrorCode"),
                            result.Win32ErrorCode ?? 0);

                text += " " +
                        string.Format(
                            GetString("VcpInformation"),
                            result.VcpCode.ToString("X2"),
                            result.VcpValue.ToString("X2"));
            }

            // --------------------------------------------------------
            // 其他 Win32 错误
            // --------------------------------------------------------

            else if (result.Win32ErrorCode.HasValue)
            {
                text += " " +
                        string.Format(
                            GetString("Win32ErrorCode"),
                            result.Win32ErrorCode.Value);
            }

            // --------------------------------------------------------
            // 异常信息
            // --------------------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                result.ExceptionMessage))
            {
                text += " " +
                        result.ExceptionMessage;
            }

            return text;
        }

        // ============================================================
        // 获取资源字符串
        // ============================================================

        private static string GetString(
            string key)
        {
            return System.Windows.Application.Current
                .TryFindResource(key)
                ?.ToString()
                ?? key;
        }
    }
}