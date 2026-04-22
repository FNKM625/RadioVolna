using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RadioVolna;

public class Station : INotifyPropertyChanged
{
    // --- 1. DANE Z JSON (GITHUB) ---
    [JsonPropertyName("label")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Url { get; set; } = string.Empty;

    // --- GRAFIKA I EMOJI ---

    private string _faviconUrl = string.Empty;
    public string FaviconUrl
    {
        get => _faviconUrl;
        set
        {
            if (_faviconUrl != value)
            {
                _faviconUrl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayFavicon));
                OnPropertyChanged(nameof(DisplayEmoji));
            }
        }
    }

    public string CountryEmoji { get; set; } = string.Empty;

    [JsonPropertyName("emoji")]
    public string Emoji { get; set; } = string.Empty;

    // --- C# DECYDUJE CO WYŚWIETLIĆ (ZAMIAST IsVisible) ---

    [JsonIgnore]
    public string DisplayFavicon => string.IsNullOrWhiteSpace(FaviconUrl) ? "" : FaviconUrl;

    [JsonIgnore]
    public string DisplayEmoji
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FaviconUrl))
                return "";

            return string.IsNullOrWhiteSpace(CountryEmoji) ? "📻" : CountryEmoji;
        }
    }

    // --- 2. DANE APLIKACJI ---
    [JsonIgnore]
    public string DisplayName { get; set; } = string.Empty;
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
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool _isPreviewing;

    [JsonIgnore]
    public bool IsPreviewing
    {
        get => _isPreviewing;
        set
        {
            if (_isPreviewing != value)
            {
                _isPreviewing = value;
                OnPropertyChanged();
            }
        }
    }
}
