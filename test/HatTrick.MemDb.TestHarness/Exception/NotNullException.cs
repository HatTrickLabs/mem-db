using System;

namespace HatTrick.Data.TestHarness
{
    public class NotNullException : Exception
    {
        #region internals
        private Type _type;
        private object _value;
        #endregion

        #region ctors
        public NotNullException(Type type, object value)
            : base($"{type.Name} {value} is not null")
        {
            _type = type;
            _value = value;
        }
        #endregion
    }
}
