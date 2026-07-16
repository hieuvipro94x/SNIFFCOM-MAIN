using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO.Ports;
using System.Threading;

namespace SniffCom
{
    public partial class MainViewModel : ObservableObject
    {
        public const string NativeRelayDriver = "Relay USB dùng DLL (BITFT)";
        public const string Ch340RelayDriver = "Relay nối tiếp CH340";
        public const string DefaultCh340OnCommandHex = "A0 01 01 A2";
        public const string DefaultCh340OffCommandHex = "A0 01 00 A1";
        public const string LegacyTwoChannelOnCommandHex = "A0 01 01 A2 A0 02 01 A3";
        public const string LegacyTwoChannelOffCommandHex = "A0 01 00 A1 A0 02 00 A2";

        private readonly SemaphoreSlim _relayOperationLock = new(1, 1);
        private int _relayHandle;
        private bool _isRelayInit;
        private const string TargetRelaySerial = "BITFT";

        public IReadOnlyList<string> RelayDriverOptions { get; } =
            new[] { NativeRelayDriver, Ch340RelayDriver };

        [ObservableProperty] private string _selectedRelayDriver = NativeRelayDriver;
        [ObservableProperty] private string? _selectedCh340RelayPort;
        [ObservableProperty] private int _ch340RelayBaudRate = 9600;
        [ObservableProperty] private string _ch340RelayOnCommandHex = DefaultCh340OnCommandHex;
        [ObservableProperty] private string _ch340RelayOffCommandHex = DefaultCh340OffCommandHex;
        [ObservableProperty] private string _relayStatus = "Sẵn sàng";

        public bool IsCh340RelaySelected => SelectedRelayDriver == Ch340RelayDriver;
        public IRelayCommand ManualRelayTestCommand { get; private set; }

        partial void OnSelectedRelayDriverChanged(string value)
        {
            OnPropertyChanged(nameof(IsCh340RelaySelected));
            RelayStatus = value == Ch340RelayDriver
                ? "Cần cấu hình relay CH340"
                : "Relay dùng DLL đã sẵn sàng";
        }

        public async Task HandleRelayOutputAsync()
        {
            if (!await _relayOperationLock.WaitAsync(0))
            {
                SetRelayStatus("Relay đang hoạt động, vui lòng chờ");
                return;
            }

            try
            {
                RelayOutputHoldTime = Math.Clamp(RelayOutputHoldTime, 1, 60_000);
                if (IsCh340RelaySelected)
                    await HandleCh340RelayOutputAsync();
                else
                    await HandleNativeRelayOutputAsync();
            }
            catch (Exception ex)
            {
                SetRelayStatus($"Lỗi relay: {ex.Message.Split('\r', '\n')[0]}");
            }
            finally
            {
                _relayOperationLock.Release();
            }
        }

