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
}
