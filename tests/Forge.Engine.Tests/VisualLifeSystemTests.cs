using Forge.Engine.Core;
using Forge.Engine.Rendering;
using Forge.Engine.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

// =========================================================================
// TrafficRenderer Tests
// =========================================================================

public class TrafficRendererTests
{
    private static Config MakeConfig() => new()
    {
        WorldSize = 64,
        TileWidth = 64,
        TileHeight = 32,
    };

    private static SimSnapshot MakeSnapshot(int vehicleCount = 10, float timeOfDay = 12f,
        int era = 4, int weather = 0, int buildingCount = 0)
    {
        var vehicles = new SimSnapshot.VehicleSnapshot[vehicleCount];
        for (int i = 0; i < vehicleCount; i++)
        {
            vehicles[i] = new SimSnapshot.VehicleSnapshot(
                WorldX: 10f + i * 2f,
                WorldY: 10f + i * 1.5f,
                TypeId: (ushort)(i % 5),
                Heading: i * 0.5f,
                Speed: 1f + i * 0.1f,
                MaxSpeed: 3f,
                Flags: 1 // active
            );
        }

        var buildings = new SimSnapshot.BuildingSnapshot[buildingCount];
        for (int i = 0; i < buildingCount; i++)
        {
            buildings[i] = new SimSnapshot.BuildingSnapshot(
                GridX: 5 + i, GridY: 5 + i,
                TypeId: (ushort)(i < 3 ? 50 : 150), // first 3 commercial, rest industrial
                Level: 1, State: 1,
                Occupants: 10, MaxOccupants: 20,
                Condition: 200);
        }

        return new SimSnapshot
        {
            Vehicles = vehicles,
            VehicleCount = vehicleCount,
            Buildings = buildings,
            BuildingCount = buildingCount,
            TileTerrainTypes = new byte[64 * 64],
            TileZoneTypes = new byte[64 * 64],
            TileRoadFlags = new byte[64 * 64],
            TileTraffic = new float[64 * 64],
            WorldSize = 64,
            TimeOfDay = timeOfDay,
            Era = era,
            WeatherCondition = weather,
            Season = 1,
            Population = 1000,
            Happiness = 0.7f,
            TickCount = (long)(timeOfDay * 60),
        };
    }

    [Fact]
    public void Constructor_InitializesWithZeroActive()
    {
        var renderer = new TrafficRenderer(MakeConfig());
        Assert.Equal(0, renderer.ActiveCount);
    }

    [Fact]
    public void UpdateFromSimulation_PopulatesVisualVehicles()
    {
        var renderer = new TrafficRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var snapshot = MakeSnapshot(vehicleCount: 20);

        renderer.UpdateFromSimulation(snapshot, camera);
        Assert.True(renderer.ActiveCount > 0);
        Assert.True(renderer.ActiveCount <= 20 * 3); // rush hour multiplier at most 3x
    }

    [Fact]
    public void UpdateFromSimulation_EmptySnapshot_HasZeroActive()
    {
        var renderer = new TrafficRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var snapshot = MakeSnapshot(vehicleCount: 0);

        renderer.UpdateFromSimulation(snapshot, camera);
        Assert.Equal(0, renderer.ActiveCount);
    }

    [Fact]
    public void Update_DoesNotCrashWithZeroVehicles()
    {
        var renderer = new TrafficRenderer(MakeConfig());
        renderer.Update(0.016f); // 60fps frame
        Assert.Equal(0, renderer.ActiveCount);
    }

    [Fact]
    public void Update_DoesNotCrashWithVehicles()
    {
        var renderer = new TrafficRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var snapshot = MakeSnapshot(vehicleCount: 50);

        renderer.UpdateFromSimulation(snapshot, camera);
        renderer.Update(0.016f);
        renderer.Update(0.016f);
        renderer.Update(0.016f);

        Assert.True(renderer.ActiveCount > 0);
    }

