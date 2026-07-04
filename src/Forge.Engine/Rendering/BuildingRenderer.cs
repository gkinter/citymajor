using System.Runtime.CompilerServices;
using Forge.Engine.Core;
using Forge.Engine.Math;
using Forge.Engine.Simulation;

namespace Forge.Engine.Rendering;

/// <summary>
/// Procedural isometric building renderer with rich architectural detail.
/// Each building is composed of multiple instanced quads: wall faces, roof,
/// windows, doors, floor lines, shadows, scaffolding, and rooftop equipment.
///
/// Building category by TypeId:
///   100-199: Residential low-density
///   200-299: Residential high-density
///   300-399: Commercial
///   400-499: Industrial
///   Other:   Service buildings
///
/// Era is derived from TypeId sub-ranges within each category:
///   x00-x19: Frontier/Colonial    (era 0)
///   x20-x39: Industrial           (era 1)
///   x40-x59: Postwar              (era 2)
///   x60-x79: Modern               (era 3)
///   x80-x99: Future               (era 4)
///
/// Features:
///   - Window grid on both faces (warm yellow at night, ~25% randomly dark)
///   - Roofline variation by category (peaked, flat+lip, sawtooth, chimney)
///   - Door/entrance on the front (right) face with optional awning
///   - Horizontal floor-separation lines on wall faces
///   - Ground shadow parallelogram scaled by building height
///   - Rich era-aware color palette
///   - Construction state: scaffolding + partial walls, no windows
///   - Abandoned state: desaturated colors, broken windows, uneven roof
/// </summary>
public sealed class BuildingRenderer
{
    private readonly Config _config;

    // Building category classification by TypeId ranges
    private const ushort ResLowStart = 100;
    private const ushort ResLowEnd = 199;
    private const ushort ResHighStart = 200;
    private const ushort ResHighEnd = 299;
    private const ushort ComStart = 300;
    private const ushort ComEnd = 399;
    private const ushort IndStart = 400;
    private const ushort IndEnd = 499;

    // Building state constants (matches BuildingData.State)
    private const byte StateConstructing = 0;
    private const byte StateOperational = 1;
    private const byte StateAbandoned = 2;

    // Height in pixels per story (at zoom=1)
    private const float PixelsPerStory = 16f;

    // Construction threshold: buildings with condition < this are shown as under construction
    private const byte ConstructionConditionThreshold = 50;

    // Night window light threshold
    private const float NightWindowThreshold = 0.3f;

    // Shadow parameters
    private const float ShadowAlpha = 0.25f;
    private const float ShadowLengthPerStory = 4f;

    // Cached building visuals for the current frame
    private readonly BuildingVisual[] _visuals;
    private int _visualCount;

    private struct BuildingVisual
    {
        public float ScreenX;
        public float ScreenY;
        public float SortY;
        public float HeightPx;
        public float BaseHalfW;
        public float BaseHalfH;
        public float RoofR, RoofG, RoofB;
        public float LeftR, LeftG, LeftB;
        public float RightR, RightG, RightB;
        public float BaseR, BaseG, BaseB; // original unmixed base color for details
        public float Progress;
        public int WindowSeed;
        public int Stories;
        public bool ShowWindows;
        public BuildingCategory Category;
        public byte BuildingEra;
        public byte State;
        public float Zoom;
    }

    public BuildingRenderer(Config config)
    {
        _config = config;
        _visuals = new BuildingVisual[16384];
    }

