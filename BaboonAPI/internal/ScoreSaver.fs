namespace BaboonAPI.Internal.Scores

open System
open BaboonAPI.Hooks.Saves
open BaboonAPI.Hooks.Scores
open BepInEx
open Microsoft.FSharp.Collections

[<Serializable>]
type private SavedScore =
    { Rank: string
      Scores: int list }

[<Serializable>]
type private Scores =
    { Scores: Map<string, SavedScore> }

type private CustomScoreStorage() =
    let mutable scores: Map<string, TrackScore> = Map.empty

    interface ScoreLookupRegistry.Listener with
        member this.AllScores() = scores.Values

        member this.Lookup(trackref) = scores.TryFind trackref

        member this.Save(score) =
            // Don't save base game tracks
            if score.isBaseGameTrack then
                false
            else
                scores <- scores.Add (score.trackref, score)
                true

    interface ICustomSaveData<Scores> with
        member this.Load(data) =
            scores <- data.Scores |> Map.map(fun trackref s -> SimpleTrackScore(trackref, s.Scores, rank s.Rank))

        member this.Save() =
            let saved = scores |> Map.map(fun _ s -> { Rank = s.rankString; Scores = s.topScores })
            { Scores = saved }

        member this.Convert o = o.ToObject()

module ScoreSaver =
    let setup (info: PluginInfo) =
        let storage = CustomScoreStorage()
        ScoreLookupRegistry.EVENT.Register storage
        CustomSaveRegistry.Register info (fun cap -> cap.Attach "scores" storage)
