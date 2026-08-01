using BenchmarkDotNet.Running;

var assembly = typeof(Program).Assembly;

if (args.Length == 0) {
	BenchmarkRunner.Run(assembly);

	return;
}

BenchmarkSwitcher.FromAssembly(assembly).Run(args);
