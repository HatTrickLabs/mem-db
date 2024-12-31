using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    internal class ObservableQueue<T> : Queue<T>
    {
        #region ctors
        public ObservableQueue() : base()
        {
        }

        public ObservableQueue(int capacity) : base(capacity)
        {
        }

        public ObservableQueue(IEnumerable<T> collection) : base(collection)
        {
        }
        #endregion

        #region can process head
        internal bool CanProcessHead(Predicate<T> condition)
        {
            if (condition is null)
                throw new ArgumentNullException(nameof(condition));

            bool canProcess = base.TryPeek(out T result) && condition(result);
            return canProcess;
        }
        #endregion
    }
}
