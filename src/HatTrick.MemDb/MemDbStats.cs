using System;

namespace HatTrick.InMemDb
{
    [Flags]
    public enum Stats : int
    {
        FreshCount = 1,
        StaleCount = 2,
        DeletedCount = 4,
        FreshSize = 8,
        StaleSize = 16,
        DeletedSize = 32,
        MaxFreshSize = 64,
        MaxStaleSize = 128,
        MaxDeletedSize = 256,
        MinFreshSize = 512,
        MinStaleSize = 1024,
        MinDeletedSize = 2048,
        AvgFreshSize = 4096,
        AvgStaleSize = 8192,
        AvgDeletedSize = 16384,
        LastId = 32768
    }
}
