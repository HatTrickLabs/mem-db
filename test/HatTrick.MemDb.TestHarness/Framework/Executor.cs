using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

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
        public void Execute(TestBase against, out int count)
        {
            Type t = against.GetType();
            Console.WriteLine($"{t.Name}: Starting resolve test methods.");
            _tests = this.ReflectTestMethods(against);
            count = _tests.Length;
            Console.WriteLine($"{t.Name}: Resolved {count} test methods.");

            Console.WriteLine($"{t.Name}: Starting test method execution.");
            for (int i = 0; i < _tests.Length; i++)
            {
                var test = _tests[i];
                against.Cleanup();
                this.ExecuteTest(test);
            }
            against.Cleanup();
            Console.WriteLine($"{t.Name}: Completed test method execution");
        }
        #endregion

        #region reflect test methods
        private Action[] ReflectTestMethods(TestBase target)
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
                var frame = new StackTrace(ex, 1, true).GetFrame(0);
                int line = frame.GetFileLineNumber();
                string target = $"{test.Method.ReflectedType.Name}.{test.Method.Name} at line {line}";
                _failures.Add(new Failure(target, ex));
            }
        }
        #endregion
    }
}
