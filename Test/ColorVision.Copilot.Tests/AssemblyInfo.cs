using Xunit;

// Copilot tests share process-wide registries, confirmation stores, and WPF services.
// Keep test collections serial so one test cannot clear another test's global state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
