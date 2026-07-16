using System.Text.RegularExpressions;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace SniffCom
{
    public partial class MainViewModel
    {
        private static readonly WpfBrush PassBrush = CreateFrozenBrush(22, 163, 74);
        private static readonly WpfBrush PassBorderBrush = CreateFrozenBrush(21, 128, 61);

        private static readonly WpfBrush NgBrush = CreateFrozenBrush(220, 38, 38);
        private static readonly WpfBrush NgBorderBrush = CreateFrozenBrush(185, 28, 28);

        private static readonly WpfBrush TestingBrush = CreateFrozenBrush(217, 119, 6);

        private static readonly WpfBrush PendingBrush = CreateFrozenBrush(241, 245, 249);
        private static readonly WpfBrush PendingBorderBrush = CreateFrozenBrush(203, 213, 225);
        private static readonly WpfBrush PendingTextBrush = CreateFrozenBrush(71, 85, 105);

        public WpfBrush WriteComStatusBrush =>
            StatusForegroundBrush(WriteComStatus);

        public WpfBrush AirPressTestStatusBrush =>
            StatusForegroundBrush(AirPressTestStatus);

        public WpfBrush RelayStatusBrush =>
            StatusForegroundBrush(RelayStatus);

        public WpfBrush OverallPressureBackground =>
            ResultBackgroundBrush(OverallPressureState);

        public WpfBrush OverallPressureBorder =>
            ResultBorderBrush(OverallPressureState);

        public WpfBrush OverallPressureTextBrush =>
            ResultTextBrush(OverallPressureState);

        public WpfBrush Channel1ResultBrush =>
            StatusForegroundBrush(Channel1TestResult);

        public WpfBrush Channel1ResultBackground =>
            ResultBackgroundBrush(Channel1TestResult);

        public WpfBrush Channel1ResultBorder =>
            ResultBorderBrush(Channel1TestResult);

        public WpfBrush Channel1ResultTextBrush =>
            ResultTextBrush(Channel1TestResult);

        public WpfBrush Channel2ResultBrush =>
            StatusForegroundBrush(Channel2TestResult);

        public WpfBrush Channel2ResultBackground =>
            ResultBackgroundBrush(Channel2TestResult);

        public WpfBrush Channel2ResultBorder =>
            ResultBorderBrush(Channel2TestResult);

        public WpfBrush Channel2ResultTextBrush =>
            ResultTextBrush(Channel2TestResult);

        public WpfBrush Channel3ResultBrush =>
            StatusForegroundBrush(Channel3TestResult);

        public WpfBrush Channel3ResultBackground =>
            ResultBackgroundBrush(Channel3TestResult);

        public WpfBrush Channel3ResultBorder =>
            ResultBorderBrush(Channel3TestResult);

        public WpfBrush Channel3ResultTextBrush =>
            ResultTextBrush(Channel3TestResult);

        partial void OnWriteComStatusChanged(string value)
        {
            OnPropertyChanged(nameof(WriteComStatusBrush));
        }

        partial void OnAirPressTestStatusChanged(string value)
        {
            OnPropertyChanged(nameof(AirPressTestStatusBrush));
        }

        partial void OnRelayStatusChanged(string value)
        {
            OnPropertyChanged(nameof(RelayStatusBrush));
        }

        partial void OnOverallPressureStateChanged(string value)
        {
            NotifyOverallPressureBrushes();
        }

        private void NotifyOverallPressureBrushes()
        {
            OnPropertyChanged(nameof(OverallPressureBackground));
            OnPropertyChanged(nameof(OverallPressureBorder));
            OnPropertyChanged(nameof(OverallPressureTextBrush));
        }

        private void NotifyChannelResultBrushes(int channel)
        {
            switch (channel)
            {
                case 1:
                    OnPropertyChanged(nameof(Channel1ResultBrush));
                    OnPropertyChanged(nameof(Channel1ResultBackground));
                    OnPropertyChanged(nameof(Channel1ResultBorder));
                    OnPropertyChanged(nameof(Channel1ResultTextBrush));
                    break;

                case 2:
                    OnPropertyChanged(nameof(Channel2ResultBrush));
                    OnPropertyChanged(nameof(Channel2ResultBackground));
                    OnPropertyChanged(nameof(Channel2ResultBorder));
                    OnPropertyChanged(nameof(Channel2ResultTextBrush));
                    break;

                case 3:
                    OnPropertyChanged(nameof(Channel3ResultBrush));
                    OnPropertyChanged(nameof(Channel3ResultBackground));
                    OnPropertyChanged(nameof(Channel3ResultBorder));
                    OnPropertyChanged(nameof(Channel3ResultTextBrush));
                    break;
            }
        }

        private static WpfBrush StatusForegroundBrush(string? status)
        {
            string text = status?.Trim() ?? string.Empty;

            if (IsNgStatus(text) ||
                ContainsAny(
                    text,
                    "Lỗi",
                    "Error",
                    "failed",
                    "thất bại",
                    "Không khả dụng",
                    "Đã ngắt",
                    "Disconnected",
                    "Not available",
                    "Quá thời gian",
                    "Timeout"))
            {
                return WpfBrushes.Red;
            }

            if (IsTestingStatus(text) ||
                ContainsAny(
                    text,
                    "Cảnh báo",
                    "Warning",
                    "Đang",
                    "WAIT"))
            {
                return WpfBrushes.DarkOrange;
            }

            if (IsPassStatus(text) ||
                ContainsAny(
                    text,
                    "Sẵn sàng",
                    "Đã kết nối",
                    "thành công",
                    "Connected",
                    "Available",
                    "Ready"))
            {
                return WpfBrushes.ForestGreen;
            }

            return WpfBrushes.DimGray;
        }

        private static WpfBrush ResultBackgroundBrush(string? result)
        {
            string text = result?.Trim() ?? string.Empty;

            if (IsNgStatus(text))
            {
                return NgBrush;
            }

            if (IsPassStatus(text))
            {
                return PassBrush;
            }

            if (IsTestingStatus(text))
            {
                return TestingBrush;
            }

            return PendingBrush;
        }

        private static WpfBrush ResultBorderBrush(string? result)
        {
            string text = result?.Trim() ?? string.Empty;

            if (IsNgStatus(text))
            {
                return NgBorderBrush;
            }

            if (IsPassStatus(text))
            {
                return PassBorderBrush;
            }

            if (IsTestingStatus(text))
            {
                return TestingBrush;
            }

            return PendingBorderBrush;
        }

        private static WpfBrush ResultTextBrush(string? result)
        {
            string text = result?.Trim() ?? string.Empty;

            return IsNgStatus(text) ||
                   IsPassStatus(text) ||
                   IsTestingStatus(text)
                ? WpfBrushes.White
                : PendingTextBrush;
        }

        private static WpfBrush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new WpfSolidColorBrush(
                WpfColor.FromRgb(r, g, b));

            brush.Freeze();

            return brush;
        }

        private static bool IsPassStatus(string text)
        {
            return HasToken(text, "PASS") ||
                   HasToken(text, "OK") ||
                   HasToken(text, "ĐẠT");
        }

        private static bool IsNgStatus(string text)
        {
            return HasToken(text, "NG") ||
                   HasToken(text, "FAIL") ||
                   text.Contains(
                       "KHÔNG ĐẠT",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTestingStatus(string text)
        {
            return HasToken(text, "TESTING") ||
                   text.Contains(
                       "ĐANG KIỂM TRA",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasToken(string text, string token)
        {
            return Regex.IsMatch(
                text,
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(token)}(?![\p{{L}}\p{{N}}])",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);
        }

        private static bool ContainsAny(
            string text,
            params string[] values)
        {
            return values.Any(
                value => text.Contains(
                    value,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
