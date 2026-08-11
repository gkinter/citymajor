using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Input;
using Forge.Engine.Rendering;
using Forge.Engine.Simulation;
using Forge.SimCore;

namespace Forge.Game;

/// <summary>
/// Iron &amp; Oak game implementation. Wires up game-specific simulation systems,
/// UI panels, tools, and engine subsystems on top of the Forge Engine.
/// </summary>
public sealed class IronAndOakGame
{
    private readonly ForgeApp _app;
    private readonly Config _config;

    private SimulationLoop? _simLoop;
    private IsometricCamera? _camera;
    private ChunkManager? _chunks;

    // Rendering subsystems
    private ChunkRenderer? _chunkRenderer;
    private ShaderProgram? _terrainShader;
    private TextureAtlas? _terrainAtlas;
    private ProceduralTerrainTextures? _terrainTextures;

    // Building rendering (procedural isometric boxes)
    private BuildingRenderer? _buildingRenderer;
    private SpriteRenderer? _buildingSpriteRenderer;
    private ShaderProgram? _buildingSpriteShader;
    private float _nightStrength;

    // Minimap and overlay systems
    private MinimapRenderer? _minimapRenderer;
    private GameOverlaySystem? _overlaySystem;

    // Engine subsystems
    private EventBus? _eventBus;
    private ToolSystem? _toolSystem;

    // Game-specific systems (registered on the simulation loop)
    private Simulation.EconomySystem? _economy;
    private Simulation.PopulationSystem? _population;
    private Simulation.TrafficSystem? _traffic;
    private Simulation.ServiceSystem? _services;
    private Simulation.ZoneGrowthSystem? _zoneGrowth;
    private Simulation.BudgetSystem? _budgetSystem;
    private Simulation.PoliticsSystem? _politicsSystem;
    private Simulation.ResearchSystem? _researchSystem;
    private Simulation.EventSystem? _eventSystem;
    private Simulation.HousingHeraldSystem? _housingHerald;
    private Simulation.ApprovalHeraldSystem? _approvalHerald;
    private Simulation.LawSystem? _lawSystem;
    private Simulation.CulturalDNASystem? _culturalDna;

    // Yearly tick tracking (CulturalDNA ticks once per game year)
    private int _lastCulturalDnaYear;

    // UI panels
    private UI.HudPanel? _hudPanel;
    private UI.ToolbarPanel? _toolbarPanel;
    private UI.BudgetPanel? _budgetPanel;
    private UI.ResearchPanel? _researchPanel;
    private UI.PoliticsPanel? _politicsPanel;

    // Debug overlay toggle
    private bool _showDebugPanel = true;

    public IronAndOakGame(Config config)
    {
        _config = config;
        _app = new ForgeApp(config);
    }

    public void Run()
    {
        _app.OnInitialized += OnInitialized;
        _app.OnInputProcessed += OnInputProcessed;
        _app.OnUpdate += OnUpdate;
        _app.OnRenderGame += OnRender;
        _app.OnRenderUI += OnRenderUI;
        _app.OnShutdown += OnShutdown;

        _app.Run();
    }

