# Interface localization

Select **Settings → Language → English / 简体中文 / System default**, then restart
InfoPanel. The preference is persisted in `settings.xml`; old settings files use
the system UI language. Chinese locales use Simplified Chinese; unsupported
locales fall back to English. Numeric parsing, serialization and user-entered
format strings keep their original culture and values.

The embedded JSON catalogs in `src/InfoPanel.App/Localization` cover navigation,
tray menu, settings, dashboard, designer toolbar and inspector labels, sensor
page, plugin page, device controls and the primary device scan messages. Resource
bindings support change notification; restart is required for labels built in C#.
Historical release notes, plugin-owned strings, sensor/model names, enum values,
user-authored profile content and detailed technical diagnostics retain their
original text. New interface text should use a catalog entry rather than adding
another hardcoded control label.

## Adding strings

```sh
python3 tools/add-translation.py 'Panel options' --zh '屏幕选项'
```

The tool prints the XAML markup and C# expression to use. The deterministic
source-text hash is shared across repeated labels; editing a translation does
not change its key. Catalogs are embedded with explicit manifest names so the
application's assembly filename does not affect lookup.

XAML uses `xmlns:loc="using:InfoPanel.Localization"` and `{loc:Tr <key>}`.
C# uses `Localizer.Text("English source")`; use numbered format arguments for
dynamic text, e.g. `Localizer.Text("Scan failed: {0}", error.Message)`, and register
the entire format string in the catalogs. Missing/empty translated values fall
back to the English catalog. Unknown keys/source strings remain visible.

To add a language, add its JSON catalog with the same keys, load it in Localizer,
add it to language resolution and the Settings selector, and extend the catalog
and persistence tests. Do not translate enum identifiers or XML element names.

`LocalizationTests` checks catalog parity, format arguments, fallback and settings
compatibility. `PickerLayoutTests.SettingsBindsChineseCatalogAndKeepsLanguageNamesRecognizable` builds the actual Settings XAML with the
Chinese catalog to verify translated bindings and the language selector.
