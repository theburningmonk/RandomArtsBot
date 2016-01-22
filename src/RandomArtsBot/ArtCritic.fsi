namespace RandomArtsBot

open System.Drawing

module Critic =
    // whether the generated bitmap is worthy of Twitter audience
    val isGoodEnough : Bitmap -> bool