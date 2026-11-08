using System.Runtime.InteropServices;

namespace InfoPanel.Utils;

/// <summary>LibUsbDotNet requests the unversioned name, but Linux runtime packages
/// ship libusb-1.0.so.0 (the .so symlink belongs to the development package).</summary>
internal static class LibUsbNativeLibrary
{
    public static void Configure()
    {
        if (!OperatingSystem.IsLinux()) return;
        NativeLibrary.SetDllImportResolver(typeof(LibUsbDotNet.UsbDevice).Assembly, (name, assembly, searchPath) =>
            name == "libusb-1.0" && NativeLibrary.TryLoad("libusb-1.0.so.0", assembly, searchPath, out var handle)
                ? handle : IntPtr.Zero);
    }
}
