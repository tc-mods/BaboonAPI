(*** condition: prepare ***)
#I "../BaboonAPI/bin/Debug/net472"
#r "BaboonAPI.dll"
#r "Assembly-CSharp.dll"

// Hilariously include TrombLoader so we can demonstrate its CustomTrack type
#I "./docs-libs"
#r "TrombLoader.dll"

(**
# Looking up tracks

If you're a mod trying to determine things about tracks, you'll probably be looking for some way to look up
information about that track. Look no further than the `cref:T:BaboonAPI.Hooks.Tracks.TrackLookup` API!

## Basic usage

Simply call `cref:M:BaboonAPI.Hooks.Tracks.TrackLookup.lookup(System.String)` with the trackref you want to look up:
*)

open BaboonAPI.Hooks.Tracks

let track = TrackLookup.lookup "mare"

track.trackname_long // => "Old Gray Mare"

(**
```csharp
// C#
var track = TrackLookup.lookup("mare");

track.trackname_long // => "Old Gray Mare"
```

Note that this method will throw an exception if the track can't be found.
If you want to handle this, use `cref:M:BaboonAPI.Hooks.Tracks.TrackLookup.tryLookup(System.String)` instead:

*)

let maybeTrack = TrackLookup.tryLookup "mare"

maybeTrack |> Option.map (fun track -> track.trackname_long)
// => Some("Old Gray Mare")

(**
```csharp
var maybeTrack = TrackLookup.tryLookup("mare")
OptionModule.Map(track => track.trackname_long, maybeTrack)
// => Some("Old Gray Mare")
```

## Checking whether a track is custom

While BaboonAPI has no knowledge of custom tracks itself, you can use an `is` pattern to conditionally cast
the returned TromboneTrack to a subclass.

For example, [TrombLoader](https://github.com/NyxTheShield/TrombLoader/)'s custom track class is named `CustomTrack`:
*)

open TrombLoader.CustomTracks

let bakaMitaiTrack = TrackLookup.lookup "BakaMitai"
match bakaMitaiTrack with
| :? CustomTrack as ct ->
    // This is a TrombLoader track!
    ct.folderPath
| track ->
    // Not a TrombLoader track - something else!
    track.trackname_long

(**
```csharp
// C#
var track = TrackLookup.lookup("BakaMitai");
if (track is CustomTrack ct)
{
    // This track is a TrombLoader track!
    ct.folderPath // returns the path to the folder containing this track
}
```
*)

(**
## Looking up scores

If you want to look up locally saved high scores for a track, you can use
`cref:M:BaboonAPI.Hooks.Tracks.TrackLookup.trackScore(System.String)`:
*)

let possibleScores = TrackLookup.lookupScore "BakaMitai"

(**
If the track has been played at least once, you'll get a `Some(SavedTrackScore)` back!
Otherwise, you'll get None. Note that the track might exist even if its score hasn't been saved -
use `tryLookup` if you want to check that!
*)

match possibleScores with
| Some score ->
    let lastFiveHighScores = score.highScores
    let highestRank = score.highestRank
    // Now we have highscores!
    ()
| None ->
    // Track wasn't found in score storage!
    ()

(**
## Additional utilities

If you need a `SingleTrackData` for whatever reason, you can convert any `TromboneTrack` easily:
*)

let trackData = TrackLookup.toTrackData bakaMitaiTrack
trackData.trackname_long // => "Baka Mitai"
