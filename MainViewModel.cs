using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using SniffCom.ulti;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;

namespace SniffCom
{
    public partial class MainViewModel : ObservableObject
    {
        private SerialPort? _readPort;
        private SerialPort? _writePort;
        private readonly SemaphoreSlim _writePortLock = new(1, 1);
        private readonly SemaphoreSlim _readPortLock = new(1, 1);
        private readonly HashSet<string> _detectedPorts = new(StringComparer.OrdinalIgnoreCase);
        private bool _isShuttingDown;
        private bool _isInitialized;
        private readonly string _logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        public string AppVersionDisplay { get; } = $"Version {GetAppVersion()}";

        private static string GetAppVersion()
        {
            var assembly = typeof(MainViewModel).Assembly;
            var informationalVersion = assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
                return informationalVersion;

            return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        }

        public MainViewModel()
        {
            // === T?I C?U H�NH T? FILE ===
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile(_settingsFile, optional: true, reloadOnChange: true)
                .AddJsonFile(_settingsPath, optional: true, reloadOnChange: true);

            _configuration = builder.Build();

            // Kh?i t?o command
            RefreshComPortsCommand = new RelayCommand(RefreshComPorts);
            ReadComConnectCommand = new AsyncRelayCommand(ToggleReadComConnectionAsync);
            CheckWriteComAvailableCommand = new RelayCommand(CheckWriteComAvailable);
            CountSelectedTextCommand = new RelayCommand(CountSelectedText);
            SwapTextCommand = new RelayCommand(SwapText);
            SaveLogCommand = new RelayCommand(SaveLog);
            WriteDataLogCommand = new RelayCommand(WriteDataLog, CanWriteDataLog);
            ManualRelayTestCommand = new AsyncRelayCommand(ManualRelayTestAsync);

            AirPressTestConnectCommand = new AsyncRelayCommand(ToggleAirPressComConnectionAsync);
            StartAirPressTestCommand = new AsyncRelayCommand(StartAirPressTestAsync, CanStartAirPressTest);
            ManualStartAirPressTestCommand = new AsyncRelayCommand(StartAirPressTestAsync, CanStartAirPressTest);

            SelectTriggerImageROICommand = new AsyncRelayCommand(SelectTriggerImageROIAsync);
            SelectTriggerImageTEMPLATECommand = new RelayCommand(SelectTriggerImageTemplate);

            // Danh s�ch
            BaudRates = new ObservableCollection<int> { 9600, 115200 };
            FontSizes = new ObservableCollection<int> { 10, 12, 14, 16, 18, 20, 24, 28, 32 };

            Directory.CreateDirectory(_logFolder);

            // === T?I C?U H�NH L�N UI ===
            LoadSettings();
            LoadTemplateImage();

            // === THEO D�I THAY �?I �? T? �?NG LUU ===
            this.PropertyChanged += MainViewModel_PropertyChanged;

            RefreshComPorts();

            // --- KH?I T?O SCANNER TIMER ---
            _scanTimer = new DispatcherTimer();
            _scanTimer.Tick += ScanTimer_Tick; // G�n h�m s? du?c g?i
            _scanTimer.Interval = TimeSpan.FromMilliseconds(NormalizeScanTemplateInterval(ScanTemplateIntervalTime));
            ApplyTemplateScannerState();

            OnPropertyChanged(nameof(IsHexMode));
            _isInitialized = true;
        }


        public async Task InitializeAsync()
        {
            await AutoConnectSavedComsAsync();
        }


        // ==================== Properties ====================

        [ObservableProperty] private ObservableCollection<string> _availablePorts = new();
        [ObservableProperty] private string? _selectedReadPort;
        [ObservableProperty] private string? _selectedWritePort;
        [ObservableProperty] private int _selectedBaudRate;
        [ObservableProperty] private ObservableCollection<int> _baudRates;
        [ObservableProperty] private ObservableCollection<int> _fontSizes;
        [ObservableProperty] private int _selectedFontSize;

        // D? li?u thu?n t? Serial (d�ng d? x? l�, count, swap, save)
        [ObservableProperty] private string _dataLog = "";

        // Th�ng b�o h? th?ng - ch? hi?n 1 d�ng m?i nh?t
        [ObservableProperty] private string _appLog = "Sẵn sàng";

        [ObservableProperty] private bool _isReadComConnected = false;
        [ObservableProperty] private string _readConnectButtonText = "Kết nối COM";
        [ObservableProperty] private string _writeComStatus = "Chưa kiểm tra";
        [ObservableProperty] private bool _isAutoWriteEnabled = false;
        [ObservableProperty] private bool _isAutoClearLogEnabled = true;

