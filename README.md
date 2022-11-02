# BaboonAPI

Trombone Champ modding API, aiming to provide nicer hooks to
promote compatibility & improve the base game code

## Installing

1. Install BepInEx over the base game
2. Install FSharp.Core - `FSharp.Core.dll` gets put in `bin/Release/net472`
   by the build process, I usually put it in `BepInEx/core`
3. Put `BaboonAPI.dll` in the plugins folder
4. Toot!

## Developer Usage

A quick-and-dirty example plugin for the track registration API:

```csharp
using System.Collections.Generic;
using BaboonAPI.Hooks.Tracks;
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
            TrackRegistrationEvent.EVENT.Register(new ChimpanzeeTrackCallback());
        }
    }

    public class ChimpanzeeTrackCallback : TrackRegistrationEvent.Listener
    {
        public IEnumerable<TromboneTrack> OnRegisterTracks()
        {
            // Load and return your tracks here.
            yield return new ChimpTrack("chimps", "Chimps Forever", "Chimps", "2022", "Me", "A cool track!",
                "Weirdcore", 2, 120, 140);
        }
    }

    public class ChimpTrack : TromboneTrack
    {
        public ChimpTrack(string trackref, string tracknameLong, string tracknameShort, string year, string artist,
            string desc, string genre, int difficulty, int tempo, int length)
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
        }

        public LoadedTromboneTrack LoadTrack()
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
    }

    public class LoadedChimpTrack : LoadedTromboneTrack
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
