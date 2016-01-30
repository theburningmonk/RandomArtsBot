namespace RandomArtsBot

open System
open System.Drawing

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

type IArtist =
    /// Randomly generates an expression. n determines the max depth
    /// of the expression.
    abstract member GenExpr : random:Random * n:int -> Expr

    /// Parses a text formula into an Expr object
    abstract member Parse : string -> Choice<Expr, string>

    /// Draws a random image using the given expression and returns
    /// the path to the image file and the Bitmap data
    abstract member DrawImage : random:Random * Expr -> Bitmap

type Artist =
    new : unit -> Artist

    interface IArtist