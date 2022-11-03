namespace BaboonAPI.Internal.Scores

open System
open BaboonAPI.Hooks.Saves
open BaboonAPI.Hooks.Scores
open Microsoft.FSharp.Collections

[<Serializable>]
type private SavedScore =
    { Rank: string
      Scores: int list }

[<Serializable>]
type private Scores =
    { Scores: Map<string, SavedScore> }

type private LoadedScore(trackref: string, loaded: SavedScore) =
    inherit TrackScore()

    override this.topScores = loaded.Scores
    override this.trackref = trackref
    override this.rank = rank loaded.Rank


type private CustomScoreStorage() =
    let mutable scores: Map<string, TrackScore> = Map.empty

    interface ScoreLookupRegistry.Listener with
        member this.AllScores() = scores.Values

        member this.Lookup(trackref) = scores.TryFind trackref

    interface ICustomSaveData<Scores> with
        member this.Load(data) =
            scores <- data.Scores |> Map.map(fun trackref s -> LoadedScore(trackref, s))

        member this.Save() =
            let saved = scores |> Map.map(fun _ s -> { Rank = s.rankString; Scores = s.topScores })
            { Scores = saved }
