# Track Registration API

Here's a simple dummy example for track registration:

```csharp
using System.Collections.Generic;
using BaboonAPI.Hooks;
using BepInEx;
using UnityEngine;

namespace Chimpanzee
{
    [BepInPlugin("ch.offbeatwit.chimpanzee", "Chimpanzee", "1.0.0.0")]
    [BepInDependency("ch.offbeatwit.baboonapi.plugin")]
    public class ChimpanzeePlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // Register a listener for the track registration event
            Tracks.EVENT.Register(new ChimpanzeeTrackCallback());
        }
    }

    public class ChimpanzeeTrackCallback : Tracks.Callback
    {
        public IEnumerable<Tracks.TromboneTrack> OnRegisterTracks(Tracks.TrackIndexGenerator gen)
        {
            // Load and return your tracks here.
            // Important: Use `gen.nextIndex()` to generate track indexes!
            yield return new ChimpTrack("chimps", "Chimps Forever", "Chimps", "2022", "Me", "A cool track!",
                "Weirdcore", 2, 120, 140, gen.nextIndex());
        }
    }

    public class ChimpTrack : Tracks.TromboneTrack
    {
        public ChimpTrack(string trackref, string tracknameLong, string tracknameShort, string year, string artist,
            string desc, string genre, int difficulty, int tempo, int length, int trackindex)
        {
            this.trackref = trackref;
            trackname_long = tracknameLong;
            trackname_short = tracknameShort;
            this.year = year;
            this.artist = artist;
            this.desc = desc;
            this.genre = genre;
            this.difficulty = difficulty;
            this.tempo = tempo;
            this.length = length;
            this.trackindex = trackindex;
        }

        public Tracks.LoadedTromboneTrack LoadTrack()
        {
            var bundle = AssetBundle.LoadFromFile("MyCoolBundle");
            return new LoadedChimpTrack(trackref, bundle);
        }
        
        public SavedLevel LoadChart()
        {
            // Load the actual chart data
            return new SavedLevel();
        }

        public bool IsVisible()
        {
            return true;
        }

        public string trackref { get; }
        public string trackname_long { get; }
        public string trackname_short { get; }
        public string year { get; }
        public string artist { get; }
        public string desc { get; }
        public string genre { get; }
        public int difficulty { get; }
        public int tempo { get; }
        public int length { get; }
        public int trackindex { get; }
    }

    public class LoadedChimpTrack : Tracks.LoadedTromboneTrack
    {
        private readonly AssetBundle _assetBundle;

        public LoadedChimpTrack(string trackref, AssetBundle assetBundle)
        {
            this.trackref = trackref;
            _assetBundle = assetBundle;
        }

        public void Dispose()
        {
            // Clean up any bundles you loaded, etc.
            _assetBundle.Unload(true);
        }

        public AudioSource LoadAudio()
        {
            // Load an audio source from somewhere!
            var obj = _assetBundle.LoadAsset<GameObject>($"music_{trackref}");
            return obj.GetComponent<AudioSource>();
        }

        public GameObject LoadBackground()
        {
            // Load or create a background GameObject
            return _assetBundle.LoadAsset<GameObject>($"BGCam_{trackref}");
        }

        public string trackref { get; }
    }
}
```