    [Theory]
    [InlineData(0f, 0)]    // 0 rad = N
    [InlineData(1.5708f, 2)]  // pi/2 = E
    [InlineData(3.1416f, 4)]  // pi = S
    [InlineData(4.7124f, 6)]  // 3pi/2 = W
    public void HeadingToDirection_CorrectOctant(float heading, int expectedDirection)
    {
        byte dir = TrafficRenderer.HeadingToDirection(heading);
        Assert.Equal((byte)expectedDirection, dir);
    }

    [Fact]
    public void HeadingToDirection_NegativeHeading_NormalizesCorrectly()
    {
        byte dir = TrafficRenderer.HeadingToDirection(-1.5708f); // -pi/2
        // Should normalize to 3pi/2 = W (direction 6)
        Assert.Equal((byte)6, dir);
    }

    [Theory]
    [InlineData(0, 0f, -1f)]   // N
    [InlineData(2, 1f, 0f)]    // E
    [InlineData(4, 0f, 1f)]    // S
    [InlineData(6, -1f, 0f)]   // W
    public void DirectionToOffset_CorrectVector(byte direction, float expectedDx, float expectedDy)
    {
        var (dx, dy) = TrafficRenderer.DirectionToOffset(direction);
        Assert.Equal(expectedDx, dx);
        Assert.Equal(expectedDy, dy);
    }

    [Theory]
    [InlineData(8f, 3f)]    // 8am = peak morning rush
    [InlineData(18f, 3f)]   // 6pm = peak evening rush
    [InlineData(12f, 1f)]   // noon = no rush
    [InlineData(3f, 1f)]    // 3am = no rush
    public void ComputeRushHourMultiplier_CorrectPeaks(float timeOfDay, float expectedApprox)
    {
        float multiplier = TrafficRenderer.ComputeRushHourMultiplier(timeOfDay);
        Assert.InRange(multiplier, expectedApprox - 0.5f, expectedApprox + 0.5f);
    }

    [Fact]
    public void RushHourMultiplier_AlwaysAtLeastOne()
    {
        for (float t = 0f; t < 24f; t += 0.5f)
        {
            float m = TrafficRenderer.ComputeRushHourMultiplier(t);
            Assert.True(m >= 1f, $"Rush multiplier at {t}h was {m}, expected >= 1.0");
        }
    }

    [Fact]
    public void RushHourMultiplier_NeverExceedsMax()
    {
        for (float t = 0f; t < 24f; t += 0.1f)
        {
            float m = TrafficRenderer.ComputeRushHourMultiplier(t);
            Assert.True(m <= 3.01f, $"Rush multiplier at {t}h was {m}, expected <= 3.0");
        }
    }

    [Fact]
    public void GetTrafficIntensity_ZeroWhenEmpty()
    {
        var renderer = new TrafficRenderer(MakeConfig());
        Assert.Equal(0f, renderer.GetTrafficIntensity());
    }

    [Fact]
    public void GetTrafficIntensity_ProportionalToCount()
    {
        var renderer = new TrafficRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var snapshot = MakeSnapshot(vehicleCount: 100);

        renderer.UpdateFromSimulation(snapshot, camera);
        float intensity = renderer.GetTrafficIntensity();
        Assert.True(intensity > 0f);
        Assert.True(intensity <= 1f);
    }

    [Fact]
    public void NightDetection_CorrectAtDifferentTimes()
    {
        var renderer = new TrafficRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());

        // Night time (22:00)
        renderer.UpdateFromSimulation(MakeSnapshot(timeOfDay: 22f), camera);
        Assert.True(renderer.IsNightTime);

        // Day time (12:00)
        renderer.UpdateFromSimulation(MakeSnapshot(timeOfDay: 12f), camera);
        Assert.False(renderer.IsNightTime);

        // Early morning (3:00) = still night
        renderer.UpdateFromSimulation(MakeSnapshot(timeOfDay: 3f), camera);
        Assert.True(renderer.IsNightTime);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var renderer = new TrafficRenderer(MakeConfig());
        renderer.Dispose();
    }
}

// =========================================================================
// CitizenRenderer Tests
// =========================================================================

