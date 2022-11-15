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

    interface TrackScoreStorage with
        member this.GetAllScores() = scores.Values
        member this.Load(trackref) = scores.TryFind trackref
        member this.Priority = 10
        member this.Save(score) =
            scores <- scores.Add (score.trackref, score)
            true

        member this.CanStore(trackref) =
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
        ScoreLookupRegistry.insert storage
        CustomSaveRegistry.Register info (fun cap -> cap.Attach "scores" storage)
