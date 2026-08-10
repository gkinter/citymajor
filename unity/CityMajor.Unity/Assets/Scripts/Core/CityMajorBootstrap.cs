using CityMajor.Audio;
using CityMajor.Input;
using CityMajor.Platform;
using CityMajor.Rendering;
using CityMajor.Sim;
using CityMajor.UI;
using UnityEngine;

namespace CityMajor.Core
{
    /// <summary>
    /// Wires the Phase 1 play loop when Play.unity loads. Attach to a single GameObject in the scene.
    /// </summary>
    public sealed class CityMajorBootstrap : MonoBehaviour
    {
        [SerializeField] int mapSize = 256;
        [SerializeField] float tileSize = 2f;

        void Awake()
        {
            var root = new GameObject("CityMajor_Root");
            root.transform.SetParent(transform, false);

            var grid = root.AddComponent<ZoneGrid>();
            grid.Initialize(mapSize, tileSize);

            var terrain = GameObject.CreatePrimitive(PrimitiveType.Plane);
            terrain.name = "Terrain";
            terrain.transform.SetParent(root.transform, false);
            terrain.transform.localScale = new Vector3(mapSize * tileSize / 10f, 1f, mapSize * tileSize / 10f);
            terrain.transform.position = new Vector3(mapSize * tileSize * 0.5f, 0f, mapSize * tileSize * 0.5f);
            terrain.layer = LayerMask.NameToLayer("Default");

            var sim = root.AddComponent<CitySimBridge>();
            sim.Configure(mapSize);
            sim.BindGrid(grid);

            root.AddComponent<ZoneGrowthVisualizer>().Configure(sim);

            var camGo = new GameObject("CityCamera");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.12f, 0.18f);
            cam.orthographic = false;
            var isoCam = camGo.AddComponent<IsometricCameraController>();
            isoCam.FocusOn(new Vector3(mapSize * tileSize * 0.5f, 0f, mapSize * tileSize * 0.5f));

            var buildings = root.AddComponent<BuildingInstancer>();
            buildings.Configure(grid, sim, isoCam);

            var overlay = root.AddComponent<ZoneOverlayRenderer>();
            overlay.Configure(grid);

            var paint = root.AddComponent<ZonePaintTool>();
            paint.Configure(cam, grid, sim);

            var roads = root.AddComponent<RoadPaintTool>();
            roads.Configure(cam, grid, sim);

            var bulldoze = root.AddComponent<BulldozeTool>();
            bulldoze.Configure(cam, grid, sim);

            var buildPlop = root.AddComponent<BuildPlopTool>();
            buildPlop.Configure(cam, grid, sim);

            var roadOverlay = root.AddComponent<RoadOverlayRenderer>();
            roadOverlay.Configure(grid, sim);

            var vehicles = root.AddComponent<VehicleInstancer>();
            vehicles.Configure(grid, sim);

            var pedestrians = root.AddComponent<PedestrianInstancer>();
            pedestrians.Configure(grid, sim);

            root.AddComponent<ConstructionPropInstancer>().Configure(grid, sim);
            root.AddComponent<ServiceCoverageOverlay>().Configure(grid, sim);
            root.AddComponent<UtilityStressOverlay>().Configure(grid, sim);
            root.AddComponent<EdgeTrafficOverlay>().Configure(grid, sim);
            root.AddComponent<AmbientLifeController>().Configure(sim);
            root.AddComponent<AmbientAudioController>().Configure(sim);

            var hud = root.AddComponent<ResourcesHudController>();
            hud.Configure(sim);

            var eraBadgeUi = new GameObject("CityMajor_EraBadge");
            eraBadgeUi.transform.SetParent(root.transform, false);
            eraBadgeUi.AddComponent<EraBadgeController>().Configure(sim);

            var demandUi = new GameObject("CityMajor_DemandOverlay");
            demandUi.transform.SetParent(root.transform, false);
            demandUi.AddComponent<DemandOverlayController>().Configure(sim);

            var happinessUi = new GameObject("CityMajor_HappinessUi");
            happinessUi.transform.SetParent(root.transform, false);
            happinessUi.AddComponent<HappinessMeterController>().Configure(sim);

