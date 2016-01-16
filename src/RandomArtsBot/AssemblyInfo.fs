namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("RandomArtsBot")>]
[<assembly: AssemblyProductAttribute("RandomArtsBot")>]
[<assembly: AssemblyDescriptionAttribute("Twitter bot for generating random art")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
