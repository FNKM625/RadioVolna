using System.Collections.ObjectModel;
using RadioVolna;

namespace RadioVolna.Services;

public class StationManager
{
    private const string FavoritesKey = "FavoritesList";

    public void MergeWithFavorites(List<Station> sourceList, ObservableCollection<Station> targetCollection)
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
        foreach (var s in sorted) targetCollection.Add(s);
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
        return stations
            .OrderByDescending(s => s.IsFavorite)
            .ThenBy(s => s.DisplayName)
            .ToList();
    }

    private void ReorderCollection(ObservableCollection<Station> collection)
    {
        var sortedList = SortStationsInternal(collection);

        collection.Clear();
        foreach (var s in sortedList) collection.Add(s);
    }
}