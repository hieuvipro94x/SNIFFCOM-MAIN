using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows;

namespace SniffCom
{
    public partial class MainViewModel : ObservableObject
    {
        private SerialPort? _airPressTestPort;
        private readonly SemaphoreSlim _airPressTestLock = new(1, 1);
        private CancellationTokenSource? _airPressTestCts;

        [ObservableProperty] private bool _isAirPressTestConnected;
        [ObservableProperty] private bool _isTesting;
        [ObservableProperty] private bool _isAirPressTestComPortAutoConnectEnabled = true;

        [ObservableProperty] private string? _selectedAirPressComPort;
        [ObservableProperty] private int _selectedAirPressComBaudrate = 115200;
        [ObservableProperty] private bool _isAirPressTestEnabled = true;
        [ObservableProperty] private string _airPressTestStatus = "Chưa kết nối";

        [ObservableProperty] private bool _isChannel1Enabled = true;
        [ObservableProperty] private bool _isChannel2Enabled = true;
        [ObservableProperty] private bool _isChannel3Enabled;

        [ObservableProperty] private double _minTestPressure = 35.0;
        [ObservableProperty] private double _maxLeakPressure = 20.0;
        [ObservableProperty] private int _raiseAirTime = 1000;
        [ObservableProperty] private int _holdAirTime = 500;

        [ObservableProperty] private string _channel1TestResult = string.Empty;
        [ObservableProperty] private string _channel2TestResult = string.Empty;
        [ObservableProperty] private string _channel3TestResult = string.Empty;

        [ObservableProperty] private string _channel1InputPressure = "---";
        [ObservableProperty] private string _channel2InputPressure = "---";
        [ObservableProperty] private string _channel3InputPressure = "---";
        [ObservableProperty] private string _channel1ReadPressure = "---";
        [ObservableProperty] private string _channel2ReadPressure = "---";
        [ObservableProperty] private string _channel3ReadPressure = "---";
        [ObservableProperty] private string _channel1LeakPressure = "---";
        [ObservableProperty] private string _channel2LeakPressure = "---";
        [ObservableProperty] private string _channel3LeakPressure = "---";

        // Trạng thái dành cho cửa sổ lớp phủ: PENDING, TESTING, PASS, NG.
        [ObservableProperty] private string _overallPressureState = "PENDING";
        [ObservableProperty] private string _overallPressureDisplayText = "CHỜ KIỂM TRA";
        [ObservableProperty] private string _overallLeakPressureDisplay = "---";
        public string OverlayChannel1LeakDisplay => IsChannel1Enabled ? Channel1LeakPressure : "---";
        public string OverlayChannel2LeakDisplay => IsChannel2Enabled ? Channel2LeakPressure : "---";
        public string OverlayChannel3LeakDisplay => IsChannel3Enabled ? Channel3LeakPressure : "---";
        public Visibility OverlayChannel1Visibility => IsChannel1Enabled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility OverlayChannel2Visibility => IsChannel2Enabled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility OverlayChannel3Visibility => IsChannel3Enabled ? Visibility.Visible : Visibility.Collapsed;

        public IRelayCommand AirPressTestConnectCommand { get; private set; }
        public IRelayCommand StartAirPressTestCommand { get; private set; }
        public IRelayCommand ManualStartAirPressTestCommand { get; private set; }

        private void CheckAirPressComAvailable()
        {
            if (string.IsNullOrWhiteSpace(SelectedAirPressComPort))
            {
                AirPressTestStatus = "Chưa chọn cổng";
                return;
            }

            if (!IsPortDetected(SelectedAirPressComPort))
            {
                AirPressTestStatus = "Không khả dụng";
                SetStatus($"APT COM {SelectedAirPressComPort}: Chưa kết nối thiết bị");
                return;
            }

            AirPressTestStatus = "Sẵn sàng";
            SetStatus($"APT COM {SelectedAirPressComPort}: Sẵn sàng");
        }

        partial void OnSelectedAirPressComPortChanged(string? value)
        {
            if (_isLoadingSettings || _isShuttingDown || !_isInitialized)
                return;

            if (IsAirPressTestConnected || IsAirPressTestComPortAutoConnectEnabled)
                _ = ReconnectAirPressComAsync(connectAfterDisconnect: true);
            else
                CheckAirPressComAvailable();
        }