    /// <summary>
    /// Update building visuals from the current simulation snapshot.
    /// Call once per frame before Render().
    /// </summary>
    public void Update(SimSnapshot snapshot, IsometricCamera camera, float nightStrength,
                       int viewportWidth = 1280, int viewportHeight = 720)
    {
        _visualCount = 0;

        if (snapshot.BuildingCount == 0) return;

        float zoom = camera.SmoothZoom;
        var (offsetX, offsetY) = camera.GetRenderOffset();
        int tileW = _config.TileWidth;
        int tileH = _config.TileHeight;
        int rotation = camera.Rotation;
        float halfW = tileW * 0.5f * zoom;
        float halfH = tileH * 0.5f * zoom;

        bool showWindows = nightStrength > NightWindowThreshold;

        int count = System.Math.Min(snapshot.BuildingCount, _visuals.Length);

        for (int i = 0; i < count; i++)
        {
            ref readonly var b = ref snapshot.Buildings[i];

            if (b.State == 3) continue; // StateDemolishing

            var (sx, sy) = IsometricMath.GridToScreen(b.GridX, b.GridY, tileW, tileH, rotation);
            float drawX = sx * zoom + offsetX;
            float drawY = sy * zoom + offsetY;

            float maxBuildingHeight = 192f * zoom;
            if (drawX + halfW < 0 || drawX - halfW > viewportWidth ||
                drawY + halfH * 2f < 0 || drawY - maxBuildingHeight > viewportHeight)
            {
                continue;
            }

            var category = ClassifyBuilding(b.TypeId);
            byte era = DeriveEra(b.TypeId, category);
            int stories = ComputeStories(category, b.Level);
            float heightPx = stories * PixelsPerStory * zoom;

            float progress = 1f;
            if (b.State == StateConstructing || b.Condition < ConstructionConditionThreshold)
            {
                progress = b.State == StateConstructing
                    ? System.Math.Clamp(b.Condition / 255f, 0.1f, 1f)
                    : System.Math.Clamp(b.Condition / (float)ConstructionConditionThreshold, 0.1f, 1f);
            }

            var (baseR, baseG, baseB) = GetBuildingColor(category, era, b.TypeId, b.Level);
            float origR = baseR, origG = baseG, origB = baseB;

            // Abandoned buildings: desaturate and darken
            if (b.State == StateAbandoned)
            {
                float grey = (baseR + baseG + baseB) / 3f;
                baseR = baseR * 0.4f + grey * 0.6f;
                baseG = baseG * 0.4f + grey * 0.6f;
                baseB = baseB * 0.4f + grey * 0.6f;
                baseR *= 0.7f;
                baseG *= 0.7f;
                baseB *= 0.7f;
            }

            if (_visualCount >= _visuals.Length) break;

            ref var vis = ref _visuals[_visualCount];
            vis.ScreenX = drawX;
            vis.ScreenY = drawY;
            vis.SortY = drawY + halfH * 2f;
            vis.HeightPx = heightPx * progress;
            vis.BaseHalfW = halfW;
            vis.BaseHalfH = halfH;

            // Roof: brightest
            vis.RoofR = System.Math.Min(baseR * 1.2f, 1f);
            vis.RoofG = System.Math.Min(baseG * 1.2f, 1f);
            vis.RoofB = System.Math.Min(baseB * 1.2f, 1f);

            // Left face: medium shade
            vis.LeftR = baseR * 0.75f;
            vis.LeftG = baseG * 0.75f;
            vis.LeftB = baseB * 0.75f;

            // Right face: darkest shade
            vis.RightR = baseR * 0.55f;
            vis.RightG = baseG * 0.55f;
            vis.RightB = baseB * 0.55f;

            vis.BaseR = origR;
            vis.BaseG = origG;
            vis.BaseB = origB;

            vis.Progress = progress;
            vis.WindowSeed = b.GridX * 73856093 ^ b.GridY * 19349669 ^ b.TypeId * 83492791;
            vis.Stories = stories;
            vis.ShowWindows = showWindows && b.State == StateOperational && progress >= 0.9f;
            vis.Category = category;
            vis.BuildingEra = era;
            vis.State = b.State;
            vis.Zoom = zoom;

            _visualCount++;
        }

        if (_visualCount > 1)
        {
            Array.Sort(_visuals, 0, _visualCount, BuildingVisualComparer.Instance);
        }
    }

    /// <summary>
    /// Render all visible buildings with full architectural detail.
    /// </summary>
    public void Render(SpriteRenderer sprites, float nightStrength)
    {
        for (int i = 0; i < _visualCount; i++)
        {
            ref readonly var vis = ref _visuals[i];

            // Layer 0: Ground shadow
            RenderGroundShadow(sprites, in vis);

            // Layer 1: Wall faces
            RenderWallFaces(sprites, in vis);

            // Layer 2: Floor separation lines
            if (vis.State != StateConstructing && vis.Progress >= 0.5f)
            {
                RenderFloorLines(sprites, in vis);
            }

            // Layer 3: Windows (day = subtle, night = glowing)
            if (vis.State == StateOperational && vis.Progress >= 0.9f)
            {
                RenderWindows(sprites, in vis, nightStrength);
            }

            // Layer 4: Door/entrance
            if (vis.State != StateConstructing && vis.Progress >= 0.7f)
            {
                RenderDoor(sprites, in vis);
            }

            // Layer 5: Roof with category-specific variation
            RenderRoof(sprites, in vis);

            // Layer 6: Rooftop details (equipment, chimneys)
            if (vis.State == StateOperational)
            {
                RenderRooftopDetails(sprites, in vis);
            }

            // Layer 7: Construction scaffolding
            if (vis.State == StateConstructing || vis.Progress < 1f)
            {
                RenderScaffolding(sprites, in vis);
            }
        }
    }

    // =========================================================================
    // Ground shadow
    // =========================================================================

    private static void RenderGroundShadow(SpriteRenderer sprites, in BuildingVisual vis)
    {
        float hw = vis.BaseHalfW;
        float hh = vis.BaseHalfH;
        float shadowLen = vis.Stories * ShadowLengthPerStory * vis.Zoom;

        // Shadow extends from base toward bottom-left as a parallelogram.
        // Approximate as a quad offset to the left and below the building base.
        float shadowX = vis.ScreenX - hw - shadowLen * 0.6f;
        float shadowY = vis.ScreenY + hh * 1.5f;
        float shadowW = hw + shadowLen * 0.6f;
        float shadowH = hh * 0.8f;

        sprites.DrawInstanced(
            shadowX, shadowY, shadowW, shadowH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            0f, 0f, 0f, ShadowAlpha,
            vis.SortY - 0.5f // draw behind the building
        );
    }

    // =========================================================================
    // Wall faces
    // =========================================================================

