using Android.OS;
using AndroidX.Media;
using System.Collections.Generic;
using Android.Service.Media;
using BrowserRoot = AndroidX.Media.MediaBrowserServiceCompat.BrowserRoot;
using Android.Support.V4.Media;

namespace RadioVolna;

public partial class AudioService
{
    private static List<Station> _autoStations = new List<Station>();
    private static AudioService? _runningServiceInstance;

    public override void OnCreate()
    {
        base.OnCreate();
        _runningServiceInstance = this;

        if (_autoStations.Count > 0)
        {
            try { NotifyChildrenChanged("root"); } catch { }
        }
    }

    public void UpdateStationsForAuto(List<Station> stations)
    {
        _autoStations = stations;

        if (_runningServiceInstance != null)
        {
            try
            {
                _runningServiceInstance.NotifyChildrenChanged("root");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidAuto] Błąd odświeżania: {ex.Message}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[AndroidAuto] Serwis jeszcze nie działa - stacje zapisane w pamięci.");
        }
    }

    public override BrowserRoot OnGetRoot(string clientPackageName, int clientUid, Bundle rootHints)
    {
        return new BrowserRoot("root", null);
    }

    public override void OnLoadChildren(string parentId, Result result)
    {
        result.Detach();

        var mediaItems = new Java.Util.ArrayList();

        if (parentId == "root")
        {
            foreach (var station in _autoStations)
            {
                var desc = new MediaDescriptionCompat.Builder()
                    .SetMediaId(station.Url)
                    .SetTitle(station.DisplayName)
                    .SetSubtitle("Radio Volna")
                    .Build();

                var item = new MediaBrowserCompat.MediaItem(desc, MediaBrowserCompat.MediaItem.FlagPlayable);
                mediaItems.Add(item);
            }
        }

        result.SendResult(mediaItems);
    }

    public override void OnDestroy()
    {
        if (_runningServiceInstance == this) _runningServiceInstance = null;
        base.OnDestroy();
    }
}