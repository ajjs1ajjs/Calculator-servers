using System.ComponentModel;

namespace AIResourceCalculator.Interfaces;

public interface ILocalizationService : INotifyPropertyChanged
{
    string CurrentLang { get; }
    string Flag { get; }
    string LangName { get; }
    string this[string key] { get; }
    string Get(string key);
    void LoadLanguage(string lang);
}