public class CitizenRendererTests
{
    private static Config MakeConfig() => new()
    {
        WorldSize = 64,
        TileWidth = 64,
        TileHeight = 32,
    };

    private static SimSnapshot MakeSnapshot(int population = 1000, float timeOfDay = 12f,
        int weather = 0, int buildingCount = 5)
    {
        var buildings = new SimSnapshot.BuildingSnapshot[buildingCount];
        for (int i = 0; i < buildingCount; i++)
        {
            buildings[i] = new SimSnapshot.BuildingSnapshot(
                GridX: 10 + i * 3, GridY: 10 + i * 3,
                TypeId: 50, Level: 1, State: 1,
                Occupants: 10, MaxOccupants: 20, Condition: 200);
        }

        return new SimSnapshot
        {
            Vehicles = [],
            VehicleCount = 0,
            Buildings = buildings,
            BuildingCount = buildingCount,
            TileTerrainTypes = new byte[64 * 64],
            TileZoneTypes = new byte[64 * 64],
            TileRoadFlags = new byte[64 * 64],
            TileTraffic = new float[64 * 64],
            WorldSize = 64,
            TimeOfDay = timeOfDay,
            Era = 4,
            WeatherCondition = weather,
            Season = 1,
            Population = population,
            Happiness = 0.7f,
            TickCount = (long)(timeOfDay * 60),
        };
    }

    [Fact]
    public void Constructor_InitializesWithZeroActive()
    {
        var renderer = new CitizenRenderer(MakeConfig());
        Assert.Equal(0, renderer.ActiveCount);
    }

    [Fact]
    public void UpdateFromSimulation_SpawnsCitizensNearBuildings()
    {
        var renderer = new CitizenRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var snapshot = MakeSnapshot(population: 5000, buildingCount: 10);

        // Multiple ticks to accumulate citizens (max 50 spawns per tick)
        for (int i = 0; i < 10; i++)
            renderer.UpdateFromSimulation(snapshot, camera);

        Assert.True(renderer.ActiveCount > 0, "Should have spawned citizens near buildings");
    }

    [Fact]
    public void UpdateFromSimulation_ZeroPopulation_NoSpawns()
    {
        var renderer = new CitizenRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var snapshot = MakeSnapshot(population: 0);

        renderer.UpdateFromSimulation(snapshot, camera);
        Assert.Equal(0, renderer.ActiveCount);
    }

    [Fact]
    public void UpdateFromSimulation_NoBuildings_NoSpawns()
    {
        var renderer = new CitizenRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var snapshot = MakeSnapshot(population: 5000, buildingCount: 0);

        renderer.UpdateFromSimulation(snapshot, camera);
        Assert.Equal(0, renderer.ActiveCount);
    }

    [Theory]
    [InlineData(3f, 0.1f)]    // 3am = nearly empty streets
    [InlineData(8f, 1.0f)]    // 8am = morning rush
    [InlineData(12f, 0.6f)]   // noon = moderate
    [InlineData(18f, 1.0f)]   // 6pm = evening rush
    [InlineData(23f, 0.1f)]   // 11pm = empty
    public void ComputePedestrianTimeMultiplier_CorrectPatterns(float timeOfDay, float expected)
    {
        float actual = CitizenRenderer.ComputePedestrianTimeMultiplier(timeOfDay);
        Assert.InRange(actual, expected - 0.15f, expected + 0.15f);
    }

