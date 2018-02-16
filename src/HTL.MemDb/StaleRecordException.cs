using System;
using System.Collections.Generic;
using System.Text;

namespace HatTrick.MemDb
{
    public class StaleRecordException : Exception
    {
        private static readonly string _msg = @"Attempted action 'Update/Delete' cannot be executed.  The record provided is stale (db has been defragmented since the record was retrieved).";
        public StaleRecordException() : base(_msg)
        {
        }
    }
}
