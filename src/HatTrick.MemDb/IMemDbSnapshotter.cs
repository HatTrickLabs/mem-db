using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.Data
{
    public interface IMemDbSnapshotter
    {
        internal DateTime Snapshot();
    }
}
