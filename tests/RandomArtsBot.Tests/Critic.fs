﻿namespace RandomArtsBot.Tests

open System
open System.Drawing
open FsUnit
open NUnit.Framework
open RandomArtsBot
open RandomArtsBot.Critic

module ``Critic tests`` =
    let artist = new Artist() :> IArtist

    // from images the bot has generated on twitter
    [<TestCase("(well const)")>]
    [<TestCase("(sin (/ (+ (well (well (- x x))) (tent const)) (- (well (tan (tent const))) (tent (well const)))))")>]
    [<TestCase("(well (* (+ (well const) (well (well (well const)))) (well (well (tent (tent const))))))")>]
    [<TestCase("(sin (tent (sin (cos (well (avg const const))))))")>]
    let ``simple one colour images should not be deemed good enough`` input =
        let random = new Random(int DateTime.UtcNow.Ticks)

        let (Choice1Of2 expr) = artist.Parse input
        let bitmap = artist.DrawImage(random, expr)

        isGoodEnough bitmap |> should be False