    private void OnInitialized()
    {
        // Create camera
        _camera = new IsometricCamera(_config);

        // Create chunk manager
        _chunks = new ChunkManager(_config.WorldSize, _config.ChunkSize);
        _chunks.MarkAllDirty();

        // Create EventBus for cross-system communication
        _eventBus = new EventBus();

        // Create and start simulation
        _simLoop = new SimulationLoop(_config);

        // Initialize game systems
        _economy = new Simulation.EconomySystem();
        _population = new Simulation.PopulationSystem();
        _traffic = new Simulation.TrafficSystem();
        _services = new Simulation.ServiceSystem(_config.WorldSize);
        _zoneGrowth = new Simulation.ZoneGrowthSystem(_config.WorldSize);
        _budgetSystem = new Simulation.BudgetSystem();
        _politicsSystem = new Simulation.PoliticsSystem();
        _researchSystem = new Simulation.ResearchSystem();
        _eventSystem = new Simulation.EventSystem(seed: 12345);
        _housingHerald = new Simulation.HousingHeraldSystem();
        _approvalHerald = new Simulation.ApprovalHeraldSystem();
        _lawSystem = new Simulation.LawSystem();
        _culturalDna = new Simulation.CulturalDNASystem();

        // Wire budget system event bus
        _budgetSystem.SetEventBus(_eventBus);

        // Load data files for event definitions and tech trees
        LoadGameData();

        // Set initial cultural DNA to a default regional preset
        Simulation.CulturalDNASystem.ApplyPreset(_simLoop.State, "western_european");
        _lastCulturalDnaYear = _simLoop.State.Year;

        // Seed starting population so the city has life from the very start
        SeedStartingPopulation(_simLoop.State);

        // Wire simulation events -- all systems connected to the correct tick tier
        _simLoop.OnTrafficTick += (state, dt) => _traffic!.Tick(state, dt);

        _simLoop.OnDayTick += (state, dt) =>
        {
            // Economy daily: collect orders, resolve prices, match supply/demand
            _economy!.DailyTick(state, dt);

            // Services daily: update coverage tiles, fire risk, crime
            _services!.DailyTick(state, dt);

            // Zone growth daily: spawn/abandon buildings based on RCI demand
            _zoneGrowth!.Tick(state, _economy);

            // Events daily: evaluate triggers, spawn new events, advance phases
            _eventSystem!.DailyTick(state);
            _eventSystem.UpdateEvents(state, 1f);

            // Apply active event effects to WorldState each day
            ApplyEventEffectsToState(state);

            // Tick event durations on WorldState (decrement remaining days, remove expired)
            state.TickEvents();

            // Politics daily: decay decision impacts, update protests
            _politicsSystem!.DailyTick(state, dt);
        };

        _simLoop.OnMonthTick += (state, dt) =>
        {
            // Services monthly: recalculate power/water grids, pollution, noise, crime
            _services!.MonthlyTick(state);

            // Feed cross-system data BEFORE systems that consume it
            FeedCrossSystemData(state);

            // Population monthly: births, deaths, migration, employment, satisfaction
            _population!.MonthlyTick(state);

            // Economy monthly (backward compat, mostly no-op with BudgetSystem)
            _economy!.MonthlyTick(state, dt);

            // Zone growth monthly: recalculate land value, check building upgrades
            _zoneGrowth!.RecalculateLandValue(state);
            _zoneGrowth.CheckUpgrades(state);

            // Budget monthly: calculate all revenue/expenses, update treasury
            _budgetSystem!.CalculateMonthlyBudget(state, _economy);

            // Check bankruptcy condition
            if (_budgetSystem.IsBankrupt)
            {
                Console.WriteLine($"[IronAndOak] WARNING: City is bankrupt! Funds={state.CityFunds}, Loans={state.LoanBalance}");
            }

            // Politics monthly: recalculate approval, corruption, check elections
            _politicsSystem!.MonthlyTick(state, dt);

            // Research monthly: generate RP, advance queue, complete techs, check era
            _researchSystem!.MonthlyTick(state, dt);

            _housingHerald!.MonthlyTick(
                state,
                _eventSystem!,
                state.MeanRentBurden,
                _economy!.ResidentialDemand);
            _approvalHerald!.MonthlyTick(state, _eventSystem!);

            // Cultural DNA yearly: drift dimensions, detect archetypes (runs once per year)
            if (state.Year > _lastCulturalDnaYear)
            {
                _culturalDna!.YearlyTick(state);
                _lastCulturalDnaYear = state.Year;
            }
        };

        // Subscribe to OnCommand to dispatch player commands to modify WorldState
        _simLoop.OnCommand += HandleCommand;

        // Initialize ToolSystem with engine tools, chunk manager, and command queue
        _toolSystem = new ToolSystem();
        _toolSystem.Chunks = _chunks;
        _toolSystem.Commands = _simLoop.Commands;
        var tiles = _simLoop.State.Tiles;
        _toolSystem.RegisterTool(
            new Forge.Engine.Input.RoadTool(_toolSystem, tiles, "Road", "icon_road", roadLevel: 1, costPerTile: 100));
        _toolSystem.RegisterTool(
            new Forge.Engine.Input.ZoneTool(_toolSystem, tiles, "Residential", "icon_res",
                zoneType: 1, density: 1, costPerTile: 50));
        _toolSystem.RegisterTool(
            new Forge.Engine.Input.ZoneTool(_toolSystem, tiles, "Commercial", "icon_com",
                zoneType: 2, density: 1, costPerTile: 75));
        _toolSystem.RegisterTool(
            new Forge.Engine.Input.ZoneTool(_toolSystem, tiles, "Industrial", "icon_ind",
                zoneType: 3, density: 1, costPerTile: 60));
        _toolSystem.RegisterTool(
            new Forge.Engine.Input.BulldozeTool(_toolSystem, tiles));

        // Initialize UI panels with system references
        _hudPanel = new UI.HudPanel { Time = _app.Time };
        _toolbarPanel = new UI.ToolbarPanel { Tools = _toolSystem };
        _budgetPanel = new UI.BudgetPanel
        {
            Budget = _budgetSystem,
            Commands = _simLoop.Commands
        };
        _researchPanel = new UI.ResearchPanel
        {
            Research = _researchSystem,
            Commands = _simLoop.Commands,
            State = _simLoop.State
        };
        _politicsPanel = new UI.PoliticsPanel
        {
            Politics = _politicsSystem,
            Commands = _simLoop.Commands
        };

        // Generate initial terrain using the full MapGenerator
        GenerateTerrain(_simLoop.State);

        // Initialize rendering pipeline
        InitRendering();

        // Start simulation thread
        _simLoop.Start();

        Console.WriteLine("[IronAndOak] Game initialized.");
    }

