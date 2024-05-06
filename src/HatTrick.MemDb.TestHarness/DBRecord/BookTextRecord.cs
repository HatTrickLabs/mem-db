using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HatTrick.MemDb;

namespace TestHarness
{
    public class BookTextRecord
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string BookName { get; set; }
        public int WordCount { get; set; }
    }
}
