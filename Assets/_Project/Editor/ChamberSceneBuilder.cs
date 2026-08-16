using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Recreates the physical chamber model defined in msu_anechoic/web/static/3d.js.
/// Diagnostic arrows, axes, trails, and configured-camera helpers are intentionally omitted.
/// </summary>
[InitializeOnLoad]
public static class ChamberSceneBuilder
{
    private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
    private const string RootName = "Chamber Geometry";
    private const string MaterialFolder = "Assets/_Project/Materials";

    private const float MetersPerInch = 0.0254f;
    private const float PanDiskDiameter = 28f * MetersPerInch;
    private const float PanDiskThickness = 1f * MetersPerInch;
    private const float PanDiskTopZeroLiftHeight = 57f * MetersPerInch;
    private const float PanDiskTopAboveTiltAxis = 3.75f * MetersPerInch;
    private const float TiltAxisZeroLiftHeight = PanDiskTopZeroLiftHeight - PanDiskTopAboveTiltAxis;
    private const float TiltDiskDiameter = 36f * MetersPerInch;
    private const float TiltDiskThickness = 1f * MetersPerInch;
    private const float AutMountHeight = 7.5f * MetersPerInch;
    private const float AutMountDepth = 16f * MetersPerInch;
    private const float AutMountWidth = 24f * MetersPerInch;
    private const float AutMountBackOffset = 1f * MetersPerInch;
    private const float AutMountClearance = 2f * MetersPerInch;
    private const float HousingOverallWidth = 27f * MetersPerInch;
    private const float HousingUprightWidth = 3f * MetersPerInch;
    private const float HousingConnectorHeight = 5f * MetersPerInch;
    private const float HousingBaseDepth = 19f * MetersPerInch;
    private const float HousingBackHeight = 4f * MetersPerInch;
    private const float HousingShaftHeight = 22.5f * MetersPerInch;
    private const float HousingShaftFrontOverhang = 1f * MetersPerInch;
    private const float HousingCrownDiameter = 4f * MetersPerInch;
    private const float TurnShaftDiameter = 2f * MetersPerInch;
    private const float TiltDiskHousingClearance = 0.01f;
    private const float TurnShaftEndExtension = 0.01f;

    private static readonly Color WallColor = Hex(0x183149);
    private static readonly Color FloorColor = Hex(0x353b42);
    private static readonly Color TableColor = Hex(0x777d84);
    private static readonly Color LiftColor = Hex(0x4f5964);
    private static readonly Color HousingColor = Hex(0x59636e);
    private static readonly Color PurpleColor = Hex(0x6f42c1);
    private static readonly Color OrangeColor = Hex(0xd46a1f);
    private static readonly Color YellowColor = Hex(0xe0b323);
    private static readonly Color StandColor = Hex(0xe4b51b);
    private static readonly Color DarkColor = Hex(0x151719);
    private static readonly Color SourceColor = Hex(0x2ecc71);
    private static readonly Color ConcreteColor = Hex(0x8a8d91);

    static ChamberSceneBuilder()
    {
        EditorApplication.delayCall += BuildNewProjectSceneIfNeeded;
    }

