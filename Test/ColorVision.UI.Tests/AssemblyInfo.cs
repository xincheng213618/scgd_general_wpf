using Xunit;

// Several production registries reflect over newly loaded assemblies while holding process-wide locks.
// Keep test collections serial until those AssemblyLoad callbacks are made lock-free.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