        [ObservableProperty] private string _selectedProcessText = "";
        [ObservableProperty] private int _selectedProcessTextCount = 0;
        [ObservableProperty] private string _textToSwap = "";

        [ObservableProperty] private int _relayOutputHoldTime = 500;

        [ObservableProperty] private bool _isEnableScanTemplate = false;
        [ObservableProperty] private int _scanTemplateIntervalTime = 500;

        [ObservableProperty] private bool _canScan;

        // ==================== Commands ====================
        public IRelayCommand RefreshComPortsCommand { get; }
        public IAsyncRelayCommand ReadComConnectCommand { get; }
        public IRelayCommand CheckWriteComAvailableCommand { get; }
        public IRelayCommand CountSelectedTextCommand { get; }
        public IRelayCommand SwapTextCommand { get; }
        public IRelayCommand SaveLogCommand { get; }
        public IRelayCommand WriteDataLogCommand { get; }

        // ==================== Methods ====================
        private void WriteDataLog()
        {
            if (string.IsNullOrEmpty(SelectedWritePort))
            {
                SetStatus("Lỗi: Chưa chọn COM gửi dữ liệu");
                return;
            }

            if (string.IsNullOrWhiteSpace(DataLog))
            {
                SetStatus("Cảnh báo: Không có dữ liệu để gửi");
                return;
            }

            try
            {
                using var port = new SerialPort(SelectedWritePort, SelectedBaudRate)
                {
                    Encoding = Encoding.ASCII,
                    WriteTimeout = 1000
                };

                port.Open();
                port.Write(DataLog + "\r\n");

                SetStatus($"Đã gửi {DataLog.Length} byte -> {SelectedWritePort}");
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi gửi dữ liệu -> {ex.Message.Split('\r')[0]}");
            }
        }

        private bool CanWriteDataLog()
        {
            //return !string.IsNullOrEmpty(SelectedWritePort) && !string.IsNullOrWhiteSpace(DataLog);
            return true;
        }
        private void RefreshComPorts()
        {
            try
            {
                var detectedPorts = SerialPort.GetPortNames()
                    .Where(port => !string.IsNullOrWhiteSpace(port))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _detectedPorts.Clear();
                foreach (string port in detectedPorts)
                    _detectedPorts.Add(port);

                var displayedPorts = new HashSet<string>(detectedPorts, StringComparer.OrdinalIgnoreCase);
                AddRememberedPort(displayedPorts, SelectedReadPort);
                AddRememberedPort(displayedPorts, SelectedWritePort);
                AddRememberedPort(displayedPorts, SelectedAirPressComPort);
                AddRememberedPort(displayedPorts, SelectedCh340RelayPort);

                AvailablePorts.Clear();
                foreach (string port in displayedPorts.OrderBy(port => port, StringComparer.OrdinalIgnoreCase))
                    AvailablePorts.Add(port);

                string? defaultPort = detectedPorts.FirstOrDefault();
                SelectedReadPort ??= defaultPort;
                SelectedWritePort ??= defaultPort;
                SelectedAirPressComPort ??= defaultPort;
                SelectedCh340RelayPort ??= defaultPort;

                SetStatus(detectedPorts.Count == 0
                    ? "Không tìm thấy cổng COM đang kết nối"
                    : $"Đã phát hiện {detectedPorts.Count} cổng COM: {string.Join(", ", detectedPorts)}");
            }
            catch (Exception ex)
            {
                _detectedPorts.Clear();
                AvailablePorts.Clear();
                SetStatus($"Lỗi đọc danh sách COM: {ex.Message.Split('\r', '\n')[0]}");
            }
        }

        private static void AddRememberedPort(ISet<string> ports, string? port)
        {
            if (!string.IsNullOrWhiteSpace(port))
                ports.Add(port);
        }

        private bool IsPortDetected(string? port)
            => !string.IsNullOrWhiteSpace(port) && _detectedPorts.Contains(port);