    private static void RenderWallFaces(SpriteRenderer sprites, in BuildingVisual vis)
    {
        float hw = vis.BaseHalfW;
        float hh = vis.BaseHalfH;
        float h = vis.HeightPx;
        float roofTop = vis.ScreenY - h;

        // Left face
        float leftX = vis.ScreenX - hw;
        float leftY = roofTop + hh;
        float leftW = hw;
        float leftH = h + hh;
        sprites.DrawInstanced(
            leftX, leftY, leftW, leftH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.LeftR, vis.LeftG, vis.LeftB, 1f,
            vis.SortY + 0.1f
        );

        // Right face
        float rightX = vis.ScreenX;
        float rightY = roofTop + hh;
        float rightW = hw;
        float rightH = h + hh;
        sprites.DrawInstanced(
            rightX, rightY, rightW, rightH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.RightR, vis.RightG, vis.RightB, 1f,
            vis.SortY + 0.1f
        );
    }

    // =========================================================================
    // Floor separation lines (horizontal bands on wall faces)
    // =========================================================================

    private static void RenderFloorLines(SpriteRenderer sprites, in BuildingVisual vis)
    {
        if (vis.Stories <= 1) return;

        float hw = vis.BaseHalfW;
        float hh = vis.BaseHalfH;
        float h = vis.HeightPx;
        float roofTop = vis.ScreenY - h;

        float lineThickness = System.Math.Max(1f, vis.Zoom);

        // Slightly darker than the wall face for floor lines
        float leftLineR = vis.LeftR * 0.8f;
        float leftLineG = vis.LeftG * 0.8f;
        float leftLineB = vis.LeftB * 0.8f;
        float rightLineR = vis.RightR * 0.8f;
        float rightLineG = vis.RightG * 0.8f;
        float rightLineB = vis.RightB * 0.8f;

        float storyHeight = h / vis.Stories;

        for (int s = 1; s < vis.Stories; s++)
        {
            float lineY = roofTop + hh + s * storyHeight;

            // Left face line
            sprites.DrawInstanced(
                vis.ScreenX - hw + 1f, lineY, hw - 2f, lineThickness,
                0f,
                0f, 0f, 0.01f, 0.01f,
                leftLineR, leftLineG, leftLineB, 0.6f,
                vis.SortY + 0.15f
            );

            // Right face line
            sprites.DrawInstanced(
                vis.ScreenX + 1f, lineY, hw - 2f, lineThickness,
                0f,
                0f, 0f, 0.01f, 0.01f,
                rightLineR, rightLineG, rightLineB, 0.6f,
                vis.SortY + 0.15f
            );
        }

        // Brick-like horizontal lines for residential frontier/industrial era
        if ((vis.Category == BuildingCategory.ResidentialLow || vis.Category == BuildingCategory.ResidentialHigh)
            && vis.BuildingEra <= 1)
        {
            float brickSpacing = System.Math.Max(3f, 4f * vis.Zoom);
            float brickThickness = System.Math.Max(0.5f, 0.5f * vis.Zoom);
            float faceTop = roofTop + hh;
            float faceBottom = faceTop + h;

            for (float by = faceTop + brickSpacing; by < faceBottom; by += brickSpacing)
            {
                // Left face brick lines
                sprites.DrawInstanced(
                    vis.ScreenX - hw + 1f, by, hw - 2f, brickThickness,
                    0f,
                    0f, 0f, 0.01f, 0.01f,
                    leftLineR, leftLineG, leftLineB, 0.25f,
                    vis.SortY + 0.12f
                );

                // Right face brick lines
                sprites.DrawInstanced(
                    vis.ScreenX + 1f, by, hw - 2f, brickThickness,
                    0f,
                    0f, 0f, 0.01f, 0.01f,
                    rightLineR, rightLineG, rightLineB, 0.25f,
                    vis.SortY + 0.12f
                );
            }
        }
    }

    // =========================================================================
    // Windows
    // =========================================================================

