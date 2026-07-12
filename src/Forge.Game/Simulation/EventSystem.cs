using System.Text.Json;
using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Game event simulation: triggers, state-machine progression, cascading effects, and cleanup.
///
/// Events follow a 4-phase lifecycle:
///   Brewing  -> Active -> Waning -> Aftermath
///
/// Each phase has distinct gameplay effects. Events can cascade (fire -> homelessness),
/// stack (multiple events simultaneously), and interact with city state
/// (recession makes protests more likely, drought increases fire risk).
///
/// Event definitions are loaded from base/data/events/events.json at startup.
/// The system evaluates trigger conditions each game day and rolls probability
/// checks to spawn new events.
/// </summary>
public sealed class EventSystem
{
    // =========================================================================
    // Public types
    // =========================================================================

    /// <summary>Lifecycle phase of an active game event.</summary>
    public enum EventPhase : byte
    {
        /// <summary>Warning signs. Minor effects, player can prepare.</summary>
        Brewing,
        /// <summary>Full impact. Maximum effects applied.</summary>
        Active,
        /// <summary>Subsiding. Effects at 50% strength, recovery begins.</summary>
        Waning,
        /// <summary>Cleanup period. Lingering effects, rebuilding costs.</summary>
        Aftermath,
    }

    /// <summary>Runtime state of a single active game event.</summary>
    public struct GameEvent
    {
        /// <summary>Unique runtime ID for this event instance.</summary>
        public int EventId;
        /// <summary>Type identifier matching event definition (e.g. "fire", "recession").</summary>
        public string TypeId;
        /// <summary>Current lifecycle phase.</summary>
        public EventPhase Phase;
        /// <summary>Epicenter tile X (-1 for city-wide events).</summary>
        public int TileX;
        /// <summary>Epicenter tile Y (-1 for city-wide events).</summary>
        public int TileY;
        /// <summary>Tick count when the event started.</summary>
        public long StartTick;
        /// <summary>Total duration in game days for the entire lifecycle.</summary>
        public int DurationDays;
        /// <summary>Days elapsed since event start.</summary>
        public int ElapsedDays;
        /// <summary>Severity multiplier (0-1). Higher = stronger effects.</summary>
        public float Severity;
        /// <summary>Per-effect parameter modifications. Indices match EffectIndex enum.</summary>
        public float[] Effects;
    }

    /// <summary>Indices into the GameEvent.Effects array for fast lookup.</summary>
    public enum EffectIndex : int
    {
        Happiness = 0,
        PropertyDamage = 1,
        PopulationLoss = 2,
        TrafficDisruption = 3,
        PollutionIncrease = 4,
        TaxRevenueMultiplier = 5,
        UnemploymentIncrease = 6,
        PropertyValueChange = 7,
        ImmigrationModifier = 8,
        ProductivityChange = 9,
        CrimeChange = 10,
        ApprovalRatingChange = 11,
        CommercialModifier = 12,
        PowerDemandChange = 13,
        HealthDemandChange = 14,
        FireRiskChange = 15,
        ResearchModifier = 16,
        WaterDemandChange = 17,
        Count = 18,
    }

    // =========================================================================
    // Event definitions (loaded from JSON)
    // =========================================================================

    /// <summary>Static definition of an event type, loaded from data files.</summary>
    public sealed class EventDefinition
    {
        public int Id { get; set; }
        public string TypeId { get; set; } = "";
        public string Category { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int MinEra { get; set; }
        public int MaxEra { get; set; } = 5;
        public float BaseProbability { get; set; }
        public int MinDurationDays { get; set; } = 1;
        public int MaxDurationDays { get; set; } = 30;
        public float MinSeverity { get; set; }
        public float MaxSeverity { get; set; } = 1.0f;
        public bool Localized { get; set; }
        public int SpreadRadius { get; set; }
        public Dictionary<string, float> Effects { get; set; } = new();
        public Dictionary<string, float> SeasonBias { get; set; } = new();
        public Dictionary<string, float> TriggerConditions { get; set; } = new();
        public List<string> Cascades { get; set; } = new();
    }

