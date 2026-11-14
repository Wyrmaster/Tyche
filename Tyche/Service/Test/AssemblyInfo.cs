// Every integration test starts real ImageMagick processes (up to 256 MiB + 512 MiB map each); running test
// classes in parallel multiplies that and can exhaust the memory of a developer machine.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