    private static void RenderWindows(SpriteRenderer sprites, in BuildingVisual vis, float nightStrength)
    {
        float hw = vis.BaseHalfW;
        float hh = vis.BaseHalfH;
        float h = vis.HeightPx;

        bool isNight = nightStrength > NightWindowThreshold;

        // Night window glow color: warm yellow #FFE082
        float nightAlpha = isNight
            ? System.Math.Clamp((nightStrength - NightWindowThreshold) / (1f - NightWindowThreshold), 0f, 1f) * 0.85f
            : 0f;

        // Day window color: slightly lighter than wall
        float dayWinLeftR = System.Math.Min(vis.LeftR * 1.3f, 1f);
        float dayWinLeftG = System.Math.Min(vis.LeftG * 1.3f, 1f);
        float dayWinLeftB = System.Math.Min(vis.LeftB * 1.35f, 1f);
        float dayWinRightR = System.Math.Min(vis.RightR * 1.3f, 1f);
        float dayWinRightG = System.Math.Min(vis.RightG * 1.3f, 1f);
        float dayWinRightB = System.Math.Min(vis.RightB * 1.35f, 1f);

        // Night window color: warm yellow #FFE082 = (1.0, 0.878, 0.510)
        float nightWinR = 1.0f;
        float nightWinG = 0.878f;
        float nightWinB = 0.510f;

        // Window grid: 2-3 columns per face, 1 row per story
        int windowCols = System.Math.Max(1, (int)(hw / 10f));
        if (windowCols > 4) windowCols = 4;

        int windowRows = vis.Stories;
        float windowW = System.Math.Clamp(2f * vis.Zoom, 1.5f, 5f);
        float windowH = System.Math.Clamp(3f * vis.Zoom, 2f, 7f);

        // Insets from face edges
        float marginX = hw * 0.15f;
        float marginY = h * 0.08f / vis.Stories;

        uint seed = (uint)vis.WindowSeed;

        // Draw windows on both faces
        for (int face = 0; face < 2; face++)
        {
            float faceX = face == 0 ? vis.ScreenX - hw : vis.ScreenX;
            float faceY = vis.ScreenY - h + hh;
            float faceW = hw;
            float faceH = h;

            float winR, winG, winB, winA;
            if (isNight)
            {
                winR = nightWinR;
                winG = nightWinG;
                winB = nightWinB;
                winA = nightAlpha;
            }
            else
            {
                winR = face == 0 ? dayWinLeftR : dayWinRightR;
                winG = face == 0 ? dayWinLeftG : dayWinRightG;
                winB = face == 0 ? dayWinLeftB : dayWinRightB;
                winA = 0.7f;
            }

            float storyHeight = faceH / windowRows;

            for (int row = 0; row < windowRows; row++)
            {
                // Skip the bottom portion of the ground floor (where door goes)
                float rowCenterY = faceY + (row + 0.45f) * storyHeight;

                for (int col = 0; col < windowCols; col++)
                {
                    // Deterministic on/off per window
                    seed = HashStep(seed);
                    bool windowDark = (seed & 3) == 0; // ~25% dark

                    // For abandoned buildings, more windows are dark/broken
                    if (vis.State == StateAbandoned)
                    {
                        seed = HashStep(seed);
                        if ((seed & 1) == 0) windowDark = true; // ~50% dark for abandoned
                    }

                    if (windowDark && isNight) continue; // dark window at night = skip
                    if (windowDark && !isNight) continue; // dark window during day = skip

                    float colSpacing = (faceW - 2f * marginX) / windowCols;
                    float wx = faceX + marginX + (col + 0.5f) * colSpacing - windowW * 0.5f;
                    float wy = rowCenterY - windowH * 0.5f;

                    // Abandoned: some windows slightly offset (broken)
                    if (vis.State == StateAbandoned)
                    {
                        seed = HashStep(seed);
                        if ((seed & 7) == 0)
                        {
                            // Broken window: darker patch instead
                            sprites.DrawInstanced(
                                wx, wy, windowW, windowH,
                                0f,
                                0f, 0f, 0.01f, 0.01f,
                                0.1f, 0.1f, 0.1f, 0.6f,
                                vis.SortY + 0.2f
                            );
                            continue;
                        }
                    }

                    sprites.DrawInstanced(
                        wx, wy, windowW, windowH,
                        0f,
                        0f, 0f, 0.01f, 0.01f,
                        winR, winG, winB, winA,
                        vis.SortY + 0.2f
                    );
                }
            }
        }
    }

    // =========================================================================
    // Door / entrance
    // =========================================================================

    private static void RenderDoor(SpriteRenderer sprites, in BuildingVisual vis)
    {
        float hw = vis.BaseHalfW;
        float hh = vis.BaseHalfH;
        float h = vis.HeightPx;

        // Door on the right (front-facing) side at ground level
        float doorW = System.Math.Clamp(3f * vis.Zoom, 2f, 8f);
        float doorH = System.Math.Clamp(4f * vis.Zoom, 3f, 10f);

        // Position: centered horizontally on the right face, at the bottom
        float doorX = vis.ScreenX + hw * 0.4f - doorW * 0.5f;
        float doorY = vis.ScreenY + hh * 2f - doorH - 1f * vis.Zoom;

        // Door color: darker than the wall
        float doorR = vis.RightR * 0.5f;
        float doorG = vis.RightG * 0.5f;
        float doorB = vis.RightB * 0.5f;

        sprites.DrawInstanced(
            doorX, doorY, doorW, doorH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            doorR, doorG, doorB, 1f,
            vis.SortY + 0.22f
        );

        // Commercial buildings: add an awning above the door
        if (vis.Category == BuildingCategory.Commercial)
        {
            float awningW = doorW * 1.8f;
            float awningH = System.Math.Max(2f, 2f * vis.Zoom);
            float awningX = doorX - (awningW - doorW) * 0.5f;
            float awningY = doorY - awningH;

            // Awning is lighter, with a slight color accent
            float awningR = System.Math.Min(vis.BaseR * 1.4f, 1f);
            float awningG = System.Math.Min(vis.BaseG * 1.2f, 1f);
            float awningB = System.Math.Min(vis.BaseB * 1.0f, 1f);

            sprites.DrawInstanced(
                awningX, awningY, awningW, awningH,
                0f,
                0f, 0f, 0.01f, 0.01f,
                awningR, awningG, awningB, 0.9f,
                vis.SortY + 0.23f
            );
        }
    }

    // =========================================================================
    // Roof with category-specific variation
    // =========================================================================

