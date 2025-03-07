using System;
using System.Collections.Generic;

namespace HatTrick.InMemDb.TestHarness
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

        public static void IsEqual(string a, string b, bool ignoreCase)
        {
            bool isEqual = string.Compare(a, b, ignoreCase) == 0;
            if (!isEqual)
                throw new NotEqualException<string>(a, b);
        }
    }
}
