namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("RandomArtsBot")>]
[<assembly: AssemblyProductAttribute("RandomArtsBot")>]
[<assembly: AssemblyDescriptionAttribute("Twitter bot for generating random art")>]
[<assembly: AssemblyVersionAttribute("0.1")>]
[<assembly: AssemblyFileVersionAttribute("0.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.1"
