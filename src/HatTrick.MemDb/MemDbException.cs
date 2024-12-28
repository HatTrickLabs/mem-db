using System;

namespace HatTrick.InMemDb
{
    #region [class] mem db exception
    public abstract class MemDbException : Exception
    {
        public MemDbException(string message) : base(message)
        { }
    }
    #endregion

    #region [class] mem db configuration exception
    public class MemDbConfigurationException : MemDbException
    {
        public MemDbConfigurationException(string message) : base(message)
        {
        }
    }
    #endregion

    #region [class] not encryption ready exception
    public class NotEncryptionReadyException : MemDbConfigurationException
    {
        public NotEncryptionReadyException(string message) : base(message)
        {
        }
    }
    #endregion

    #region [class] mem db corrupt exception
    public class MemDbCorruptException : MemDbException
    {
        public MemDbCorruptException(string message) : base(message)
        { }
    }
    #endregion
}
