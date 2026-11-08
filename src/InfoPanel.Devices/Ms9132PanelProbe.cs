using Serilog;
using System;

namespace InfoPanel;

/// <summary>Reads EDID geometry without programming or enabling the shared MS9132 bridge.</summary>
public static class Ms9132PanelProbe
{
    private static readonly ILogger Logger = Log.ForContext(typeof(Ms9132PanelProbe));

    /// <summary>EDID monitor-name descriptor; a shared size or USB ID is not a model identity.</summary>
    public static string? ReadPanelName(string hidPath)
    {
        try
        {
            using var raw = LinuxHidRawFeature.Open(hidPath);
            if (raw == null) return null;
            byte Read(int offset)
            {
                int address = 0xC000 + offset;
                raw.SetFeature([0xB5, (byte)(address >> 8), (byte)address, 0, 0, 0, 0, 0]);
                return raw.GetFeature()[4];
            }
            for (int descriptor = 54; descriptor <= 108; descriptor += 18)
            {
                if (Read(descriptor) != 0 || Read(descriptor + 1) != 0 || Read(descriptor + 2) != 0 || Read(descriptor + 3) != 0xFC)
                    continue;
                return System.Text.Encoding.ASCII.GetString(Enumerable.Range(descriptor + 5, 13).Select(Read).ToArray()).Trim(' ', '\n', '\r', '\0');
            }
        }
        catch (Exception ex) { Logger.Debug(ex, "Ms9132PanelProbe: panel-name read failed"); }
        return null;
    }

    public static (int Width, int Height)? ReadSize(string hidPath)
    {
        try
        {
            var raw = LinuxHidRawFeature.Open(hidPath);
            if (raw == null) return null;

            using (raw)
            {
                int ReadEdidByte(int offset)
                {
                    ushort address = (ushort)(0xC000 + offset);
                    raw.SetFeature([0xB5, (byte)(address >> 8), (byte)(address & 0xFF), 0, 0, 0, 0, 0]);
                    return raw.GetFeature()[4];
                }

                // EDID header sanity: 00 FF FF FF FF FF FF 00.
                if (ReadEdidByte(0) != 0x00 || ReadEdidByte(1) != 0xFF || ReadEdidByte(7) != 0x00)
                {
                    Logger.Debug("Ms9132PanelProbe: MS9132 EDID header mismatch");
                    return null;
                }

                // First detailed timing descriptor at offset 54. A zero pixel
                // clock means no DTD is present.
                if (ReadEdidByte(54) == 0 && ReadEdidByte(55) == 0) return null;

                int hactive = ReadEdidByte(56) | ((ReadEdidByte(58) & 0xF0) << 4);
                int vactive = ReadEdidByte(59) | ((ReadEdidByte(61) & 0xF0) << 4);
                Logger.Information("Ms9132PanelProbe: MS9132 panel EDID reports {W}x{H}", hactive, vactive);

                if (hactive <= 0 || vactive <= 0 || hactive > 4096 || vactive > 4096) return null;
                return (hactive, vactive);
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Ms9132PanelProbe: MS9132 EDID probe failed");
            return null;
        }
    }
}
