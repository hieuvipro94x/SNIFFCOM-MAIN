using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SniffCom.ulti;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SniffCom
{
    public partial class MainViewModel : ObservableObject
    {
        private const int MinTemplateScanIntervalMs = 300;
        private const string DefaultTriggerImageTemplatePath = @"trigger\img\template.png";
        private readonly DispatcherTimer _scanTimer;
        private bool _imageTriggerLatched;
        private bool _scanTickBusy;
        private bool _isTemplateReady;

        [ObservableProperty] private bool _isTriggerOnTextMatch;
        [ObservableProperty] private bool _isTriggerOnImgMatch;
        [ObservableProperty] private string _triggerText = string.Empty;
        [ObservableProperty] private int _triggerImageROIX;
        [ObservableProperty] private int _triggerImageROIY;
        [ObservableProperty] private int _triggerImageROIW = 100;
        [ObservableProperty] private int _triggerImageROIH = 100;
        [ObservableProperty] private string _triggerImageTemplatePath = DefaultTriggerImageTemplatePath;
        [ObservableProperty] private BitmapSource? _triggerImagePreviewSource;

        public IRelayCommand SelectTriggerImageROICommand { get; }
        public IRelayCommand SelectTriggerImageTEMPLATECommand { get; }

        private static int NormalizeScanTemplateInterval(int value)
            => Math.Max(MinTemplateScanIntervalMs, value);

        private void LoadTemplateImage()
        {
            if (!TryResolveTemplateImagePath(out string fullPath))
            {
                _isTemplateReady = false;
                TriggerImagePreviewSource = null;
                SetStatus($"Cảnh báo: Không tìm thấy ảnh mẫu PNG tại {ResolveTemplatePath()}");
                return;
            }

            try
            {
                ImageMatcher.SetTemplatePath(fullPath);

                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var decoder = new BitmapImage();
                decoder.BeginInit();
                decoder.CacheOption = BitmapCacheOption.OnLoad;
                decoder.StreamSource = stream;
                decoder.EndInit();
                decoder.Freeze();
                TriggerImagePreviewSource = decoder;

                if (!ImageMatcher.LoadTemplate())
                    throw new InvalidDataException("OpenCV không đọc được ảnh mẫu PNG.");

                _isTemplateReady = true;
                UpdateStoredTemplatePath(fullPath);
                SetStatus($"Đã tải ảnh mẫu kích hoạt: {Path.GetFileName(fullPath)}");
            }
            catch (Exception ex)
            {
                _isTemplateReady = false;
                TriggerImagePreviewSource = null;
                SetStatus($"Không thể tải ảnh mẫu: {ex.Message.Split('\r', '\n')[0]}");
            }
        }

        private string ResolveTemplatePath()
            => ImageMatcher.ResolveTemplatePath(TriggerImageTemplatePath);

        private bool TryResolveTemplateImagePath(out string fullPath)
            => ImageMatcher.TryResolveExistingTemplatePath(TriggerImageTemplatePath, out fullPath);

        private void UpdateStoredTemplatePath(string fullPath)
        {
            string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedFullPath = Path.GetFullPath(fullPath);

            string value = normalizedFullPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase)
                ? Path.GetRelativePath(baseDirectory, normalizedFullPath)
                : normalizedFullPath;

            if (!string.Equals(TriggerImageTemplatePath, value, StringComparison.Ordinal))
                TriggerImageTemplatePath = value;
        }

        private void SelectTriggerImageTemplate()
        {
            _ = TestTriggerImageTemplateAsync();
        }

        private async Task TestTriggerImageTemplateAsync()
        {
            try
            {
                if (!_isTemplateReady)
                    LoadTemplateImage();
                if (!_isTemplateReady)
                    return;

                var result = await Task.Run(() => ImageMatcher.FindTemplate(
                    roiX: TriggerImageROIX,
                    roiY: TriggerImageROIY,
                    roiW: TriggerImageROIW,
                    roiH: TriggerImageROIH));

                SetStatus(result.IsFound
                    ? $"Kiểm tra ảnh ĐẠT: {result.mess}"
                    : $"Kiểm tra ảnh KHÔNG ĐẠT: {result.mess}");
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi kiểm tra ảnh: {ex.Message.Split('\r', '\n')[0]}");
            }
        }

        private async Task SelectTriggerImageROIAsync()
        {
            try
            {
                if (!TryResolveTemplateImagePath(out string fullPath))
                {
                    TriggerImagePreviewSource = null;
                    SetStatus($"Không thể quét ảnh: hãy đặt ảnh mẫu PNG tại {ResolveTemplatePath()}");
                    return;
                }

                UpdateStoredTemplatePath(fullPath);
                LoadTemplateImage();
                if (!_isTemplateReady)
                {
                    SetStatus("Ảnh mẫu không hợp lệ hoặc không đọc được");
                    return;
                }

                var result = await Task.Run(() => ImageMatcher.FindTemplate());
                if (!result.IsFound)
                {
                    SetStatus($"Không tìm thấy ảnh mẫu: {result.mess}");
                    return;
                }

                // Gi? nguy?n t?a ?? ?m ?? h? tr? m?n h?nh ph? n?m b?n tr?i/ph?a tr?n m?n h?nh ch?nh.
                TriggerImageROIX = result.X - 5;
                TriggerImageROIY = result.Y - 5;
                TriggerImageROIW = Math.Max(1, result.W + 10);
                TriggerImageROIH = Math.Max(1, result.H + 10);
                _imageTriggerLatched = false;

                IsTriggerOnImgMatch = true;
                IsEnableScanTemplate = true;
                ApplyTemplateScannerState();

                SetStatus($"Đã bật quét ảnh tại X={TriggerImageROIX}, Y={TriggerImageROIY}, W={TriggerImageROIW}, H={TriggerImageROIH}");
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi quét ảnh mẫu: {ex.Message.Split('\r', '\n')[0]}");
            }
        }

        partial void OnIsEnableScanTemplateChanged(bool value)
        {
            if (_scanTimer == null)
                return;

            _imageTriggerLatched = false;
            ApplyTemplateScannerState();
        }

        partial void OnIsTriggerOnImgMatchChanged(bool value)
        {
            if (_scanTimer == null)
                return;

            _imageTriggerLatched = false;
            ApplyTemplateScannerState();
        }

        partial void OnTriggerImageTemplatePathChanged(string value)
        {
            if (_scanTimer == null || _isLoadingSettings)
                return;

            _imageTriggerLatched = false;
            LoadTemplateImage();
            ApplyTemplateScannerState();
        }

        partial void OnScanTemplateIntervalTimeChanged(int value)
        {
            if (_scanTimer == null)
                return;

            int normalized = NormalizeScanTemplateInterval(value);
            if (value != normalized)
            {
                ScanTemplateIntervalTime = normalized;
                return;
            }

            _scanTimer.Interval = TimeSpan.FromMilliseconds(normalized);
        }

        private void ApplyTemplateScannerState()
        {
            if (_scanTimer == null)
                return;

            _scanTimer.Interval = TimeSpan.FromMilliseconds(NormalizeScanTemplateInterval(ScanTemplateIntervalTime));
            CanScan = IsEnableScanTemplate && IsTriggerOnImgMatch && _isTemplateReady;

            if (CanScan)
            {
                _scanTimer.Start();
                if (OverallPressureState != "PASS" && OverallPressureState != "NG" && OverallPressureState != "TESTING")
                {
                    OverallPressureState = "PENDING";
                    OverallPressureDisplayText = "CHỜ KIỂM TRA";
                }
                SetStatus($"Đã bật quét ảnh, chu kỳ {_scanTimer.Interval.TotalMilliseconds:F0} ms");
            }
            else
            {
                _scanTimer.Stop();
                if (OverallPressureState != "PASS" && OverallPressureState != "NG" && OverallPressureState != "TESTING")
                {
                    OverallPressureState = "PENDING";
                    OverallPressureDisplayText = "CHỜ KIỂM TRA";
                }
                SetStatus("Đã tắt quét ảnh");
            }
        }

        private void ResumeTemplateScanIfNeeded()
        {
            if (_scanTimer == null || _isShuttingDown)
                return;

            CanScan = IsEnableScanTemplate && IsTriggerOnImgMatch && _isTemplateReady;
            if (CanScan)
                _scanTimer.Start();
        }

        private async void ScanTimer_Tick(object? sender, EventArgs e)
        {
            if (_scanTickBusy || !CanScan || IsTesting || _isShuttingDown)
                return;

            if (!CanStartAirPressTest())
                return;

            _scanTickBusy = true;
            try
            {
                var result = await Task.Run(() => ImageMatcher.FindTemplate(
                    threshold: 0.85,
                    roiX: TriggerImageROIX,
                    roiY: TriggerImageROIY,
                    roiW: TriggerImageROIW,
                    roiH: TriggerImageROIH));

                if (!result.IsFound)
                {
                    _imageTriggerLatched = false;
                    return;
                }

                if (_imageTriggerLatched || !IsTriggerOnImgMatch || !CanStartAirPressTest())
                    return;

                _imageTriggerLatched = true;
                await StartAirPressTestAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi quét ảnh: {ex.Message.Split('\r', '\n')[0]}");
            }
            finally
            {
                _scanTickBusy = false;
                ResumeTemplateScanIfNeeded();
            }
        }
    }
}
