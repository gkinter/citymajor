using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Serializes tests that call <see cref="SimHost.Init"/> — map generation mutates
/// <c>SimplexNoise</c> static permutation tables and is not thread-safe.
/// </summary>
[CollectionDefinition("SimHost", DisableParallelization = true)]
public sealed class SimHostTestCollection;