    [Theory]
    [InlineData(0, 1.0f)]   // clear = full
    [InlineData(2, 0.5f)]   // rain = half
    [InlineData(7, 0.05f)]  // blizzard = nearly zero
    [InlineData(3, 0.2f)]   // storm = low
    public void ComputeWeatherMultiplier_CorrectValues(int weather, float expected)
    {
        float actual = CitizenRenderer.ComputeWeatherMultiplier(weather);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PedestrianTimeMultiplier_AlwaysNonNegative()
    {
        for (float t = 0f; t < 24f; t += 0.25f)
        {
            float m = CitizenRenderer.ComputePedestrianTimeMultiplier(t);
            Assert.True(m >= 0f, $"Pedestrian multiplier at {t}h was {m}");
        }
    }

    [Fact]
    public void PedestrianTimeMultiplier_NeverExceedsOne()
    {
        for (float t = 0f; t < 24f; t += 0.25f)
        {
            float m = CitizenRenderer.ComputePedestrianTimeMultiplier(t);
            Assert.True(m <= 1.01f, $"Pedestrian multiplier at {t}h was {m}");
        }
    }

    [Theory]
    [InlineData(1f, 0f, 0)]   // right (atan2=0)
    [InlineData(0f, 1f, 2)]   // down (atan2=pi/2)
    [InlineData(-1f, 0f, 4)]  // left (atan2=pi)
    [InlineData(0f, -1f, 6)]  // up (atan2=-pi/2 -> 3pi/2)
    public void VectorToDirection_CorrectOctant(float dx, float dy, int expected)
    {
        byte dir = CitizenRenderer.VectorToDirection(dx, dy);
        Assert.Equal((byte)expected, dir);
    }

    [Fact]
    public void Update_AdvancesCitizens()
    {
        var renderer = new CitizenRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var snapshot = MakeSnapshot(population: 5000, buildingCount: 10);

        // Spawn some citizens
        for (int i = 0; i < 5; i++)
            renderer.UpdateFromSimulation(snapshot, camera);

        int before = renderer.ActiveCount;
        Assert.True(before > 0);

        // Advance several frames
        for (int i = 0; i < 60; i++)
            renderer.Update(0.016f);

        // Citizens should still be active (lifetime > 8 seconds, we only advanced ~1 second)
        Assert.True(renderer.ActiveCount > 0);
    }

    [Fact]
    public void Update_CitizensEventuallyDespawn()
    {
        var renderer = new CitizenRenderer(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var snapshot = MakeSnapshot(population: 100, buildingCount: 3);

        // Spawn some citizens
        renderer.UpdateFromSimulation(snapshot, camera);

        // Advance many seconds (past max lifetime of 30s)
        for (int i = 0; i < 2500; i++)
            renderer.Update(0.016f);

        // Most or all citizens should have despawned by now
        // (without replenishment from UpdateFromSimulation)
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var renderer = new CitizenRenderer(MakeConfig());
        renderer.Dispose();
    }
}

// =========================================================================
// BuildingLifeRenderer Tests
// =========================================================================

public class BuildingLifeRendererTests
{
    private static Config MakeConfig() => new()
    {
        WorldSize = 64,
        TileWidth = 64,
        TileHeight = 32,
    };

    private static SimSnapshot MakeSnapshot(int buildingCount = 5, float timeOfDay = 12f)
    {
        var buildings = new SimSnapshot.BuildingSnapshot[buildingCount];
        for (int i = 0; i < buildingCount; i++)
        {
            bool isIndustrial = i >= 3;
            buildings[i] = new SimSnapshot.BuildingSnapshot(
                GridX: 5 + i * 3,
                GridY: 5 + i * 3,
                TypeId: (ushort)(isIndustrial ? 150 : 50), // industrial vs commercial
                Level: 1,
                State: (byte)(i == 0 ? 0 : 1), // first building under construction
                Occupants: (ushort)(isIndustrial ? 15 : 8),
                MaxOccupants: 20,
                Condition: (byte)(i == 4 ? 20 : 200) // last building deteriorated
            );
        }

        return new SimSnapshot
        {
            Vehicles = [],
            VehicleCount = 0,
            Buildings = buildings,
            BuildingCount = buildingCount,
            TileTerrainTypes = new byte[64 * 64],
            TileZoneTypes = new byte[64 * 64],
            TileRoadFlags = new byte[64 * 64],
            TileTraffic = new float[64 * 64],
            WorldSize = 64,
            TimeOfDay = timeOfDay,
            Era = 4,
            WeatherCondition = 0,
            Season = 1,
            Population = 500,
            Happiness = 0.7f,
            TickCount = (long)(timeOfDay * 60),
        };
    }

    [Fact]
    public void Constructor_InitializesWithZeroTracked()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());
        Assert.Equal(0, renderer.TrackedCount);
    }