        private async Task HandleCh340RelayOutputAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedCh340RelayPort))
            {
                SetRelayStatus("Lỗi CH340: Chưa chọn cổng COM");
                return;
            }

            byte[] onCommand;
            byte[] offCommand;
            try
            {
                onCommand = ParseHexCommand(Ch340RelayOnCommandHex);
                offCommand = ParseHexCommand(Ch340RelayOffCommandHex);
            }
            catch (FormatException ex)
            {
                SetRelayStatus($"Lệnh CH340 không hợp lệ: {ex.Message}");
                return;
            }

            using var port = new SerialPort(
                SelectedCh340RelayPort,
                Ch340RelayBaudRate,
                Parity.None,
                8,
                StopBits.One)
            {
                WriteTimeout = 1000,
                ReadTimeout = 500
            };

            bool relayIsOn = false;
            try
            {
                port.Open();
                port.Write(onCommand, 0, onCommand.Length);
                relayIsOn = true;
                SetRelayStatus($"CH340 đang BẬT trong {RelayOutputHoldTime} ms");
                await Task.Delay(RelayOutputHoldTime);
                port.Write(offCommand, 0, offCommand.Length);
                relayIsOn = false;
                SetRelayStatus("Đã tắt relay CH340");
            }
            finally
            {
                if (relayIsOn && port.IsOpen)
                {
                    try { port.Write(offCommand, 0, offCommand.Length); }
                    catch { }
                }
            }
        }

        private async Task HandleNativeRelayOutputAsync()
        {
            if (!EnsureNativeRelayReady())
                return;

            bool channel1On = false;
            bool channel2On = false;
            try
            {
                int open1 = UsbRelayNative.usb_relay_device_open_one_relay_channel(_relayHandle, 1);
                channel1On = open1 == 0;
                if (!channel1On)
                {
                    SetRelayStatus($"Không thể BẬT relay kênh 1: mã {open1}");
                    return;
                }

                int open2 = UsbRelayNative.usb_relay_device_open_one_relay_channel(_relayHandle, 2);
                channel2On = open2 == 0;
                if (!channel2On)
                {
                    SetRelayStatus($"Không thể BẬT relay kênh 2: mã {open2}");
                    return;
                }

                SetRelayStatus($"Relay dùng DLL đang BẬT trong {RelayOutputHoldTime} ms");
                await Task.Delay(RelayOutputHoldTime);
            }
            finally
            {
                int close1 = 0;
                int close2 = 0;

                if (_relayHandle > 0 && channel2On)
                    close2 = UsbRelayNative.usb_relay_device_close_one_relay_channel(_relayHandle, 2);
                if (_relayHandle > 0 && channel1On)
                    close1 = UsbRelayNative.usb_relay_device_close_one_relay_channel(_relayHandle, 1);

                if (channel1On || channel2On)
                {
                    SetRelayStatus(close1 == 0 && close2 == 0
                        ? "Đã tắt relay dùng DLL"
                        : $"Cảnh báo: Tắt relay lỗi K1={close1}, K2={close2}");
                }
            }
        }

        private bool EnsureNativeRelayReady()
        {
            if (!_isRelayInit)
            {
                int initResult = UsbRelayNative.usb_relay_init();
                if (initResult != 0)
                {
                    SetRelayStatus($"Không thể khởi tạo relay dùng DLL ({initResult})");
                    return false;
                }
                _isRelayInit = true;
            }

            if (_relayHandle <= 0)
            {
                _relayHandle = UsbRelayNative.usb_relay_device_open_with_serial_number(
                    TargetRelaySerial,
                    TargetRelaySerial.Length);

                if (_relayHandle <= 0)
                {
                    SetRelayStatus("Không tìm thấy relay dùng DLL; kiểm tra thiết bị và bản x86");
                    return false;
                }
            }

            return true;
        }

        private void ReleaseRelayResources()
        {
            try
            {
                if (_relayHandle > 0)
                {
                    UsbRelayNative.usb_relay_device_close_all_relay_channel(_relayHandle);
                    UsbRelayNative.usb_relay_device_close(_relayHandle);
                }
            }
            catch
            {
                // Giải phóng thiết bị theo kiểu best effort.
            }
            finally
            {
                _relayHandle = 0;
            }

            try
            {
                if (_isRelayInit)
                    UsbRelayNative.usb_relay_exit();
            }
            catch
            {
            }
            finally
            {
                _isRelayInit = false;
            }
        }

        private static byte[] ParseHexCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new FormatException("Chuỗi lệnh đang trống");

            string[] parts = command.Split(
                new[] { ' ', ',', '-', ';', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            var bytes = new byte[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? parts[i][2..]
                    : parts[i];

                if (value.Length is < 1 or > 2 ||
                    !byte.TryParse(
                        value,
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out bytes[i]))
                    throw new FormatException($"'{parts[i]}' không phải byte HEX hợp lệ");
            }

            return bytes;
        }

        private void SetRelayStatus(string message)
        {
            RelayStatus = message;
            SetStatus(message);
        }

        private async Task ManualRelayTestAsync()
        {
            SetRelayStatus("Đang bắt đầu kiểm tra relay...");
            await HandleRelayOutputAsync();
        }
    }
}
