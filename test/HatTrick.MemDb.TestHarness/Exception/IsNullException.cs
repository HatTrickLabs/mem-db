using System;

namespace HatTrick.Data.TestHarness
{
    public class IsNullException : Exception
    {
        #region internals
        private Type _type;
        private object _value;
        #endregion

        #region ctors
        public IsNullException(Type type, object value)
            : base($"{type.Name} {value} is null")
        {
            _type = type;
            _value = value;
        }
        #endregion
    }
}
