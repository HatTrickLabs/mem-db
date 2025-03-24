using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HatTrick.CommandLine;

namespace HatTrick.InMemDb.TestHarness
{
    class Program
    {
        #region main
        static void Main(string[] args)
        {
            Command cmd = default;
            try
            {
                DefinitionRegistry registry = DefinitionRegistry.GetInstance();
                RegisterCommandDefinitions(registry);
                cmd = CommandBuilder.Build(args);
                CommandExecutor exe = registry.GetCommandExecutor(cmd);
                exe.Execute();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Concat("Exception: ", ex.Message, Environment.NewLine, ex.StackTrace));
            }

#if DEBUG
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
#endif
        }
        #endregion

        #region run tests
        static void RunTests(ICommand cmd)
        {
            string container = cmd["container"].GetValue<string>();
            string method = cmd["method"].GetValue<string>();

            Assembly assembly = Assembly.GetExecutingAssembly();
            var tests = assembly.GetTypes().Where(t => t.Name.EndsWith("Tests") && !t.IsAbstract && t.IsPublic).ToArray();
            if (container is not null)
            {
                tests = Array.FindAll(tests, (t) => t.Name == container);
                if (tests is null || tests.Length == 0)
                    throw new CommandExecutionException("No test methods found for provided for provided input.");
            }

            List<Failure> failures = null;
            int total = method is null ? 0 : 1;

            if (method is not null)
                failures = ExecuteSingleTest(tests[0], method);

            else
                failures = ExecuteTestSet(tests, out total);

            if (failures.Count == 0)
            {
                Console.WriteLine($"Executed {total} tests with 0 failures");
                return;
            }

            Console.WriteLine($"Executed {total} tests with {failures.Count} failures");
            foreach (var f in failures)
            {
                Console.WriteLine($"Failed: {f.Target}...{f.Exception.Message}");
            }
        }
        #endregion

        #region execute single test
        static List<Failure> ExecuteSingleTest(Type target, string method)
        {
            List<Failure> failures = new List<Failure>();
            AssetResolver resolver = new AssetResolver();

            var test = (TestBase)Activator.CreateInstance(target, resolver);
            test.Go(ref failures, method);
            return failures;
        }
        #endregion

        #region execute test set
        static List<Failure> ExecuteTestSet(Type[] containers, out int total)
        {
            total = 0;
            List<Failure> failures = new List<Failure>();
            AssetResolver resolver = new AssetResolver();

            for (int i = 0; i < containers.Length; i++)
            {
                var test = (TestBase)Activator.CreateInstance(containers[i], resolver);
                test.Go(ref failures, out int count);
                total += count;
            }
            return failures;
        }
        #endregion

        #region list test container names
        static void ListTestContainerNames(bool includeMethods)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            var tests = assembly.GetTypes().Where(t => t.Name.EndsWith("Tests") && !t.IsAbstract && t.IsPublic).ToArray();

            Console.WriteLine($"Found {tests.Length} test containers:");
            Console.WriteLine(string.Empty);
            for (int i = 0; i < tests.Length; i++)
            {
                Type test = tests[i];
                Console.WriteLine(test.Name);
                if (includeMethods)
                {
                    MethodInfo[] methods = test.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    methods = Array.FindAll(methods, m => m.Name.StartsWith("Test_"));
                    foreach (var method in methods)
                    {
                        Console.Write("\t");
                        Console.WriteLine(method.Name);
                    }
                    Console.WriteLine(string.Empty);
                }
            }
        }
        #endregion

        #region register command definitions
        static void RegisterCommandDefinitions(DefinitionRegistry registry)
        {
            RegisterListCommand(registry);
            RegisterRunCommand(registry);
        }
        #endregion

        #region register list command
        static void RegisterListCommand(DefinitionRegistry registry)
        {
            var cmdDef = new CommandDefinition(name: "list");
            cmdDef.Help = "Lists all test method container names and optionally each containers test methods.";
            cmdDef.Handler = (cmd) => { ListTestContainerNames(cmd["include-methods"].GetValue<bool>()); };
            cmdDef.AddOption<bool>(
                key: "include-methods", 
                defaultArg: false, 
                help: "Include all container test methods.", 
                flags: (terse: "-m", verbose: "--include-methods")
            );
            registry.Add(cmdDef);
        }
        #endregion

        #region register run command
        static void RegisterRunCommand(DefinitionRegistry registry)
        {
            var cmdDef = new CommandDefinition(name: "run");
            cmdDef.Help = "Runs all (or a subset) of the blanket tests...execute '--help list' to view the container and method filter options.";
            cmdDef.Handler = RunTests;
            cmdDef.AddOption<string>(
                key: "container",
                defaultArg: null,
                help: "The name of single test container (restrict run to methods within a container)...see 'List' command for container names.",
                flags: (terse: "-c", verbose: "--container")
            );

            cmdDef.AddOption<string>(
                key: "method",
                defaultArg: null,
                help: "The name of a method within the provided container name...see 'List --include-methods' command for container and method names.",
                flags: (terse: "-m", verbose: "--method")
            );

            cmdDef.ApplyConstraint(
                constraint: (cmd) => {
                    if (cmd["method"].GetValue<string>() is not null)
                        if (string.IsNullOrEmpty(cmd["container"].GetValue<string>()))
                            return false;

                    return true;
                },
                name: "container required",
                description: "Adding a 'method' argument requires that a 'container' argument also be provided (must be a method within the provided container)."
            );

            registry.Add(cmdDef);
        }
        #endregion
    }
}