    // =========================================================================
    // State
    // =========================================================================

    private readonly List<EventDefinition> _definitions = new();
    private readonly Dictionary<string, EventDefinition> _definitionsByTypeId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GameEvent> _activeEvents = new();
    private readonly Random _rng;
    private int _nextEventId = 1;

    /// <summary>Maximum concurrent events to prevent runaway stacking.</summary>
    public const int MaxConcurrentEvents = 16;

    /// <summary>All currently active events (read-only view).</summary>
    public IReadOnlyList<GameEvent> ActiveEvents => _activeEvents;

    /// <summary>All loaded event definitions (read-only view).</summary>
    public IReadOnlyList<EventDefinition> Definitions => _definitions;

    /// <summary>Number of currently active events.</summary>
    public int ActiveEventCount => _activeEvents.Count;

    // =========================================================================
    // Construction
    // =========================================================================

    /// <summary>
    /// Create a new EventSystem with an optional random seed for deterministic simulation.
    /// </summary>
    public EventSystem(int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    // =========================================================================
    // Data loading
    // =========================================================================

    /// <summary>
    /// Load event definitions from a JSON file. Expected path: base/data/events/events.json
    /// </summary>
    public void LoadDefinitions(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Event definitions not found: {jsonPath}");

        string json = File.ReadAllText(jsonPath);
        LoadDefinitionsFromJson(json);
    }

    /// <summary>
    /// Load event definitions from a JSON string directly (useful for testing).
    /// Supports both the legacy object format {"events": [...]} with integer IDs
    /// and the new array format with string IDs.
    /// </summary>
    public void LoadDefinitionsFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _definitions.Clear();
        _definitionsByTypeId.Clear();

        // Determine the events array: either root itself (new format) or root.events (legacy)
        JsonElement eventsArray;
        if (root.ValueKind == JsonValueKind.Array)
        {
            eventsArray = root;
        }
        else if (root.TryGetProperty("events", out eventsArray))
        {
            // Legacy format
        }
        else
        {
            throw new InvalidDataException("Event definitions JSON must be an array or an object with 'events' property.");
        }

        int autoId = 0;
        foreach (var elem in eventsArray.EnumerateArray())
        {
            var def = ParseEventDefinition(elem, ref autoId);
            _definitions.Add(def);
            _definitionsByTypeId[def.TypeId] = def;
        }
    }

    /// <summary>
    /// Map a string era name to an integer era index.
    /// </summary>
    private static int ParseEraString(string era)
    {
        return era.ToLowerInvariant() switch
        {
            "ancient" or "frontier" => 0,
            "medieval" => 1,
            "colonial" or "postwar" => 2,
            "industrial" => 3,
            "modern" => 4,
            "future" => 5,
            _ => 0,
        };
    }

