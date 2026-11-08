using InfoPanel.Utils;
using Xunit;

namespace InfoPanel.App.Tests;

public class SystemUiFontTests
{
    [Theory]
    [InlineData("'Ubuntu Sans 11'", "Ubuntu Sans", 14.6666666667)]
    [InlineData("Noto Sans CJK SC 10.5", "Noto Sans CJK SC", 14)]
    [InlineData("Ubuntu Sans 16px", "Ubuntu Sans", 16)]
    [InlineData("'IBM Plex Sans 12'\n", "IBM Plex Sans", 16)]
    public void ParsesDesktopFontWithoutTreatingPointSizeAsPixels(string description, string family, double size)
    {
        var font = SystemUiFont.Parse(description);
        Assert.NotNull(font);
        Assert.Equal(family, font.Family);
        Assert.Equal(size, font.Size, 6);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("No such schema")]
    [InlineData("Ubuntu Sans 0")]
    [InlineData("Ubuntu Sans 9999")]
    public void InvalidSettingsAllowPlatformFallback(string? value) => Assert.Null(SystemUiFont.Parse(value));
}