        private async Task AutoConnectSavedComsAsync()
        {
            await Task.Yield();

            if (IsAirPressTestComPortAutoConnectEnabled &&
                IsPortDetected(SelectedAirPressComPort) &&
                !IsAirPressTestConnected)
            {
                await ToggleAirPressComConnectionAsync();
            }
        }
        private async Task ToggleReadComConnectionAsync()
        {
            await _readPortLock.WaitAsync();
            try
            {
                if (IsReadComConnected)
                {
                    CloseReadPort();
                    SetStatus($"Đã ngắt kết nối {SelectedReadPort}");
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedReadPort))
                {
                    SetStatus("Chưa chọn COM đọc dữ liệu");
                    return;
                }

                if (!IsPortDetected(SelectedReadPort))
                {
                    SetStatus($"COM {SelectedReadPort} hiện không được kết nối");
                    return;
                }

                try
                {
                    _readPort = new SerialPort(SelectedReadPort, SelectedBaudRate)
                    {
                        ReadTimeout = 500,
                        WriteTimeout = 500,
                        Encoding = Encoding.ASCII
                    };

                    _readPort.DataReceived += ReadPort_DataReceived;
                    _readPort.Open();

                    IsReadComConnected = true;
                    ReadConnectButtonText = "Ngắt kết nối";
                    SetStatus($"Đã kết nối {SelectedReadPort} @ {SelectedBaudRate} baud");
                }
                catch (Exception ex)
                {
                    CloseReadPort();
                    SetStatus($"Kết nối thất bại: {ex.Message.Split('\r', '\n')[0]}");
                }
            }
            finally
            {
                _readPortLock.Release();
            }
        }

        private void CloseReadPort()
        {
            try
            {
                if (_readPort != null)
                {
                    _readPort.DataReceived -= ReadPort_DataReceived;
                    if (_readPort.IsOpen)
                        _readPort.Close();
                    _readPort.Dispose();
                }
            }
            catch
            {
                // Đóng cổng theo kiểu best effort.
            }
            finally
            {
                _readPort = null;
                IsReadComConnected = false;
                ReadConnectButtonText = "Kết nối COM";
            }
        }

        private async Task ReconnectReadPortIfNeededAsync()
        {
            if (_isLoadingSettings || _isShuttingDown || !IsReadComConnected)
                return;

            await ToggleReadComConnectionAsync();
            await Task.Delay(100);
            await ToggleReadComConnectionAsync();
        }

        private void ReadPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var port = sender as SerialPort ?? _readPort;
            if (port == null || !port.IsOpen) return;