    private static void RenderRoof(SpriteRenderer sprites, in BuildingVisual vis)
    {
        float hw = vis.BaseHalfW;
        float hh = vis.BaseHalfH;
        float h = vis.HeightPx;
        float roofTop = vis.ScreenY - h;

        switch (vis.Category)
        {
            case BuildingCategory.ResidentialLow:
                RenderPeakedRoof(sprites, in vis, roofTop, hw, hh);
                break;

            case BuildingCategory.ResidentialHigh:
                RenderFlatRoofWithLip(sprites, in vis, roofTop, hw, hh);
                break;

            case BuildingCategory.Commercial:
                RenderFlatRoofWithLip(sprites, in vis, roofTop, hw, hh);
                break;

            case BuildingCategory.Industrial:
                RenderSawtoothRoof(sprites, in vis, roofTop, hw, hh);
                break;

            case BuildingCategory.Service:
                RenderFlatRoofWithAccent(sprites, in vis, roofTop, hw, hh);
                break;

            default:
                RenderFlatRoof(sprites, in vis, roofTop, hw, hh);
                break;
        }
    }

    /// <summary>Peaked roof for low-density residential: a triangle on top of the diamond.</summary>
    private static void RenderPeakedRoof(SpriteRenderer sprites, in BuildingVisual vis,
        float roofTop, float hw, float hh)
    {
        // Base roof diamond
        float roofX = vis.ScreenX - hw;
        float roofY = roofTop;
        float roofW = hw * 2f;
        float roofH = hh * 2f;

        // Darker roof color for the peaked part
        float peakR = vis.RoofR * 0.7f;
        float peakG = vis.RoofG * 0.7f;
        float peakB = vis.RoofB * 0.65f;

        sprites.DrawInstanced(
            roofX, roofY, roofW, roofH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            peakR, peakG, peakB, 1f,
            vis.SortY + 0.3f
        );

        // Peak ridge: a narrow bright line along the top center
        float ridgeH = System.Math.Max(2f, 3f * vis.Zoom);
        float peakHeight = System.Math.Max(3f, 5f * vis.Zoom);
        float ridgeX = vis.ScreenX - hw * 0.3f;
        float ridgeY = roofTop - peakHeight;
        float ridgeW = hw * 0.6f;

        // Left slope (from peak down to left edge of roof)
        sprites.DrawInstanced(
            vis.ScreenX - hw, roofTop, hw, peakHeight,
            0f,
            0f, 0f, 0.01f, 0.01f,
            peakR * 0.9f, peakG * 0.9f, peakB * 0.9f, 1f,
            vis.SortY + 0.31f
        );

        // Right slope (from peak down to right edge — slightly lighter)
        sprites.DrawInstanced(
            vis.ScreenX, roofTop, hw, peakHeight,
            0f,
            0f, 0f, 0.01f, 0.01f,
            peakR * 1.05f, peakG * 1.05f, peakB * 1.05f, 1f,
            vis.SortY + 0.31f
        );

        // Ridge highlight
        sprites.DrawInstanced(
            ridgeX, ridgeY, ridgeW, ridgeH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            System.Math.Min(peakR * 1.3f, 1f), System.Math.Min(peakG * 1.3f, 1f), System.Math.Min(peakB * 1.3f, 1f), 0.8f,
            vis.SortY + 0.32f
        );

        // Abandoned: slightly uneven roofline (offset one side by 1-2px)
        if (vis.State == StateAbandoned)
        {
            uint roofSeed = HashStep((uint)vis.WindowSeed ^ 0xDEAD);
            float offset = (roofSeed % 3) * vis.Zoom;
            sprites.DrawInstanced(
                vis.ScreenX - hw * 0.1f, ridgeY + offset, hw * 0.2f, ridgeH,
                0f,
                0f, 0f, 0.01f, 0.01f,
                peakR * 0.6f, peakG * 0.6f, peakB * 0.6f, 0.5f,
                vis.SortY + 0.33f
            );
        }
    }

    /// <summary>Flat roof with 1-2px edge lip for high-rise residential and commercial.</summary>
    private static void RenderFlatRoofWithLip(SpriteRenderer sprites, in BuildingVisual vis,
        float roofTop, float hw, float hh)
    {
        // Main roof diamond
        float roofX = vis.ScreenX - hw;
        float roofY = roofTop;
        float roofW = hw * 2f;
        float roofH = hh * 2f;

        sprites.DrawInstanced(
            roofX, roofY, roofW, roofH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.RoofR, vis.RoofG, vis.RoofB, 1f,
            vis.SortY + 0.3f
        );

        // Lip/parapet: darker border around the roof edge
        float lipThickness = System.Math.Max(1f, 1.5f * vis.Zoom);

        // Top edge
        sprites.DrawInstanced(
            roofX, roofY, roofW, lipThickness,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.RoofR * 0.7f, vis.RoofG * 0.7f, vis.RoofB * 0.7f, 0.8f,
            vis.SortY + 0.31f
        );

        // Bottom edge
        sprites.DrawInstanced(
            roofX, roofY + roofH - lipThickness, roofW, lipThickness,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.RoofR * 0.7f, vis.RoofG * 0.7f, vis.RoofB * 0.7f, 0.8f,
            vis.SortY + 0.31f
        );

        // Left edge
        sprites.DrawInstanced(
            roofX, roofY, lipThickness, roofH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.RoofR * 0.7f, vis.RoofG * 0.7f, vis.RoofB * 0.7f, 0.8f,
            vis.SortY + 0.31f
        );

        // Right edge
        sprites.DrawInstanced(
            roofX + roofW - lipThickness, roofY, lipThickness, roofH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.RoofR * 0.7f, vis.RoofG * 0.7f, vis.RoofB * 0.7f, 0.8f,
            vis.SortY + 0.31f
        );
    }