        partial void OnSelectedAirPressComBaudrateChanged(int value)
        {
            if (_isInitialized && !_isLoadingSettings && IsAirPressTestConnected)
                _ = ReconnectAirPressComAsync(connectAfterDisconnect: true);
        }

        partial void OnIsAirPressTestComPortAutoConnectEnabledChanged(bool value)
        {
            if (!_isInitialized || _isLoadingSettings || !value || IsAirPressTestConnected)
                return;

            if (IsPortDetected(SelectedAirPressComPort))
                _ = ToggleAirPressComConnectionAsync();
        }

        private async Task ReconnectAirPressComAsync(bool connectAfterDisconnect)
        {
            if (IsTesting)
            {
                SetStatus("Không thể đổi APT COM khi bài kiểm tra đang chạy");
                return;
            }

            await _airPressTestLock.WaitAsync();
            try
            {
                DisconnectAirPressCore();
                if (connectAfterDisconnect && IsPortDetected(SelectedAirPressComPort))
                {
                    await Task.Delay(100);
                    ConnectAirPressCore();
                }
            }
            finally
            {
                _airPressTestLock.Release();
            }
        }

        private async Task ToggleAirPressComConnectionAsync()
        {
            if (IsTesting)
            {
                SetStatus("APT đang kiểm tra, chưa thể thay đổi kết nối");
                return;
            }

            if (!await _airPressTestLock.WaitAsync(0))
            {
                SetStatus("APT đang xử lý kết nối, vui lòng chờ");
                return;
            }

            try
            {
                if (IsAirPressTestConnected)
                    DisconnectAirPressCore();
                else
                    ConnectAirPressCore();
            }
            finally
            {
                _airPressTestLock.Release();
            }
        }

        private void ConnectAirPressCore()
        {
            if (string.IsNullOrWhiteSpace(SelectedAirPressComPort) || SelectedAirPressComBaudrate <= 0)
            {
                AirPressTestStatus = "Cài đặt COM không hợp lệ";
                SetStatus("Lỗi: Cài đặt APT COM không hợp lệ");
                return;
            }

            if (!IsPortDetected(SelectedAirPressComPort))
            {
                AirPressTestStatus = "Không khả dụng";
                SetStatus($"APT COM {SelectedAirPressComPort}: Không tìm thấy thiết bị");
                return;
            }

            try
            {
                DisconnectAirPressCore(updateStatus: false);
                _airPressTestPort = new SerialPort(SelectedAirPressComPort, SelectedAirPressComBaudrate)
                {
                    Encoding = Encoding.ASCII,
                    NewLine = "\r\n",
                    WriteTimeout = 1000,
                    ReadTimeout = 1000
                };
                _airPressTestPort.Open();
                _airPressTestPort.DiscardInBuffer();
                _airPressTestPort.DiscardOutBuffer();

                IsAirPressTestConnected = true;
                AirPressTestStatus = "Đã kết nối";
                SetStatus($"APT COM {SelectedAirPressComPort}: Kết nối thành công");
            }
            catch (Exception ex)
            {
                DisconnectAirPressCore(updateStatus: false);
                AirPressTestStatus = "Kết nối thất bại";
                SetStatus($"APT COM {SelectedAirPressComPort}: {ex.Message.Split('\r', '\n')[0]}");
            }
        }

        private void DisconnectAirPressCore(bool updateStatus = true)
        {
            _airPressTestCts?.Cancel();

            try
            {
                if (_airPressTestPort != null)
                {
                    if (_airPressTestPort.IsOpen)
                        _airPressTestPort.Close();
                    _airPressTestPort.Dispose();
                }
            }
            catch
            {
                // Đóng COM theo kiểu best effort.
            }
            finally
            {
                _airPressTestPort = null;
                IsAirPressTestConnected = false;
            }

            ReleaseRelayResources();

            if (updateStatus)
            {
                AirPressTestStatus = "Đã ngắt kết nối";
                SetStatus($"APT COM {SelectedAirPressComPort}: Đã ngắt kết nối");
            }
        }

