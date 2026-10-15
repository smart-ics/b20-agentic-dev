using Xunit;

// Architecture §19.8: Integration tests run against a single shared test database (CakraTestDb).
// Disable test parallelization across test classes to prevent concurrent Respawn resets.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