    /// <summary>
    /// Dispatch player commands from the simulation thread to modify WorldState.
    /// This is the bridge between the command queue and actual game-state mutations.
    /// </summary>
    private void HandleCommand(WorldState state, CommandQueue.Command cmd)
    {
        switch (cmd.Type)
        {
            case CommandQueue.CommandType.PlaceRoad:
            {
                int x0 = cmd.X, y0 = cmd.Y;
                int x1 = cmd.Width, y1 = cmd.Height;

                int dx = x1 > x0 ? 1 : (x1 < x0 ? -1 : 0);
                for (int x = x0; x != x1 + dx; x += dx == 0 ? 1 : dx)
                {
                    if (state.Tiles.InBounds(x, y0))
                    {
                        int idx = state.Tiles.Index(x, y0);
                        state.Tiles.RoadFlags[idx] = 1;
                    }
                    if (dx == 0) break;
                }
                int dy = y1 > y0 ? 1 : (y1 < y0 ? -1 : 0);
                for (int y = y0; y != y1 + dy; y += dy == 0 ? 1 : dy)
                {
                    if (state.Tiles.InBounds(x1, y))
                    {
                        int idx = state.Tiles.Index(x1, y);
                        state.Tiles.RoadFlags[idx] = 1;
                    }
                    if (dy == 0) break;
                }

                _eventBus?.Publish(new RoadBuiltEvent { TileX = x0, TileY = y0, RoadType = 1 });
                _chunks?.MarkAllDirty();
                break;
            }

            case CommandQueue.CommandType.PlaceZone:
            {
                byte zoneType = (byte)cmd.DataId;
                for (int dy = 0; dy < cmd.Height; dy++)
                {
                    for (int dx = 0; dx < cmd.Width; dx++)
                    {
                        int tx = cmd.X + dx;
                        int ty = cmd.Y + dy;
                        if (state.Tiles.InBounds(tx, ty))
                        {
                            int idx = state.Tiles.Index(tx, ty);
                            byte oldZone = state.Tiles.ZoneType[idx];
                            state.Tiles.ZoneType[idx] = zoneType;
                            _eventBus?.Publish(new ZoneChangedEvent
                            {
                                TileX = tx, TileY = ty,
                                OldZone = oldZone, NewZone = zoneType
                            });
                        }
                    }
                }
                _chunks?.MarkAllDirty();
                break;
            }

            case CommandQueue.CommandType.PlaceBuilding:
            {
                int bIdx = state.Buildings.Allocate();
                if (bIdx >= 0)
                {
                    state.Buildings.GridX[bIdx] = cmd.X;
                    state.Buildings.GridY[bIdx] = cmd.Y;
                    state.Buildings.TypeId[bIdx] = cmd.DataId;
                    state.Buildings.State[bIdx] = 1; // operational

                    // Mark tile as occupied by this building
                    if (state.Tiles.InBounds(cmd.X, cmd.Y))
                    {
                        state.Tiles.BuildingId[state.Tiles.Index(cmd.X, cmd.Y)] = (ushort)bIdx;
                    }

                    _eventBus?.Publish(new BuildingPlacedEvent
                    {
                        BuildingId = bIdx,
                        TileX = cmd.X, TileY = cmd.Y,
                        TypeId = cmd.DataId
                    });
                    _chunks?.MarkAllDirty();
                }
                break;
            }

            case CommandQueue.CommandType.Bulldoze:
            {
                for (int dy = 0; dy < cmd.Height; dy++)
                {
                    for (int dx = 0; dx < cmd.Width; dx++)
                    {
                        int tx = cmd.X + dx;
                        int ty = cmd.Y + dy;
                        if (state.Tiles.InBounds(tx, ty))
                        {
                            int idx = state.Tiles.Index(tx, ty);

                            // Free building occupying this tile
                            ushort buildingId = state.Tiles.BuildingId[idx];
                            if (buildingId != 0 && buildingId < state.Buildings.Capacity
                                && state.Buildings.IsActive(buildingId))
                            {
                                state.Buildings.Free(buildingId);
                            }

                            state.Tiles.BuildingId[idx] = 0;
                            state.Tiles.ZoneType[idx] = 0;
                            state.Tiles.RoadFlags[idx] = 0;
                        }
                    }
                }
                _chunks?.MarkAllDirty();
                break;
            }

            case CommandQueue.CommandType.SetTaxRate:
            {
                float rate = System.Math.Clamp(cmd.DataValue, 0f, 1f);
                switch (cmd.DataId)
                {
                    case 0: state.PropertyTaxRate = rate; break;
                    case 1: state.CommercialTaxRate = rate; break;
                    case 2: state.IndustrialTaxRate = rate; break;
                }
                break;
            }

            case CommandQueue.CommandType.SetResearch:
            {
                _researchSystem?.EnqueueResearch(cmd.DataId, state);
                break;
            }

            case CommandQueue.CommandType.TakeLoan:
            {
                long amount = (long)cmd.DataValue;
                if (amount > 0 && state.LoanBalance + amount <= 1_000_000)
                {
                    state.LoanBalance += amount;
                    state.CityFunds += amount;
                }
                break;
            }

            case CommandQueue.CommandType.RepayLoan:
            {
                long repayAmount = (long)cmd.DataValue;
                repayAmount = System.Math.Min(repayAmount, state.LoanBalance);
                repayAmount = System.Math.Min(repayAmount, state.CityFunds);
                if (repayAmount > 0)
                {
                    state.LoanBalance -= repayAmount;
                    state.CityFunds -= repayAmount;
                }
                break;
            }

            case CommandQueue.CommandType.RepealLaw:
            {
                _politicsSystem?.RepealLaw(cmd.DataId);
                break;
            }

            case CommandQueue.CommandType.ToggleOrdinance:
            {
                state.ToggleOrdinance(cmd.DataId);
                break;
            }

            case CommandQueue.CommandType.SetGameSpeed:
            {
                _simLoop?.SetSpeed(cmd.DataId);
                break;
            }

            case CommandQueue.CommandType.SetPolicy:
            {
                state.ToggleOrdinance(cmd.DataId);
                break;
            }

            case CommandQueue.CommandType.Terraform:
            {
                if (state.Tiles.InBounds(cmd.X, cmd.Y))
                {
                    int idx = state.Tiles.Index(cmd.X, cmd.Y);
                    state.Tiles.TerrainType[idx] = (byte)cmd.DataId;
                    _chunks?.MarkAllDirty();
                }
                break;
            }

            case CommandQueue.CommandType.SetBudget:
            case CommandQueue.CommandType.ProposeLaw:
            {
                // These command types are placeholders for future UI integration
                break;
            }
        }
    }

    // =========================================================================
    // Game data loading
    // =========================================================================

    /// <summary>
    /// Load JSON data files for event definitions and tech trees.
    /// Silently skips missing files so the game can run without data packs.
    /// </summary>
    private void LoadGameData()
    {
        string eventsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "base", "data", "events", "events.json");
        if (File.Exists(eventsPath))
        {
            try
            {
                _eventSystem!.LoadDefinitions(eventsPath);
                Console.WriteLine($"[IronAndOak] Loaded event definitions from {eventsPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IronAndOak] WARNING: Failed to load events: {ex.Message}");
            }
        }

