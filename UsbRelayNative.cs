using System.Runtime.InteropServices;

namespace SniffCom
{
    public static class UsbRelayNative
    {
        private const string DllName = "usb_relay_device.dll";

        public enum UsbRelayDeviceType
        {
            OneChannel = 1,
            TwoChannel = 2,
            FourChannel = 4,
            EightChannel = 8
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UsbRelayDeviceInfo
        {
            public nint serial_number; 
            public nint device_path;
            public UsbRelayDeviceType type;
            public nint next;
        }

        // --- Function imports ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int usb_relay_init();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int usb_relay_exit();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern nint usb_relay_device_enumerate();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void usb_relay_device_free_enumerate(nint info);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int usb_relay_device_open_with_serial_number(
            [MarshalAs(UnmanagedType.LPStr)] string serial_number,
            int len);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int usb_relay_device_open(nint device_info);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void usb_relay_device_close(int hHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int usb_relay_device_open_one_relay_channel(int hHandle, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int usb_relay_device_open_all_relay_channel(int hHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int usb_relay_device_close_one_relay_channel(int hHandle, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int usb_relay_device_close_all_relay_channel(int hHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int usb_relay_device_get_status(int hHandle, out uint status);
    }
}
