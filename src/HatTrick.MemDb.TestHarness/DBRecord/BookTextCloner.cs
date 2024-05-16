using System;
using System.Collections.Generic;
using HatTrick.MemDb;

namespace TestHarness
{
    public class BookTextCloner : IMemDbCloner<BookTextRecord>
    {
        public BookTextRecord DeepCopy(BookTextRecord value)
        {
            var r = new BookTextRecord();
            r.Id = value.Id;
            r.Text = value.Text;
            r.BookName = value.BookName;
            r.WordCount = value.WordCount;
            return r;
        }

        public BookTextRecord[] DeepCopy(IList<BookTextRecord> values)
        {
            int len = values.Count;
            var recs = new BookTextRecord[len];
            for (int i = 0; i < values.Count; i++)
            {
                var v = values[i];
                var r = new BookTextRecord();
                r.Id = v.Id;
                r.Text = v.Text;
                r.BookName = v.BookName;
                r.WordCount = v.WordCount;
                recs[i] = r;
            }
            return recs;
        }
    }
}
