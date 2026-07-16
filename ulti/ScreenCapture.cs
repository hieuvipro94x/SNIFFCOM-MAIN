// File: SniffCom.ulti/ScreenCapture.cs

using System.Drawing.Imaging;
using SysDraw = System.Drawing; // Alias cho System.Drawing
using SysForms = System.Windows.Forms; // Alias cho System.Windows.Forms

namespace SniffCom.ulti
{
    public static class ScreenCapture
    {
        public static SysDraw.Bitmap CaptureFullScreen()
        {
            int screenLeft = SysForms.SystemInformation.VirtualScreen.Left;
            int screenTop = SysForms.SystemInformation.VirtualScreen.Top;
            int screenWidth = SysForms.SystemInformation.VirtualScreen.Width;
            int screenHeight = SysForms.SystemInformation.VirtualScreen.Height;

            // 1. Tạo Bitmap trống
            var bmp = new SysDraw.Bitmap(screenWidth, screenHeight);

            // 2. Chụp ảnh vào Bitmap
            using (SysDraw.Graphics g = SysDraw.Graphics.FromImage(bmp))
            {
                // Chụp từ tọa độ Top/Left của màn hình ảo (screenLeft, screenTop)
                g.CopyFromScreen(screenLeft, screenTop, 0, 0, bmp.Size);
            }

            // Trả về đối tượng Bitmap (gọi hàm này phải chịu trách nhiệm dispose nó)
            return bmp;
        }

        /* // Bạn có thể giữ lại hàm cũ nếu cần để debug, nhưng cần xóa nó trong FindTemplateOnScreen
        public static void CaptureFullScreen(string filePath)
        {
            using(var bmp = CaptureFullScreen())
            {
                bmp.Save(filePath, SysDraw.Imaging.ImageFormat.Png);
            }
        }
        */

        /// <summary>
        /// Chụp một khu vực cụ thể trên màn hình (ROI) và trả về đối tượng Bitmap.
        /// </summary>
        /// <param name="x">Tọa độ X bắt đầu trên màn hình (pixel).</param>
        /// <param name="y">Tọa độ Y bắt đầu trên màn hình (pixel).</param>
        /// <param name="width">Chiều rộng của khu vực cần chụp (pixel).</param>
        /// <param name="height">Chiều cao của khu vực cần chụp (pixel).</param>
        /// <returns>Đối tượng System.Drawing.Bitmap chứa ảnh của khu vực đã chọn.
        /// Người gọi hàm chịu trách nhiệm Dispose đối tượng này.</returns>
        public static SysDraw.Bitmap CaptureWithRect(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Width và Height phải lớn hơn 0.");
            }

            // 1. Tạo Bitmap trống với kích thước của ROI
            var bmp = new SysDraw.Bitmap(width, height);

            // 2. Chụp ảnh vào Bitmap
            using (SysDraw.Graphics g = SysDraw.Graphics.FromImage(bmp))
            {
                // SourceX (x), SourceY (y): Tọa độ màn hình cần chụp
                // DestX (0), DestY (0): Tọa độ bắt đầu vẽ trên Bitmap mới
                // Size (width, height): Kích thước khu vực cần chụp
                g.CopyFromScreen(x, y, 0, 0, new SysDraw.Size(width, height));
            }

            // Trả về đối tượng Bitmap (cần được dispose bởi người gọi)
            return bmp;
        }
    }
}