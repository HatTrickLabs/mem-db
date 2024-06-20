using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    internal interface IMemDbArchiver<T> where T : class, new()
    {
        internal void Archive();
    }
}
