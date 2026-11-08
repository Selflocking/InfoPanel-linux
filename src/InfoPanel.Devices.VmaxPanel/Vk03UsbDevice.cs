using HidSharp;
using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace InfoPanel.VmaxPanel;

internal sealed class Vk03UsbDevice : IValkyriePanelDevice
{
    private UsbDevice? _usb;
    private IUsbDevice? _whole;
    private UsbEndpointWriter? _writer;
    private LinuxHidRawFeature? _hid;
    private Vk03Protocol? _protocol;
    private bool _claimed;
    private bool _screen = true;
    private readonly byte[] _frame = new byte[Vk03Protocol.PixelDataLength + 16];

    public static Vk03UsbDevice Open(string deviceId)
    {
        foreach (UsbRegistry entry in UsbDevice.AllDevices)
        {
            if (entry.Vid != 0x345F || entry.Pid != 0x9132) continue;
            var device = new Vk03UsbDevice();
            try
            {
                device._usb = entry.Device;
                if (device._usb == null) continue;
                var serial = device._usb.Info.SerialString;
                if (string.IsNullOrEmpty(serial) || deviceId != $@"USB\VID_345F&PID_9132\{serial}")
                {
                    device.Dispose();
                    continue;
                }
                var hid = DeviceList.Local.GetHidDevices(0x345F, 0x9132)
                    .FirstOrDefault(h => h.GetSerialNumber() == serial);
                if (hid == null || !VmaxPanelHelper.IsVk03Panel(Ms9132PanelProbe.ReadSize(hid.DevicePath), Ms9132PanelProbe.ReadPanelName(hid.DevicePath)))
                    throw new IOException("The selected device is not a confirmed VK03 360x960 panel.");
                device._hid = LinuxHidRawFeature.Open(hid.DevicePath)
                    ?? throw new IOException("Cannot open VK03 HID interface; check USB/HID permissions.");
                device._whole = device._usb as IUsbDevice
                    ?? throw new IOException("VK03 needs a libusb device.");
                if (!device._whole.ClaimInterface(3))
                    throw new IOException("Cannot claim VK03 video interface 3: " + UsbDevice.LastErrorString);
                device._claimed = true;
                device._writer = device._usb.OpenEndpointWriter(WriteEndpointID.Ep04);
                device._protocol = new Vk03Protocol(device._hid.SetFeature, device._hid.GetFeature);
                device._protocol.Initialize();
                return device;
            }
            catch { device.Dispose(); throw; }
        }
        throw new IOException("VK03 device not found.");
    }

    public void SendFrame(byte[] pixels, int length)
    {
        var frame = Vk03Protocol.PackFrame(pixels, length, _frame);
        for (int offset = 0; offset < frame.Length;)
        {
            int size = Math.Min(0x80000, frame.Length - offset);
            var result = _writer!.Write(frame, offset, size, 10000, out int written);
            if (result != ErrorCode.None || written <= 0)
                throw new IOException($"VK03 bulk transfer failed: {result} ({written}/{size}).");
            offset += written;
        }
        var zlp = _writer!.Write(Array.Empty<byte>(), 0, 0, 10000, out _);
        if (zlp != ErrorCode.None) throw new IOException("VK03 end-of-frame transfer failed: " + zlp);
        _protocol!.CommitFrame();
    }

    public void SetScreenSwitch(bool enabled)
    {
        if (_screen == enabled) return;
        _protocol!.SetOutput(enabled);
        _screen = enabled;
    }

    public void KeepAlive() { } // Frames (including cached static frames) are the keep-alive.

    public void Dispose()
    {
        _writer?.Dispose();
        if (_claimed) _whole?.ReleaseInterface(3);
        _hid?.Dispose();
        _usb?.Close();
    }
}
