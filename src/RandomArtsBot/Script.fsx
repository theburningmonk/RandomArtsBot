#r "../../packages/FParsec/lib/net40-client/FParsec.dll"
#r "../../packages/FParsec/lib/net40-client/FParsecCS.dll"
#r "../../packages/NLog/lib/net45/NLog.dll"
#r "../../packages/linqtotwitter/lib/net45/LinqToTwitterPcl.dll"
#r "../../packages/linqtotwitter/lib/net45/LinqToTwitter.AspNet.dll"
#r "../../packages/Rx-Core/lib/net45/System.Reactive.Core.dll"
#r "../../packages/Rx-Interfaces/lib/net45/System.Reactive.Interfaces.dll"
#r "../../packages/Rx-Linq/lib/net45/System.Reactive.Linq.dll"
#r "../../packages/Rx-PlatformServices/lib/net45/System.Reactive.PlatformServices.dll"
#r "System.Linq.Expressions"
#r "System.Drawing"
#r "System.Configuration"
#r "System.Threading.Tasks"

#load "Logging.fs"
#load "Twitter.fs"
#load "RandomArt.fs"

open RandomArtsBot
open RandomArt

let text = ""
let expr = parse text