            var budgetUi = new GameObject("CityMajor_BudgetUi");
            budgetUi.transform.SetParent(root.transform, false);
            budgetUi.AddComponent<BudgetPanelController>().Configure(sim);

            var approvalUi = new GameObject("CityMajor_ApprovalUi");
            approvalUi.transform.SetParent(root.transform, false);
            approvalUi.AddComponent<ApprovalMeterController>().Configure(sim);

            var steamPresence = root.AddComponent<SteamRichPresenceController>();
            steamPresence.Configure(sim);

            var achievements = root.AddComponent<SteamAchievementTracker>();
            achievements.Configure(sim);

            var achievementToastUi = new GameObject("CityMajor_AchievementToast");
            achievementToastUi.transform.SetParent(root.transform, false);
            var toast = achievementToastUi.AddComponent<AchievementToastController>();
            toast.BindTracker(achievements);

            var researchUi = new GameObject("CityMajor_ResearchUi");
            researchUi.transform.SetParent(root.transform, false);
            researchUi.AddComponent<ResearchPanelController>().Configure(sim);

            var heraldUi = new GameObject("CityMajor_HeraldUi");
            heraldUi.transform.SetParent(root.transform, false);
            heraldUi.AddComponent<HeraldPanelController>().Configure(sim);

            var citizenUi = new GameObject("CityMajor_CitizenUi");
            citizenUi.transform.SetParent(root.transform, false);
            var citizenPanel = citizenUi.AddComponent<CitizenPanelController>();
            citizenPanel.Configure(sim);

            var tradeUi = new GameObject("CityMajor_TradeUi");
            tradeUi.transform.SetParent(root.transform, false);
            tradeUi.AddComponent<TradeStripController>().Configure(sim);

            var economyPoliticsUi = new GameObject("CityMajor_EconomyPoliticsUi");
            economyPoliticsUi.transform.SetParent(root.transform, false);
            var economyPanel = economyPoliticsUi.AddComponent<EconomyPanelController>();
            economyPanel.Configure(sim);
            var politicsPanel = economyPoliticsUi.AddComponent<PoliticsPanelController>();
            politicsPanel.Configure(sim);
            economyPoliticsUi.AddComponent<SimPanelToolbarController>().Configure(economyPanel, politicsPanel);

            var buildUi = new GameObject("CityMajor_BuildUi");
            buildUi.transform.SetParent(root.transform, false);
            buildUi.AddComponent<BuildPanelController>().Configure(sim, buildPlop);

            var lawUi = new GameObject("CityMajor_LawUi");
            lawUi.transform.SetParent(root.transform, false);
            lawUi.AddComponent<LawPanelController>().Configure(sim);

            var blueprintUi = new GameObject("CityMajor_BlueprintUi");
            blueprintUi.transform.SetParent(root.transform, false);
            blueprintUi.AddComponent<BlueprintPanelController>().Configure(sim);

            var tickerUi = new GameObject("CityMajor_TickerUi");
            tickerUi.transform.SetParent(root.transform, false);
            tickerUi.AddComponent<EventTickerController>().Configure(sim);

            var timeUi = new GameObject("CityMajor_TimeUi");
            timeUi.transform.SetParent(root.transform, false);
            timeUi.AddComponent<TimeControlsController>().Configure(sim);

            var saveUi = new GameObject("CityMajor_SaveLoadUi");
            saveUi.transform.SetParent(root.transform, false);
            saveUi.AddComponent<SaveLoadPanelController>().Configure(sim);

            var helpUi = new GameObject("CityMajor_HelpUi");
            helpUi.transform.SetParent(root.transform, false);
            helpUi.AddComponent<HelpPanelController>().Configure(sim);

            var toolHud = new GameObject("CityMajor_ToolModeHud");
            toolHud.transform.SetParent(root.transform, false);
            toolHud.AddComponent<ToolModeHudController>().Configure(paint, roads, buildPlop, bulldoze);

            root.AddComponent<CitizenPickTool>().Configure(cam, pedestrians, citizenPanel);

            root.AddComponent<OnboardingOverlayController>().Configure();

            // Disable template Main Camera if present
            var main = GameObject.Find("Main Camera");
            if (main != null && main != camGo)
                main.SetActive(false);
        }
    }
}