    /// <summary>Sawtooth (zigzag) roof for industrial buildings.</summary>
    private static void RenderSawtoothRoof(SpriteRenderer sprites, in BuildingVisual vis,
        float roofTop, float hw, float hh)
    {
        // Base roof
        float roofX = vis.ScreenX - hw;
        float roofY = roofTop;
        float roofW = hw * 2f;
        float roofH = hh * 2f;

        sprites.DrawInstanced(
            roofX, roofY, roofW, roofH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.RoofR * 0.85f, vis.RoofG * 0.85f, vis.RoofB * 0.85f, 1f,
            vis.SortY + 0.3f
        );

        // Sawtooth teeth across the top: alternating dark/light strips
        int teeth = System.Math.Max(2, (int)(hw * 2f / (8f * vis.Zoom)));
        float toothW = roofW / teeth;
        float toothH = System.Math.Max(2f, 3f * vis.Zoom);

        for (int t = 0; t < teeth; t++)
        {
            float tx = roofX + t * toothW;
            float shade = (t % 2 == 0) ? 0.65f : 0.9f;

            sprites.DrawInstanced(
                tx, roofY, toothW, toothH,
                0f,
                0f, 0f, 0.01f, 0.01f,
                vis.RoofR * shade, vis.RoofG * shade, vis.RoofB * shade, 1f,
                vis.SortY + 0.31f
            );
        }
    }

    /// <summary>Flat roof with a colored accent stripe for service buildings.</summary>
    private static void RenderFlatRoofWithAccent(SpriteRenderer sprites, in BuildingVisual vis,
        float roofTop, float hw, float hh)
    {
        // Main roof
        float roofX = vis.ScreenX - hw;
        float roofY = roofTop;
        float roofW = hw * 2f;
        float roofH = hh * 2f;

        // White/light base
        sprites.DrawInstanced(
            roofX, roofY, roofW, roofH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            0.92f, 0.92f, 0.90f, 1f,
            vis.SortY + 0.3f
        );

        // Colored accent stripe across the middle
        float stripeH = System.Math.Max(2f, 3f * vis.Zoom);
        float stripeY = roofY + (roofH - stripeH) * 0.5f;

        sprites.DrawInstanced(
            roofX + 2f, stripeY, roofW - 4f, stripeH,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.BaseR, vis.BaseG, vis.BaseB, 0.85f,
            vis.SortY + 0.31f
        );
    }

    /// <summary>Fallback flat roof.</summary>
    private static void RenderFlatRoof(SpriteRenderer sprites, in BuildingVisual vis,
        float roofTop, float hw, float hh)
    {
        sprites.DrawInstanced(
            vis.ScreenX - hw, roofTop, hw * 2f, hh * 2f,
            0f,
            0f, 0f, 0.01f, 0.01f,
            vis.RoofR, vis.RoofG, vis.RoofB, 1f,
            vis.SortY + 0.3f
        );
    }

    // =========================================================================
    // Rooftop details (equipment, chimneys, water tanks)
    // =========================================================================

    private static void RenderRooftopDetails(SpriteRenderer sprites, in BuildingVisual vis)
    {
        float hw = vis.BaseHalfW;
        float h = vis.HeightPx;
        float roofTop = vis.ScreenY - h;

        uint seed = HashStep((uint)vis.WindowSeed ^ 0xBEEF);

        switch (vis.Category)
        {
            case BuildingCategory.Commercial:
            {
                // Mechanical equipment blob: small darker rectangle on roof
                float equipW = System.Math.Max(4f, hw * 0.3f);
                float equipH = System.Math.Max(3f, 4f * vis.Zoom);
                float equipX = vis.ScreenX - equipW * 0.5f + (seed % 5 - 2) * vis.Zoom;
                float equipY = roofTop - equipH * 0.3f;

                sprites.DrawInstanced(
                    equipX, equipY, equipW, equipH,
                    0f,
                    0f, 0f, 0.01f, 0.01f,
                    0.35f, 0.35f, 0.38f, 0.85f,
                    vis.SortY + 0.35f
                );
                break;
            }

            case BuildingCategory.Industrial:
            {
                // Chimney: thin tall rectangle
                float chimW = System.Math.Max(2f, 3f * vis.Zoom);
                float chimH = System.Math.Max(6f, 10f * vis.Zoom);
                float chimX = vis.ScreenX + hw * 0.2f;
                float chimY = roofTop - chimH;

                sprites.DrawInstanced(
                    chimX, chimY, chimW, chimH,
                    0f,
                    0f, 0f, 0.01f, 0.01f,
                    0.30f, 0.28f, 0.25f, 1f,
                    vis.SortY + 0.35f
                );

                // Chimney cap
                sprites.DrawInstanced(
                    chimX - 1f * vis.Zoom, chimY, chimW + 2f * vis.Zoom, System.Math.Max(1f, 1.5f * vis.Zoom),
                    0f,
                    0f, 0f, 0.01f, 0.01f,
                    0.25f, 0.23f, 0.20f, 1f,
                    vis.SortY + 0.36f
                );
                break;
            }

            case BuildingCategory.ResidentialHigh:
            {
                // Water tank: small box on tall buildings (4+ stories)
                if (vis.Stories >= 4)
                {
                    float tankW = System.Math.Max(3f, hw * 0.25f);
                    float tankH = System.Math.Max(3f, 4f * vis.Zoom);
                    float tankX = vis.ScreenX - hw * 0.3f;
                    float tankY = roofTop - tankH * 0.5f;

                    sprites.DrawInstanced(
                        tankX, tankY, tankW, tankH,
                        0f,
                        0f, 0f, 0.01f, 0.01f,
                        0.45f, 0.45f, 0.50f, 0.8f,
                        vis.SortY + 0.35f
                    );
                }
                break;
            }
        }
    }

