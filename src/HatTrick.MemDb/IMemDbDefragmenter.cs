using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.MemDb
{
    public interface IMemDbDefragmenter<T> where T : class, new()
    {
        public void Defrag();
    }
}
