using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using RadioVolna;

namespace RadioVolna.Services;

public class StationManager
{
    private const string FavoritesKey = "FavoritesList";
    private readonly string _customStationsFilePath = Path.Combine(
        FileSystem.AppDataDirectory,
        "custom_stations.json"
    );

    public void MergeWithFavorites(
        List<Station> sourceList,
        ObservableCollection<Station> targetCollection
    )
    {
        string favoritesString = Preferences.Get(FavoritesKey, "");

        targetCollection.Clear();

        var tempList = new List<Station>();

        foreach (var station in sourceList)
        {
            station.DisplayName = station.Name;

            if (favoritesString.Contains(station.DisplayName))
            {
                station.IsFavorite = true;
            }
            tempList.Add(station);
        }

        var sorted = SortStationsInternal(tempList);
        foreach (var s in sorted)
            targetCollection.Add(s);
    }

    public void ToggleFavorite(Station station, ObservableCollection<Station> collection)
    {
        station.IsFavorite = !station.IsFavorite;

        SaveFavorites(collection);
        ReorderCollection(collection);
    }

    private void SaveFavorites(IEnumerable<Station> stations)
    {
        var favNames = stations.Where(s => s.IsFavorite).Select(s => s.DisplayName);
        Preferences.Set(FavoritesKey, string.Join("|", favNames));
    }

    private List<Station> SortStationsInternal(IEnumerable<Station> stations)
    {
        return stations.OrderByDescending(s => s.IsFavorite).ThenBy(s => s.DisplayName).ToList();
    }

    private void ReorderCollection(ObservableCollection<Station> collection)
    {
        var sortedList = SortStationsInternal(collection);

        collection.Clear();
        foreach (var s in sortedList)
            collection.Add(s);
    }

    public List<Station> LoadCustomStations()
    {
        if (!File.Exists(_customStationsFilePath))
            return new List<Station>();
        try
        {
            string json = File.ReadAllText(_customStationsFilePath);
            var stations = JsonSerializer.Deserialize<List<Station>>(json) ?? new List<Station>();

            foreach (var station in stations)
            {
                station.DisplayName = station.Name;
            }

            return stations;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Błąd wczytywania stacji: {ex.Message}");
            return new List<Station>();
        }
    }

    public void SaveCustomStation(Station newStation)
    {
        var customStations = LoadCustomStations();

        if (!customStations.Any(s => s.Url == newStation.Url))
        {
            customStations.Add(newStation);

            string json = JsonSerializer.Serialize(customStations);
            File.WriteAllText(_customStationsFilePath, json);
        }
    }

    public void DeleteCustomStation(Station stationToDelete)
    {
        var customStations = LoadCustomStations();

        var stationToRemove = customStations.FirstOrDefault(s => s.Url == stationToDelete.Url);

        if (stationToRemove != null)
        {
            customStations.Remove(stationToRemove);

            string json = JsonSerializer.Serialize(customStations);
            File.WriteAllText(_customStationsFilePath, json);
        }
    }
}
