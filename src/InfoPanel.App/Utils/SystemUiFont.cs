using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Serilog;

namespace InfoPanel.Utils;

/// <summary>GNOME's Pango font setting, with Fontconfig fallback on other Linux desktops.</summary>
public sealed partial class SystemUiFont : IDisposable
{
    public sealed record FontSetting(string Family, double Size);
    private readonly Application _application;
    private Process? _monitor;
    private bool _disposed;
    private FontSetting? _current;

    public SystemUiFont(Application application)
    {
        _application = application;
        Apply(Read());
        if (!OperatingSystem.IsLinux()) return;
        try
        {
            _monitor = new Process { StartInfo = Command("gsettings", "monitor", "org.gnome.desktop.interface", "font-name") };
            _monitor.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null || _disposed) return;
                var font = Read();
                Dispatcher.UIThread.Post(() => { if (!_disposed) Apply(font); });
            };
            if (_monitor.Start()) _monitor.BeginOutputReadLine();
        }
        catch (Exception ex) { Log.Debug(ex, "System font change monitor unavailable"); }
    }

    private void Apply(FontSetting font)
    {
        if (_current == font) return;
        ApplyResources(_application, font);
        _current = font;
        Log.Information("Interface font: {Family}, {Size:0.##} DIP (system setting)", font.Family, font.Size);
    }

    public static void ApplyResources(Application application, FontSetting font)
    {
        application.Resources["ContentControlThemeFontFamily"] = new FontFamily(font.Family);
        application.Resources["ControlContentThemeFontSize"] = font.Size;
    }

    public static FontSetting Read()
    {
        if (OperatingSystem.IsLinux())
        {
            if (Parse(ReadCommand("gsettings", "get", "org.gnome.desktop.interface", "font-name")) is { } font)
                return font;
            var family = ReadCommand("fc-match", "-f", "%{family}", "sans-serif")?.Trim();
            if (!string.IsNullOrWhiteSpace(family)) return new(family, 14);
        }
        return new("sans-serif", 14);
    }

    public static FontSetting? Parse(string? description)
    {
        var match = FontDescription().Match(description?.Trim().Trim('\'', '"') ?? "");
        if (!match.Success || !double.TryParse(match.Groups[2].Value, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var size) || size <= 0 || size > 200) return null;
        // GNOME stores points unless the Pango description explicitly says px.
        return new(match.Groups[1].Value, match.Groups[3].Success ? size : size * 96 / 72);
    }

    [GeneratedRegex(@"^(.+?)\s+(\d+(?:\.\d+)?)(px)?$")]
    private static partial Regex FontDescription();

    private static ProcessStartInfo Command(string executable, params string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        return info;
    }

    private static string? ReadCommand(string executable, params string[] arguments)
    {
        try
        {
            using var process = Process.Start(Command(executable, arguments));
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(1000)) { process.Kill(); return null; }
            return process.ExitCode == 0 ? output.GetAwaiter().GetResult() : null;
        }
        catch { return null; }
    }

    public void Dispose()
    {
        _disposed = true;
        try { if (_monitor is { HasExited: false }) _monitor.Kill(); }
        catch (Exception ex) { Log.Debug(ex, "System font monitor already stopped"); }
        _monitor?.Dispose();
    }
}
