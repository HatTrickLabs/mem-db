using System;
using System.Collections.Generic;

namespace HatTrick.Data.TestHarness
{
    public class Assert
    {
        public static void IsEqual<T>(T a, T b)
        {
            EqualityComparer<T> comp = EqualityComparer<T>.Default;
            bool isEqual = comp.Equals(a, b);
            if (!isEqual)
                throw new NotEqualException<T>(a, b);
        }

        public static void IsNotEqual<T>(T a, T b)
        {
            EqualityComparer<T> comp = EqualityComparer<T>.Default;
            bool isEqual = comp.Equals(a, b);
            if (isEqual)
                throw new IsEqualException<T>(a, b);
        }

        public static void IsEqual(string a, string b, bool ignoreCase = false)
        {
            bool isEqual = string.Compare(a, b, ignoreCase) == 0;
            if (!isEqual)
                throw new NotEqualException<string>(a, b);
        }

        public static void IsNotEqual(string a, string b, bool ignoreCase = false)
        {
            bool isEqual = string.Compare(a, b, ignoreCase) == 0;
            if (isEqual)
                throw new IsEqualException<string>(a, b);
        }

        public static void IsNull(object o)
        {
            bool isNull = o is null;
            if (!isNull)
                throw new NotNullException(o.GetType(), o);

        }

        public static void IsNotNull(object o)
        {
            bool isNotNull = o is not null;
            if (!isNotNull)
                throw new IsNullException(o.GetType(), o);
        }

        //public static void TrueForAll<T>(T[] set, Func<T, bool> condition)
        //{
        //    for (int i = 0; i < set.Length; i++)
        //    {
        //        if (!condition(set[i]))
        //            throw new NotTrueException($"Condition NOT true for index {i} within set.");
        //    }
        //}

        public static Exception Throws<T>(Action when) where T : Exception
        {
            try
            {
                when();
                throw new DoesNotThrowException();
            }
            catch (Exception ex) when (ex.GetType() == typeof(T))
            {
                return ex;
            }
        }

        public static Exception Throws<T>(Action when, string messageContains) where T : Exception
        {
            try
            {
                when();
                throw new DoesNotThrowException();
            }
            catch (Exception ex) 
            when (ex.GetType() == typeof(T) && ex.Message.Contains(messageContains, StringComparison.OrdinalIgnoreCase))
            {
                return ex;
            }
        }
    }
}
