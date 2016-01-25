namespace RandomArtsBot

[<AutoOpen>]
module Extensions =
    open LinqToTwitter

    type User with
        member user.PrettyPrint () =
            sprintf """[@%s]
        [name]        : %s
        [description] : %s
        [followers]   : %d
        [friends]     : %d
        [favorites]   : %d
        [location]    : %s"""
                    user.ScreenNameResponse 
                    user.Name 
                    user.Description 
                    user.FollowersCount
                    user.FriendsCount
                    user.FavoritesCount
                    user.Location

    open System.Linq

    module Seq =
        /// the built-in Seq.take excepts when there are insufficient no
        /// of items, but LINQ's Take doesn't, and that's the behaviour
        /// we usually want anyway
        let take n (xs : seq<'a>) = xs.Take(n) :> seq<'a>

    type Async with
        static member StartProtected(computation, onError, ?cancellationToken) =
            let wrapped =
                async {
                    let! res = Async.Catch computation
                    match res with
                    | Choice1Of2 _ -> ()
                    | Choice2Of2 exn ->
                        onError exn
                        Async.StartProtected(
                            computation, 
                            onError, 
                            ?cancellationToken = cancellationToken)
                }

            Async.Start(wrapped, ?cancellationToken = cancellationToken)

    open System
    open System.Threading

    type Agent<'T> = MailboxProcessor<'T>

    type MailboxProcessor<'T> with
        static member StartProtected
                (body : MailboxProcessor<'T> -> Async<unit>,
                 ?cancellationToken : CancellationToken,
                 ?onRestart : Exception -> unit) =
            let rec wrapped f x = async {
                let! res = f x |> Async.Catch
                match res with
                | Choice1Of2 _ -> ()
                | Choice2Of2 exn -> 
                    match onRestart with
                    | Some f -> f exn
                    | _      -> ()

                    do! wrapped f x
            }

            MailboxProcessor<'T>.Start(
                (fun inbox -> wrapped body inbox),
                ?cancellationToken = cancellationToken)