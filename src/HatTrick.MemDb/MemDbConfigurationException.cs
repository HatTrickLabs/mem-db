using System;

namespace HatTrick.InMemDb
{
    public class MemDbConfigurationException : Exception
    {
        public MemDbConfigurationException(string message) : base(message)
        {
        }
    }

    public class NotEncryptionReadyException : MemDbConfigurationException
    {
        public NotEncryptionReadyException(string message) : base(message)
        {
        }
    }
}