    [MenuItem("Tools/Chamber/Rebuild Main Scene Geometry")]
    public static void RebuildMainScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        BuildScene(scene);
    }

    // Entry point for headless verification.
    public static void RebuildMainSceneFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        BuildScene(scene);
    }

    public static void RebuildActiveMainSceneFromBridge()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MainScenePath)
        {
            throw new System.InvalidOperationException(
                $"The active scene must be {MainScenePath}, but it is {scene.path}.");
        }

        BuildScene(scene);
    }

    private static void BuildNewProjectSceneIfNeeded()
    {
        if (Application.isBatchMode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path == MainScenePath && GameObject.Find(RootName) == null)
        {
            BuildScene(scene);
        }
    }

    private static void BuildScene(Scene scene)
    {
        GameObject existing = GameObject.Find(RootName);
        bool preserveCutawayView = false;
        if (existing != null)
        {
            ChamberShellVisibilityController existingController =
                existing.GetComponent<ChamberShellVisibilityController>();
            preserveCutawayView = existingController != null && existingController.CutawayView;
            Object.DestroyImmediate(existing);
        }

        EnsureFolder(MaterialFolder);
        Material wall = GetMaterial("Wall", WallColor, 0f, 0f);
        Material floor = GetMaterial("Floor", FloorColor, 0f, 0f);
        Material table = GetMaterial("Table", TableColor, 0.15f, 0.2f);
        Material lift = GetMaterial("Lift", LiftColor, 0.4f, 0.45f);
        Material housing = GetMaterial("Housing", HousingColor, 0.3f, 0.35f);
        Material purple = GetMaterial("TurntablePurple", PurpleColor, 0.35f, 0.45f);
        Material orange = GetMaterial("TiltOrange", OrangeColor, 0.35f, 0.45f);
        Material yellow = GetMaterial("AntennaYellow", YellowColor, 0.2f, 0.45f);
        Material stand = GetMaterial("FloodStandYellow", StandColor, 0.25f, 0.45f);
        Material dark = GetMaterial("FixtureDark", DarkColor, 0.35f, 0.35f);
        Material source = GetMaterial("SourceGreen", SourceColor, 0.15f, 0.5f);
        Material concrete = GetMaterial("Concrete", ConcreteColor, 0f, 0.05f);
        Material lightPanel = GetMaterial("LightPanel", Color.white, 0f, 0.75f, true, 8f);

        Transform root = NewGroup(RootName, null);
        List<Renderer> shellRenderers = new();
        List<Renderer> cutawayRenderers = new();
        BuildContainingRoom(NewGroup("Containing Room", root), concrete, lightPanel,
            shellRenderers, cutawayRenderers);
        BuildArchitecture(NewGroup("Architecture", root), wall, floor,
            shellRenderers, cutawayRenderers);
        BuildLightingFixtures(NewGroup("Lighting Fixtures", root), stand, dark, lightPanel);
        BuildEquipment(NewGroup("Equipment", root), table, lift, housing, purple, orange, yellow, source);
        ChamberShellVisibilityController shellController =
            root.gameObject.AddComponent<ChamberShellVisibilityController>();
        shellController.Configure(shellRenderers.ToArray(), cutawayRenderers.ToArray());
        shellController.SetCutawayView(preserveCutawayView);
        ConfigureSceneCameraAndLight();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Built chamber geometry from the Three.js reference into Main.unity.");
    }

    private static void BuildContainingRoom(
        Transform parent,
        Material concrete,
        Material lightPanel,
        List<Renderer> shellRenderers,
        List<Renderer> cutawayRenderers)
    {
        // Chamber bounds are x = +/-2.5, z = -5..5, y = 0..3.5.
        // The containing room extends 2 m on each side, 4 m forward,
        // 3 m rearward, 0.3 m downward, and 2 m upward.
        const float left = -4.5f;
        const float right = 4.5f;
        const float front = -9f;
        const float rear = 8f;
        const float bottom = -0.3f;
        const float top = 5.5f;
        const float centerZ = (front + rear) / 2f;
        const float centerY = (bottom + top) / 2f;
        const float width = right - left;
        const float depth = rear - front;
        const float height = top - bottom;
        const float wallThickness = 0.15f;
        const float halfThickness = wallThickness / 2f;

        Transform shell = NewGroup("Concrete Shell", parent);
        // Closed slabs extend outward, preserving the original room's interior dimensions.
        ShellBox("Left Wall", shell,
            new Vector3(-(left - halfThickness), centerY, centerZ),
            new Vector3(wallThickness, height + wallThickness * 2f, depth + wallThickness * 2f),
            concrete, shellRenderers);
        ShellBox("Right Wall", shell,
            new Vector3(-(right + halfThickness), centerY, centerZ),
            new Vector3(wallThickness, height + wallThickness * 2f, depth + wallThickness * 2f),
            concrete, shellRenderers);
        ShellBox("Front Wall", shell, new Vector3(0f, centerY, front - halfThickness),
            new Vector3(width, height + wallThickness * 2f, wallThickness), concrete, shellRenderers);
        ShellBox("Rear Wall", shell, new Vector3(0f, centerY, rear + halfThickness),
            new Vector3(width, height + wallThickness * 2f, wallThickness), concrete, shellRenderers);
        ShellBox("Floor", shell, new Vector3(0f, bottom - halfThickness, centerZ),
            new Vector3(width, wallThickness, depth), concrete, shellRenderers);
        ShellBox("Ceiling", shell, new Vector3(0f, top + halfThickness, centerZ),
            new Vector3(width, wallThickness, depth), concrete, shellRenderers);

        Transform cutaway = NewGroup("Cutaway Surfaces", parent);
        CutawayQuad("Left Wall", cutaway, new Vector3(-left, centerY, centerZ),
            depth, height, Vector3.left, concrete, cutawayRenderers);
        CutawayQuad("Right Wall", cutaway, new Vector3(-right, centerY, centerZ),
            depth, height, Vector3.right, concrete, cutawayRenderers);
        CutawayQuad("Front Wall", cutaway, new Vector3(0f, centerY, front),
            width, height, Vector3.forward, concrete, cutawayRenderers);
        CutawayQuad("Rear Wall", cutaway, new Vector3(0f, centerY, rear),
            width, height, Vector3.back, concrete, cutawayRenderers);
        CutawayQuad("Floor", cutaway, new Vector3(0f, bottom, centerZ),
            width, depth, Vector3.up, concrete, cutawayRenderers);
        CutawayQuad("Ceiling", cutaway, new Vector3(0f, top, centerZ),
            width, depth, Vector3.down, concrete, cutawayRenderers);

        Transform ceilingLights = NewGroup("Ceiling Lights", parent);
        const float fixtureY = top - 0.04f;
        Vector3 fixtureSize = new(1.15f, 0.08f, 0.28f);
        Quaternion fixtureRotation = Quaternion.Euler(0f, 90f, 0f);
        float[] columnX = { -3.5f, -1.75f, 0f, 1.75f, 3.5f };
        float[] sideZ = { -4f, -0.5f, 3f };
        int fixtureNumber = 1;

        foreach (float x in columnX)
        {
            Box($"Ceiling Light {fixtureNumber++:00}", ceilingLights,
                new Vector3(x, fixtureY, front + 1.5f), fixtureSize, lightPanel, fixtureRotation);
        }
        foreach (float z in sideZ)
        {
            Box($"Ceiling Light {fixtureNumber++:00}", ceilingLights,
                new Vector3(columnX[0], fixtureY, z), fixtureSize, lightPanel, fixtureRotation);
            Box($"Ceiling Light {fixtureNumber++:00}", ceilingLights,
                new Vector3(columnX[columnX.Length - 1], fixtureY, z), fixtureSize, lightPanel, fixtureRotation);
        }
        foreach (float x in columnX)
        {
            Box($"Ceiling Light {fixtureNumber++:00}", ceilingLights,
                new Vector3(x, fixtureY, rear - 1.5f), fixtureSize, lightPanel, fixtureRotation);
        }

        Transform roomIllumination = NewGroup("Room Illumination", parent);
        foreach (float x in new[] { -2.1f, 2.1f })
        {
            foreach (float z in new[] { -4.5f, 3.5f })
            {
                SpotLight("Ceiling Illumination", roomIllumination,
                    new Vector3(-x, top - 0.12f, z), Vector3.down,
                    Color.white, 12f, 10f, 110f, 80f, true);
            }
        }
    }

    private static void BuildArchitecture(
        Transform parent,
        Material wall,
        Material floor,
        List<Renderer> shellRenderers,
        List<Renderer> cutawayRenderers)
    {
        const float wallThickness = 0.15f;
        const float halfThickness = wallThickness / 2f;

        // Closed wall slabs extend outward so the original 5 x 10 x 3.5 m
        // interior dimensions and both openings remain unchanged.
        Transform doorWall = NewGroup("Left Wall - Door", parent);
        ShellBox("Rear Section", doorWall, new Vector3(2.5f + halfThickness, 1.75f, 4f),
            new Vector3(wallThickness, 3.5f, 2f), wall, shellRenderers);
        ShellBox("Front Section", doorWall, new Vector3(2.5f + halfThickness, 1.75f, -1.5f),
            new Vector3(wallThickness, 3.5f, 7f), wall, shellRenderers);
        ShellBox("Above Door", doorWall, new Vector3(2.5f + halfThickness, 2.75f, 2.5f),
            new Vector3(wallThickness, 1.5f, 1f), wall, shellRenderers);
        ShellBox("Door Frame Front Jamb", doorWall, new Vector3(2.5f + halfThickness, 1f, 1.75f),
            new Vector3(wallThickness, 2f, 0.5f), wall, shellRenderers);
        ShellBox("Door Frame Rear Jamb", doorWall, new Vector3(2.5f + halfThickness, 1f, 3.25f),
            new Vector3(wallThickness, 2f, 0.5f), wall, shellRenderers);
        ShellBox("Door Frame Header", doorWall, new Vector3(2.5f + halfThickness, 2.25f, 2.5f),
            new Vector3(wallThickness, 0.5f, 2f), wall, shellRenderers);

        Transform solidWall = NewGroup("Right Wall - Solid", parent);
        ShellBox("Wall", solidWall, new Vector3(-2.5f - halfThickness, 1.75f, 0f),
            new Vector3(wallThickness, 3.5f, 10f), wall, shellRenderers);

        Transform backWall = NewGroup("Back Wall", parent);
        ShellBox("Wall", backWall, new Vector3(0f, 1.75f, 5f + halfThickness),
            new Vector3(5f, 3.5f, wallThickness), wall, shellRenderers);

        Transform frontWall = NewGroup("Front Wall", parent);
        float frontCenterZ = -5f - halfThickness;
        ShellBox("Right Section", frontWall, new Vector3(-1.4375f, 1.75f, frontCenterZ),
            new Vector3(2.125f, 3.5f, wallThickness), wall, shellRenderers);
        ShellBox("Left Section", frontWall, new Vector3(1.4375f, 1.75f, frontCenterZ),
            new Vector3(2.125f, 3.5f, wallThickness), wall, shellRenderers);
        ShellBox("Below Source Opening", frontWall, new Vector3(0f, 1.0625f, frontCenterZ),
            new Vector3(0.75f, 2.125f, wallThickness), wall, shellRenderers);
        ShellBox("Above Source Opening", frontWall, new Vector3(0f, 3.1875f, frontCenterZ),
            new Vector3(0.75f, 0.625f, wallThickness), wall, shellRenderers);
        ShellBox("Source Frame Right", frontWall, new Vector3(-0.625f, 2.5f, frontCenterZ),
            new Vector3(0.5f, 1.75f, wallThickness), wall, shellRenderers);
        ShellBox("Source Frame Left", frontWall, new Vector3(0.625f, 2.5f, frontCenterZ),
            new Vector3(0.5f, 1.75f, wallThickness), wall, shellRenderers);
        ShellBox("Source Frame Bottom", frontWall, new Vector3(0f, 1.875f, frontCenterZ),
            new Vector3(0.75f, 0.5f, wallThickness), wall, shellRenderers);
        ShellBox("Source Frame Top", frontWall, new Vector3(0f, 3.125f, frontCenterZ),
            new Vector3(0.75f, 0.5f, wallThickness), wall, shellRenderers);

        ShellBox("Floor", parent, new Vector3(0f, -halfThickness, 0f),
            new Vector3(5f, wallThickness, 10f), floor, shellRenderers);
        ShellBox("Ceiling", parent, new Vector3(0f, 3.5f + halfThickness, 0f),
            new Vector3(5f, wallThickness, 10f), wall, shellRenderers);

        Transform cutaway = NewGroup("Cutaway Surfaces", parent);
        CutawayQuad("Door Wall Rear Section", cutaway,
            new Vector3(2.5f, 1.75f, 4f), 2f, 3.5f, Vector3.left, wall, cutawayRenderers);
        CutawayQuad("Door Wall Front Section", cutaway,
            new Vector3(2.5f, 1.75f, -1.5f), 7f, 3.5f, Vector3.left, wall, cutawayRenderers);
        CutawayQuad("Door Wall Above Door", cutaway,
            new Vector3(2.5f, 2.75f, 2.5f), 1f, 1.5f, Vector3.left, wall, cutawayRenderers);
        CutawayQuad("Door Frame Front Jamb", cutaway,
            new Vector3(2.5f, 1f, 1.75f), 0.5f, 2f, Vector3.left, wall, cutawayRenderers);
        CutawayQuad("Door Frame Rear Jamb", cutaway,
            new Vector3(2.5f, 1f, 3.25f), 0.5f, 2f, Vector3.left, wall, cutawayRenderers);
        CutawayQuad("Door Frame Header", cutaway,
            new Vector3(2.5f, 2.25f, 2.5f), 2f, 0.5f, Vector3.left, wall, cutawayRenderers);

        CutawayQuad("Right Wall", cutaway,
            new Vector3(-2.5f, 1.75f, 0f), 10f, 3.5f, Vector3.right, wall, cutawayRenderers);
        CutawayQuad("Back Wall", cutaway,
            new Vector3(0f, 1.75f, 5f), 5f, 3.5f, Vector3.back, wall, cutawayRenderers);

        CutawayQuad("Front Wall Right Section", cutaway,
            new Vector3(-1.4375f, 1.75f, -5f), 2.125f, 3.5f, Vector3.forward, wall, cutawayRenderers);
        CutawayQuad("Front Wall Left Section", cutaway,
            new Vector3(1.4375f, 1.75f, -5f), 2.125f, 3.5f, Vector3.forward, wall, cutawayRenderers);
        CutawayQuad("Below Source Opening", cutaway,
            new Vector3(0f, 1.0625f, -5f), 0.75f, 2.125f, Vector3.forward, wall, cutawayRenderers);
        CutawayQuad("Above Source Opening", cutaway,
            new Vector3(0f, 3.1875f, -5f), 0.75f, 0.625f, Vector3.forward, wall, cutawayRenderers);
        CutawayQuad("Source Frame Right", cutaway,
            new Vector3(-0.625f, 2.5f, -5f), 0.5f, 1.75f, Vector3.forward, wall, cutawayRenderers);
        CutawayQuad("Source Frame Left", cutaway,
            new Vector3(0.625f, 2.5f, -5f), 0.5f, 1.75f, Vector3.forward, wall, cutawayRenderers);
        CutawayQuad("Source Frame Bottom", cutaway,
            new Vector3(0f, 1.875f, -5f), 0.75f, 0.5f, Vector3.forward, wall, cutawayRenderers);
        CutawayQuad("Source Frame Top", cutaway,
            new Vector3(0f, 3.125f, -5f), 0.75f, 0.5f, Vector3.forward, wall, cutawayRenderers);

        CutawayQuad("Floor", cutaway,
            new Vector3(0f, 0f, 0f), 5f, 10f, Vector3.up, floor, cutawayRenderers);
        CutawayQuad("Ceiling", cutaway,
            new Vector3(0f, 3.5f, 0f), 5f, 10f, Vector3.down, wall, cutawayRenderers);
    }

    private static void BuildLightingFixtures(Transform parent, Material stand, Material dark, Material lightPanel)
    {
        Transform backFixtures = NewGroup("Back Wall Fixtures", parent);
        foreach (float x in new[] { -1.5f, 1.5f })
        {
            GameObject fixture = Box("Light Fixture", backFixtures,
                new Vector3(x, 2.5f, 4.915f), new Vector3(0.1f, 0.3f, 0.02f), lightPanel);
            SpotLight("Illumination", fixture.transform, new Vector3(0f, 0f, -0.03f), Vector3.back,
                Color.white, 10f, 10f, 100f, 70f, true);
        }

        Transform floodStand = NewGroup("Flood Light Stand", parent);
        floodStand.localPosition = MirrorPosition(new Vector3(1.5f, 0f, 2f));
        Cylinder("Pole", floodStand, new Vector3(0f, 0.7f, 0f), 0.025f, 1.4f, stand);

        Vector3 legStart = new(0f, 0.28f, 0f);
        Rod("Foot Front Right", floodStand, legStart, new Vector3(0.3f, 0.02f, -0.45f), 0.018f, stand);
        Rod("Foot Rear Right", floodStand, legStart, new Vector3(0.3f, 0.02f, 0.45f), 0.018f, stand);
        Rod("Foot Left", floodStand, legStart, new Vector3(-0.5f, 0.02f, 0f), 0.018f, stand);
        Box("Crossbar", floodStand, new Vector3(0f, 1.38f, 0f), new Vector3(0.04f, 0.04f, 0.8f), stand);

        Transform heads = NewGroup("Fixed Flood Heads", parent);
        Vector3 sourceHeadPosition = new(1.5f, 1.5f, 2f);
        Vector3 sourceHeadTarget = new(0f, 1.925f, 2.9f);
        heads.localPosition = MirrorPosition(sourceHeadPosition);
        heads.localRotation = MirrorRotation(
            Quaternion.LookRotation(sourceHeadTarget - sourceHeadPosition, Vector3.up));
        foreach (float x in new[] { -0.22f, 0.22f })
        {
            Transform head = NewGroup(x < 0 ? "Right Head" : "Left Head", heads);
            head.localPosition = MirrorPosition(new Vector3(x, 0f, 0f));
            Box("Frame", head, Vector3.zero, new Vector3(0.32f, 0.22f, 0.07f), dark);
            Box("Panel", head, new Vector3(0f, 0f, 0.041f), new Vector3(0.26f, 0.16f, 0.012f), lightPanel);
            SpotLight("Illumination", head, new Vector3(0f, 0f, 0.055f), Vector3.forward,
                Color.white, 14f, 10f, 60f, 42f, true);
        }
    }

    private static void BuildEquipment(
        Transform parent,
        Material table,
        Material lift,
        Material housing,
        Material purple,
        Material orange,
        Material yellow,
        Material source)
    {
        Sphere("Source Antenna", parent, new Vector3(0f, 2.5f, -5f), 0.125f, source);

        Transform positioner = NewGroup("Turntable Positioner", parent);
        Box("Fixed Turntable Table", positioner, new Vector3(0f, 0.25f, 3.9f), new Vector3(0.75f, 0.5f, 1.5f), table);
        Box("Scissor Lift Bottom", positioner, new Vector3(0f, 0.55f, 3.9f), new Vector3(0.75f, 0.1f, 1.5f), lift);

        const float liftHeight = 0.2f;
        Transform heightAssembly = NewGroup("Height Assembly", positioner);
        heightAssembly.localPosition = MirrorPosition(new Vector3(0f, liftHeight, 0f));
        Box("Scissor Lift Top", heightAssembly, new Vector3(0f, 0.65f, 3.9f), new Vector3(0.75f, 0.1f, 1.5f), lift);
        BuildScissorForks(positioner, lift, liftHeight,
            out Transform[] risingForwardForks,
            out Transform[] risingBackwardForks);
        BuildHousing(heightAssembly, housing);
        BuildTurntable(
            heightAssembly,
            purple,
            orange,
            yellow,
            risingForwardForks,
            risingBackwardForks);
    }

    private static void BuildScissorForks(
        Transform parent,
        Material material,
        float height,
        out Transform[] risingForwardForks,
        out Transform[] risingBackwardForks)
    {
        Transform forks = NewGroup("Scissor Forks", parent);
        float horizontalSpan = Mathf.Sqrt(Mathf.Max(0.05f * 0.05f, 1.2f * 1.2f - height * height));
        float halfZ = horizontalSpan / 2f;
        List<Transform> forwardForks = new();
        List<Transform> backwardForks = new();
        foreach (float sideX in new[] { -0.3f, 0.3f })
        {
            GameObject forward = Rod("Rising Forward", forks,
                new Vector3(sideX - 0.015f, 0.6f, 3.9f - halfZ),
                new Vector3(sideX - 0.015f, 0.6f + height, 3.9f + halfZ),
                0.025f, material, true);
            GameObject backward = Rod("Rising Backward", forks,
                new Vector3(sideX + 0.015f, 0.6f, 3.9f + halfZ),
                new Vector3(sideX + 0.015f, 0.6f + height, 3.9f - halfZ),
                0.025f, material, true);
            forwardForks.Add(forward.transform);
            backwardForks.Add(backward.transform);
        }

        risingForwardForks = forwardForks.ToArray();
        risingBackwardForks = backwardForks.ToArray();
    }

    private static void BuildHousing(Transform parent, Material material)
    {
        float baseY = TiltAxisZeroLiftHeight - HousingShaftHeight;
        float hubY = TiltAxisZeroLiftHeight;
        float rearZ = 3f + HousingBaseDepth + HousingShaftFrontOverhang;
        float hubZ = 3f;
        float sideX = HousingOverallWidth / 2f - HousingUprightWidth / 2f;

        foreach (float x in new[] { -sideX, sideX })
        {
            string side = x < 0f ? "Right Housing" : "Left Housing";
            Transform housing = NewGroup(side, parent);
            Box("Base", housing,
                new Vector3(x, baseY + HousingBackHeight / 2f, 3f + HousingBaseDepth / 2f),
                new Vector3(HousingUprightWidth, HousingBackHeight, HousingBaseDepth), material);
            Rod("Angled Upright", housing,
                new Vector3(x, baseY + HousingBackHeight, rearZ),
                new Vector3(x, hubY, hubZ),
                HousingCrownDiameter / 2f, material, true);
            Cylinder("Crown", housing, new Vector3(x, hubY, hubZ), HousingCrownDiameter / 2f,
                HousingUprightWidth, material, Quaternion.Euler(0f, 0f, 90f));
        }

        Box("Housing Connector", parent,
            new Vector3(0f, baseY + HousingConnectorHeight / 2f,
                3f + HousingShaftFrontOverhang + HousingBaseDepth / 2f),
            new Vector3(HousingOverallWidth, HousingConnectorHeight, HousingBaseDepth), material);
    }

    private static void BuildTurntable(
        Transform parent,
        Material purple,
        Material orange,
        Material yellow,
        Transform[] risingForwardForks,
        Transform[] risingBackwardForks)
    {
        Transform turntable = NewGroup("Turntable", parent);
        turntable.localPosition = MirrorPosition(new Vector3(0f, TiltAxisZeroLiftHeight, 3f));
        Transform tiltAssembly = NewGroup("Tilt Assembly", turntable);

        float leftHousingOuterFaceX = -HousingOverallWidth / 2f;
        float tiltDiskInnerFaceX = leftHousingOuterFaceX - TiltDiskHousingClearance;
        float tiltDiskOuterFaceX = tiltDiskInnerFaceX - TiltDiskThickness;
        float tiltDiskX = (tiltDiskInnerFaceX + tiltDiskOuterFaceX) / 2f;
        Cylinder("Tilt Disk", tiltAssembly, new Vector3(tiltDiskX, 0f, 0f), TiltDiskDiameter / 2f,
            TiltDiskThickness, orange, Quaternion.Euler(0f, 0f, 90f));
        Box("Tilt Housing", tiltAssembly, new Vector3(0f, -0.1f, 0f), new Vector3(0.6f, 0.2f, 0.4f), orange);

        float shaftRight = HousingOverallWidth / 2f + TurnShaftEndExtension;
        float shaftLeft = tiltDiskOuterFaceX - TurnShaftEndExtension;
        Cylinder("Tilt Shaft", tiltAssembly, new Vector3((shaftRight + shaftLeft) / 2f, 0f, 0f),
            TurnShaftDiameter / 2f, shaftRight - shaftLeft, orange, Quaternion.Euler(0f, 0f, 90f));

        Transform panAssembly = NewGroup("Pan Assembly", tiltAssembly);
        panAssembly.localPosition = MirrorPosition(
            new Vector3(0f, PanDiskTopAboveTiltAxis - PanDiskThickness / 2f, 0f));
        Cylinder("Turning Surface", panAssembly, Vector3.zero, PanDiskDiameter / 2f, PanDiskThickness, purple);
        BuildAutMount(panAssembly, purple);
        BuildAntennaUnderTest(panAssembly, yellow);

        TurntableController controller = turntable.gameObject.AddComponent<TurntableController>();
        controller.Configure(
            panAssembly,
            tiltAssembly,
            parent,
            risingForwardForks,
            risingBackwardForks);
    }

    private static void BuildAutMount(Transform parent, Material material)
    {
        Transform mount = NewGroup("AUT Mount", parent);
        mount.localPosition = MirrorPosition(new Vector3(0f,
            PanDiskThickness / 2f + AutMountClearance + AutMountHeight / 2f,
            AutMountBackOffset));

        const float memberThickness = 0.02f;
        float plateY = (AutMountHeight - memberThickness) / 2f;
        float postHeight = AutMountHeight - 2f * memberThickness;
        Box("Bottom Plate", mount, new Vector3(0f, -plateY, 0f),
            new Vector3(AutMountWidth, memberThickness, AutMountDepth), material);
        Box("Top Plate", mount, new Vector3(0f, plateY, 0f),
            new Vector3(AutMountWidth, memberThickness, AutMountDepth), material);

        int index = 1;
        foreach (float x in new[] { -(AutMountWidth - memberThickness) / 2f, (AutMountWidth - memberThickness) / 2f })
        {
            foreach (float z in new[] { -(AutMountDepth - memberThickness) / 2f, (AutMountDepth - memberThickness) / 2f })
            {
                Box($"Post {index++}", mount, new Vector3(x, 0f, z),
                    new Vector3(memberThickness, postHeight, memberThickness), material);
            }
        }
    }

    private static void BuildAntennaUnderTest(Transform parent, Material material)
    {
        Transform aut = NewGroup("Antenna Under Test", parent);
        aut.localPosition = MirrorPosition(new Vector3(0f,
            PanDiskThickness / 2f + AutMountClearance + AutMountHeight,
            AutMountBackOffset));

        const float height = 0.2f;
        const float depth = 0.2f;
        const float tube = 0.025f;
        Box("Stem", aut, new Vector3(0f, height / 2f, 0f), new Vector3(tube, height, tube), material);
        Box("Arm", aut, new Vector3(0f, height - tube / 2f, -depth / 2f), new Vector3(tube, tube, depth), material);
        Transform pivot = NewGroup("Antenna Pivot", aut);
        pivot.localPosition = MirrorPosition(new Vector3(0f, height - tube / 2f, -depth));
        Box("Patch Antenna", pivot, new Vector3(0f, 0f, -0.01f), new Vector3(0.1f, 0.1f, 0.02f), material);
    }

    private static void ConfigureSceneCameraAndLight()
    {
        Camera camera = Object.FindFirstObjectByType<Camera>();
        if (camera != null)
        {
            Vector3 sourcePosition = new(-1.8f, 1.7f, -3.8f);
            Vector3 sourceTarget = new(0f, 1.25f, 3.2f);
            camera.transform.position = MirrorPosition(sourcePosition);
            camera.transform.rotation = MirrorRotation(
                Quaternion.LookRotation(sourceTarget - sourcePosition, Vector3.up));
            camera.nearClipPlane = 0.05f;
        }

        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.transform.rotation = MirrorRotation(Quaternion.Euler(50f, -30f, 0f));
            light.intensity = 1.2f;
            break;
        }
    }

    private static Transform NewGroup(string name, Transform parent)
    {
        GameObject group = new(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static GameObject Box(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 size,
        Material material,
        Quaternion? rotation = null)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        SetPrimitive(gameObject, name, parent, position, rotation ?? Quaternion.identity, size, material);
        return gameObject;
    }

    private static GameObject ShellBox(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 size,
        Material material,
        List<Renderer> shellRenderers)
    {
        GameObject gameObject = Box(name, parent, position, size, material);
        shellRenderers.Add(gameObject.GetComponent<Renderer>());
        return gameObject;
    }

    private static GameObject Quad(
        string name,
        Transform parent,
        Vector3 position,
        float width,
        float height,
        Vector3 inwardNormal,
        Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Mesh mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
        Vector3 primitiveNormal = mesh.normals.Length > 0 ? mesh.normals[0].normalized : Vector3.back;
        Quaternion rotation = Quaternion.FromToRotation(primitiveNormal, inwardNormal.normalized);
        SetPrimitive(gameObject, name, parent, position, rotation, new Vector3(width, height, 1f), material);
        return gameObject;
    }

    private static GameObject CutawayQuad(
        string name,
        Transform parent,
        Vector3 position,
        float width,
        float height,
        Vector3 inwardNormal,
        Material material,
        List<Renderer> cutawayRenderers)
    {
        GameObject gameObject = Quad(name, parent, position, width, height, inwardNormal, material);
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        Renderer renderer = gameObject.GetComponent<Renderer>();
        renderer.enabled = false;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        cutawayRenderers.Add(renderer);
        return gameObject;
    }

    private static Light SpotLight(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localDirection,
        Color color,
        float intensity,
        float range,
        float outerAngle,
        float innerAngle,
        bool castShadows)
    {
        GameObject gameObject = new(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = Quaternion.LookRotation(localDirection, Vector3.forward);

        Light light = gameObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = outerAngle;
        light.innerSpotAngle = innerAngle;
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = 0.85f;
        UniversalAdditionalLightData lightData =
            gameObject.GetComponent<UniversalAdditionalLightData>()
            ?? gameObject.AddComponent<UniversalAdditionalLightData>();
        SerializedObject serializedLightData = new(lightData);
        serializedLightData.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue =
            UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
        serializedLightData.ApplyModifiedPropertiesWithoutUndo();
        return light;
    }

    private static GameObject Sphere(string name, Transform parent, Vector3 position, float radius, Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        SetPrimitive(gameObject, name, parent, position, Quaternion.identity, Vector3.one * (radius * 2f), material);
        return gameObject;
    }

    private static GameObject Cylinder(
        string name,
        Transform parent,
        Vector3 position,
        float radius,
        float height,
        Material material,
        Quaternion? rotation = null)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Vector3 scale = new(radius * 2f, height / 2f, radius * 2f);
        SetPrimitive(gameObject, name, parent, position, rotation ?? Quaternion.identity, scale, material);
        return gameObject;
    }

    private static GameObject Rod(
        string name,
        Transform parent,
        Vector3 start,
        Vector3 end,
        float radius,
        Material material,
        bool square = false)
    {
        Vector3 direction = end - start;
        GameObject gameObject = square
            ? GameObject.CreatePrimitive(PrimitiveType.Cube)
            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Vector3 scale = square
            ? new Vector3(radius * 2f, direction.magnitude, radius * 2f)
            : new Vector3(radius * 2f, direction.magnitude / 2f, radius * 2f);
        SetPrimitive(gameObject, name, parent, (start + end) / 2f,
            Quaternion.FromToRotation(Vector3.up, direction.normalized), scale, material);
        return gameObject;
    }

    private static void SetPrimitive(
        GameObject gameObject,
        string name,
        Transform parent,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Material material)
    {
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = MirrorPosition(position);
        gameObject.transform.localRotation = MirrorRotation(rotation);
        gameObject.transform.localScale = scale;
        Renderer renderer = gameObject.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }

    private static Vector3 MirrorPosition(Vector3 position)
    {
        return new Vector3(-position.x, position.y, position.z);
    }

    private static Quaternion MirrorRotation(Quaternion rotation)
    {
        // For reflection S = diag(-1, 1, 1), the mirrored rotation is S * R * S.
        return new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
    }

    private static Material GetMaterial(
        string name,
        Color color,
        float metallic,
        float smoothness,
        bool emissive = false,
        float emissionIntensity = 3f)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
        material.doubleSidedGI = false;
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * emissionIntensity);
            }
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }
            current = next;
        }
    }

    private static Color Hex(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xff) / 255f,
            ((rgb >> 8) & 0xff) / 255f,
            (rgb & 0xff) / 255f,
            1f);
    }
}
