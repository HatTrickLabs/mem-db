using System;

namespace HatTrick.InMemDb
{
    public class MemDbConfigurationException : Exception
    {
        public MemDbConfigurationException(string message) : base(message)
        {
        }
    }
}
