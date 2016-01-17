namespace RandomArtsBot.Tests

open FsUnit
open NUnit.Framework
open RandomArtsBot.RandomArt

module Option =
    let ofChoice = function
        | Choice1Of2 x -> Some x
        | _ -> None

module ``Parser tests`` =
    [<Test>]
    let ``"x", "y" and "const" should be parsed as simple expressions`` () =
        parse "x" |> Option.ofChoice |> should equal <| Some VariableX
        parse "y" |> Option.ofChoice |> should equal <| Some VariableY
        parse "const" |> Option.ofChoice |> should equal <| Some Constant
        
    [<Test>]
    let ``"(sin x)" should be parsed as a Sin expression`` () =
        parse "(sin x)"
        |> Option.ofChoice
        |> should equal (Some <| Sin VariableX)
        
    [<Test>]
    let ``"(cos x)" should be parsed as a Cos expression`` () =
        parse "(cos x)"
        |> Option.ofChoice
        |> should equal (Some <| Cos VariableX)
        
    [<Test>]
    let ``"(tan x)" should be parsed as a Tan expression`` () =
        parse "(tan x)"
        |> Option.ofChoice
        |> should equal (Some <| Tan VariableX)
        
    [<Test>]
    let ``"(+ (tan x) (cos y))" should be parsed as a nested Add expression`` () =
        let expected = Add ((Tan VariableX), (Cos VariableY))

        parse "(+ (tan x) (cos y))"
        |> Option.ofChoice
        |> should equal (Some expected)
        
    [<Test>]
    let ``"(- (Sqr x) const)" should be parsed as a nested Subtract expression`` () =
        let expected = Subtract ((Sqr VariableX), Constant)

        parse "(- (Sqr x) const)"
        |> Option.ofChoice
        |> should equal (Some expected)
        
    [<Test>]
    let ``"(mix (Sqr x) const (* (Avg x y) (Sqrt const)))" should be parsed as a nested Mix expression`` () =
        let expected = 
            Mix (
                Sqr VariableX, 
                Constant,
                Product (
                    Average (VariableX, VariableY),
                    Sqrt Constant
                )
            )

        parse "(mix (Sqr x) const (* (Avg x y) (Sqrt const)))"
        |> Option.ofChoice
        |> should equal (Some expected)