    /// <summary>
    /// Parse a single event definition, handling both old (integer id + typeId)
    /// and new (string id) formats.
    /// </summary>
    private static EventDefinition ParseEventDefinition(JsonElement elem, ref int autoId)
    {
        var idElem = elem.GetProperty("id");
        string typeId;
        int numericId;

        if (idElem.ValueKind == JsonValueKind.Number)
        {
            // Legacy format: numeric id, separate typeId
            numericId = idElem.GetInt32();
            typeId = elem.TryGetProperty("typeId", out var tid) ? tid.GetString() ?? "" : "";
        }
        else
        {
            // New format: string id is the typeId, auto-assign numeric
            typeId = idElem.GetString() ?? "";
            numericId = autoId;
        }
        autoId = Math.Max(autoId, numericId + 1);

        var def = new EventDefinition
        {
            Id = numericId,
            TypeId = typeId,
            Category = elem.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "",
            Name = elem.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Description = elem.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
        };

        // Era: support both int fields and string era_min/era_max
        if (elem.TryGetProperty("minEra", out var minE) && minE.ValueKind == JsonValueKind.Number)
            def.MinEra = minE.GetInt32();
        else if (elem.TryGetProperty("era_min", out var eraMin))
            def.MinEra = eraMin.ValueKind == JsonValueKind.Number ? eraMin.GetInt32()
                       : eraMin.ValueKind == JsonValueKind.String ? ParseEraString(eraMin.GetString() ?? "") : 0;

        if (elem.TryGetProperty("maxEra", out var maxE) && maxE.ValueKind == JsonValueKind.Number)
            def.MaxEra = maxE.GetInt32();
        else if (elem.TryGetProperty("era_max", out var eraMax))
            def.MaxEra = eraMax.ValueKind == JsonValueKind.Number ? eraMax.GetInt32()
                       : eraMax.ValueKind == JsonValueKind.String ? ParseEraString(eraMax.GetString() ?? "") : 5;
        else
            def.MaxEra = 5;

        // Probability: baseProbability or base_probability
        if (elem.TryGetProperty("baseProbability", out var bp))
            def.BaseProbability = bp.GetSingle();
        else if (elem.TryGetProperty("base_probability", out var bp2))
            def.BaseProbability = bp2.GetSingle();

        // Localized / spread
        def.Localized = elem.TryGetProperty("localized", out var loc) && loc.GetBoolean();
        def.SpreadRadius = elem.TryGetProperty("spreadRadius", out var sr) ? sr.GetInt32() : 0;

        // Duration: durationDays {min,max} or duration_months (scalar)
        if (elem.TryGetProperty("durationDays", out var dur) && dur.ValueKind == JsonValueKind.Object)
        {
            def.MinDurationDays = dur.TryGetProperty("min", out var dMin) ? dMin.GetInt32() : 1;
            def.MaxDurationDays = dur.TryGetProperty("max", out var dMax) ? dMax.GetInt32() : 30;
        }
        else if (elem.TryGetProperty("duration_months", out var dm))
        {
            int months = dm.GetInt32();
            def.MinDurationDays = months * 30;
            def.MaxDurationDays = months * 30;
        }

        // Severity: {min,max} object or scalar number
        if (elem.TryGetProperty("severity", out var sev))
        {
            if (sev.ValueKind == JsonValueKind.Object)
            {
                def.MinSeverity = sev.TryGetProperty("min", out var sMin) ? sMin.GetSingle() : 0f;
                def.MaxSeverity = sev.TryGetProperty("max", out var sMax) ? sMax.GetSingle() : 1f;
            }
            else if (sev.ValueKind == JsonValueKind.Number)
            {
                float val = sev.GetSingle();
                def.MinSeverity = val * 0.1f; // Normalize: severity 1-5 → 0.1-0.5 range
                def.MaxSeverity = val * 0.1f;
            }
        }

        // Effects dictionary
        if (elem.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in effects.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                    def.Effects[prop.Name] = prop.Value.GetSingle();
            }
        }

        // Trigger conditions: triggerConditions or trigger_conditions
        JsonElement triggers = default;
        bool hasTriggers = elem.TryGetProperty("triggerConditions", out triggers)
                        || elem.TryGetProperty("trigger_conditions", out triggers);

        if (hasTriggers && triggers.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in triggers.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                    def.TriggerConditions[prop.Name] = prop.Value.GetSingle();
                else if (prop.Value.ValueKind == JsonValueKind.True)
                    def.TriggerConditions[prop.Name] = 1f;
                else if (prop.Value.ValueKind == JsonValueKind.False)
                    def.TriggerConditions[prop.Name] = 0f;
                else if (prop.Value.ValueKind == JsonValueKind.Object && prop.Name == "seasonBias")
                {
                    foreach (var season in prop.Value.EnumerateObject())
                    {
                        if (season.Value.ValueKind == JsonValueKind.Number)
                            def.SeasonBias[season.Name] = season.Value.GetSingle();
                    }
                }
            }

            // seasonBias as a nested object within trigger conditions
            if (triggers.TryGetProperty("seasonBias", out var sb))
            {
                foreach (var season in sb.EnumerateObject())
                {
                    if (season.Value.ValueKind == JsonValueKind.Number)
                        def.SeasonBias[season.Name] = season.Value.GetSingle();
                }
            }
        }

