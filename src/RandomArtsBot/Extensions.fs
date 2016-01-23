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