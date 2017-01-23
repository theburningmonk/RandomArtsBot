namespace RandomArtsBot

open System.Drawing

module Critic =
    module Bitmap =
        let coordinates (bitmap : Bitmap) = 
            seq {
                for x = 0 to bitmap.Width - 1 do
                    for y = 0 to bitmap.Height - 1 do
                        yield x, y
            }

        let mapPixels f bitmap =
            bitmap
            |> coordinates 
            |> Seq.map (bitmap.GetPixel >> f)

        let mapPixelsi f bitmap =
            bitmap
            |> coordinates 
            |> Seq.map (fun (x, y) -> f x y <| bitmap.GetPixel(x, y))

        let filter f bitmap =
            bitmap
            |> coordinates
            |> Seq.filter (bitmap.GetPixel >> f)
            |> Seq.map bitmap.GetPixel

        let inline sumBy f bitmap =
            bitmap
            |> coordinates
            |> Seq.sumBy (bitmap.GetPixel >> f)

        let forAll f bitmap =
            bitmap
            |> coordinates
            |> Seq.forall (bitmap.GetPixel >> f)

    [<AutoOpen>]
    module Examinations =
        let isAlmostBlackScreen bitmap =
            let numBlackPixels = 
                bitmap
                |> Bitmap.sumBy (fun color ->
                    let r, g, b = color.R, color.G, color.B
                    if r <= 10uy && g <= 10uy && b <= 10uy then 1 else 0)
            let numPixels = float (bitmap.Width * bitmap.Height)

            (float numBlackPixels / float numPixels) > 0.80

        let isOneColour (bitmap : Bitmap) =
            let control = bitmap.GetPixel(bitmap.Width/2, bitmap.Height/2)
            let ctrlR, ctrlG, ctrlB = control.R, control.G, control.B
            bitmap
            |> Bitmap.forAll (fun colour -> 
                abs (int colour.R - int ctrlR) < 10 &&
                abs (int colour.G - int ctrlG) < 10 &&
                abs (int colour.B - int ctrlB) < 10)

    let (<||>) f g x = f x || g x

    let isGoodEnough (bitmap : Bitmap) =
        bitmap
        |> (isAlmostBlackScreen <||> isOneColour) 
        |> not