        string techPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "base", "data", "tech", "technologies.json");
        if (File.Exists(techPath))
        {
            try
            {
                _researchSystem!.LoadFromFile(techPath);
                Console.WriteLine($"[IronAndOak] Loaded {_researchSystem.TechCount} technologies from {techPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IronAndOak] WARNING: Failed to load tech tree: {ex.Message}");
            }
        }

        string lawsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "base", "data", "laws", "laws.json");
        if (File.Exists(lawsPath))
        {
            try
            {
                _lawSystem!.LoadFromFile(lawsPath);
                Console.WriteLine($"[IronAndOak] Loaded {_lawSystem.DefinitionCount} laws from {lawsPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IronAndOak] WARNING: Failed to load laws: {ex.Message}");
            }
        }
    }

    // =========================================================================
    // Starting population seed
    // =========================================================================

    /// <summary>
    /// Seed the game with an initial population of ~100 households so the city
    /// has visible economic activity from the very start.
    /// </summary>
    private static void SeedStartingPopulation(WorldState state)
    {
        const int initialHouseholds = 100;
        var rng = new Random(42);

        for (int i = 0; i < initialHouseholds; i++)
        {
            int slot = state.Households.Allocate();
            if (slot < 0) break;

            state.Households.MemberCount[slot] = (byte)rng.Next(1, 5);
            state.Households.AgeGroup[slot] = 1; // working age
            state.Households.Education[slot] = (byte)rng.Next(0, 4);
            state.Households.Income[slot] = 1500 + rng.Next(0, 3000);
            state.Households.Savings[slot] = rng.Next(500, 10000);
            state.Households.WealthLevel[slot] = (byte)rng.Next(1, 4);
            state.Households.Happiness[slot] = 160; // slightly above neutral
            state.Households.HealthSatisfaction[slot] = 150;
            state.Households.SafetySatisfaction[slot] = 150;
            state.Households.TransportSatisfaction[slot] = 128;
            state.Households.LeisureSatisfaction[slot] = 128;
        }

        // Update population count from household member counts
        int totalPop = 0;
        for (int i = 0; i < state.Households.Capacity; i++)
        {
            if (state.Households.IsActive(i))
                totalPop += state.Households.MemberCount[i];
        }
        state.Population = totalPop;
        state.Happiness = 0.6f;

        Console.WriteLine($"[IronAndOak] Seeded {initialHouseholds} households, population={totalPop}");
    }

    // =========================================================================
    // Cross-system data wiring
    // =========================================================================

    /// <summary>
    /// Feed data between systems that read from shared WorldState or from each other.
    /// Called at the start of each monthly tick to ensure all inputs are current.
    /// </summary>
    private void FeedCrossSystemData(WorldState state)
    {
        // --- Politics inputs from other systems ---
        float fundsRatio = Math.Clamp(state.CityFunds / 100_000f, 0f, 1f);
        float employmentRate = CalculateEmploymentRate(state);
        _politicsSystem!.EconomyScore = fundsRatio * 0.5f + employmentRate * 0.5f;

        _politicsSystem.ServiceScore = CalculateAverageServiceScore(state);

        float avgCrime = CalculateAverageCrime(state);
        _politicsSystem.SafetyScore = Math.Clamp(1f - avgCrime, 0f, 1f);

        // --- Research system inputs: count research-relevant buildings ---
        int libraryCount = 0, universityCount = 0, heavyIndustryCount = 0;
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != 1) continue; // operational only

            uint flags = buildings.ServiceFlags[i];

            if ((flags & (1u << 5)) != 0) // ServiceEducation
            {
                if (buildings.Level[i] >= 3)
                    universityCount++;
                else
                    libraryCount++;
            }

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (tiles.InBounds(bx, by))
            {
                byte zone = tiles.ZoneType[tiles.Index(bx, by)];
                if (zone == 4 && buildings.Level[i] >= 3) // Industrial
                    heavyIndustryCount++;
            }
        }

        _researchSystem!.LibraryCount = libraryCount;
        _researchSystem.UniversityCount = universityCount;
        _researchSystem.HeavyIndustryCount = heavyIndustryCount;

        int educatedPop = 0;
        for (int i = 0; i < state.Households.Capacity; i++)
        {
            if (!state.Households.IsActive(i)) continue;
            if (state.Households.Education[i] >= 2)
                educatedPop += state.Households.MemberCount[i];
        }
        _researchSystem.EducatedPopulation = educatedPop;
        _researchSystem.EducationLevelMultiplier =
            EducationProgression.ResearchEducationMultiplier(state.MeanEducationLevel);

        // --- Budget system: sync tax rates from WorldState ---
        _budgetSystem!.PropertyTaxRate = state.PropertyTaxRate;
        _budgetSystem.CommercialTaxRate = state.CommercialTaxRate;
        _budgetSystem.IndustrialTaxRate = state.IndustrialTaxRate;
    }

    /// <summary>
    /// Apply aggregate effects from active game events to WorldState (Cathedral P6.2).
    /// </summary>
    private void ApplyEventEffectsToState(WorldState state)
    {
        _eventSystem?.ApplyEffectsToState(state);
    }

    private static float CalculateEmploymentRate(WorldState state)
    {
        int employed = 0, laborForce = 0;
        for (int i = 0; i < state.Households.Capacity; i++)
        {
            if (!state.Households.IsActive(i)) continue;
            if (state.Households.AgeGroup[i] != 1) continue;
            laborForce++;
            if (state.Households.WorkBuildingId[i] != 0) employed++;
        }
        return laborForce > 0 ? (float)employed / laborForce : 0.5f;
    }

    private static float CalculateAverageServiceScore(WorldState state)
    {
        int count = 0;
        float total = 0f;
        var tiles = state.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.BuildingId[i] == 0) continue;
            count++;
            float power = tiles.PowerGrid[i] != 0 ? 0.25f : 0f;
            float water = tiles.WaterGrid[i] != 0 ? 0.25f : 0f;
            float fire = tiles.GetFireCoverage(i) / 3f * 0.25f;
            float police = tiles.GetPoliceCoverage(i) / 3f * 0.25f;
            total += power + water + fire + police;
        }
        return count > 0 ? total / count : 0.5f;
    }

    private static float CalculateAverageCrime(WorldState state)
    {
        int count = 0;
        float total = 0f;
        var tiles = state.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.BuildingId[i] == 0) continue;
            count++;
            total += tiles.Crime[i];
        }
        return count > 0 ? total / count : 0f;
    }

    // =========================================================================
    // Terrain generation
    // =========================================================================

    private void GenerateTerrain(WorldState state)
    {
        // Use the full MapGenerator for proper terrain with rivers, forests, resources
        var (generatedTiles, validation) = MapGenerator.Generate(
            seed: 12345, mapType: MapType.Valley, size: _config.WorldSize);

        // Copy generated terrain into the simulation's WorldState tile data
        int tileCount = state.Tiles.Size * state.Tiles.Size;
        Array.Copy(generatedTiles.TerrainType, state.Tiles.TerrainType, tileCount);
        Array.Copy(generatedTiles.Elevation, state.Tiles.Elevation, tileCount);
        Array.Copy(generatedTiles.RoadFlags, state.Tiles.RoadFlags, tileCount);
        Array.Copy(generatedTiles.ZoneType, state.Tiles.ZoneType, tileCount);

        Console.WriteLine($"[IronAndOak] Map generated: playable={validation.IsPlayable}, " +
                          $"flat={validation.FlatTileCount}, water={validation.WaterTileCount}, " +
                          $"start=({validation.StartX},{validation.StartY})");

        // Center camera on starting zone if valid
        if (_camera != null && validation.StartX > 0 && validation.StartY > 0)
        {
            _camera.CenterOnTile(validation.StartX, validation.StartY);
        }
    }

    /// <summary>
    /// Per-frame input handling. Runs exactly once per frame after SDL event polling,
    /// before fixed timestep updates. Handles all transient input states (pressed/released)
    /// that would be lost if processed inside the fixed timestep loop.
    /// </summary>
    private void OnInputProcessed()
    {
        if (_camera == null || _simLoop == null) return;

        var input = _app.Input;

        // Check if ImGui wants to capture input (mouse over a UI panel or keyboard in a text field)
        var io = ImGuiNET.ImGui.GetIO();
        bool imguiWantsMouse = io.WantCaptureMouse;
        bool imguiWantsKeyboard = io.WantCaptureKeyboard;

        // Camera rotation (Q/E) and scroll — transient per-frame state, accumulated for fixed updates
        if (!imguiWantsKeyboard)
        {
            if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_Q)) _pendingRotateLeft = true;
            if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_E)) _pendingRotateRight = true;
        }
        if (!imguiWantsMouse)
        {
            _pendingScrollDelta += input.ScrollDelta;
        }

        // Minimap input handling (before tool system so minimap can consume clicks)
        bool minimapConsumedInput = false;
        if (_minimapRenderer != null && !imguiWantsMouse)
        {
            int vpW = _app.Renderer?.ViewportWidth ?? _config.WindowWidth;
            int vpH = _app.Renderer?.ViewportHeight ?? _config.WindowHeight;
            minimapConsumedInput = _minimapRenderer.HandleInput(
                input.MouseX, input.MouseY,
                input.IsLeftMouseHeld, input.IsLeftMousePressed,
                vpW, vpH, _camera);
        }

        // Route input through ToolSystem only if neither minimap nor ImGui consumed it
        if (!minimapConsumedInput && !imguiWantsMouse)
        {
            _toolSystem?.HandleInput(input, _camera, _simLoop.State.Tiles);
        }

        // Tool selection keyboard shortcuts (only when ImGui doesn't want keyboard)
        if (!imguiWantsKeyboard)
        {
            HandleToolShortcuts(input);
        }

        // Keyboard toggles for panels and overlays (only when ImGui doesn't want keyboard)
        if (!imguiWantsKeyboard)
        {
            HandlePanelToggles(input);
            HandleOverlayToggles(input);
        }
    }

    // Pending input flags set per-frame, consumed by OnUpdate
    private bool _pendingRotateLeft;
    private bool _pendingRotateRight;
    private int _pendingScrollDelta;

    private void OnUpdate(double dt)
    {
        if (_camera == null || _simLoop == null) return;

        // Update camera from input — uses held (continuous) state + pending rotation flags
        var input = _app.Input;
        _camera.SetViewport(_app.Renderer?.ViewportWidth ?? _config.WindowWidth,
                            _app.Renderer?.ViewportHeight ?? _config.WindowHeight);

        _camera.Update(
            (float)dt,
            left: input.IsKeyHeld(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_A),
            right: input.IsKeyHeld(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_D),
            up: input.IsKeyHeld(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_W),
            down: input.IsKeyHeld(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_S),
            mouseX: input.MouseX,
            mouseY: input.MouseY,
            middleMouseDown: input.IsMiddleMouseHeld,
            mouseRelX: input.MouseRelX,
            mouseRelY: input.MouseRelY,
            scrollDelta: _pendingScrollDelta,
            rotateLeft: _pendingRotateLeft,
            rotateRight: _pendingRotateRight
        );

        // Clear pending flags after consumption (consumed on first fixed update)
        _pendingRotateLeft = false;
        _pendingRotateRight = false;
        _pendingScrollDelta = 0;

        // Flush queued EventBus events on the main thread
        _eventBus?.FlushQueued();

        // Sync speed to simulation
        _simLoop.SetSpeed(_app.Time.SpeedLevel);

        // Update overlay system
        if (_overlaySystem != null)
        {
            _overlaySystem.Update((float)dt, _simLoop.State.Tiles);

            // Feed happiness data from snapshot
            var currentSnapshot = _simLoop.CurrentSnapshot;
            _overlaySystem.UpdateHappinessData(currentSnapshot.Happiness);

            // Feed overlay data to minimap
            if (_minimapRenderer != null)
            {
                if (_overlaySystem.IsActive)
                {
                    _minimapRenderer.SetOverlayData(_overlaySystem.OverlayBuffer,
                        _overlaySystem.GetShaderOverlayType());
                }
                else
                {
                    _minimapRenderer.SetOverlayData(null, 0);
                }
            }
        }

        // Update minimap pixels periodically (every frame is fine for 512x512)
        if (_minimapRenderer != null)
        {
            _minimapRenderer.UpdatePixels(_simLoop.State.Tiles);
            _minimapRenderer.UploadTexture();
        }

        // Update building renderer from simulation snapshot
        {
            var buildingSnapshot = _simLoop.CurrentSnapshot;
            // Compute night strength from TimeOfDay (same formula as LightingSystem)
            float hour = buildingSnapshot.TimeOfDay % 24f;
            float radians = (hour - 12f) / 12f * MathF.PI;
            _nightStrength = MathF.Max(0f, (1f - MathF.Cos(radians)) * 0.5f);

            int bvpW = _app.Renderer?.ViewportWidth ?? _config.WindowWidth;
            int bvpH = _app.Renderer?.ViewportHeight ?? _config.WindowHeight;
            _buildingRenderer?.Update(buildingSnapshot, _camera, _nightStrength, bvpW, bvpH);
        }

        // Profiler counters
        var snapshot = _simLoop.CurrentSnapshot;
        Profiler.SetCounter("Population", snapshot.Population);
        Profiler.SetCounter("Buildings", snapshot.BuildingCount);
        Profiler.SetCounter("Vehicles", snapshot.VehicleCount);
    }

    /// <summary>
    /// Handle keyboard shortcuts for toggling UI panels.
    /// </summary>
    private void HandlePanelToggles(InputManager input)
    {
        // P = Budget panel
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_P))
        {
            if (_budgetPanel != null) _budgetPanel.IsOpen = !_budgetPanel.IsOpen;
        }

        // T = Research panel
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_T))
        {
            if (_researchPanel != null) _researchPanel.IsOpen = !_researchPanel.IsOpen;
        }

        // G = Politics panel
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_G))
        {
            if (_politicsPanel != null) _politicsPanel.IsOpen = !_politicsPanel.IsOpen;
        }

        // F1 = Debug panel toggle
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_F1))
        {
            _showDebugPanel = !_showDebugPanel;
        }

        // M = Minimap toggle
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_M))
        {
            if (_minimapRenderer != null) _minimapRenderer.Visible = !_minimapRenderer.Visible;
        }

        // Speed controls via number keys
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_SPACE))
        {
            _app.Time.TogglePause();
        }
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_KP_1) ||
            input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_MINUS))
        {
            _app.Time.SetSpeed(1);
        }
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_KP_2) ||
            input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_EQUALS))
        {
            _app.Time.SetSpeed(2);
        }
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_KP_3))
        {
            _app.Time.SetSpeed(3);
        }
    }

    /// <summary>
    /// Handle keyboard shortcuts for tool selection.
    /// R = Road, 1 = Residential, 2 = Commercial, 3 = Industrial, B/X = Bulldoze, Escape = deselect.
    /// </summary>
    private void HandleToolShortcuts(InputManager input)
    {
        if (_toolSystem == null) return;

        // Don't process tool shortcuts if Ctrl is held (reserved for undo/redo)
        bool ctrl = input.IsKeyHeld(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_LCTRL) ||
                     input.IsKeyHeld(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_RCTRL);
        if (ctrl) return;

        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_R))
        {
            _toolSystem.SetActiveTool("Road");
        }
        else if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_1))
        {
            _toolSystem.SetActiveTool("Residential");
        }
        else if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_2))
        {
            _toolSystem.SetActiveTool("Commercial");
        }
        else if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_3))
        {
            _toolSystem.SetActiveTool("Industrial");
        }
        else if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_B) ||
                 input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_X))
        {
            _toolSystem.SetActiveTool("Bulldoze");
        }
    }

    /// <summary>
    /// Handle F2-F9 hotkeys for data overlay toggling.
    /// </summary>
    private void HandleOverlayToggles(InputManager input)
    {
        if (_overlaySystem == null) return;

        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_F2))
            _overlaySystem.ToggleOverlay(GameOverlaySystem.OverlayId.Traffic);

        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_F3))
            _overlaySystem.ToggleOverlay(GameOverlaySystem.OverlayId.LandValue);

        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_F4))
            _overlaySystem.ToggleOverlay(GameOverlaySystem.OverlayId.Happiness);

        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_F5))
            _overlaySystem.ToggleOverlay(GameOverlaySystem.OverlayId.Pollution);

        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_F6))
            _overlaySystem.ToggleOverlay(GameOverlaySystem.OverlayId.Crime);

        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_F7))
            _overlaySystem.ToggleOverlay(GameOverlaySystem.OverlayId.FireRisk);

        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_F8))
            _overlaySystem.ToggleOverlay(GameOverlaySystem.OverlayId.PowerGrid);

        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_F9))
            _overlaySystem.ToggleOverlay(GameOverlaySystem.OverlayId.Water);
    }

    /// <summary>
    /// Create shader, placeholder atlas, and chunk renderer using the live GL context.
    /// Must be called after the ForgeApp renderer is initialized and terrain is generated.
    /// </summary>
    private void InitRendering()
    {
        var gl = _app.Renderer?.GL;
        if (gl == null || _simLoop == null || _camera == null || _chunks == null)
        {
            Console.WriteLine("[IronAndOak] WARNING: Cannot init rendering — GL context or subsystems not ready.");
            return;
        }

        // Compile terrain chunk shader (position already includes zoom+offset from BuildAndUploadChunk)
        const string vertexSource = @"#version 330 core
layout(location = 0) in vec2 a_position;
layout(location = 1) in vec2 a_uv;
layout(location = 2) in vec4 a_tint;

uniform mat4 u_projection;

out vec2 v_uv;
out vec4 v_tint;

void main()
{
    gl_Position = u_projection * vec4(a_position, 0.0, 1.0);
    v_uv = a_uv;
    v_tint = a_tint;
}
";

        const string fragmentSource = @"#version 330 core
in vec2 v_uv;
in vec4 v_tint;

uniform sampler2D u_texture;

out vec4 fragColor;

void main()
{
    vec4 texColor = texture(u_texture, v_uv);
    fragColor = texColor * v_tint;
}
";

        _terrainShader = new ShaderProgram(gl, vertexSource, fragmentSource);

        // Create a 1x1 white placeholder atlas so tint colors drive appearance
        _terrainAtlas = new TextureAtlas(gl);
        _terrainAtlas.CreatePlaceholder();

        // Generate procedural terrain textures and upload to GPU
        _terrainTextures = new ProceduralTerrainTextures();
        _terrainTextures.Generate(gl, _config.TileWidth, _config.TileHeight);

        // Create chunk renderer with the live GL context
        _chunkRenderer = new ChunkRenderer(gl, _config, _chunks, _simLoop.State.Tiles);
        _chunkRenderer.Init(_terrainShader);
        _chunkRenderer.SetTerrainTextures(_terrainTextures);

        // Initialize minimap renderer
        _minimapRenderer = new MinimapRenderer(gl, _config.WorldSize);
        _minimapRenderer.Init();
        _minimapRenderer.UpdatePixels(_simLoop.State.Tiles);
        _minimapRenderer.UploadTexture();

        // Initialize overlay system
        _overlaySystem = new GameOverlaySystem(gl, _config.WorldSize);

        // Initialize building renderer with instanced sprite pipeline
        _buildingRenderer = new BuildingRenderer(_config);
        _buildingSpriteRenderer = new SpriteRenderer(gl);

        // Sprite instancing shader: transforms a unit quad per-instance with position, size, UV, and tint
        const string spriteVertSource = @"#version 330 core
layout(location = 0) in vec4 a_quad;       // per-vertex: pos.xy, uv.xy
layout(location = 1) in vec4 a_posSize;    // per-instance: x, y, w, h
layout(location = 2) in vec2 a_layer;      // per-instance: atlasLayer, pad
layout(location = 3) in vec4 a_uv;         // per-instance: u0, v0, u1, v1
layout(location = 4) in vec4 a_tint;       // per-instance: r, g, b, a
layout(location = 5) in vec2 a_sort;       // per-instance: sortY, pad

uniform mat4 u_projection;

out vec4 v_tint;

void main()
{
    vec2 pos = a_posSize.xy + a_quad.xy * a_posSize.zw;
    gl_Position = u_projection * vec4(pos, 0.0, 1.0);
    v_tint = a_tint;
}
";

        const string spriteFragSource = @"#version 330 core
in vec4 v_tint;
out vec4 fragColor;

void main()
{
    fragColor = v_tint;
}
";

        _buildingSpriteShader = new ShaderProgram(gl, spriteVertSource, spriteFragSource);
        _buildingSpriteRenderer.Init(_buildingSpriteShader);

        Console.WriteLine("[IronAndOak] Rendering pipeline initialized (shader + chunk renderer + buildings + minimap + overlays).");
    }

    private void OnRender(double alpha)
    {
        if (_chunkRenderer == null || _camera == null || _terrainAtlas == null) return;

        // Bind the placeholder atlas and render all visible terrain chunks
        _terrainAtlas.Bind(0);
        float gameTime = (float)_app.Time.TotalTime;
        _chunkRenderer.Render(_camera, _terrainAtlas, gameTime);

        // Render procedural buildings on top of terrain
        if (_buildingSpriteRenderer != null && _buildingSpriteShader != null && _buildingRenderer != null)
        {
            _buildingSpriteShader.Use();

            // Set orthographic projection matching the terrain shader
            int vpW = _app.Renderer?.ViewportWidth ?? _config.WindowWidth;
            int vpH = _app.Renderer?.ViewportHeight ?? _config.WindowHeight;
            float[] ortho =
            [
                2f / vpW, 0f, 0f, 0f,
                0f, -2f / vpH, 0f, 0f,
                0f, 0f, -1f, 0f,
                -1f, 1f, 0f, 1f,
            ];
            _buildingSpriteShader.SetUniformMatrix4("u_projection", ortho);

            _buildingSpriteRenderer.Begin();
            _buildingRenderer.Render(_buildingSpriteRenderer, _nightStrength);
            _buildingSpriteRenderer.End();
        }

        // Render minimap (GL quad, must be after terrain but can be before UI)
        if (_minimapRenderer != null)
        {
            int vpW = _app.Renderer?.ViewportWidth ?? _config.WindowWidth;
            int vpH = _app.Renderer?.ViewportHeight ?? _config.WindowHeight;
            _minimapRenderer.Render(vpW, vpH, _camera);
        }
    }

    private void OnRenderUI()
    {
        if (_simLoop == null || _camera == null) return;

        var snapshot = _simLoop.CurrentSnapshot;

        // HUD panel (always visible, top bar)
        _hudPanel?.Draw(snapshot);

        // Toolbar panel (always visible, bottom bar)
        _toolbarPanel?.Draw();

        // Budget panel (toggle with P)
        _budgetPanel?.Draw(snapshot);

        // Research panel (toggle with T)
        _researchPanel?.Draw(snapshot);

        // Politics panel (toggle with G)
        _politicsPanel?.Draw(snapshot);

        // Tool system's active tool UI (options panel)
        _toolSystem?.RenderUI();

        // Active overlay indicator (top bar, right side)
        if (_overlaySystem != null && _overlaySystem.IsActive)
        {
            DrawOverlayIndicator();
        }

        // Minimap ImGui overlay (border, camera viewport rectangle)
        _minimapRenderer?.RenderUI();

        // Debug overlay (toggle with F1)
        if (_showDebugPanel)
        {
            DrawDebugPanel(snapshot);
        }

        // Profiler overlay
        Profiler.RenderOverlay();
    }

    /// <summary>
    /// Show the active overlay name in the HUD area (top-right).
    /// </summary>
    private void DrawOverlayIndicator()
    {
        if (_overlaySystem == null) return;

        string overlayName = _overlaySystem.ActiveOverlayName;
        if (string.IsNullOrEmpty(overlayName)) return;

        var io = ImGuiNET.ImGui.GetIO();
        float screenWidth = io.DisplaySize.X;

        // Position in top-right, below the HUD bar
        ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(screenWidth - 200, 52));
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(190, 30));

        var flags = ImGuiNET.ImGuiWindowFlags.NoTitleBar | ImGuiNET.ImGuiWindowFlags.NoResize |
                    ImGuiNET.ImGuiWindowFlags.NoMove | ImGuiNET.ImGuiWindowFlags.NoScrollbar |
                    ImGuiNET.ImGuiWindowFlags.NoInputs;

        ImGuiNET.ImGui.PushStyleColor(ImGuiNET.ImGuiCol.WindowBg,
            new System.Numerics.Vector4(0.1f, 0.1f, 0.15f, 0.85f));
        ImGuiNET.ImGui.PushStyleVar(ImGuiNET.ImGuiStyleVar.WindowRounding, 4f);

        if (ImGuiNET.ImGui.Begin("##overlay_indicator", flags))
        {
            // Yellow text for overlay name
            ImGuiNET.ImGui.PushStyleColor(ImGuiNET.ImGuiCol.Text,
                new System.Numerics.Vector4(1f, 0.9f, 0.3f, _overlaySystem.FadeOpacity));
            ImGuiNET.ImGui.Text($"Overlay: {overlayName}");
            ImGuiNET.ImGui.PopStyleColor();
        }
        ImGuiNET.ImGui.End();

        ImGuiNET.ImGui.PopStyleVar();
        ImGuiNET.ImGui.PopStyleColor();
    }

    /// <summary>
    /// Debug info panel with detailed engine stats. Toggle with F1.
    /// </summary>
    private void DrawDebugPanel(SimSnapshot snapshot)
    {
        ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 56), ImGuiNET.ImGuiCond.FirstUseEver);
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(260, 240), ImGuiNET.ImGuiCond.FirstUseEver);

        ImGuiNET.ImGui.PushStyleColor(ImGuiNET.ImGuiCol.WindowBg,
            new System.Numerics.Vector4(0.05f, 0.05f, 0.08f, 0.85f));

        if (ImGuiNET.ImGui.Begin("Debug [F1]"))
        {
            ImGuiNET.ImGui.Text($"Camera: ({_camera!.X:F0}, {_camera.Y:F0})");
            ImGuiNET.ImGui.Text($"Zoom: {_camera.ZoomLevel}x | Rot: {_camera.Rotation * 90} deg");

            var (gx, gy) = _camera.ScreenToGrid(_app.Input.MouseX, _app.Input.MouseY);
            ImGuiNET.ImGui.Text($"Cursor: ({gx}, {gy})");

            ImGuiNET.ImGui.Separator();
            ImGuiNET.ImGui.Text($"Sim Tick: {snapshot.TickCount}");
            ImGuiNET.ImGui.Text($"Buildings: {snapshot.BuildingCount}");
            ImGuiNET.ImGui.Text($"Vehicles: {snapshot.VehicleCount}");
            ImGuiNET.ImGui.Text($"Active Events: {snapshot.ActiveEventCount}");

            if (_chunkRenderer != null)
            {
                ImGuiNET.ImGui.Separator();
                ImGuiNET.ImGui.Text($"Chunks drawn: {_chunkRenderer.ChunksDrawnLastFrame}");
                ImGuiNET.ImGui.Text($"Chunks culled: {_chunkRenderer.ChunksCulledLastFrame}");
                ImGuiNET.ImGui.Text($"Chunks uploaded: {_chunkRenderer.ChunksUploadedLastFrame}");
            }

            ImGuiNET.ImGui.Separator();
            ImGuiNET.ImGui.Text($"Speed: {_app.Time.SpeedLevel}x | FPS: {_app.Time.Fps:F0}");
        }
        ImGuiNET.ImGui.End();

        ImGuiNET.ImGui.PopStyleColor();
    }

    private void OnShutdown()
    {
        _simLoop?.Stop();
        _simLoop?.Dispose();
        _minimapRenderer?.Dispose();
        _overlaySystem?.Dispose();
        _buildingSpriteRenderer?.Dispose();
        _buildingSpriteShader?.Dispose();
        _chunkRenderer?.Dispose();
        _terrainTextures?.Dispose();
        _terrainShader?.Dispose();
        _terrainAtlas?.Dispose();
        Console.WriteLine("[IronAndOak] Shutdown.");
    }
}
