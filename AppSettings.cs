namespace SniffCom
{
    public sealed class AppSettings
    {
        // Serial chung
        public string? SelectedReadPort { get; set; }
        public string? SelectedWritePort { get; set; }
        public int SelectedBaudRate { get; set; } = 115200;
        public int SelectedFontSize { get; set; } = 18;
        public bool IsAutoWriteEnabled { get; set; }
        public bool IsAutoClearLogEnabled { get; set; } = true;

        // Khởi động và chế độ lớp phủ
        public bool IsRunAtWindowsStartup { get; set; } = true;
        public bool StartInOverlayMode { get; set; }
        public bool MinimizeToOverlay { get; set; } = true;

        // Air Pressure Test
        public string? SelectedAirPressComPort { get; set; }
        public int SelectedAirPressComBaudrate { get; set; } = 115200;
        public bool IsAirPressTestEnabled { get; set; } = true;
        public bool IsAirPressTestComPortAutoConnectEnabled { get; set; } = true;

        public bool IsChannel1Enabled { get; set; } = true;
        public bool IsChannel2Enabled { get; set; } = true;
        public bool IsChannel3Enabled { get; set; }

        public int RaiseAirTime { get; set; } = 1000;
        public int HoldAirTime { get; set; } = 500;
        public double MinTestPressure { get; set; } = 35.0;
        public double MaxLeakPressure { get; set; } = 20.0;
        public int RelayOutputHoldTime { get; set; } = 500;

        // Relay
        public string SelectedRelayDriver { get; set; } = MainViewModel.NativeRelayDriver;
        public string? SelectedCh340RelayPort { get; set; }
        public int Ch340RelayBaudRate { get; set; } = 9600;
        public string Ch340RelayOnCommandHex { get; set; } = MainViewModel.DefaultCh340OnCommandHex;
        public string Ch340RelayOffCommandHex { get; set; } = MainViewModel.DefaultCh340OffCommandHex;

        // Kích hoạt tự động
        public bool IsTriggerOnTextMatch { get; set; }
        public bool IsTriggerOnImgMatch { get; set; }
        public string? TriggerText { get; set; } = string.Empty;
        public int TriggerImageROIX { get; set; }
        public int TriggerImageROIY { get; set; }
        public int TriggerImageROIW { get; set; } = 100;
        public int TriggerImageROIH { get; set; } = 100;
        public string? TriggerImageTemplatePath { get; set; } = @"trigger\img\template.png";
        public bool IsEnableScanTemplate { get; set; }
        public int ScanTemplateIntervalTime { get; set; } = 500;
    }
}