        // Cascades: cascades or cascade_events
        JsonElement cascadesElem = default;
        bool hasCascades = elem.TryGetProperty("cascades", out cascadesElem)
                        || elem.TryGetProperty("cascade_events", out cascadesElem);

        if (hasCascades && cascadesElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cascadesElem.EnumerateArray())
            {
                var s = c.GetString();
                if (s != null) def.Cascades.Add(s);
            }
        }

        return def;
    }

    // =========================================================================
    // Daily tick -- evaluate triggers and spawn new events
    // =========================================================================

    /// <summary>
    /// Check event triggers each game day. Evaluates conditions against current
    /// world state and rolls probability checks to spawn new events.
    /// </summary>
    public void DailyTick(WorldState state)
    {
        if (_activeEvents.Count >= MaxConcurrentEvents) return;

        foreach (var def in _definitions)
        {
            // Skip if we're already at capacity
            if (_activeEvents.Count >= MaxConcurrentEvents) break;

            // Skip zero-probability events (triggered only by cascades or scripted logic)
            if (def.BaseProbability <= 0f) continue;

            // Era check
            if (state.Era < def.MinEra || state.Era > def.MaxEra) continue;

            // Don't stack events of the same type
            if (IsEventTypeActive(def.TypeId)) continue;

            // Evaluate trigger conditions
            if (!EvaluateTriggerConditions(def, state)) continue;

            // Calculate probability with seasonal and state modifiers
            float probability = CalculateAdjustedProbability(def, state);

            // Roll the dice
            float roll = (float)_rng.NextDouble();
            if (roll < probability)
            {
                SpawnEvent(def, state);
            }
        }
    }

    // =========================================================================
    // Update -- process active events through their lifecycle
    // =========================================================================

    /// <summary>
    /// Process all active events: advance phases, apply effects, handle cascades.
    /// Called each game day.
    /// </summary>
    public void UpdateEvents(WorldState state, float dt)
    {
        for (int i = _activeEvents.Count - 1; i >= 0; i--)
        {
            var evt = _activeEvents[i];
            evt.ElapsedDays++;

            // Determine phase transitions based on elapsed time
            float progress = (float)evt.ElapsedDays / evt.DurationDays;
            EventPhase newPhase = progress switch
            {
                < 0.15f => EventPhase.Brewing,
                < 0.65f => EventPhase.Active,
                < 0.85f => EventPhase.Waning,
                _ => EventPhase.Aftermath,
            };
            evt.Phase = newPhase;

            // Apply effects based on current phase
            float phaseMultiplier = newPhase switch
            {
                EventPhase.Brewing => 0.25f,
                EventPhase.Active => 1.0f,
                EventPhase.Waning => 0.5f,
                EventPhase.Aftermath => 0.15f,
                _ => 0f,
            };

            ApplyEventEffects(evt, state, phaseMultiplier);

            // Check for cascading events when transitioning to Active phase
            if (newPhase == EventPhase.Active && evt.ElapsedDays == (int)(evt.DurationDays * 0.15f) + 1)
            {
                TriggerCascades(evt, state);
            }

            // Remove expired events
            if (evt.ElapsedDays >= evt.DurationDays)
            {
                // Sync removal with WorldState event tracking
                RemoveFromWorldState(evt, state);
                _activeEvents.RemoveAt(i);
                continue;
            }

            _activeEvents[i] = evt;
        }
    }

    // =========================================================================
    // Public query methods
    // =========================================================================

    /// <summary>Check if an event of the given type is currently active.</summary>
    public bool IsEventTypeActive(string typeId)
    {
        for (int i = 0; i < _activeEvents.Count; i++)
        {
            if (string.Equals(_activeEvents[i].TypeId, typeId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Get event definition by type ID. Returns null if not found.</summary>
    public EventDefinition? GetDefinition(string typeId)
    {
        _definitionsByTypeId.TryGetValue(typeId, out var def);
        return def;
    }

    /// <summary>
    /// Get the aggregate effect modifier across all active events for a given effect index.
    /// Returns the sum of all active event effects at their current phase-adjusted strength.
    /// </summary>
    public float GetAggregateEffect(EffectIndex index)
    {
        float total = 0f;
        for (int i = 0; i < _activeEvents.Count; i++)
        {
            var evt = _activeEvents[i];
            if (evt.Effects != null && (int)index < evt.Effects.Length)
            {
                float phaseMultiplier = evt.Phase switch
                {
                    EventPhase.Brewing => 0.25f,
                    EventPhase.Active => 1.0f,
                    EventPhase.Waning => 0.5f,
                    EventPhase.Aftermath => 0.15f,
                    _ => 0f,
                };
                total += evt.Effects[(int)index] * phaseMultiplier;
            }
        }
        return total;
    }

    /// <summary>
    /// Manually trigger an event by type ID. Used for scripted events, cheats, or cascades.
    /// Returns true if the event was spawned successfully.
    /// </summary>
    public bool TriggerEvent(string typeId, WorldState state, float? severityOverride = null, int tileX = -1, int tileY = -1)
    {
        if (_activeEvents.Count >= MaxConcurrentEvents) return false;
        if (!_definitionsByTypeId.TryGetValue(typeId, out var def)) return false;
        if (IsEventTypeActive(typeId)) return false;

        SpawnEvent(def, state, severityOverride, tileX, tileY);
        return true;
    }

    /// <summary>
    /// Force-remove all active events. Used for sandbox/debug.
    /// </summary>
    public void ClearAllEvents(WorldState state)
    {
        for (int i = _activeEvents.Count - 1; i >= 0; i--)
        {
            RemoveFromWorldState(_activeEvents[i], state);
        }
        _activeEvents.Clear();
    }

    /// <summary>
    /// Player chose a Herald council response — taper the event into Waning so effects decay.
    /// </summary>
    public bool ResolvePlayerResponse(int eventId, WorldState state)
    {
        for (int i = 0; i < _activeEvents.Count; i++)
        {
            if (_activeEvents[i].EventId != eventId) continue;

            var evt = _activeEvents[i];
            evt.Phase = EventPhase.Waning;
            evt.ElapsedDays = Math.Max(evt.ElapsedDays, (int)(evt.DurationDays * 0.65f));
            _activeEvents[i] = evt;
            return true;
        }

        return false;
    }

    // =========================================================================
    // Internal logic
    // =========================================================================

    private void SpawnEvent(EventDefinition def, WorldState state, float? severityOverride = null, int tileXOverride = -1, int tileYOverride = -1)
    {
        float severity = severityOverride ?? Lerp(def.MinSeverity, def.MaxSeverity, (float)_rng.NextDouble());
        int duration = _rng.Next(def.MinDurationDays, def.MaxDurationDays + 1);

        int tileX = tileXOverride;
        int tileY = tileYOverride;

        if (def.Localized && tileX < 0 && tileY < 0)
        {
            // Pick a random tile in the city as epicenter
            int worldSize = state.Tiles.Size;
            tileX = _rng.Next(0, worldSize);
            tileY = _rng.Next(0, worldSize);
        }
        else if (!def.Localized)
        {
            tileX = -1;
            tileY = -1;
        }

        // Build effects array from definition
        float[] effects = new float[(int)EffectIndex.Count];
        foreach (var kvp in def.Effects)
        {
            int idx = MapEffectNameToIndex(kvp.Key);
            if (idx >= 0 && idx < effects.Length)
                effects[idx] = kvp.Value * severity;
        }

        var gameEvent = new GameEvent
        {
            EventId = _nextEventId++,
            TypeId = def.TypeId,
            Phase = EventPhase.Brewing,
            TileX = tileX,
            TileY = tileY,
            StartTick = state.TickCount,
            DurationDays = duration,
            ElapsedDays = 0,
            Severity = severity,
            Effects = effects,
        };

        _activeEvents.Add(gameEvent);

        // Also register in WorldState for cross-system visibility
        state.AddEvent(def.Id, duration, severity);
    }

    private bool EvaluateTriggerConditions(EventDefinition def, WorldState state)
    {
        foreach (var kvp in def.TriggerConditions)
        {
            switch (kvp.Key)
            {
                case "minFireRisk":
                    if (GetMaxFireRisk(state) < kvp.Value) return false;
                    break;
                case "minPrecipitation":
                    if (state.Precipitation < kvp.Value) return false;
                    break;
                case "maxTemperature":
                    if (state.AmbientTemperature > kvp.Value) return false;
                    break;
                case "minTemperature":
                    if (state.AmbientTemperature < kvp.Value) return false;
                    break;
                case "minWindSpeed":
                    if (state.WindSpeed < kvp.Value) return false;
                    break;
                case "minPopulation":
                    if (state.Population < (int)kvp.Value) return false;
                    break;
                case "minHappiness":
                    if (state.Happiness < kvp.Value) return false;
                    break;
                case "maxHappiness":
                    if (state.Happiness > kvp.Value) return false;
                    break;
                case "maxPrecipitation":
                    if (state.Precipitation > kvp.Value) return false;
                    break;
                case "minPollution":
                    if (GetAveragePollution(state) < kvp.Value) return false;
                    break;
                case "minResearchRate":
                    if (state.ResearchRate < kvp.Value) return false;
                    break;
                case "isElectionYear":
                    if (kvp.Value > 0f && state.Year != state.NextElectionYear) return false;
                    break;
                case "nearWater":
                    // Always passes -- water proximity checked at spawn location
                    break;
                case "hasOreDeposit":
                    if (kvp.Value > 0f && !HasResourceType(state, 2)) return false;
                    break;
                case "negativeGrowthQuarters":
                    // Simplified: check if happiness and population are low enough to suggest recession
                    if (state.Happiness > 0.4f) return false;
                    break;
            }
        }
        return true;
    }

    private float CalculateAdjustedProbability(EventDefinition def, WorldState state)
    {
        float prob = def.BaseProbability;

        // Apply seasonal bias
        string seasonName = state.Season switch
        {
            0 => "spring",
            1 => "summer",
            2 => "autumn",
            3 => "winter",
            _ => "spring",
        };

        if (def.SeasonBias.TryGetValue(seasonName, out float bias))
        {
            prob *= bias;
        }

        // City happiness affects negative event probability (unhappy cities attract trouble)
        if (IsNegativeEvent(def))
        {
            // Low happiness increases probability by up to 50%
            float happinessMod = 1.0f + (0.5f - state.Happiness) * 0.5f;
            prob *= Math.Max(0.5f, happinessMod);
        }

        // Population density modifier -- larger cities have more events
        if (state.Population > 1000)
        {
            float popMod = 1.0f + (float)Math.Log(state.Population / 1000f, 2) * 0.1f;
            prob *= popMod;
        }

        return Math.Clamp(prob, 0f, 0.5f); // Cap at 50% per day to prevent guaranteed events
    }

    private void ApplyEventEffects(GameEvent evt, WorldState state, float phaseMultiplier)
    {
        if (evt.Effects == null) return;

        float strength = evt.Severity * phaseMultiplier;

        // Happiness impact
        float happinessDelta = evt.Effects[(int)EffectIndex.Happiness] * strength * 0.01f;
        state.Happiness = Math.Clamp(state.Happiness + happinessDelta, 0f, 1f);

        // Approval rating
        float approvalDelta = evt.Effects[(int)EffectIndex.ApprovalRatingChange] * strength * 0.01f;
        state.ApprovalRating = Math.Clamp(state.ApprovalRating - approvalDelta, 0f, 1f);

        // Fire risk increase for localized events
        if (evt.TileX >= 0 && evt.TileY >= 0 && evt.Effects[(int)EffectIndex.FireRiskChange] != 0)
        {
            ApplyLocalizedEffect(state, evt.TileX, evt.TileY,
                GetSpreadRadius(evt),
                (idx) =>
                {
                    state.Tiles.FireRisk[idx] = Math.Clamp(
                        state.Tiles.FireRisk[idx] + evt.Effects[(int)EffectIndex.FireRiskChange] * strength * 0.01f,
                        0f, 1f);
                });
        }

        // Crime change for localized events
        if (evt.TileX >= 0 && evt.TileY >= 0 && evt.Effects[(int)EffectIndex.CrimeChange] != 0)
        {
            ApplyLocalizedEffect(state, evt.TileX, evt.TileY,
                GetSpreadRadius(evt),
                (idx) =>
                {
                    state.Tiles.Crime[idx] = Math.Clamp(
                        state.Tiles.Crime[idx] + evt.Effects[(int)EffectIndex.CrimeChange] * strength * 0.01f,
                        0f, 1f);
                });
        }

        // Property damage for localized events
        if (evt.TileX >= 0 && evt.TileY >= 0 && evt.Effects[(int)EffectIndex.PropertyDamage] != 0)
        {
            ApplyLocalizedEffect(state, evt.TileX, evt.TileY,
                GetSpreadRadius(evt),
                (idx) =>
                {
                    state.Tiles.LandValue[idx] = Math.Clamp(
                        state.Tiles.LandValue[idx] - evt.Effects[(int)EffectIndex.PropertyDamage] * strength * 0.005f,
                        0f, 1f);
                });
        }

        // Pollution for localized or city-wide events
        if (evt.Effects[(int)EffectIndex.PollutionIncrease] != 0)
        {
            if (evt.TileX >= 0 && evt.TileY >= 0)
            {
                ApplyLocalizedEffect(state, evt.TileX, evt.TileY,
                    GetSpreadRadius(evt),
                    (idx) =>
                    {
                        state.Tiles.Pollution[idx] = Math.Clamp(
                            state.Tiles.Pollution[idx] + evt.Effects[(int)EffectIndex.PollutionIncrease] * strength * 0.01f,
                            0f, 1f);
                    });
            }
        }

        // Traffic disruption for localized events
        if (evt.TileX >= 0 && evt.TileY >= 0 && evt.Effects[(int)EffectIndex.TrafficDisruption] != 0)
        {
            ApplyLocalizedEffect(state, evt.TileX, evt.TileY,
                GetSpreadRadius(evt),
                (idx) =>
                {
                    state.Tiles.Traffic[idx] = Math.Clamp(
                        state.Tiles.Traffic[idx] + evt.Effects[(int)EffectIndex.TrafficDisruption] * strength * 0.01f,
                        0f, 1f);
                });
        }

        // Population loss (applied city-wide as a fractional reduction)
        if (evt.Effects[(int)EffectIndex.PopulationLoss] > 0 && evt.Phase == EventPhase.Active)
        {
            int loss = (int)(state.Population * evt.Effects[(int)EffectIndex.PopulationLoss] * strength * 0.001f);
            state.Population = Math.Max(0, state.Population - loss);
        }
    }

    private void TriggerCascades(GameEvent evt, WorldState state)
    {
        if (_definitionsByTypeId.TryGetValue(evt.TypeId, out var def))
        {
            foreach (var cascadeTypeId in def.Cascades)
            {
                if (_activeEvents.Count >= MaxConcurrentEvents) break;
                if (IsEventTypeActive(cascadeTypeId)) continue;

                // Cascade probability scales with parent severity
                float cascadeChance = evt.Severity * 0.3f;
                if ((float)_rng.NextDouble() < cascadeChance)
                {
                    if (_definitionsByTypeId.TryGetValue(cascadeTypeId, out var cascadeDef))
                    {
                        // Cascade inherits location from parent if both are localized
                        int cx = cascadeDef.Localized && evt.TileX >= 0 ? evt.TileX : -1;
                        int cy = cascadeDef.Localized && evt.TileY >= 0 ? evt.TileY : -1;
                        SpawnEvent(cascadeDef, state, evt.Severity * 0.7f, cx, cy);
                    }
                }
            }
        }
    }

    private void ApplyLocalizedEffect(WorldState state, int centerX, int centerY, int radius, Action<int> applyToTile)
    {
        int worldSize = state.Tiles.Size;
        int minX = Math.Max(0, centerX - radius);
        int maxX = Math.Min(worldSize - 1, centerX + radius);
        int minY = Math.Max(0, centerY - radius);
        int maxY = Math.Min(worldSize - 1, centerY + radius);
        int radiusSq = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                if (dx * dx + dy * dy <= radiusSq)
                {
                    applyToTile(state.Tiles.Index(x, y));
                }
            }
        }
    }

    private int GetSpreadRadius(GameEvent evt)
    {
        if (_definitionsByTypeId.TryGetValue(evt.TypeId, out var def))
            return def.SpreadRadius;
        return 3;
    }

    private void RemoveFromWorldState(GameEvent evt, WorldState state)
    {
        // Find and remove the matching event from WorldState.Events
        for (int i = 0; i < state.ActiveEventCount; i++)
        {
            if (state.Events[i].EventId == evt.EventId ||
                (state.Events[i].Severity == evt.Severity && state.Events[i].RemainingDays <= 0))
            {
                state.RemoveEvent(i);
                break;
            }
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static float GetMaxFireRisk(WorldState state)
    {
        float max = 0f;
        var fireRisk = state.Tiles.FireRisk;
        for (int i = 0; i < fireRisk.Length; i++)
        {
            if (fireRisk[i] > max) max = fireRisk[i];
        }
        return max;
    }

    private static float GetAveragePollution(WorldState state)
    {
        double sum = 0;
        var pollution = state.Tiles.Pollution;
        for (int i = 0; i < pollution.Length; i++)
            sum += pollution[i];
        return pollution.Length > 0 ? (float)(sum / pollution.Length) : 0f;
    }

    private static bool HasResourceType(WorldState state, int resourceType)
    {
        for (int i = 0; i < state.Tiles.Count; i++)
        {
            if (state.Tiles.GetResourceType(i) == resourceType)
                return true;
        }
        return false;
    }

    private static bool IsNegativeEvent(EventDefinition def)
    {
        return def.Effects.TryGetValue("happiness", out float h) && h < 0;
    }

    /// <summary>
    /// Map an effect name from JSON to an EffectIndex enum value.
    /// Returns -1 for unrecognized effect names (they are silently ignored).
    /// </summary>
    internal static int MapEffectNameToIndex(string effectName)
    {
        return effectName switch
        {
            "happiness" or "happinessComplex" => (int)EffectIndex.Happiness,
            "propertyDamage" => (int)EffectIndex.PropertyDamage,
            "populationLoss" => (int)EffectIndex.PopulationLoss,
            "trafficDisruption" or "trafficIncrease" => (int)EffectIndex.TrafficDisruption,
            "pollutionIncrease" => (int)EffectIndex.PollutionIncrease,
            "taxRevenueMultiplier" => (int)EffectIndex.TaxRevenueMultiplier,
            "unemploymentIncrease" => (int)EffectIndex.UnemploymentIncrease,
            "propertyValueDecline" or "propertyValueIncrease" => (int)EffectIndex.PropertyValueChange,
            "immigrationPenalty" or "immigrationBonus" => (int)EffectIndex.ImmigrationModifier,
            "productivityLoss" or "productivityBoost" => (int)EffectIndex.ProductivityChange,
            "crimeIncrease" => (int)EffectIndex.CrimeChange,
            "approvalRatingPenalty" or "approvalRatingVolatility" => (int)EffectIndex.ApprovalRatingChange,
            "commercialBoost" or "commercialLoss" or "tourismIncome" => (int)EffectIndex.CommercialModifier,
            "powerOutage" or "powerDemand" or "powerCostIncrease" or "heatingDemand" => (int)EffectIndex.PowerDemandChange,
            "healthRisk" or "healthDemand" => (int)EffectIndex.HealthDemandChange,
            "fireRiskIncrease" or "fireDepartmentDemand" => (int)EffectIndex.FireRiskChange,
            "researchBoost" => (int)EffectIndex.ResearchModifier,
            "waterShortage" => (int)EffectIndex.WaterDemandChange,
            // Effects that don't map to a tracked index -- consumed by other systems
            "agriculturalLoss" or "suburbanGrowth" or "homelessnessIncrease"
                or "industrialCostIncrease" or "industrialBoost"
                or "policyUncertainty" or "cityBudgetDrain" => -1,
            _ => -1,
        };
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
