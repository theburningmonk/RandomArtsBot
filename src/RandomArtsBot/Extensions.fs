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