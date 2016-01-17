(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

(**
Random Arts Twitter Bot
======================

This project implements a Twitter bot ([@RandomArtsBot][twitter]) that generates 
random art using your message. To test it out, send a message to the [bot][twitter]
using the following syntax.

An `expression` is made up of `functions` and `primitives`.

__Primitives__ : `x`, `y`, `const`

__Functions__ :

 * `+` e.g. `(+ x y)`
 * `-` e.g. `(- x y)`
 * `*` e.g. `(* x y)`
 * `/` e.g. `(/ x y)`
 * `sin` e.g. `(sin x)`
 * `cos` e.g. `(cos x)`
 * `tan` e.g. `(tan x)`
 * `sqr` e.g. `(sqr x)`
 * `sqrt` e.g. `(sqrt x)`
 * `mod` e.g. `(mod x const)`
 * `well` e.g. `(well x)`
 * `tent` e.g. `(tent x)`
 * `max` e.g. `(max x)`
 * `min` e.g. `(min x)`
 * `avg` e.g. `(avg x)`
 * `lvl` e.g. `(lvl x y const)`
 * `mix` e.g. `(mix x y const)`

You can mix & match different functions together, for example:

* `(+ (tan x) (cos y))`
* `(+ (mod (* (sin y) const) (mod x y)) (well (+ (sin x) (sin y))))`
* `(mod (avg x y) (mix (well y) (tent (cos (+ x const))) const))`

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under Public Domain license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [twitter]: https://twitter.com/randomartsbot
  [content]: https://github.com/theburningmonk/RandomArtsBot/tree/master/docs/content
  [gh]: https://github.com/theburningmonk/RandomArtsBot
  [issues]: https://github.com/theburningmonk/RandomArtsBot/issues
  [readme]: https://github.com/theburningmonk/RandomArtsBot/blob/master/README.md
  [license]: https://github.com/theburningmonk/RandomArtsBot/blob/master/LICENSE.txt
*)
