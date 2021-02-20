using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Apex.Runtime;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Validators;
using Benchmarks;

namespace Benchmark
{
    public class Config : ManualConfig
    {
        public Config()
        {
            Add(JitOptimizationsValidator.DontFailOnError);
            Add(DefaultConfig.Instance.GetLoggers().ToArray()); // manual config has no loggers by default
            Add(DefaultConfig.Instance.GetExporters().ToArray()); // manual config has no exporters by default
            Add(DefaultConfig.Instance.GetColumnProviders().ToArray()); // manual config has no columns by default

            Add(Job.Default.With(CsProjCoreToolchain.NetCoreApp50).WithGcServer(false));
            //Add(Job.Default.With(CsProjCoreToolchain.NetCoreApp50).WithGcServer(true));
            //Add(Job.Clr.With(CsProjClassicNetToolchain.Net472));
            //Add(Job.CoreRT);
            //Add(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions);

            Add(MemoryDiagnoser.Default);
        }
    }

    public sealed class T
    {
    }

    class Program
    {
        static void Main(string[] args)
        {
            var summaries = BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(config: new Config());

            Console.ReadKey();
        }
    }
}
