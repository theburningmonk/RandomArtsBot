namespace RandomArtsBot

type IGenerator =
    /// Starts a loop to generate random images periodically
    abstract member Start : freqMs:int -> unit

type Generator =
    new : ITwitterClient * IArtist * IState -> Generator

    interface IGenerator