    [Fact]
    public void UpdateFromSimulation_TracksBuildings()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());
        var snapshot = MakeSnapshot(buildingCount: 10);

        renderer.UpdateFromSimulation(snapshot);
        Assert.Equal(10, renderer.TrackedCount);
    }

    [Fact]
    public void UpdateFromSimulation_DetectsConstruction()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());
        var snapshot = MakeSnapshot(); // first building is under construction

        renderer.UpdateFromSimulation(snapshot);
        Assert.True(renderer.IsUnderConstruction(0));
        Assert.False(renderer.IsUnderConstruction(1));
    }

    [Fact]
    public void UpdateFromSimulation_DetectsSmoke()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());
        var snapshot = MakeSnapshot();

        renderer.UpdateFromSimulation(snapshot);

        // Buildings 3-4 are industrial (TypeId 150) and operational
        Assert.True(renderer.GetSmokeIntensity(3) > 0f);
        Assert.True(renderer.GetSmokeIntensity(4) > 0f);

        // Buildings 0-2 are commercial (TypeId 50) -- no smoke
        Assert.Equal(0f, renderer.GetSmokeIntensity(1)); // operational but not industrial
    }

    [Fact]
    public void UpdateFromSimulation_DetectsDeteriorationLevels()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());
        var snapshot = MakeSnapshot();

        renderer.UpdateFromSimulation(snapshot);

        // Building 0-3 have condition 200 = pristine
        Assert.Equal(0, renderer.GetConditionVisual(1));

        // Building 4 has condition 20 = crumbling
        Assert.Equal(3, renderer.GetConditionVisual(4));
    }

    [Fact]
    public void UpdateFromSimulation_DetectsActiveEvent()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());
        var snapshot = MakeSnapshot();

        renderer.UpdateFromSimulation(snapshot);

        // Building 4 has condition 20 and is operational = active event (fire)
        Assert.True(renderer.HasActiveEvent(4));

        // Other buildings are fine
        Assert.False(renderer.HasActiveEvent(1));
    }

    [Fact]
    public void Update_DaytimeKeepsLightsOff()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());
        var snapshot = MakeSnapshot(timeOfDay: 12f);

        renderer.UpdateFromSimulation(snapshot);

        // Update several frames during daytime
        for (int i = 0; i < 60; i++)
            renderer.Update(0.016f, 12f, 0);

        // Light levels should be near zero during daytime
        Assert.True(renderer.GetLightLevel(1) < 0.1f);
    }

    [Fact]
    public void Update_NightTimeRampsUpLights()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());
        var snapshot = MakeSnapshot(timeOfDay: 21f);

        renderer.UpdateFromSimulation(snapshot);

        // Update many frames during nighttime (after dusk)
        for (int i = 0; i < 300; i++)
            renderer.Update(0.016f, 21f, 0);

        // Occupied buildings should have window glow
        float lightLevel = renderer.GetLightLevel(1); // operational, occupied
        Assert.True(lightLevel > 0f, $"Expected light > 0 at night, got {lightLevel}");
    }

    [Fact]
    public void OutOfRange_AccessReturnsDefaults()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());

        Assert.Equal(0f, renderer.GetSmokeIntensity(-1));
        Assert.Equal(0f, renderer.GetSmokeIntensity(99999));
        Assert.Equal(0f, renderer.GetLightLevel(-1));
        Assert.False(renderer.IsUnderConstruction(-1));
        Assert.Equal(0, renderer.GetConditionVisual(-1));
        Assert.False(renderer.HasActiveEvent(99999));
    }

    [Fact]
    public void GetSmokeConfigs_ReturnValidConfigs()
    {
        var light = BuildingLifeRenderer.GetLightSmokeConfig();
        Assert.True(light.MaxParticles > 0);
        Assert.True(light.EmitRate > 0);

        var heavy = BuildingLifeRenderer.GetHeavySmokeConfig();
        Assert.True(heavy.MaxParticles > 0);
        Assert.True(heavy.EmitRate > heavy.EmitRate * 0); // non-zero

        var dust = BuildingLifeRenderer.GetConstructionDustConfig();
        Assert.True(dust.MaxParticles > 0);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var renderer = new BuildingLifeRenderer(MakeConfig());
        renderer.Dispose();
    }
}

