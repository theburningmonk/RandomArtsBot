namespace RandomArtsBot

module RandomArt =
    open System

    type Expr =
        | VariableX
        | VariableY
        | Constant

        | Add      of Expr * Expr
        | Subtract of Expr * Expr
        | Product  of Expr * Expr
        | Divide   of Expr * Expr

        | Max      of Expr * Expr
        | Min      of Expr * Expr
        | Average  of Expr * Expr

        | Mod      of Expr * Expr
        | Well     of Expr
        | Tent     of Expr

        | Sin of Expr
        | Cos of Expr
        | Tan of Expr

        | Sqr  of Expr
        | Sqrt of Expr

        | Level of Expr * Expr * Expr
        | Mix   of Expr * Expr * Expr

        override x.ToString() =
            match x with
            | VariableX -> "x"
            | VariableY -> "y"
            | Constant  -> "const"

            | Add      (e1, e2) -> sprintf "(+ %O %O)" e1 e2
            | Subtract (e1, e2) -> sprintf "(- %O %O)" e1 e2
            | Product  (e1, e2) -> sprintf "(* %O %O)" e1 e2
            | Divide   (e1, e2) -> sprintf "(/ %O %O)" e1 e2
            | Max      (e1, e2) -> sprintf "(max %O %O)" e1 e2
            | Min      (e1, e2) -> sprintf "(min %O %O)" e1 e2
            | Average  (e1, e2) -> sprintf "(avg %O %O)" e1 e2
            | Mod      (e1, e2) -> sprintf "(mod %O %O)" e1 e2

            | Well e -> sprintf "(well %O)" e
            | Tent e -> sprintf "(tent %O)" e
            | Sin  e -> sprintf "(sin %O)" e
            | Cos  e -> sprintf "(cos %O)" e
            | Tan  e -> sprintf "(tan %O)" e
            | Sqr  e -> sprintf "(sqr %O)" e
            | Sqrt e -> sprintf "(sqrt %O)" e

            | Level (e1, e2, e3) -> sprintf "(lvl %O %O %O)" e1 e2 e3 
            | Mix   (e1, e2, e3) -> sprintf "(mix %O %O %O)" e1 e2 e3

    let next (random : Random) = random.NextDouble()

    let average (c1, c2, w) =
        let r1, g1, b1 = c1
        let r2, g2, b2 = c2
        let r = w * r1 + (1.0 - w) * r2
        let g = w * g1 + (1.0 - w) * g2
        let b = w * b1 + (1.0 - w) * b2
        r, g, b

    let well x = 1.0 - 2.0 / (1.0 + x * x) ** 8.0

    let tent x = 1.0 - 2.0 * abs x

    let rec combine random expr1 expr2 op =
        let f1, f2 = eval random expr1, eval random expr2
        fun (x, y) ->
            let r1, g1, b1 = f1 (x, y)
            let r2, g2, b2 = f2 (x, y)
            op r1 r2, op g1 g2, op b1 b2

    and map random expr map =
        let f = eval random expr
        fun (x, y) ->
            let r, g, b = f(x,y)
            map r, map g, map b

    and eval random = function
        | VariableX -> fun (x, _) -> (x, x, x)
        | VariableY -> fun (_, y) -> (y, y, y)
        | Constant  -> fun (_, _) -> (next random, next random, next random)

        | Add (e1, e2)      -> combine random e1 e2 (+)
        | Subtract (e1, e2) -> combine random e1 e2 (-)
        | Product (e1, e2)  -> combine random e1 e2 (*)
        | Divide (e1, e2)   -> combine random e1 e2 (/)

        | Max (e1, e2) -> combine random e1 e2 max
        | Min (e1, e2) -> combine random e1 e2 min
        | Average (e1, e2) ->
            let f1, f2 = eval random e1, eval random e2
            fun (x, y) ->
                average (f1(x, y), f2(x, y), 0.5)

        | Sqr (e) -> map random e (fun x -> x * x)
        | Sqrt(e) -> map random e sqrt

        | Mod (e1, e2) -> combine random e1 e2 (%)
        | Well (e)     -> map random e well
        | Tent (e)     -> map random e tent

        | Sin (e) ->
            let phase = next random * System.Math.PI
            let freq  = (next random) * 5.0 + 1.0
            map random e (fun x -> sin (phase + x * freq))
        | Cos (e) ->
            let phase = next random * System.Math.PI
            let freq  = (next random) * 5.0 + 1.0
            map random e (fun x -> cos (phase + x * freq))
        | Tan (e) ->
            let phase = next random * System.Math.PI
            let freq  = (next random) * 5.0 + 1.0
            map random e (fun x -> tan (phase + x * freq))

        | Level (e1, e2, e3) ->
            let f1, f2, f3 = eval random e1, eval random e2, eval random e3
            let threshold  = next random * 2.0 - 1.0
            fun (x, y) ->
                let r1, g1, b1 = f1 (x, y)
                let r2, g2, b2 = f2 (x, y)
                let r3, g3, b3 = f3 (x, y)
                let r = if r1 < threshold then r2 else r3
                let g = if g1 < threshold then g2 else g3
                let b = if b1 < threshold then b2 else b3
                r, g, b
        | Mix (e1, e2, e3) ->
            let f1, f2, f3 = eval random e1, eval random e2, eval random e3
            fun (x, y) ->
                let n, _, _ = f1 (x,y)
                let w  = 0.5 * (n + 1.0)
                let c1 = f2 (x,y)
                let c2 = f3 (x,y)
                average (c1, c2, w)

    open System
    open System.Drawing
    open System.IO

    let rgb (r, g, b) =
       let r = max 0 (min 255 (int (128.0 * (r + 1.0))))
       let g = max 0 (min 255 (int (128.0 * (g + 1.0))))
       let b = max 0 (min 255 (int (128.0 * (b + 1.0))))
       r, g, b
 
    let width, height = 512, 384

    let drawWith f n =
       let image = new Bitmap(width, height)
       use graphics = Graphics.FromImage(image)

       [| for y in 0..n..height-n do
            for x in 0..n..width-n -> x, y
       |]
       |> Array.Parallel.map (fun (x, y) ->
             let x' = -1.0 + (((float x+(float n/2.0))*2.0)/float width)
             let y' = -1.0 + (((float y+(float n/2.0))*2.0)/float height)
             let r, g, b = f (x',y') |> rgb
             x, y, r, g, b
       )
       |> Array.iter (fun (x, y, r, g, b) ->         
          use pen = new SolidBrush(Color.FromArgb(r, g, b))
          graphics.FillRectangle(pen, x, y, n, n)
       )

       image

    let rec genExpr random n =
        if n <= 0 || next random < 0.01 then
            let terminals = [| VariableX; VariableY; Constant |]
            terminals.[ random.Next(terminals.Length) ]
        else
            let operators = [
                fun () -> Add ( genExpr random (n-1), genExpr random (n-1) )
                fun () -> Subtract ( genExpr random (n-1), genExpr random (n-1) )
                fun () -> Product  ( genExpr random (n-1), genExpr random (n-1) )
                fun () -> Divide   ( genExpr random (n-1), genExpr random (n-1) )

                fun () -> Max ( genExpr random (n-1), genExpr random (n-1) )
                fun () -> Min ( genExpr random (n-1), genExpr random (n-1) )
                fun () -> Average ( genExpr random (n-1), genExpr random (n-1) )

                fun () -> Mod  ( genExpr random (n-1), genExpr random (n-1) )
                fun () -> Well ( genExpr random (n-1) )
                fun () -> Tent ( genExpr random (n-1) )

                fun () -> Sin ( genExpr random (n-1) )
                fun () -> Cos ( genExpr random (n-1) )
                fun () -> Tan ( genExpr random (n-1) )

                fun () -> Sqr  ( genExpr random (n-1) )
                fun () -> Sqrt ( genExpr random (n-1) )

                fun () -> Level ( genExpr random (n-1), genExpr random (n-1), genExpr random (n-1) )
                fun () -> Mix   ( genExpr random (n-1), genExpr random (n-1), genExpr random (n-1) )
            ]

            operators.[random.Next(operators.Length)]()

    [<AutoOpen>]
    module Parsers =
        open FParsec
        open FParsec.Internals

        type Parser<'t> = Parser<'t, unit>

        // FParsec only supports up to pipe5, extend it by piping the result of 
        // the first 5 parser through a second pipe
        let pipe6 
                (p1: Parser<'a,'u>) 
                (p2: Parser<'b,'u>) 
                (p3: Parser<'c,'u>) 
                (p4: Parser<'d,'u>) 
                (p5: Parser<'e,'u>) 
                (p6: Parser<'f, 'u>) 
                map =
            pipe2 
                (pipe5 p1 p2 p3 p4 p5 (fun a b c d e -> a, b, c, d, e)) 
                p6 
                (fun (a, b, c, d, e) f -> map a b c d e f)

        // abbreviations
        let ws = spaces     // eats any whitespace

        // shadow functions to make them ignore whitespace
        let skipStringCI s     = skipStringCI s .>> ws
        let stringCIReturn s r = stringCIReturn s r .>> ws

        let openParen  = skipStringCI "("
        let closeParen = skipStringCI ")"

        let (expr : Parser<Expr>), exprImpl = createParserForwardedToRef()

        let variableX = stringCIReturn "x" VariableX
        let variableY = stringCIReturn "y" VariableY
        let constant  = stringCIReturn "const" Constant

        let unaryOp symbol op =
            openParen >>. (skipStringCI symbol) >>. expr .>> closeParen |>> op

        let binaryOp symbol op =
            pipe5
                openParen (skipStringCI symbol) expr expr closeParen
                (fun _ _ e1 e2 _ -> op (e1, e2))
        let ternaryOp symbol op =
            pipe6
                openParen (skipStringCI symbol) expr expr expr closeParen
                (fun _ _ e1 e2 e3 _ -> op (e1, e2, e3))

        let sin = unaryOp "sin" Sin
        let cos = unaryOp "cos" Cos
        let tan = unaryOp "tan" Tan

        let sqr  = unaryOp "sqr" Sqr
        let sqrt = unaryOp "sqrt" Sqrt

        let mod' = binaryOp "mod" Mod
        let well = unaryOp "well" Well
        let tent = unaryOp "tent" Tent

        let max = binaryOp "max" Max
        let min = binaryOp "min" Min
        let avg = binaryOp "avg" Average

        let add  = binaryOp "+" Add
        let sub  = binaryOp "-" Subtract
        let prod = binaryOp "*" Product
        let div  = binaryOp "/" Divide
        
        let lvl = ternaryOp "lvl" Level
        let mix = ternaryOp "mix" Mix

        // NOTE : this is rather inefficient with lots of backtracking
        do exprImpl := 
            choice [ 
                attempt variableX 
                attempt variableY
                attempt constant 
                attempt sin
                attempt cos
                attempt tan
                attempt sqr
                attempt sqrt
                attempt mod' 
                attempt well
                attempt tent
                attempt max
                attempt min
                attempt avg
                attempt add
                attempt sub
                attempt prod
                attempt div
                attempt lvl
                attempt mix
            ]

        let tryParseExpr text = 
            match run expr text with
            | Success (result, _, _) -> Choice1Of2 result
            | Failure (errStr, _, _) -> Choice2Of2 errStr

    let parse text = tryParseExpr text

    let drawImage random (expr : Expr) =
        let image = drawWith (eval random expr) 1
        let path  = 
            Path.Combine(
                Path.GetTempPath(), 
                Path.GetTempFileName() + ".png")
        image.Save(path)
        path