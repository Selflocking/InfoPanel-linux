using System.Globalization;
using System.Text.Json;
using System.Xml.Serialization;
using InfoPanel.Localization;
using InfoPanel.Models;
using Xunit;

namespace InfoPanel.App.Tests;

[Collection("AppState")]
public class LocalizationTests
{
    [Fact]
    public void CatalogsHaveIdenticalKeysAndPreserveFormatArguments()
    {
        Dictionary<string, string> Load(string language)
        {
            using var stream = typeof(Localizer).Assembly.GetManifestResourceStream($"InfoPanel.Localization.{language}.json")!;
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        }
        var english = Load("en"); var chinese = Load("zh-CN");
        Assert.Equal(english.Keys.Order(), chinese.Keys.Order());
        foreach (var (key, value) in english)
        {
            var args = System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).Order();
            var translatedArgs = System.Text.RegularExpressions.Regex.Matches(chinese[key], @"\{\d+\}").Select(m => m.Value).Order();
            Assert.Equal(args, translatedArgs);
        }
    }

    [Fact]
    public void ChineseAndEnglishSelectionFallbackAndFormattingDoNotChangeNumericCulture()
    {
        var previous = Localizer.Instance.Language;
        var culture = CultureInfo.CurrentCulture;
        try
        {
            Localizer.Instance.SetLanguage("zh-CN");
            Assert.Equal("设备", Localizer.Text("Devices"));
            Assert.Equal("创建匹配的 360×960 配置", Localizer.Text("Create matching {0}×{1} profile", 360, 960));
            Assert.Equal("MissingKey", Localizer.Instance["MissingKey"]);
            Assert.Same(culture, CultureInfo.CurrentCulture);
            Localizer.Instance.SetLanguage("de-DE");
            Assert.Equal("Devices", Localizer.Text("Devices"));
        }
        finally { Localizer.Instance.SetLanguage(previous); }
    }

    [Fact]
    public void LanguagePreferenceRoundTripsAndOldSettingsUseSystemDefault()
    {
        var serializer = new XmlSerializer(typeof(Settings));
        using var writer = new StringWriter();
        serializer.Serialize(writer, new Settings { Language = "zh-CN" });
        using var reader = new StringReader(writer.ToString());
        Assert.Equal("zh-CN", ((Settings)serializer.Deserialize(reader)!).Language);
        Assert.Equal("", new Settings().Language);
    }
}
