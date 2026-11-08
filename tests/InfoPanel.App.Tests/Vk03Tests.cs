using InfoPanel.Models;
using InfoPanel.Services;
using InfoPanel.VmaxPanel;
using Xunit;

namespace InfoPanel.App.Tests;

public class Vk03Tests
{
    [Fact]
    public void RealPanelGeometrySelectsVk03AndNeverTheVmaxInitializer()
    {
        var info = VmaxPanelHelper.ClassifyMs9132((360, 960));
        Assert.Equal(VmaxPanelModel.ValkyrieVk03, info!.Model);
        Assert.True(VmaxPanelHelper.IsVk03Panel((360, 960), "MCVKC03"));
        Assert.False(VmaxPanelHelper.IsVk03Panel((360, 960), "other"));
        Assert.False(VmaxPanelHelper.IsVk03Panel((320, 960), "MCVKC03"));
        Assert.True(info.UsesBgr888);
        Assert.True(info.IsValkyrie);
        Assert.Equal(5, new VmaxPanelDevice { Model = VmaxPanelModel.ValkyrieVk03, TargetFrameRate = 1 }.TargetFrameRate);
        Assert.True(VmaxPanelHelper.IsOtherMs9132Panel((360, 960)));
        Assert.Equal(VmaxPanelModel.Vmax46Inch, VmaxPanelHelper.ClassifyMs9132((320, 960))!.Model);
    }

    [Fact]
    public void FrameMatchesVerifiedCaptureSizeAndEscapesOnlyPixelBytes()
    {
        var pixels = Enumerable.Repeat((byte)255, Vk03Protocol.PixelDataLength).ToArray();
        var frame = Vk03Protocol.PackFrame(pixels, pixels.Length);
        Assert.Equal(1036816, frame.Length);
        Assert.Equal(new byte[] { 255, 0, 0, 0, 0, 0x16, 0x83, 0xc0 }, frame[..8]);
        Assert.All(frame[8..^8], b => Assert.Equal(254, b));
        Assert.Equal(new byte[] { 255, 192, 0, 0, 0, 0, 0, 0 }, frame[^8..]);
        var reused = Enumerable.Repeat((byte)123, frame.Length).ToArray();
        Assert.Same(reused, Vk03Protocol.PackFrame(pixels, pixels.Length, reused));
        Assert.Equal(frame, reused);
        Assert.Throws<ArgumentException>(() => Vk03Protocol.PackFrame(new byte[8], 8));
    }

    [Fact]
    public void RgbaConversionUsesBgrAndHonorsStrideAndBrightness()
    {
        byte[] rgba = [255, 100, 0, 255, 9, 9, 9, 9, 0, 50, 200, 255];
        var output = new byte[6];
        Vk03Protocol.ConvertRgba(rgba, 1, 2, 8, output, 50);
        Assert.Equal(new byte[] { 0, 50, 127, 100, 25, 0 }, output);
    }

    [Fact]
    public void InitializationUsesTestedTimingAndCommitsAlternatingFrameIds()
    {
        var registers = new Dictionary<int, byte> { [0xf000] = 0xb7, [0xf001] = 0x16, [0xf002] = 0x0a, [0x31] = 6, [0xf005] = 0xa3 };
        var sent = new List<byte[]>();
        int address = 0;
        var protocol = new Vk03Protocol(bytes =>
        {
            sent.Add(bytes.ToArray());
            if (bytes[0] == 0xb5) address = bytes[1] * 256 + bytes[2];
            if (bytes[0] == 0xb6) registers[bytes[1] * 256 + bytes[2]] = bytes[3];
        }, () => { var reply = new byte[9]; reply[4] = registers.GetValueOrDefault(address); return reply; });
        protocol.Initialize();
        Assert.Contains(sent, b => b.SequenceEqual(new byte[] { 0xa6, 1, 1, 0x68, 3, 0xc0, 0x11 }));
        Assert.Contains(sent, b => b.SequenceEqual(new byte[] { 0xa6, 2, 0xa0, 0, 1, 0x68, 3, 0xc0 }));
        Assert.Equal(0xa3, registers[0xf005]);
        protocol.CommitFrame();
        Assert.Equal(0xb3, registers[0xf005]);
        protocol.SetOutput(false);
        int before = sent.Count;
        protocol.CommitFrame();
        Assert.Equal(0xa3, registers[0xf005]); // static refresh must not turn a disabled screen back on
        Assert.Single(sent.Skip(before));
        var triggers = sent.Where(b => b[0] == 0xa6 && b[1] == 0).ToArray();
        Assert.Equal(new byte[] { 1, 0 }, triggers.Select(b => b[2]).ToArray());
    }

    [Fact]
    public void UnknownChipIsRejectedBeforeInitializationWrites()
    {
        var sent = new List<byte[]>();
        var protocol = new Vk03Protocol(b => sent.Add(b), () => new byte[9]);
        Assert.Throws<IOException>(protocol.Initialize);
        Assert.All(sent, b => Assert.Equal(0xb5, b[0]));
    }

    [Fact]
    public void DiscoveryMigratesMistakenVmaxWithoutLosingProfileOrHotkeys()
    {
        var settings = new Settings();
        var profile = Guid.NewGuid();
        settings.VmaxPanelDevices.Add(new VmaxPanelDevice { DeviceId = "old", ProfileGuid = profile, Brightness = 42, Enabled = false });
        settings.HotkeyBindings.Add(new HotkeyBinding { DeviceId = "old", DeviceType = "Vmax" });
        var found = new VmaxPanelDiscoveryInfo
        {
            Model = VmaxPanelModel.ValkyrieVk03, DeviceId = "serial-id", LegacyDeviceId = "old",
            ModelInfo = VmaxPanelModelDatabase.GetModelByPanelSize(360, 960),
        };
        PanelDiscoveryReconciler.Apply(settings, [], [found]);
        Assert.Empty(settings.VmaxPanelDevices);
        var device = Assert.Single(settings.ValkyriePanelDevices);
        Assert.Equal(profile, device.ProfileGuid);
        Assert.Equal(42, device.Brightness);
        Assert.Equal(5, device.TargetFrameRate);
        Assert.False(device.Enabled);
        Assert.Equal("serial-id", settings.HotkeyBindings[0].DeviceId);
        Assert.Equal("Valkyrie", settings.HotkeyBindings[0].DeviceType);
    }
}