    // =========================================================================
    // Construction scaffolding
    // =========================================================================

    private static void RenderScaffolding(SpriteRenderer sprites, in BuildingVisual vis)
    {
        if (vis.State != StateConstructing && vis.Progress >= 0.95f) return;

        float hw = vis.BaseHalfW;
        float hh = vis.BaseHalfH;
        float h = vis.HeightPx;
        float roofTop = vis.ScreenY - h;

        // Scaffolding color: light brown wood
        float scR = 0.55f;
        float scG = 0.40f;
        float scB = 0.25f;
        float scA = 0.7f;

        float poleW = System.Math.Max(1f, 1.5f * vis.Zoom);
        float faceH = h + hh;

        // Vertical poles on left face
        for (int p = 0; p < 3; p++)
        {
            float px = vis.ScreenX - hw + (p + 0.5f) * (hw / 3f);
            float py = roofTop + hh;

            sprites.DrawInstanced(
                px, py, poleW, faceH,
                0f,
                0f, 0f, 0.01f, 0.01f,
                scR, scG, scB, scA,
                vis.SortY + 0.4f
            );
        }

        // Vertical poles on right face
        for (int p = 0; p < 3; p++)
        {
            float px = vis.ScreenX + (p + 0.5f) * (hw / 3f);
            float py = roofTop + hh;

            sprites.DrawInstanced(
                px, py, poleW, faceH,
                0f,
                0f, 0f, 0.01f, 0.01f,
                scR, scG, scB, scA,
                vis.SortY + 0.4f
            );
        }

        // Horizontal crossbeams every few pixels
        float beamSpacing = System.Math.Max(6f, 10f * vis.Zoom);
        float beamH = System.Math.Max(1f, vis.Zoom);

        for (float by = roofTop + hh + beamSpacing; by < roofTop + hh + faceH; by += beamSpacing)
        {
            // Left face crossbeam
            sprites.DrawInstanced(
                vis.ScreenX - hw, by, hw, beamH,
                0f,
                0f, 0f, 0.01f, 0.01f,
                scR * 0.9f, scG * 0.9f, scB * 0.9f, scA,
                vis.SortY + 0.41f
            );

            // Right face crossbeam
            sprites.DrawInstanced(
                vis.ScreenX, by, hw, beamH,
                0f,
                0f, 0f, 0.01f, 0.01f,
                scR * 0.9f, scG * 0.9f, scB * 0.9f, scA,
                vis.SortY + 0.41f
            );
        }
    }

    // =========================================================================
    // Building classification, era derivation, and color assignment
    // =========================================================================

    private enum BuildingCategory
    {
        ResidentialLow,
        ResidentialHigh,
        Commercial,
        Industrial,
        Service
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BuildingCategory ClassifyBuilding(ushort typeId)
    {
        if (typeId >= ResLowStart && typeId <= ResLowEnd) return BuildingCategory.ResidentialLow;
        if (typeId >= ResHighStart && typeId <= ResHighEnd) return BuildingCategory.ResidentialHigh;
        if (typeId >= ComStart && typeId <= ComEnd) return BuildingCategory.Commercial;
        if (typeId >= IndStart && typeId <= IndEnd) return BuildingCategory.Industrial;
        return BuildingCategory.Service;
    }

    /// <summary>
    /// Derive a building's visual era from its TypeId sub-range within its category.
    /// Each category spans 100 IDs; we divide into 5 era bands of 20 each.
    /// 0=Frontier, 1=Industrial, 2=Postwar, 3=Modern, 4=Future
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte DeriveEra(ushort typeId, BuildingCategory category)
    {
        int baseId = category switch
        {
            BuildingCategory.ResidentialLow => ResLowStart,
            BuildingCategory.ResidentialHigh => ResHighStart,
            BuildingCategory.Commercial => ComStart,
            BuildingCategory.Industrial => IndStart,
            _ => 0
        };

        if (category == BuildingCategory.Service)
        {
            // Service buildings don't have era variation; default to Modern
            return 3;
        }

        int offset = typeId - baseId;
        return (byte)System.Math.Clamp(offset / 20, 0, 4);
    }

