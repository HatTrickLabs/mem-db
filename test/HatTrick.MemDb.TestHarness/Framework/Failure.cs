using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.Data.TestHarness
{
    public class Failure
    {
        #region internals
        private string _target;
        private Exception _exception;
        #endregion

        #region interface
        public string Target => _target;
        public Exception Exception => _exception;
        #endregion

        #region constructors
        internal Failure(string target, Exception exception)
        {
            _target = target;
            _exception = exception;
        }
        #endregion
    }
}
