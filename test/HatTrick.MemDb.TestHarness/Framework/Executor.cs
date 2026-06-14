// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using HatTrick.CommandLine;

namespace HatTrick.Data.TestHarness
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
        public void Execute(TestBase against, string methodName)
        {
            Type t = against.GetType();
            Console.WriteLine($"{t.Name}: Starting resolve test methods.");
            var action = this.ReflectTestMethod(against, methodName);

            if (action is null)
                throw new CommandExecutionException("No test method found for provided input.");

            _tests = [action];

            Console.WriteLine($"{t.Name}: Resolved test method: " + methodName);

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
                Console.WriteLine($"Executing {test.Method.Name}.");
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
            MethodInfo[] tests = Array.FindAll(methods, m => m.Name.StartsWith("Test_", StringComparison.OrdinalIgnoreCase));

            List<Action> actions = new List<Action>();
            foreach (var test in tests)
            {
                actions.Add(test.CreateDelegate<Action>(target));
            }

            return actions.ToArray();
        }
        #endregion

        #region reflect test method
        private Action ReflectTestMethod(TestBase target, string methodName)
        {
            MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            MethodInfo test = Array.Find(methods, m => m.Name == methodName);
            Action action = test is null ? null : test.CreateDelegate<Action>(target);
            return action;
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