        private bool TryProcessTestResult(string resultString, out bool allPassed)
        {
            allPassed = false;
            if (!resultString.StartsWith(":RESULT", StringComparison.OrdinalIgnoreCase))
                return false;

            string dataSegment = resultString[":RESULT".Length..].Trim().TrimStart(',').Trim();
            string[] data = dataSegment.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (data.Length < 6)
            {
                SetStatus($"Lỗi APT: Kết quả cần 6 giá trị nhưng chỉ nhận {data.Length}");
                return false;
            }

            var culture = CultureInfo.InvariantCulture;
            var values = new double[6];
            for (int i = 0; i < values.Length; i++)
            {
                if (!double.TryParse(data[i], NumberStyles.Any, culture, out values[i]))
                {
                    SetStatus($"Lỗi APT: Giá trị '{data[i]}' không hợp lệ");
                    return false;
                }
                values[i] = Math.Abs(values[i]);
            }

            bool ch1Passed = UpdateChannelResult(1, values[0], values[1]);
            bool ch2Passed = UpdateChannelResult(2, values[2], values[3]);
            bool ch3Passed = UpdateChannelResult(3, values[4], values[5]);
            allPassed = ch1Passed && ch2Passed && ch3Passed;
            return true;
        }

        private bool UpdateChannelResult(int channel, double inputPressure, double readPressure)
        {
            bool enabled = channel switch
            {
                1 => IsChannel1Enabled,
                2 => IsChannel2Enabled,
                3 => IsChannel3Enabled,
                _ => false
            };

            if (!enabled)
            {
                SetChannelValues(channel, "---", "---", "---", "---");
                return true;
            }

            double leak = Math.Max(0, inputPressure - readPressure);
            bool passed = inputPressure >= MinTestPressure && leak <= MaxLeakPressure;
            SetChannelValues(
                channel,
                inputPressure.ToString("F1", CultureInfo.InvariantCulture),
                readPressure.ToString("F1", CultureInfo.InvariantCulture),
                leak.ToString("F1", CultureInfo.InvariantCulture),
                passed ? "PASS" : "NG");
            return passed;
        }

        private void SetChannelValues(int channel, string input, string read, string leak, string result)
        {
            switch (channel)
            {
                case 1:
                    Channel1InputPressure = input;
                    Channel1ReadPressure = read;
                    Channel1LeakPressure = leak;
                    Channel1TestResult = result;
                    break;
                case 2:
                    Channel2InputPressure = input;
                    Channel2ReadPressure = read;
                    Channel2LeakPressure = leak;
                    Channel2TestResult = result;
                    break;
                case 3:
                    Channel3InputPressure = input;
                    Channel3ReadPressure = read;
                    Channel3LeakPressure = leak;
                    Channel3TestResult = result;
                    break;
            }

            UpdateOverallPressureState();
        }

        private void UpdateOverallPressureState()
        {
            if (IsTesting)
            {
                SetOverallPressureState("TESTING", "ĐANG KIỂM TRA");
                return;
            }

            var selectedResults = new List<string>(3);
            if (IsChannel1Enabled) selectedResults.Add(Channel1TestResult);
            if (IsChannel2Enabled) selectedResults.Add(Channel2TestResult);
            if (IsChannel3Enabled) selectedResults.Add(Channel3TestResult);

            if (selectedResults.Count == 0)
            {
                SetOverallPressureState("PENDING", "CHỜ KIỂM TRA");
                return;
            }

            if (selectedResults.Any(IsNgResult))
            {
                SetOverallPressureState("NG", "ÁP SUẤT NG");
                return;
            }

            if (selectedResults.All(IsPassResult))
            {
                SetOverallPressureState("PASS", "ÁP SUẤT OK");
                return;
            }

            SetOverallPressureState("PENDING", "CHỜ KIỂM TRA");
        }

        private void SetOverallPressureState(string state, string displayText)
        {
            if (!string.Equals(OverallPressureState, state, StringComparison.Ordinal))
                OverallPressureState = state;
            if (!string.Equals(OverallPressureDisplayText, displayText, StringComparison.Ordinal))
                OverallPressureDisplayText = displayText;
        }

