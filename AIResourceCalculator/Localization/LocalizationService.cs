using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AIResourceCalculator.Localization;

public class LocalizationService : INotifyPropertyChanged
{
    private static readonly LocalizationService _instance = new();
    public static LocalizationService Instance => _instance;

    private Dictionary<string, string> _strings = new();
    private string _currentLang = "uk";

    public string CurrentLang => _currentLang;
    public string Flag => _currentLang == "uk" ? "🇺🇦" : "🇬🇧";
    public string LangName => _currentLang == "uk" ? "Українська" : "English";

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService()
    {
        LoadLanguage("uk");
    }

    public void LoadLanguage(string lang)
    {
        var baseDir = Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory) ?? ".";
        var path = Path.Combine(baseDir, "Localization", $"strings.{lang}.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(baseDir, "Localization", "strings.en.json");
            if (!File.Exists(path))
            {
                _strings = new Dictionary<string, string> { ["app.title"] = "AI Resource Calculator" };
                return;
            }
        }

        var json = File.ReadAllText(path);
        _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        _currentLang = lang;

        OnPropertyChanged("");
        OnPropertyChanged(nameof(CurrentLang));
        OnPropertyChanged(nameof(Flag));
        OnPropertyChanged(nameof(LangName));
    }

    public string this[string key] => _strings.TryGetValue(key, out var val) ? val : $"[{key}]";

    public string Get(string key) => this[key];

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
