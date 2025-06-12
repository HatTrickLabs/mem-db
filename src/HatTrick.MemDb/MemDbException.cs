using System;

namespace HatTrick.InMemDb
{
    #region [class] mem db exception
    public abstract class MemDbException : Exception
    {
        #region ctors
        public MemDbException(string message) : base(message)
        { }

        public MemDbException(string message, Exception original) : base(message, original)
        {
        }
        #endregion
    }
    #endregion

    #region mem db flush exception
    public class MemDbFlushException : MemDbException
    {
        #region ctors
        public MemDbFlushException(Exception original) : base(
            message: "An exception was thrown on a timer thread during MemDb 'Flush'.", 
            original: original
            )
        {
        }
        #endregion
    }
    #endregion

    #region [class] mem db configuration exception
    public class MemDbConfigurationException : MemDbException
    {
        #region ctors
        public MemDbConfigurationException(string message) : base(message)
        {
        }
        #endregion
    }
    #endregion

    #region [class] not encryption ready exception
    public class NotEncryptionReadyException : MemDbConfigurationException
    {
        #region ctors
        public NotEncryptionReadyException(string message) : base(message)
        {
        }
        #endregion
    }
    #endregion

    #region [class] mem db disposed exception
    public class MemDbPersisterDisposedException : MemDbException
    {
        #region internals
        private MemDbException _flushException;
        #endregion

        #region ctors
        public MemDbPersisterDisposedException(string message) : base(message)
        {
        }
        #endregion

        public MemDbPersisterDisposedException(string message, MemDbException flushException) : base(message)
        {
            _flushException = flushException;//allow null here...
        }
    }
    #endregion

    #region [class] mem db corrupt exception
    public class MemDbCorruptException : MemDbException
    {
        #region ctors
        public MemDbCorruptException(string message) : base(message)
        { }
        #endregion
    }
    #endregion
}