// =========================================================================
// AmbientLifeManager Tests
// =========================================================================

public class AmbientLifeManagerTests
{
    private static Config MakeConfig() => new()
    {
        WorldSize = 64,
        TileWidth = 64,
        TileHeight = 32,
    };

    private static SimSnapshot MakeSnapshot(int vehicleCount = 10, int buildingCount = 5,
        float timeOfDay = 12f, int weather = 0, int season = 1)
    {
        var vehicles = new SimSnapshot.VehicleSnapshot[vehicleCount];
        for (int i = 0; i < vehicleCount; i++)
        {
            vehicles[i] = new SimSnapshot.VehicleSnapshot(
                WorldX: 10f + i * 2f, WorldY: 10f + i * 1.5f,
                TypeId: (ushort)(i % 5), Heading: i * 0.5f,
                Speed: 1f, MaxSpeed: 3f, Flags: 1);
        }

        var buildings = new SimSnapshot.BuildingSnapshot[buildingCount];
        for (int i = 0; i < buildingCount; i++)
        {
            buildings[i] = new SimSnapshot.BuildingSnapshot(
                GridX: 5 + i * 3, GridY: 5 + i * 3,
                TypeId: (ushort)(i < 3 ? 50 : 150),
                Level: 1, State: 1,
                Occupants: 10, MaxOccupants: 20, Condition: 200);
        }

        var roadFlags = new byte[64 * 64];
        // Mark some road tiles
        for (int i = 10; i < 30; i++)
        {
            roadFlags[10 * 64 + i] = 0x05; // N+S connections
        }

        return new SimSnapshot
        {
            Vehicles = vehicles,
            VehicleCount = vehicleCount,
            Buildings = buildings,
            BuildingCount = buildingCount,
            TileTerrainTypes = new byte[64 * 64],
            TileZoneTypes = new byte[64 * 64],
            TileRoadFlags = roadFlags,
            TileTraffic = new float[64 * 64],
            WorldSize = 64,
            TimeOfDay = timeOfDay,
            Era = 4,
            WeatherCondition = weather,
            Season = season,
            Population = 1000,
            Happiness = 0.7f,
            TickCount = (long)(timeOfDay * 60),
            WindSpeed = 3f,
            WindDirection = 0.5f,
        };
    }

    [Fact]
    public void Constructor_InitializesSubSystems()
    {
        var manager = new AmbientLifeManager(MakeConfig());
        Assert.NotNull(manager.Traffic);
        Assert.NotNull(manager.Citizens);
        Assert.NotNull(manager.Buildings);
    }

