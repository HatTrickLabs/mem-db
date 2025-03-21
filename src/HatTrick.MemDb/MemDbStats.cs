using System;

namespace HatTrick.InMemDb
{
    [Flags]
    public enum Stats : int
    {
        FreshCount = 0,
        StaleCount = 1,
        DeletedCount = 2,
        FreshSize = 4,
        StaleSize = 8,
        DeletedSize = 16,
        MaxFreshSize = 32,
        MaxStaleSize = 64,
        MaxDeletedSize = 128,
        MinFreshSize = 256,
        MinStaleSize = 512,
        MinDeletedSize = 1024,
        AvgFreshSize = 2048,
        AvgStaleSize = 4096,
        AvgDeletedSize = 8192,
        LastId = 16384
    }
}
