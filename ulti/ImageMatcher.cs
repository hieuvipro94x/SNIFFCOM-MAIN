using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.IO;
using SysDraw = System.Drawing;
using SysForms = System.Windows.Forms;

namespace SniffCom.ulti
{
    public static class ImageMatcher
    {
        private const string DefaultTemplateRelativePath = @"trigger\img\template.png";
        private static readonly object SyncRoot = new();
        private static Mat? _templateMat;
        private static string _templatePath = ResolveTemplatePath(DefaultTemplateRelativePath);
        private static int _templateWidth;
        private static int _templateHeight;

        public static string CurrentTemplatePath
        {
            get
            {
                lock (SyncRoot)
                    return _templatePath;
            }
        }

        public static void SetTemplatePath(string? templatePath)
        {
            lock (SyncRoot)
                _templatePath = ResolveTemplatePath(templatePath);
        }

        public static string ResolveTemplatePath(string? templatePath)
        {
            string requestedPath = string.IsNullOrWhiteSpace(templatePath)
                ? DefaultTemplateRelativePath
                : templatePath.Trim();

            string fullPath = Path.IsPathRooted(requestedPath)
                ? Path.GetFullPath(requestedPath)
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, requestedPath));

            return FindExistingFileIgnoringCase(fullPath) ?? fullPath;
        }

        public static bool TryResolveExistingTemplatePath(string? templatePath, out string fullPath)
        {
            fullPath = ResolveTemplatePath(templatePath);
            return File.Exists(fullPath) && IsPngPath(fullPath);
        }

        private static string? FindExistingFileIgnoringCase(string fullPath)
        {
            if (File.Exists(fullPath))
                return fullPath;

            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return null;

            string fileName = Path.GetFileName(fullPath);
            string? exactNameMatch = Directory.EnumerateFiles(directory)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));
            if (exactNameMatch != null)
                return exactNameMatch;

            string requestedNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string requestedExtension = Path.GetExtension(fileName);
            if (!string.Equals(requestedExtension, ".png", StringComparison.OrdinalIgnoreCase))
                return null;

            return Directory.EnumerateFiles(directory)
                .FirstOrDefault(path =>
                    string.Equals(Path.GetFileNameWithoutExtension(path), requestedNameWithoutExtension, StringComparison.OrdinalIgnoreCase) &&
                    IsPngPath(path));
        }

        private static bool IsPngPath(string path)
            => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);

        public static bool LoadTemplate()
        {
            lock (SyncRoot)
            {
                DisposeTemplateCore();

                try
                {
                    _templatePath = ResolveTemplatePath(_templatePath);
                    string? directory = Path.GetDirectoryName(_templatePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);

                    if (!File.Exists(_templatePath) || !IsPngPath(_templatePath))
                        return false;

                    using var sourceMat = Cv2.ImRead(_templatePath, ImreadModes.Color);
                    if (sourceMat.Empty())
                        return false;

                    _templateMat = new Mat();
                    Cv2.CvtColor(sourceMat, _templateMat, ColorConversionCodes.BGR2GRAY);
                    _templateWidth = _templateMat.Width;
                    _templateHeight = _templateMat.Height;
                    return _templateWidth > 0 && _templateHeight > 0;
                }
                catch
                {
                    DisposeTemplateCore();
                    return false;
                }
            }
        }

        public static (bool IsFound, string mess, int X, int Y, int W, int H) FindTemplate(
            double threshold = 0.85,
            int roiX = 0,
            int roiY = 0,
            int roiW = 0,
            int roiH = 0)
        {
            Mat templateSnapshot;
            int templateWidth;
            int templateHeight;

            lock (SyncRoot)
            {
                if (_templateMat == null || _templateMat.Empty() || _templateMat.Channels() != 1)
                    return (false, "Ảnh mẫu chưa được tải hoặc không hợp lệ.", 0, 0, 0, 0);

                templateSnapshot = _templateMat.Clone();
                templateWidth = _templateWidth;
                templateHeight = _templateHeight;
            }

            using (templateSnapshot)
            {
                SysDraw.Rectangle virtualScreen = SysForms.SystemInformation.VirtualScreen;
                bool useRoiCapture = roiW > 0 && roiH > 0;
                int captureX = virtualScreen.Left;
                int captureY = virtualScreen.Top;
                int captureWidth = virtualScreen.Width;
                int captureHeight = virtualScreen.Height;

                if (useRoiCapture)
                {
                    var requested = new SysDraw.Rectangle(roiX, roiY, roiW, roiH);
                    var clipped = SysDraw.Rectangle.Intersect(requested, virtualScreen);
                    if (clipped.Width <= 0 || clipped.Height <= 0)
                        return (false, "Vùng quét nằm ngoài phạm vi màn hình.", 0, 0, 0, 0);

                    captureX = clipped.X;
                    captureY = clipped.Y;
                    captureWidth = clipped.Width;
                    captureHeight = clipped.Height;
                }

                using SysDraw.Bitmap screenBitmap = useRoiCapture
                    ? ScreenCapture.CaptureWithRect(captureX, captureY, captureWidth, captureHeight)
                    : ScreenCapture.CaptureFullScreen();

                try
                {
                    using var sourceMat = BitmapConverter.ToMat(screenBitmap);
                    using var capturedMat = new Mat();
                    Cv2.CvtColor(sourceMat, capturedMat, ColorConversionCodes.BGR2GRAY);

                    if (capturedMat.Width < templateWidth || capturedMat.Height < templateHeight)
                        return (false, "Vùng quét nhỏ hơn ảnh mẫu.", 0, 0, 0, 0);

                    using var resultMat = new Mat();
                    Cv2.MatchTemplate(capturedMat, templateSnapshot, resultMat, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(resultMat, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                    if (maxVal < threshold)
                        return (false, $"Độ tương đồng {maxVal:P2}, yêu cầu tối thiểu {threshold:P2}.", 0, 0, 0, 0);

                    int finalX = maxLoc.X + captureX;
                    int finalY = maxLoc.Y + captureY;
                    return (
                        true,
                        $"T?m th?y t?i ({finalX}, {finalY}), Độ tương đồng {maxVal:P2}.",
                        finalX,
                        finalY,
                        templateWidth,
                        templateHeight);
                }
                catch (Exception ex)
                {
                    return (false, $"Lỗi OpenCV: {ex.Message}", 0, 0, 0, 0);
                }
            }
        }

        public static void DisposeTemplate()
        {
            lock (SyncRoot)
                DisposeTemplateCore();
        }

        private static void DisposeTemplateCore()
        {
            _templateMat?.Dispose();
            _templateMat = null;
            _templateWidth = 0;
            _templateHeight = 0;
        }
    }
}