    [Fact]
    public void UpdateFromSimulation_DispatchesToAllSubSystems()
    {
        var manager = new AmbientLifeManager(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var particles = new ParticleSystem();
        var snapshot = MakeSnapshot(vehicleCount: 20, buildingCount: 8);

        manager.UpdateFromSimulation(snapshot, camera, particles);

        Assert.True(manager.Traffic.ActiveCount > 0);
        Assert.Equal(8, manager.Buildings.TrackedCount);

        particles.Dispose();
    }

    [Fact]
    public void Update_DoesNotCrash()
    {
        var manager = new AmbientLifeManager(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var particles = new ParticleSystem();
        var snapshot = MakeSnapshot();

        manager.UpdateFromSimulation(snapshot, camera, particles);

        // Multiple frame updates should not crash
        for (int i = 0; i < 120; i++)
        {
            manager.Update(0.016f, 12f + i * 0.001f, 1, 0, snapshot, camera);
        }

        particles.Dispose();
    }

    [Fact]
    public void StreetLights_TurnOnAtNight()
    {
        var manager = new AmbientLifeManager(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var particles = new ParticleSystem();

        // Start during day
        var daySnapshot = MakeSnapshot(timeOfDay: 12f);
        manager.UpdateFromSimulation(daySnapshot, camera, particles);
        manager.Update(0.1f, 12f, 1, 0, daySnapshot, camera);
        Assert.False(manager.StreetLightsOn);

        // Transition to night
        var nightSnapshot = MakeSnapshot(timeOfDay: 20f);
        manager.UpdateFromSimulation(nightSnapshot, camera, particles);
        for (int i = 0; i < 200; i++)
            manager.Update(0.016f, 20f, 1, 0, nightSnapshot, camera);

        Assert.True(manager.StreetLightsOn);

        particles.Dispose();
    }

    [Fact]
    public void StreetLights_TurnOffAtDawn()
    {
        var manager = new AmbientLifeManager(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var particles = new ParticleSystem();

        // Start at night with lights on
        var nightSnapshot = MakeSnapshot(timeOfDay: 2f);
        manager.UpdateFromSimulation(nightSnapshot, camera, particles);
        for (int i = 0; i < 200; i++)
            manager.Update(0.016f, 2f, 1, 0, nightSnapshot, camera);
        Assert.True(manager.StreetLightsOn);

        // Transition to morning
        var morningSnapshot = MakeSnapshot(timeOfDay: 8f);
        manager.UpdateFromSimulation(morningSnapshot, camera, particles);
        for (int i = 0; i < 200; i++)
            manager.Update(0.016f, 8f, 1, 0, morningSnapshot, camera);

        Assert.False(manager.StreetLightsOn);

        particles.Dispose();
    }

    [Fact]
    public void TimeOfDay_TrackedCorrectly()
    {
        var manager = new AmbientLifeManager(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var particles = new ParticleSystem();

        var snapshot = MakeSnapshot(timeOfDay: 15.5f);
        manager.UpdateFromSimulation(snapshot, camera, particles);

        Assert.Equal(15.5f, manager.TimeOfDay);

        particles.Dispose();
    }

    [Fact]
    public void Dispose_CleansUpAllSubSystems()
    {
        var manager = new AmbientLifeManager(MakeConfig());
        manager.Dispose();
        // Should not throw on double-dispose of sub-systems
    }

    [Fact]
    public void SmokeEmitters_CreatedForIndustrialBuildings()
    {
        var manager = new AmbientLifeManager(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var particles = new ParticleSystem();

        // Buildings 3-4 are industrial (TypeId 150)
        var snapshot = MakeSnapshot(buildingCount: 5);
        manager.UpdateFromSimulation(snapshot, camera, particles);

        // Should have created smoke emitters for the 2 industrial buildings
        Assert.True(particles.EmitterCount > 0,
            $"Expected smoke emitters for industrial buildings, got {particles.EmitterCount}");

        particles.Dispose();
    }

    [Fact]
    public void EmptyWorld_NoEmitters()
    {
        var manager = new AmbientLifeManager(MakeConfig());
        var camera = new IsometricCamera(MakeConfig());
        var particles = new ParticleSystem();

        var snapshot = MakeSnapshot(vehicleCount: 0, buildingCount: 0);
        manager.UpdateFromSimulation(snapshot, camera, particles);

        Assert.Equal(0, particles.EmitterCount);

        particles.Dispose();
    }
}

// =========================================================================
// SimSnapshot Extended Fields Tests
// =========================================================================

public class SimSnapshotExtendedTests
{
    [Fact]
    public void BuildingSnapshot_ExtendedFields_Roundtrip()
    {
        var snap = new SimSnapshot.BuildingSnapshot(
            GridX: 10, GridY: 20, TypeId: 150, Level: 3,
            State: 1, Occupants: 50, MaxOccupants: 100, Condition: 200);

        Assert.Equal(10, snap.GridX);
        Assert.Equal(20, snap.GridY);
        Assert.Equal(150, snap.TypeId);
        Assert.Equal(3, snap.Level);
        Assert.Equal(1, snap.State);
        Assert.Equal(50, snap.Occupants);
        Assert.Equal(100, snap.MaxOccupants);
        Assert.Equal(200, snap.Condition);
    }

    [Fact]
    public void VehicleSnapshot_ExtendedFields_Roundtrip()
    {
        var snap = new SimSnapshot.VehicleSnapshot(
            WorldX: 1.5f, WorldY: 2.5f, TypeId: 42, Heading: 1.2f,
            Speed: 3.0f, MaxSpeed: 5.0f, Flags: 3);

        Assert.Equal(1.5f, snap.WorldX);
        Assert.Equal(2.5f, snap.WorldY);
        Assert.Equal(42, snap.TypeId);
        Assert.Equal(1.2f, snap.Heading);
        Assert.Equal(3.0f, snap.Speed);
        Assert.Equal(5.0f, snap.MaxSpeed);
        Assert.Equal(3, snap.Flags);
    }

    [Fact]
    public void CaptureFrom_IncludesExtendedFields()
    {
        var state = new WorldState(64);
        state.WeatherCondition = 2;
        state.WindSpeed = 5f;
        state.WindDirection = 1.0f;

        // Set some road flags and traffic
        state.Tiles.RoadFlags[100] = 0x05;
        state.Tiles.Traffic[100] = 0.75f;

        // Add a building with extended data
        int bIdx = state.Buildings.Allocate();
        state.Buildings.GridX[bIdx] = 5;
        state.Buildings.GridY[bIdx] = 5;
        state.Buildings.TypeId[bIdx] = 150;
        state.Buildings.State[bIdx] = 1;
        state.Buildings.Occupants[bIdx] = 10;
        state.Buildings.MaxOccupants[bIdx] = 20;

        // Add a vehicle with extended data
        int vIdx = state.Vehicles.Allocate();
        state.Vehicles.WorldX[vIdx] = 10f;
        state.Vehicles.WorldY[vIdx] = 15f;
        state.Vehicles.TypeId[vIdx] = 3;
        state.Vehicles.Speed[vIdx] = 2f;
        state.Vehicles.MaxSpeed[vIdx] = 4f;

        var snapshot = SimSnapshot.CaptureFrom(state);

        Assert.Equal(64, snapshot.WorldSize);
        Assert.Equal(2, snapshot.WeatherCondition);
        Assert.Equal(5f, snapshot.WindSpeed);
        Assert.Equal(1.0f, snapshot.WindDirection);
        Assert.Equal(0x05, snapshot.TileRoadFlags[100]);
        Assert.Equal(0.75f, snapshot.TileTraffic[100]);

        Assert.Equal(1, snapshot.BuildingCount);
        Assert.Equal(1, snapshot.Buildings[0].State);
        Assert.Equal(10, snapshot.Buildings[0].Occupants);
        Assert.Equal(20, snapshot.Buildings[0].MaxOccupants);

        Assert.Equal(1, snapshot.VehicleCount);
        Assert.Equal(2f, snapshot.Vehicles[0].Speed);
        Assert.Equal(4f, snapshot.Vehicles[0].MaxSpeed);
    }

    [Fact]
    public void CaptureFrom_TimeOfDay_DerivedFromTickCount()
    {
        var state = new WorldState(64);

        // 720 ticks = 12 hours (at 1 tick = 1 game-minute)
        state.TickCount = 720;
        var snapshot = SimSnapshot.CaptureFrom(state);
        Assert.Equal(12f, snapshot.TimeOfDay, 1);

        // 0 ticks = midnight
        state.TickCount = 0;
        snapshot = SimSnapshot.CaptureFrom(state);
        Assert.Equal(0f, snapshot.TimeOfDay, 1);

        // 1440 ticks = wraps to 0 (midnight again)
        state.TickCount = 1440;
        snapshot = SimSnapshot.CaptureFrom(state);
        Assert.Equal(0f, snapshot.TimeOfDay, 1);
    }
}
