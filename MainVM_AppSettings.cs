using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading;

namespace SniffCom
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IConfiguration _configuration;
        private readonly string _settingsFile = "appSettings.json";
        private readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SniffCom",
            "appSettings.json");

        private CancellationTokenSource? _saveSettingsCts;
        private readonly SemaphoreSlim _settingsSaveLock = new(1, 1);
        private bool _isLoadingSettings;

        [ObservableProperty] private bool _isRunAtWindowsStartup = true;
        [ObservableProperty] private bool _startInOverlayMode;
        [ObservableProperty] private bool _minimizeToOverlay = true;

        private static readonly HashSet<string> SettingsProperties = new(StringComparer.Ordinal)
        {
            nameof(SelectedReadPort),
            nameof(SelectedWritePort),
            nameof(SelectedBaudRate),
            nameof(SelectedFontSize),
            nameof(IsAutoWriteEnabled),
            nameof(IsAutoClearLogEnabled),
            nameof(IsRunAtWindowsStartup),
            nameof(StartInOverlayMode),
            nameof(MinimizeToOverlay),
            nameof(SelectedAirPressComPort),
            nameof(SelectedAirPressComBaudrate),
            nameof(IsAirPressTestEnabled),
            nameof(IsAirPressTestComPortAutoConnectEnabled),
            nameof(IsChannel1Enabled),
            nameof(IsChannel2Enabled),
            nameof(IsChannel3Enabled),
            nameof(RaiseAirTime),
            nameof(HoldAirTime),
            nameof(MinTestPressure),
            nameof(MaxLeakPressure),
            nameof(RelayOutputHoldTime),
            nameof(SelectedRelayDriver),
            nameof(SelectedCh340RelayPort),
            nameof(Ch340RelayBaudRate),
            nameof(Ch340RelayOnCommandHex),
            nameof(Ch340RelayOffCommandHex),
            nameof(IsTriggerOnTextMatch),
            nameof(IsTriggerOnImgMatch),
            nameof(TriggerText),
            nameof(TriggerImageROIX),
            nameof(TriggerImageROIY),
            nameof(TriggerImageROIW),
            nameof(TriggerImageROIH),
            nameof(TriggerImageTemplatePath),
            nameof(IsEnableScanTemplate),
            nameof(ScanTemplateIntervalTime)
        };

        private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            if (e.PropertyName != null && SettingsProperties.Contains(e.PropertyName))
                RequestSaveSettings();
        }

        private void LoadSettings()
        {
            _isLoadingSettings = true;
            try
            {
                var settings = new AppSettings();
                _configuration.Bind(settings);

                SelectedReadPort = NormalizePort(settings.SelectedReadPort);
                SelectedWritePort = NormalizePort(settings.SelectedWritePort);
                SelectedBaudRate = BaudRates.Contains(settings.SelectedBaudRate) ? settings.SelectedBaudRate : 115200;
                SelectedFontSize = FontSizes.Contains(settings.SelectedFontSize) ? settings.SelectedFontSize : 18;
                IsAutoWriteEnabled = settings.IsAutoWriteEnabled;
                IsAutoClearLogEnabled = settings.IsAutoClearLogEnabled;

                IsRunAtWindowsStartup = settings.IsRunAtWindowsStartup;
                StartInOverlayMode = settings.StartInOverlayMode;
                MinimizeToOverlay = settings.MinimizeToOverlay;

                SelectedAirPressComPort = NormalizePort(settings.SelectedAirPressComPort);
                SelectedAirPressComBaudrate = BaudRates.Contains(settings.SelectedAirPressComBaudrate)
                    ? settings.SelectedAirPressComBaudrate
                    : 115200;
                IsAirPressTestEnabled = settings.IsAirPressTestEnabled;
                IsAirPressTestComPortAutoConnectEnabled = settings.IsAirPressTestComPortAutoConnectEnabled;

                IsChannel1Enabled = settings.IsChannel1Enabled;
                IsChannel2Enabled = settings.IsChannel2Enabled;
                IsChannel3Enabled = settings.IsChannel3Enabled;

                RaiseAirTime = Math.Clamp(settings.RaiseAirTime, 1, 300_000);
                HoldAirTime = Math.Clamp(settings.HoldAirTime, 1, 300_000);
                MinTestPressure = settings.MinTestPressure > 0 ? settings.MinTestPressure : 35.0;
                MaxLeakPressure = settings.MaxLeakPressure >= 0 ? settings.MaxLeakPressure : 20.0;
                RelayOutputHoldTime = Math.Clamp(settings.RelayOutputHoldTime, 1, 60_000);

                SelectedRelayDriver = settings.SelectedRelayDriver switch
                {
                    Ch340RelayDriver or "CH340 Serial Relay" => Ch340RelayDriver,
                    _ => NativeRelayDriver
                };
                SelectedCh340RelayPort = NormalizePort(settings.SelectedCh340RelayPort);
                Ch340RelayBaudRate = settings.Ch340RelayBaudRate > 0 ? settings.Ch340RelayBaudRate : 9600;
                Ch340RelayOnCommandHex = NormalizeRelayCommand(
                    settings.Ch340RelayOnCommandHex,
                    LegacyTwoChannelOnCommandHex,
                    DefaultCh340OnCommandHex);
                Ch340RelayOffCommandHex = NormalizeRelayCommand(
                    settings.Ch340RelayOffCommandHex,
                    LegacyTwoChannelOffCommandHex,
                    DefaultCh340OffCommandHex);

                IsTriggerOnTextMatch = settings.IsTriggerOnTextMatch;
                IsTriggerOnImgMatch = settings.IsTriggerOnImgMatch;
                TriggerText = settings.TriggerText?.Trim() ?? string.Empty;
                TriggerImageROIX = settings.TriggerImageROIX;
                TriggerImageROIY = settings.TriggerImageROIY;
                TriggerImageROIW = Math.Max(1, settings.TriggerImageROIW);
                TriggerImageROIH = Math.Max(1, settings.TriggerImageROIH);
                TriggerImageTemplatePath = string.IsNullOrWhiteSpace(settings.TriggerImageTemplatePath)
                    ? @"trigger\img\template.png"
                    : settings.TriggerImageTemplatePath;
                ScanTemplateIntervalTime = NormalizeScanTemplateInterval(settings.ScanTemplateIntervalTime);
                IsEnableScanTemplate = settings.IsEnableScanTemplate;
            }
            finally
            {
                _isLoadingSettings = false;
            }

            ApplyWindowsStartupSetting(showStatus: false);
            SetStatus("Đã tải cài đặt");
        }

        private static string? NormalizePort(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string NormalizeRelayCommand(string? value, string legacyValue, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value.Trim(), legacyValue, StringComparison.OrdinalIgnoreCase))
                return defaultValue;

            return value.Trim();
        }

        partial void OnIsRunAtWindowsStartupChanged(bool value)
        {
            if (!_isLoadingSettings)
                ApplyWindowsStartupSetting(showStatus: true);
        }

        private void ApplyWindowsStartupSetting(bool showStatus)
        {
            bool success = StartupManager.SetEnabled(IsRunAtWindowsStartup, out string? error);
            if (!showStatus)
                return;

            SetStatus(success
                ? IsRunAtWindowsStartup
                    ? "Đã bật khởi động cùng Windows"
                    : "Đã tắt khởi động cùng Windows"
                : $"Không thể cập nhật khởi động cùng Windows: {error}");
        }

        private void RequestSaveSettings()
        {
            _saveSettingsCts?.Cancel();
            _saveSettingsCts?.Dispose();
            _saveSettingsCts = new CancellationTokenSource();
            CancellationToken token = _saveSettingsCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, token);
                    await _settingsSaveLock.WaitAsync(token);
                    try
                    {
                        SaveSettings();
                    }
                    finally
                    {
                        _settingsSaveLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Một thay đổi mới hơn sẽ được lưu sau.
                }
                catch (Exception ex)
                {
                    _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        SetStatus($"Không thể lưu cài đặt: {ex.Message.Split('\r', '\n')[0]}")));
                }
            }, token);
        }

        private void SaveSettings()
        {
            var settings = new AppSettings
            {
                SelectedReadPort = SelectedReadPort,
                SelectedWritePort = SelectedWritePort,
                SelectedBaudRate = SelectedBaudRate,
                SelectedFontSize = SelectedFontSize,
                IsAutoWriteEnabled = IsAutoWriteEnabled,
                IsAutoClearLogEnabled = IsAutoClearLogEnabled,
                IsRunAtWindowsStartup = IsRunAtWindowsStartup,
                StartInOverlayMode = StartInOverlayMode,
                MinimizeToOverlay = MinimizeToOverlay,

                SelectedAirPressComPort = SelectedAirPressComPort,
                SelectedAirPressComBaudrate = SelectedAirPressComBaudrate,
                IsAirPressTestEnabled = IsAirPressTestEnabled,
                IsAirPressTestComPortAutoConnectEnabled = IsAirPressTestComPortAutoConnectEnabled,
                IsChannel1Enabled = IsChannel1Enabled,
                IsChannel2Enabled = IsChannel2Enabled,
                IsChannel3Enabled = IsChannel3Enabled,
                RaiseAirTime = RaiseAirTime,
                HoldAirTime = HoldAirTime,
                MinTestPressure = MinTestPressure,
                MaxLeakPressure = MaxLeakPressure,
                RelayOutputHoldTime = RelayOutputHoldTime,

                SelectedRelayDriver = SelectedRelayDriver,
                SelectedCh340RelayPort = SelectedCh340RelayPort,
                Ch340RelayBaudRate = Ch340RelayBaudRate,
                Ch340RelayOnCommandHex = Ch340RelayOnCommandHex,
                Ch340RelayOffCommandHex = Ch340RelayOffCommandHex,

                IsTriggerOnTextMatch = IsTriggerOnTextMatch,
                IsTriggerOnImgMatch = IsTriggerOnImgMatch,
                TriggerText = TriggerText,
                TriggerImageROIX = TriggerImageROIX,
                TriggerImageROIY = TriggerImageROIY,
                TriggerImageROIW = TriggerImageROIW,
                TriggerImageROIH = TriggerImageROIH,
                TriggerImageTemplatePath = TriggerImageTemplatePath,
                IsEnableScanTemplate = IsEnableScanTemplate,
                ScanTemplateIntervalTime = ScanTemplateIntervalTime
            };

            string? directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string tempPath = _settingsPath + ".tmp";
            string json = System.Text.Json.JsonSerializer.Serialize(
                settings,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
    }
}

