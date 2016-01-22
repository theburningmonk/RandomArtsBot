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

    /// Randomly generates an expression. n determines the max depth
    /// of the expression.
    val genExpr : random:Random -> n:int -> Expr

    /// Parses a text formula into an Expr object
    val parse : string -> Choice<Expr, string>

    /// Draws a random image using the given expression and returns
    /// the path to the image file
    val drawImage : random:Random -> Expr -> string