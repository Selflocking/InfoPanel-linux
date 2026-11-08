namespace InfoPanel.VmaxPanel;

/// <summary>MCVKC03/MS912C protocol, verified on the VK03/VN03M panel.
/// Wire reference: https://github.com/codehz/msdisplay/tree/main/ms2160
/// The private VIC A0 and BGR888 format are specific to this panel.
/// </summary>
public sealed class Vk03Protocol(Action<byte[]> setFeature, Func<byte[]> getFeature)
{
    public const int Width = 360, Height = 960;
    public const int PixelDataLength = Width * Height * 3;
    private byte _frameId = 1;
    private bool _firstFrameSent;

    public byte Read(ushort address)
    {
        setFeature([0xB5, (byte)(address >> 8), (byte)address]);
        return getFeature()[4]; // hidraw includes the leading report-number byte
    }

    private void Write(ushort address, byte value) =>
        setFeature([0xB6, (byte)(address >> 8), (byte)address, value]);

    private void Command(byte[] payload, ushort status)
    {
        setFeature(payload);
        Thread.Sleep(payload[1] == 7 ? 100 : 20);
        var deadline = Environment.TickCount64 + 1500;
        while (Read(status) != 0)
        {
            if (Environment.TickCount64 >= deadline)
                throw new IOException($"VK03 command {payload[1]:X2} timed out.");
            Thread.Sleep(2);
        }
    }

    public void Initialize()
    {
        if (Read(0xF000) != 0xB7 || Read(0xF001) != 0x16 || Read(0xF002) != 0x0A || Read(0x31) != 6)
            throw new IOException("VK03 requires an MS912C chip on video port 6.");
        Command([0xA6, 3, 3], 0xC558);
        Write(0xDEEE, 1);
        Command([0xA6, 7, 1], 0xC454);
        Command([0xA6, 5, 0], 0xC555);
        Write(0xF005, (byte)(Read(0xF005) & ~0x10));
        Command([0xA6, 1, 1, 0x68, 3, 0xC0, 0x11], 0xC555);
        Command([0xA6, 2, 0xA0, 0, 1, 0x68, 3, 0xC0], 0xC557);
        Command([0xA6, 4, 1], 0xC555);
        _firstFrameSent = false;
    }

    // Call after the complete bulk frame and its zero-length packet.
    public void CommitFrame()
    {
        setFeature([0xA6, 0, _frameId, 100]);
        _frameId ^= 1;
        if (!_firstFrameSent)
        {
            SetOutput(true);
            _firstFrameSent = true;
        }
    }

    public void SetOutput(bool enabled)
    {
        Write(0xF005, (byte)(enabled ? Read(0xF005) | 0x10 : Read(0xF005) & ~0x10));
        Command([0xA6, 5, enabled ? (byte)1 : (byte)0], 0xC555);

    }

    public static byte[] PackFrame(byte[] pixels, int length, byte[]? destination = null)
    {
        if (length != PixelDataLength || pixels.Length < length)
            throw new ArgumentException("VK03 needs a complete 360x960 BGR888 frame.");
        var frame = destination ?? new byte[length + 16];
        if (frame.Length != length + 16) throw new ArgumentException("Invalid VK03 frame buffer size.");
        byte[] header = [0xFF, 0, 0, 0, 0, 0x16, 0x83, 0xC0];
        header.CopyTo(frame, 0);
        for (int i = 0; i < length; i++) frame[i + 8] = Math.Min(pixels[i], (byte)0xFE);
        frame.AsSpan(length + 8, 8).Clear();
        frame[length + 8] = 0xFF;
        frame[length + 9] = 0xC0;
        return frame;
    }

    public static void ConvertRgba(ReadOnlySpan<byte> rgba, int width, int height, int stride,
        Span<byte> output, int brightness)
    {
        if (stride < width * 4 || rgba.Length < (height - 1) * stride + width * 4 || output.Length < width * height * 3)
            throw new ArgumentException("Invalid RGBA buffer geometry.");
        int scale = Math.Clamp(brightness, 0, 100), target = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int offset = y * stride + x * 4;
                for (int c = 2; c >= 0; c--)
                    output[target++] = (byte)Math.Min(254, rgba[offset + c] * scale / 100);
            }
    }
}
