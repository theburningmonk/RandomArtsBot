﻿namespace RandomArtsBot.Tests

open FsUnit
open NUnit.Framework
open FsCheck

open RandomArtsBot

module Option =
    let ofChoice = function
        | Choice1Of2 x -> Some x
        | _ -> None

module ``Parser tests`` =
    let artist = new Artist() :> IArtist

    [<Test>]
    let ``expressions generated from Expr should be parsed to the same Expr`` () = 
        let property (expr : Expr) =
            let expr' = 
                expr.ToString() 
                |> artist.Parse 
                |> Option.ofChoice
            expr' = Some expr

        Check.VerboseThrowOnFailure property