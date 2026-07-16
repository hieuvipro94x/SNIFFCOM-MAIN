using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace SniffCom
{
    public sealed class StatusToBrushConverter : IValueConverter
    {
        private static readonly WpfBrush PassBrush = CreateFrozenBrush(22, 163, 74);
        private static readonly WpfBrush PassBorderBrush = CreateFrozenBrush(21, 128, 61);
        private static readonly WpfBrush NgBrush = CreateFrozenBrush(220, 38, 38);
        private static readonly WpfBrush NgBorderBrush = CreateFrozenBrush(185, 28, 28);
        private static readonly WpfBrush TestingBrush = CreateFrozenBrush(217, 119, 6);
        private static readonly WpfBrush PendingBrush = CreateFrozenBrush(241, 245, 249);
        private static readonly WpfBrush PendingBorderBrush = CreateFrozenBrush(203, 213, 225);
        private static readonly WpfBrush PendingTextBrush = CreateFrozenBrush(71, 85, 105);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value?.ToString()?.Trim() ?? string.Empty;
            string mode = parameter?.ToString() ?? string.Empty;

            if (mode.Equals("ResultBackground", StringComparison.OrdinalIgnoreCase))
            {
                if (IsNg(text)) return NgBrush;
                if (IsPass(text)) return PassBrush;
                if (IsTesting(text)) return TestingBrush;
                return PendingBrush;
            }

            if (mode.Equals("ResultBorder", StringComparison.OrdinalIgnoreCase))
            {
                if (IsNg(text)) return NgBorderBrush;
                if (IsPass(text)) return PassBorderBrush;
                if (IsTesting(text)) return TestingBrush;
                return PendingBorderBrush;
            }

            if (mode.Equals("ResultText", StringComparison.OrdinalIgnoreCase))
            {
                if (IsNg(text) || IsPass(text) || IsTesting(text))
                    return WpfBrushes.White;
                return PendingTextBrush;
            }

            if (IsNg(text) || ContainsAny(text,
                "Lỗi", "Error", "failed", "thất bại", "Không khả dụng",
                "Đã ngắt", "Disconnected", "Not available", "Quá thời gian", "Timeout"))
                return WpfBrushes.Red;

            if (IsTesting(text) || ContainsAny(text, "Cảnh báo", "Warning", "Đang", "WAIT"))
                return WpfBrushes.DarkOrange;

            if (IsPass(text) || ContainsAny(text,
                "Sẵn sàng", "Đã kết nối", "thành công", "Connected", "Available", "Ready"))
                return WpfBrushes.ForestGreen;

            return WpfBrushes.DimGray;
        }

        private static WpfBrush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new WpfSolidColorBrush(WpfColor.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static bool IsPass(string text)
            => HasToken(text, "PASS") || HasToken(text, "OK") || HasToken(text, "ĐẠT");

        private static bool IsNg(string text)
            => HasToken(text, "NG") || HasToken(text, "FAIL") ||
               text.Contains("KHÔNG ĐẠT", StringComparison.OrdinalIgnoreCase);

        private static bool IsTesting(string text)
            => HasToken(text, "TESTING") || text.Contains("ĐANG KIỂM TRA", StringComparison.OrdinalIgnoreCase);

        private static bool HasToken(string text, string token)
            => Regex.IsMatch(
                text,
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(token)}(?![\p{{L}}\p{{N}}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}

