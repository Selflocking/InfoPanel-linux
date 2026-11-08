using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace InfoPanel.Localization;

/// <summary>Embedded catalogs with English fallback. Keys are stable across translations.</summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();
    private readonly Dictionary<string, string> _english = Load("en");
    private readonly Dictionary<string, string> _chinese = Load("zh-CN");
    private readonly Dictionary<string, string> _keysByEnglish;
    private string _language = "en";
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Language => _language;

    private Localizer()
    {
        _keysByEnglish = _english.ToDictionary(pair => pair.Value, pair => pair.Key);
        SetLanguage("");
    }

    public void SetLanguage(string? language)
    {
        var requested = string.IsNullOrEmpty(language) ? CultureInfo.CurrentUICulture.Name : language;
        _language = requested.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en";
        // Only UI culture changes; numeric parsing/serialization keeps its existing culture.
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(_language);
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string this[string key] =>
        _language == "zh-CN" && _chinese.TryGetValue(key, out var translated) && !string.IsNullOrEmpty(translated)
            ? translated : _english.GetValueOrDefault(key, key);

    public static string Text(string english, params object[] args)
    {
        var value = Instance._keysByEnglish.TryGetValue(english, out var key) ? Instance[key] : english;
        return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
    }

    private static Dictionary<string, string> Load(string language)
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream($"InfoPanel.Localization.{language}.json")
            ?? throw new InvalidOperationException($"Missing localization catalog: {language}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
