namespace RandomArtsBot

open System.Drawing

module Critic =
    let isGoodEnough (bitmap : Bitmap) =
        let coordinates =
            seq {
                for x = 0 to bitmap.Width - 1 do
                    for y = 0 to bitmap.Height - 1 do
                        yield x, y
            }

        let numBlackPixels = 
            coordinates 
            |> Seq.fold (fun n (x, y) -> 
                let color = bitmap.GetPixel(x, y)
                let r, g, b = color.R, color.G, color.B
                if r <= 10uy && g <= 10uy && b <= 10uy
                then n + 1.0
                else n) 0.0

        let numPixels = float (bitmap.Width * bitmap.Height)
        let blackPixelsPerc = numBlackPixels / numPixels

        blackPixelsPerc < 0.80