        private static bool IsPassResult(string? result)
        {
            string text = result?.Trim() ?? string.Empty;
            return text.Equals("PASS", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("OK", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNgResult(string? result)
        {
            string text = result?.Trim() ?? string.Empty;
            return text.StartsWith("NG", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateOverallLeakPressureDisplay()
        {
            var values = new List<double>(3);
            AddLeakValue(values, IsChannel1Enabled, Channel1LeakPressure);
            AddLeakValue(values, IsChannel2Enabled, Channel2LeakPressure);
            AddLeakValue(values, IsChannel3Enabled, Channel3LeakPressure);

            string display = values.Count == 0
                ? "---"
                : values.Max().ToString("F1", CultureInfo.InvariantCulture);

            if (!string.Equals(OverallLeakPressureDisplay, display, StringComparison.Ordinal))
                OverallLeakPressureDisplay = display;
        }

        private static void AddLeakValue(ICollection<double> values, bool enabled, string text)
        {
            if (!enabled)
                return;

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                values.Add(Math.Max(0, value));
        }

        private void NotifyOverlayLeakDisplayProperties()
        {
            OnPropertyChanged(nameof(OverlayChannel1LeakDisplay));
            OnPropertyChanged(nameof(OverlayChannel2LeakDisplay));
            OnPropertyChanged(nameof(OverlayChannel3LeakDisplay));
            OnPropertyChanged(nameof(OverlayChannel1Visibility));
            OnPropertyChanged(nameof(OverlayChannel2Visibility));
            OnPropertyChanged(nameof(OverlayChannel3Visibility));
        }

        private void UpdateRealtimeLeakPreview(int channel, double readPressure)
        {
            string inputText = channel switch
            {
                1 => Channel1InputPressure,
                2 => Channel2InputPressure,
                3 => Channel3InputPressure,
                _ => "---"
            };

            if (!double.TryParse(inputText, NumberStyles.Any, CultureInfo.InvariantCulture, out double inputPressure))
                return;

            string leak = Math.Max(0, inputPressure - readPressure)
                .ToString("F1", CultureInfo.InvariantCulture);

            switch (channel)
            {
                case 1:
                    if (IsChannel1Enabled) Channel1LeakPressure = leak;
                    break;
                case 2:
                    if (IsChannel2Enabled) Channel2LeakPressure = leak;
                    break;
                case 3:
                    if (IsChannel3Enabled) Channel3LeakPressure = leak;
                    break;
            }
        }


        private bool CanStartAirPressTest()
            => IsAirPressTestEnabled &&
               IsAirPressTestConnected &&
               !IsTesting &&
               (IsChannel1Enabled || IsChannel2Enabled || IsChannel3Enabled);

        private void NotifyAptStartCommandState()
        {
            StartAirPressTestCommand?.NotifyCanExecuteChanged();
            ManualStartAirPressTestCommand?.NotifyCanExecuteChanged();
        }

        partial void OnIsAirPressTestEnabledChanged(bool value) => NotifyAptStartCommandState();
        partial void OnIsAirPressTestConnectedChanged(bool value) => NotifyAptStartCommandState();
        partial void OnIsTestingChanged(bool value)
        {
            NotifyAptStartCommandState();
            UpdateOverallPressureState();
        }
        partial void OnIsChannel1EnabledChanged(bool value)
        {
            NotifyAptStartCommandState();
            UpdateOverallPressureState();
            UpdateOverallLeakPressureDisplay();
            NotifyOverlayLeakDisplayProperties();
        }
        partial void OnIsChannel2EnabledChanged(bool value)
        {
            NotifyAptStartCommandState();
            UpdateOverallPressureState();
            UpdateOverallLeakPressureDisplay();
            NotifyOverlayLeakDisplayProperties();
        }
        partial void OnIsChannel3EnabledChanged(bool value)
        {
            NotifyAptStartCommandState();
            UpdateOverallPressureState();
            UpdateOverallLeakPressureDisplay();
            NotifyOverlayLeakDisplayProperties();
        }
        partial void OnChannel1TestResultChanged(string value)
        {
            UpdateOverallPressureState();
            NotifyChannelResultBrushes(1);
        }

        partial void OnChannel2TestResultChanged(string value)
        {
            UpdateOverallPressureState();
            NotifyChannelResultBrushes(2);
        }

        partial void OnChannel3TestResultChanged(string value)
        {
            UpdateOverallPressureState();
            NotifyChannelResultBrushes(3);
        }

        partial void OnChannel1LeakPressureChanged(string value)
        {
            UpdateOverallLeakPressureDisplay();
            OnPropertyChanged(nameof(OverlayChannel1LeakDisplay));
        }

        partial void OnChannel2LeakPressureChanged(string value)
        {
            UpdateOverallLeakPressureDisplay();
            OnPropertyChanged(nameof(OverlayChannel2LeakDisplay));
        }

        partial void OnChannel3LeakPressureChanged(string value)
        {
            UpdateOverallLeakPressureDisplay();
            OnPropertyChanged(nameof(OverlayChannel3LeakDisplay));
        }

        private void MarkEnabledChannelsFail(string resultText)
        {
            Channel1TestResult = IsChannel1Enabled ? resultText : "---";
            Channel2TestResult = IsChannel2Enabled ? resultText : "---";
            Channel3TestResult = IsChannel3Enabled ? resultText : "---";
        }

        private void SetOverallPressureResult(bool passed)
        {
            SetOverallPressureState(passed ? "PASS" : "NG", passed ? "ÁP SUẤT OK" : "ÁP SUẤT NG");
        }

        private async Task StartAirPressTestAsync()
        {
            if (!CanStartAirPressTest())
            {
                SetStatus("APT: Hãy bật APT, kết nối COM và chọn ít nhất một kênh");
                return;
            }

            SerialPort? port = _airPressTestPort;
            if (port == null || !port.IsOpen)
            {
                IsAirPressTestConnected = false;
                SetStatus("Lỗi APT: COM kiểm tra chưa kết nối");
                return;
            }

            RaiseAirTime = Math.Clamp(RaiseAirTime, 1, 300_000);
            HoldAirTime = Math.Clamp(HoldAirTime, 1, 300_000);
            MinTestPressure = Math.Max(0.1, MinTestPressure);
            MaxLeakPressure = Math.Max(0, MaxLeakPressure);

            _airPressTestCts?.Cancel();
            _airPressTestCts?.Dispose();
            _airPressTestCts = new CancellationTokenSource();
            CancellationToken token = _airPressTestCts.Token;

            IsTesting = true;
            CanScan = false;
            _scanTimer.Stop();
            SetOverallPressureState("TESTING", "ĐANG KIỂM TRA");
            ResetTestDisplay();

            string command = $":TEST,{(IsChannel1Enabled ? 1 : 0)},{(IsChannel2Enabled ? 1 : 0)},{(IsChannel3Enabled ? 1 : 0)},0,{RaiseAirTime},{HoldAirTime}\r\n";
            SetStatus($"APT: Gá»­i lá»‡nh {command.Trim()}");

            try
            {
                port.DiscardInBuffer();
                port.Write(command);

                var buffer = new StringBuilder();
                DateTime startedUtc = DateTime.UtcNow;
                bool hasRealtimePressure = false;
                double maxWaitSeconds = Math.Max(10.0, (RaiseAirTime + HoldAirTime) / 1000.0 + 10.0);
                UpdatePressureRampPreview(0);

                while ((DateTime.UtcNow - startedUtc).TotalSeconds < maxWaitSeconds)
                {
                    token.ThrowIfCancellationRequested();

                    if (!hasRealtimePressure)
                    {
                        double elapsedMs = (DateTime.UtcNow - startedUtc).TotalMilliseconds;
                        UpdatePressureRampPreview(Math.Clamp(elapsedMs / Math.Max(1.0, RaiseAirTime), 0, 1));
                    }

                    string chunk = port.ReadExisting();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        buffer.Append(chunk);
                        foreach (string line in ExtractCompleteAptLines(buffer))
                        {
                            string cleanLine = line.Trim();
                            if (cleanLine.Length == 0)
                                continue;

                            if (ProcessRealtimeData(cleanLine))
                                hasRealtimePressure = true;

                            if (!cleanLine.StartsWith(":RESULT", StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (!TryProcessTestResult(cleanLine, out bool allPassed))
                            {
                                MarkEnabledChannelsFail("NG (DATA)");
                                SetOverallPressureResult(false);
                                SetStatus("APT: Dữ liệu kết quả không hợp lệ");
                                return;
                            }

                            SetOverallPressureResult(allPassed);
                            SetStatus(allPassed ? "APT: PASS - tất cả kênh đạt" : "APT: NG - có kênh không đạt");
                            if (allPassed && RelayOutputHoldTime > 0)
                                _ = HandleRelayOutputAsync();
                            return;
                        }

                        // Một số máy gửi :RESULT không có ký tự xuống dòng.
                        string pending = buffer.ToString().Trim();
                        if (pending.StartsWith(":RESULT", StringComparison.OrdinalIgnoreCase) && pending.Split(',').Length >= 6)
                        {
                            buffer.Clear();
                            if (!TryProcessTestResult(pending, out bool allPassed))
                            {
                                MarkEnabledChannelsFail("NG (DATA)");
                                SetOverallPressureResult(false);
                                SetStatus("APT: Dữ liệu kết quả không hợp lệ");
                                return;
                            }

                            SetOverallPressureResult(allPassed);
                            SetStatus(allPassed ? "APT: PASS - tất cả kênh đạt" : "APT: NG - có kênh không đạt");
                            if (allPassed && RelayOutputHoldTime > 0)
                                _ = HandleRelayOutputAsync();
                            return;
                        }
                    }

                    await Task.Delay(50, token);
                }

                MarkEnabledChannelsFail("NG (TIMEOUT)");
                SetOverallPressureResult(false);
                SetStatus($"Lỗi APT: Quá thời gian {maxWaitSeconds:F0} giây chờ kết quả");
            }
            catch (OperationCanceledException)
            {
                if (!_isShuttingDown)
                    SetStatus("APT: Bài kiểm tra đã bị hủy");
            }
            catch (Exception ex)
            {
                MarkEnabledChannelsFail("NG (ERROR)");
                SetOverallPressureResult(false);
                SetStatus($"Lá»—i APT: {ex.Message.Split('\r', '\n')[0]}");
            }
            finally
            {
                IsTesting = false;
                ResumeTemplateScanIfNeeded();
            }
        }

        private static IEnumerable<string> ExtractCompleteAptLines(StringBuilder buffer)
        {
            string text = buffer.ToString();
            int start = 0;
            var lines = new List<string>();

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\r' && text[i] != '\n')
                    continue;

                if (i > start)
                    lines.Add(text[start..i]);

                while (i + 1 < text.Length && (text[i + 1] == '\r' || text[i + 1] == '\n'))
                    i++;
                start = i + 1;
            }

            if (start > 0)
            {
                buffer.Clear();
                if (start < text.Length)
                    buffer.Append(text[start..]);
            }

            return lines;
        }

        private void ResetTestDisplay()
        {
            Channel1InputPressure = Channel2InputPressure = Channel3InputPressure = "---";
            Channel1ReadPressure = Channel2ReadPressure = Channel3ReadPressure = "---";
            Channel1LeakPressure = Channel2LeakPressure = Channel3LeakPressure = "---";
            Channel1TestResult = Channel2TestResult = Channel3TestResult = string.Empty;
            UpdateOverallLeakPressureDisplay();
        }

        private bool ProcessRealtimeData(string resultString)
        {
            bool isPress = resultString.StartsWith(":PRESS", StringComparison.OrdinalIgnoreCase);
            bool isWait = resultString.StartsWith(":WAIT", StringComparison.OrdinalIgnoreCase);
            if (!isPress && !isWait)
                return false;

            int prefixLength = isPress ? ":PRESS".Length : ":WAIT".Length;
            string[] data = resultString[prefixLength..]
                .Trim()
                .TrimStart(',')
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (data.Length < 3)
                return false;

            var values = new double[3];
            for (int i = 0; i < values.Length; i++)
            {
                if (!double.TryParse(data[i], NumberStyles.Any, CultureInfo.InvariantCulture, out values[i]))
                    return false;
                values[i] = Math.Abs(values[i]);
            }

            string p1 = values[0].ToString("F1", CultureInfo.InvariantCulture);
            string p2 = values[1].ToString("F1", CultureInfo.InvariantCulture);
            string p3 = values[2].ToString("F1", CultureInfo.InvariantCulture);

            if (isPress)
            {
                if (IsChannel1Enabled) Channel1InputPressure = p1;
                if (IsChannel2Enabled) Channel2InputPressure = p2;
                if (IsChannel3Enabled) Channel3InputPressure = p3;
                return true;
            }

            if (IsChannel1Enabled)
            {
                Channel1ReadPressure = p1;
                UpdateRealtimeLeakPreview(1, values[0]);
            }
            if (IsChannel2Enabled)
            {
                Channel2ReadPressure = p2;
                UpdateRealtimeLeakPreview(2, values[1]);
            }
            if (IsChannel3Enabled)
            {
                Channel3ReadPressure = p3;
                UpdateRealtimeLeakPreview(3, values[2]);
            }
            return false;
        }

        private void UpdatePressureRampPreview(double progress)
        {
            string value = (Math.Max(0, MinTestPressure) * Math.Clamp(progress, 0, 1))
                .ToString("F1", CultureInfo.InvariantCulture);

            if (IsChannel1Enabled) Channel1InputPressure = value;
            if (IsChannel2Enabled) Channel2InputPressure = value;
            if (IsChannel3Enabled) Channel3InputPressure = value;
        }
    }
}


