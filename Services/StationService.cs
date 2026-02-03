using System.Net.Http.Json;

namespace RadioVolna.Services;

public class StationService
{
    private readonly HttpClient _httpClient = new();
    private const string GitHubJsonUrl = "https://raw.githubusercontent.com/FNKM625/RadioVolnaData/refs/heads/main/station.json";

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
}