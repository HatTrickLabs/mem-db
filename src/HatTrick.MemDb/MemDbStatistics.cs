using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.Data
{
    public class MemDbStatistics
    {
        public int?     FreshCount      { get; set; }
        public int?     StaleCount      { get; set; }
        public int?     DeletedCount    { get; set; }
        public long?    FreshSize       { get; set; }
        public long?    StaleSize       { get; set; }
        public long?    DeletedSize     { get; set; }
        public int?     MaxFreshSize    { get; set; }
        public int?     MaxStaleSize    { get; set; }
        public int?     MaxDeletedSize  { get; set; }
        public int?     MinFreshSize    { get; set; }
        public int?     MinStaleSize    { get; set; }
        public int?     MinDeletedSize  { get; set; }
        public double?  AvgFreshSize    { get; set; }
        public double?  AvgStaleSize    { get; set; }
        public double?  AvgDeletedSize  { get; set; }
        public long?    LastId          { get; set; }
    }
}
