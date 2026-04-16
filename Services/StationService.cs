using System.Net.Http.Json;
using System.Text.Json;

namespace RadioVolna.Services;

public class StationService
{
    private readonly HttpClient _httpClient = new();
    private const string GitHubJsonUrl =
        "https://raw.githubusercontent.com/FNKM625/RadioVolnaData/refs/heads/main/station.json";

    public async Task<List<Station>> GetStationsAsync()
    {
        try
        {
            var stations = await _httpClient.GetFromJsonAsync<List<Station>>(GitHubJsonUrl);
            return stations ?? new List<Station>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Błąd pobierania stacji: {ex.Message}");
            return new List<Station>();
        }
    }

    public async Task<List<Station>> SearchRadioBrowserAsync(
        string name,
        string country,
        string tags
    )
    {
        try
        {
            // Tworzymy zapytanie - szukamy tylko działających stacji (hidebroken=true) z limitem 30
            var queryParams = new List<string> { "hidebroken=true", "limit=30" };

            if (!string.IsNullOrWhiteSpace(name))
                queryParams.Add($"name={Uri.EscapeDataString(name)}");
            if (!string.IsNullOrWhiteSpace(country))
                queryParams.Add($"country={Uri.EscapeDataString(country)}");
            if (!string.IsNullOrWhiteSpace(tags))
                queryParams.Add($"tag={Uri.EscapeDataString(tags)}");

            // d1.api.radio-browser.info to główny serwer obsługujący zapytania
            string url =
                $"https://de1.api.radio-browser.info/json/stations/search?{string.Join("&", queryParams)}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var results = new List<Station>();

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    // RadioBrowser API ma inne nazwy pól (name zamiast label, url_resolved zamiast value)
                    var stationName =
                        element.GetProperty("name").GetString()?.Trim() ?? "Nieznana stacja";
                    var streamUrl = element.GetProperty("url_resolved").GetString() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(streamUrl))
                    {
                        results.Add(
                            new Station
                            {
                                Name = stationName, // zapisywane do lokalnego pliku JSON
                                DisplayName = stationName, // pokazywane w UI
                                Url = streamUrl,
                                IsFavorite = false,
                            }
                        );
                    }
                }
                return results;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Błąd API: {ex.Message}");
        }
        return new List<Station>();
    }
}
