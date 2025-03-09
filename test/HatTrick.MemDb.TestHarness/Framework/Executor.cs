using System;
using System.Collections.Generic;
using System.Reflection;

namespace HatTrick.InMemDb.TestHarness
{
    public class Executor
    {
        #region internals
        private Action[] _tests;
        private List<Failure> _failures;
        #endregion

        #region has failures
        public bool HasFailures => _failures.Count > 0;
        #endregion

        #region ctors
        public Executor()
        {
            _failures = new List<Failure>();
        }
        #endregion

        #region execute
        public void Execute(BaseTests against)
        {
            Console.WriteLine("Starting resolving test methods.");
            _tests = this.ReflectTestMethods(against);
            Console.WriteLine($"Resolved {_tests.Length} test methods.");

            Console.WriteLine("Starting test method execution.");
            for (int i = 0; i < _tests.Length; i++)
            {
                var test = _tests[i];
                against.Cleanup();
                this.ExecuteTest(test);
            }
            Console.WriteLine("Completed test method excetion");
        }
        #endregion

        #region reflect test methods
        private Action[] ReflectTestMethods(BaseTests target)
        {
            MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            MethodInfo[] tests = Array.FindAll(methods, m => m.Name.StartsWith("Test_"));

            List<Action> actions = new List<Action>();
            foreach (var test in tests)
            {
                actions.Add(test.CreateDelegate<Action>(target));
            }

            return actions.ToArray();
        }
        #endregion

        #region get failures
        public Failure[] GetFailures()
        {
            return _failures.ToArray();
        }
        #endregion

        #region execute test
        private void ExecuteTest(Action test)
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                string target = test.Method.ReflectedType.Name + "." + test.Method.Name;
                _failures.Add(new Failure(target, ex));
            }
        }
        #endregion
    }
}