            try
            {
                int bytesToRead = port.BytesToRead;
                if (bytesToRead <= 0) return;

                byte[] buffer = new byte[bytesToRead];
                int read = port.Read(buffer, 0, bytesToRead);
                if (read <= 0) return;

                if (read != buffer.Length)
                    Array.Resize(ref buffer, read);

                string data = DecodeSerialBytes(buffer);
                bool shouldAutoWrite = IsAutoWriteEnabled;

                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    AppendDataLog(data, buffer);
                    SetStatus($"Đã nhận {buffer.Length} byte");
                }));

                if (shouldAutoWrite)
                    _ = Task.Run(() => TryWriteToWriteComAsync(data));
            }
            catch (Exception ex)
            {
                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    SetStatus($"Lỗi đọc COM: {ex.Message.Split('\r')[0]}")));
            }
        }

        private static string DecodeSerialBytes(byte[] buffer)
        {
            string data = Encoding.UTF8.GetString(buffer);

            if (!ContainsGarbage(data))
                return data;

            data = Encoding.Latin1.GetString(buffer);
            if (!ContainsGarbage(data))
                return data;

            return Encoding.ASCII.GetString(buffer).Replace("?", ".");
        }

        private static bool ContainsGarbage(string s)
        {
            return s.Any(c => (c < 32 && c != '\r' && c != '\n' && c != '\t') || (c > 126 && c < 160));
        }

        private void CheckWriteComAvailable()
        {
            if (string.IsNullOrEmpty(SelectedWritePort))
            {
                WriteComStatus = "Chưa chọn cổng";
                SetStatus("COM gửi: Chưa chọn cổng");
                return;
            }

            try
            {
                using var temp = new SerialPort(SelectedWritePort, SelectedBaudRate);
                temp.Open();
                WriteComStatus = "Sẵn sàng";
                SetStatus($"COM gửi {SelectedWritePort}: Sẵn sàng");
            }
            catch (Exception ex)
            {
                WriteComStatus = "Không khả dụng";
                SetStatus($"COM gửi {SelectedWritePort}: Không khả dụng. " + ex.Message);
            }
        }

        private async Task TryWriteToWriteComAsync(string data)
        {
            if (string.IsNullOrWhiteSpace(SelectedWritePort) || string.IsNullOrEmpty(data))
                return;

            await _writePortLock.WaitAsync();
            try
            {
                if (_writePort == null ||
                    !string.Equals(_writePort.PortName, SelectedWritePort, StringComparison.OrdinalIgnoreCase) ||
                    _writePort.BaudRate != SelectedBaudRate)
                {
                    CloseWritePort();
                    _writePort = new SerialPort(SelectedWritePort, SelectedBaudRate)
                    {
                        Encoding = Encoding.ASCII,
                        WriteTimeout = 500
                    };
                }

                if (!_writePort.IsOpen)
                    _writePort.Open();

                _writePort.Write(data);

                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    SetStatus($"Đã tự động gửi {data.Length} byte -> {SelectedWritePort}")));
            }
            catch (Exception ex)
            {
                CloseWritePort();
                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    SetStatus($"Tự động gửi thất bại: {ex.Message.Split('\r')[0]}")));
            }
            finally
            {
                _writePortLock.Release();
            }
        }

        private void CloseWritePort()
        {
            try
            {
                if (_writePort?.IsOpen == true)
                    _writePort.Close();
                _writePort?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _writePort = null;
            }
        }

        partial void OnSelectedReadPortChanged(string? value)
        {
            if (!_isLoadingSettings)
                _ = ReconnectReadPortIfNeededAsync();
        }

        partial void OnSelectedWritePortChanged(string? value) => CloseWritePort();

        partial void OnSelectedBaudRateChanged(int value)
        {
            CloseWritePort();
            if (!_isLoadingSettings)
                _ = ReconnectReadPortIfNeededAsync();
        }

        private void SetStatus(string message)
        {
            AppLog = $"[ {DateTime.Now:HH:mm:ss} ] {message}";
        }

        private void CountSelectedText()
        {
            if (string.IsNullOrWhiteSpace(SelectedProcessText))
            {
                SelectedProcessTextCount = 0;
                return;
            }

            var escaped = Regex.Escape(SelectedProcessText);
            SelectedProcessTextCount = Regex.Count(DataLog, escaped, RegexOptions.IgnoreCase);
        }

        private void SwapText()
        {
            if (string.IsNullOrWhiteSpace(SelectedProcessText)) return;

            string replacement = TextToSwap switch
            {
                ":del" => "",
                ":space" => " ",
                _ => TextToSwap
            };

            var escaped = Regex.Escape(SelectedProcessText);
            var newLog = Regex.Replace(DataLog, escaped, replacement, RegexOptions.IgnoreCase);

            if (newLog != DataLog)
            {
                DataLog = newLog;
                RebuildLogBuffersFromDataLog();
                SetStatus($"Đã thay '{SelectedProcessText}' -> '{replacement}'");
            }
        }

        private void SaveLog()
        {
            Directory.CreateDirectory(_logFolder);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = _logFolder,
                FileName = $"SniffCom_Log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt",
                DefaultExt = "txt",
                Filter = "Tệp văn bản (*.txt)|*.txt|Tất cả tệp (*.*)|*.*",
                Title = "Lưu nhật ký dữ liệu COM"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                File.WriteAllText(dialog.FileName, DataLog ?? string.Empty, Encoding.UTF8);
                SetStatus($"Đã lưu nhật ký: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                SetStatus($"Lưu thất bại: {ex.Message.Split('\r')[0]}");
            }
        }

        public void Shutdown()
        {
            if (_isShuttingDown)
                return;

            _isShuttingDown = true;
            PropertyChanged -= MainViewModel_PropertyChanged;

            _scanTimer.Stop();
            _scanTimer.Tick -= ScanTimer_Tick;
            StopLogUiUpdates();

            _saveSettingsCts?.Cancel();
            _saveSettingsCts?.Dispose();
            _saveSettingsCts = null;

            try
            {
                _settingsSaveLock.Wait();
                try
                {
                    SaveSettings();
                }
                finally
                {
                    _settingsSaveLock.Release();
                }
            }
            catch
            {
                // App is closing; settings failures must not prevent hardware cleanup.
            }

            CloseReadPort();

            CloseWritePort();

            _airPressTestCts?.Cancel();
            _airPressTestCts?.Dispose();
            _airPressTestCts = null;

            if (_airPressTestPort != null)
            {
                try
                {
                    if (_airPressTestPort.IsOpen)
                        _airPressTestPort.Close();
                    _airPressTestPort.Dispose();
                }
                catch
                {
                    // Continue with relay cleanup.
                }
                finally
                {
                    _airPressTestPort = null;
                }
            }

            ReleaseRelayResources();

            ImageMatcher.DisposeTemplate();
        }
    }
}
