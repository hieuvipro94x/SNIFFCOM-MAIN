using CommunityToolkit.Mvvm.ComponentModel;
using System.Text;
using System.Windows.Threading;

namespace SniffCom
{
    public partial class MainViewModel : ObservableObject
    {
        private const int MaxDisplayLogChars = 20_000;
        private static readonly TimeSpan AutoClearIdleGap = TimeSpan.FromMilliseconds(250);

        private readonly StringBuilder _dataLogBuilder = new();
        private readonly StringBuilder _hexLogBuilder = new();
        private readonly DispatcherTimer _logUiTimer = new() { Interval = TimeSpan.FromMilliseconds(75) };
        private bool _isLogUiUpdatePending;
        private string _triggerTail = string.Empty;
        private DateTime _lastLogReceiveUtc = DateTime.MinValue;

        [ObservableProperty] private bool _isHexMode;
        [ObservableProperty] private string _hexLog = string.Empty;
        [ObservableProperty] private string _displayLog = string.Empty;

        private void EnsureLogUiTimer()
        {
            if (_logUiTimer.IsEnabled)
                return;

            _logUiTimer.Tick -= LogUiTimer_Tick;
            _logUiTimer.Tick += LogUiTimer_Tick;
            _logUiTimer.Start();
        }

        private void LogUiTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isLogUiUpdatePending)
            {
                _logUiTimer.Stop();
                return;
            }

            _isLogUiUpdatePending = false;
            DataLog = _dataLogBuilder.ToString();
            HexLog = _hexLogBuilder.ToString();
            DisplayLog = IsHexMode ? HexLog : DataLog;
        }

        private void RequestLogUiUpdate()
        {
            _isLogUiUpdatePending = true;
            EnsureLogUiTimer();
        }

        private void AppendDataLog(string text, ReadOnlySpan<byte> rawBytes)
        {
            if (string.IsNullOrEmpty(text) && rawBytes.IsEmpty)
                return;

            DateTime now = DateTime.UtcNow;
            bool beginsNewMessage = _lastLogReceiveUtc == DateTime.MinValue ||
                                    now - _lastLogReceiveUtc >= AutoClearIdleGap;
            _lastLogReceiveUtc = now;

            if (IsAutoClearLogEnabled && beginsNewMessage)
            {
                _dataLogBuilder.Clear();
                _hexLogBuilder.Clear();
            }

            if (!string.IsNullOrEmpty(text))
            {
                _dataLogBuilder.Append(text);
                TrimBuilder(_dataLogBuilder, MaxDisplayLogChars);
            }

            if (!rawBytes.IsEmpty)
            {
                if (_hexLogBuilder.Length > 0)
                    _hexLogBuilder.Append(' ');

                AppendBytesAsHex(_hexLogBuilder, rawBytes);
                TrimBuilder(_hexLogBuilder, MaxDisplayLogChars);
            }

            RequestLogUiUpdate();
            TryStartTextTrigger(text);
        }

        private void TryStartTextTrigger(string newestText)
        {
            if (!IsTriggerOnTextMatch || !IsAirPressTestEnabled || IsTesting || !IsAirPressTestConnected)
                return;

            string trigger = TriggerText?.Trim() ?? string.Empty;
            if (trigger.Length == 0)
                return;

            string triggerWindow = _triggerTail + newestText;
            int tailLength = Math.Max(0, trigger.Length - 1);
            _triggerTail = tailLength == 0
                ? string.Empty
                : triggerWindow.Length <= tailLength
                    ? triggerWindow
                    : triggerWindow[^tailLength..];

            if (triggerWindow.Contains(trigger, StringComparison.OrdinalIgnoreCase))
                _ = StartAirPressTestAsync();
        }

        private void RebuildLogBuffersFromDataLog()
        {
            _dataLogBuilder.Clear();
            _dataLogBuilder.Append(DataLog ?? string.Empty);
            TrimBuilder(_dataLogBuilder, MaxDisplayLogChars);

            _hexLogBuilder.Clear();
            AppendBytesAsHex(_hexLogBuilder, Encoding.ASCII.GetBytes(_dataLogBuilder.ToString()));
            TrimBuilder(_hexLogBuilder, MaxDisplayLogChars);

            HexLog = _hexLogBuilder.ToString();
            DisplayLog = IsHexMode ? HexLog : DataLog ?? string.Empty;
            _isLogUiUpdatePending = false;
        }

        private static void AppendBytesAsHex(StringBuilder builder, ReadOnlySpan<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                    builder.Append(' ');
                builder.Append(bytes[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private static void TrimBuilder(StringBuilder builder, int maxChars)
        {
            if (builder.Length > maxChars)
                builder.Remove(0, builder.Length - maxChars);
        }

        partial void OnIsHexModeChanged(bool value)
        {
            DisplayLog = value ? _hexLogBuilder.ToString() : _dataLogBuilder.ToString();
        }

        private void StopLogUiUpdates()
        {
            _logUiTimer.Stop();
            _logUiTimer.Tick -= LogUiTimer_Tick;
        }
    }
}
