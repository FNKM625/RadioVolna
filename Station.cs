using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RadioVolna;

public class Station : INotifyPropertyChanged
{
    // --- 1. DANE Z JSON (GITHUB) ---
    // Te pola muszą pasować do Twojego pliku .json na GitHubie

    [JsonPropertyName("label")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Url { get; set; } = string.Empty;

    // --- 2. DANE APLIKACJI ---

    [JsonIgnore]
    public string DisplayName { get; set; } = string.Empty;

    // To pole odpowiada za serduszko. 
    // Musi być tak napisane, aby powiadamiać widok o zmianie (OnPropertyChanged).
    private bool _isFavorite;

    [JsonIgnore]
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                // To kluczowa linijka - mówi przyciskowi w XAML: "Zmieniłem się! Sprawdź DataTrigger!"
                OnPropertyChanged();
            }
        }
    }

    // --- 3. MECHANIZM ODŚWIEŻANIA (Boilerplate) ---
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}