    private static int ComputeStories(BuildingCategory category, byte level)
    {
        int baseStories = category switch
        {
            BuildingCategory.ResidentialLow => 1,
            BuildingCategory.ResidentialHigh => 3,
            BuildingCategory.Commercial => 2,
            BuildingCategory.Industrial => 1,
            BuildingCategory.Service => 2,
            _ => 1
        };

        int totalStories = category switch
        {
            BuildingCategory.ResidentialLow => baseStories + System.Math.Min(level - 1, 2),
            BuildingCategory.ResidentialHigh => baseStories + (level - 1) * 2,
            BuildingCategory.Commercial => baseStories + System.Math.Min((level - 1) * 2, 8),
            BuildingCategory.Industrial => baseStories + System.Math.Min(level - 1, 2),
            BuildingCategory.Service => baseStories + System.Math.Min(level - 1, 1),
            _ => baseStories
        };

        return System.Math.Clamp(totalStories, 1, 12);
    }

    /// <summary>
    /// Rich era-aware color palette. Returns the base wall color.
    /// </summary>
    private static (float R, float G, float B) GetBuildingColor(
        BuildingCategory category, byte era, ushort typeId, byte level)
    {
        float variation = (typeId % 7) / 7f;

        return category switch
        {
            BuildingCategory.ResidentialLow => era switch
            {
                0 => LerpColor(Hex(0x8D6E63), Hex(0xA1887F), variation), // Frontier: warm wood
                1 => LerpColor(Hex(0xB71C1C), Hex(0xC62828), variation), // Industrial: dark brick
                2 => LerpColor(Hex(0xECEFF1), Hex(0xFFF9C4), variation), // Postwar: light gray/cream
                3 => LerpColor(Hex(0xE0E0E0), Hex(0xCFD8DC), variation), // Modern: concrete
                4 => LerpColor(Hex(0xE1F5FE), Hex(0xB2EBF2), variation), // Future: white-blue
                _ => LerpColor(Hex(0xE0E0E0), Hex(0xCFD8DC), variation),
            },

            BuildingCategory.ResidentialHigh => era switch
            {
                0 => LerpColor(Hex(0x8D6E63), Hex(0xA1887F), variation),
                1 => LerpColor(Hex(0xC62828), Hex(0xD32F2F), variation), // Brighter brick
                2 => LerpColor(Hex(0xCFD8DC), Hex(0xECEFF1), variation), // Blue-gray
                3 => LerpColor(Hex(0x90CAF9), Hex(0xE0E0E0), variation), // Glass blue / concrete
                4 => LerpColor(Hex(0xB2EBF2), Hex(0xE1F5FE), variation), // Cyan tint
                _ => LerpColor(Hex(0xE0E0E0), Hex(0xCFD8DC), variation),
            },

            BuildingCategory.Commercial => era switch
            {
                0 => LerpColor(Hex(0x78909C), Hex(0x8D6E63), variation), // Early commercial: steel/wood
                1 => LerpColor(Hex(0x455A64), Hex(0x607D8B), variation), // Industrial: dark steel
                2 => LerpColor(Hex(0x78909C), Hex(0x90A4AE), variation), // Postwar: lighter steel
                3 => LerpColor(Hex(0x1565C0), Hex(0x0D47A1), variation), // Modern: blue glass
                4 => LerpColor(Hex(0x0D47A1), Hex(0x1565C0), variation), // Future: dark glass
                _ => LerpColor(Hex(0x1565C0), Hex(0x78909C), variation),
            },

            BuildingCategory.Industrial => era switch
            {
                0 => LerpColor(Hex(0x5D4037), Hex(0x6D4C41), variation), // Frontier: rust/wood
                1 => LerpColor(Hex(0x37474F), Hex(0x455A64), variation), // Industrial: charcoal
                2 => LerpColor(Hex(0x455A64), Hex(0x546E7A), variation), // Postwar: dark steel
                3 => LerpColor(Hex(0x455A64), Hex(0x607D8B), variation), // Modern: medium steel
                4 => LerpColor(Hex(0x546E7A), Hex(0x78909C), variation), // Future: lighter steel
                _ => LerpColor(Hex(0x455A64), Hex(0x37474F), variation),
            },

            BuildingCategory.Service => GetServiceColor(typeId),

            _ => (0.6f, 0.6f, 0.6f)
        };
    }

    private static (float R, float G, float B) GetServiceColor(ushort typeId)
    {
        int group = typeId % 5;
        return group switch
        {
            0 => (0.85f, 0.40f, 0.35f), // fire station (red accent)
            1 => (0.35f, 0.50f, 0.80f), // police (blue accent)
            2 => (0.85f, 0.85f, 0.80f), // hospital (white/cream)
            3 => (0.45f, 0.70f, 0.45f), // school (green accent)
            _ => (0.80f, 0.75f, 0.65f), // generic civic (tan)
        };
    }

    // =========================================================================
    // Utility helpers
    // =========================================================================

    /// <summary>Convert a hex color (0xRRGGBB) to normalized float tuple.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (float R, float G, float B) Hex(int rgb)
    {
        return (
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (float R, float G, float B) LerpColor(
        (float R, float G, float B) a, (float R, float G, float B) b, float t)
    {
        return (
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HashStep(uint x)
    {
        x ^= x >> 16;
        x *= 0x45d9f3b;
        x ^= x >> 16;
        return x;
    }

    private sealed class BuildingVisualComparer : IComparer<BuildingVisual>
    {
        public static readonly BuildingVisualComparer Instance = new();
        public int Compare(BuildingVisual a, BuildingVisual b) => a.SortY.CompareTo(b.SortY);
    }
}
