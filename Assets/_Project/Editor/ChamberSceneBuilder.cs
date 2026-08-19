using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Synchronizes the physical chamber model defined in msu_anechoic/web/static/3d.js.
/// Diagnostic arrows, axes, trails, and configured-camera helpers are intentionally omitted.
/// </summary>
[InitializeOnLoad]
public static class ChamberSceneBuilder
{
    private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
    private const string RootName = "Chamber Geometry";
    private const string MaterialFolder = "Assets/_Project/Materials";
    private const string MeshFolder = "Assets/_Project/Generated/Meshes";
    private const string RenderTextureFolder = "Assets/_Project/Generated/RenderTextures";
    private const string SpectrumAnalyzerTexturePath =
        "Assets/_Project/Textures/SpectrumAnalyzerScreen.png";

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
    private static readonly Color PanAssemblyColor = Hex(0x343a40);
    private static readonly Color TiltAssemblyColor = Hex(0x343a40);
    private static readonly Color YellowColor = Hex(0xe0b323);
    private static readonly Color StandColor = Hex(0xe4b51b);
    private static readonly Color DarkColor = Hex(0x151719);
    private static readonly Color SourceColor = Hex(0x2ecc71);
    private static readonly Color ConcreteColor = Hex(0x8a8d91);
    private static readonly Color PlayerColor = Hex(0x2d8cff);

    private static GameObject syncRoot;
    private static HashSet<GameObject> staleGeneratedObjects;
    private static HashSet<GameObject> claimedGeneratedObjects;

    static ChamberSceneBuilder()
    {
        EditorApplication.delayCall += BuildNewProjectSceneIfNeeded;
    }

    [MenuItem("Tools/Chamber/Sync Main Scene Geometry")]
    public static void RebuildMainScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        BuildScene(scene, false);
    }

    [MenuItem("Tools/Chamber/Full Rebuild Main Scene Geometry")]
    public static void FullRebuildMainScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()
            || !EditorUtility.DisplayDialog(
                "Full chamber rebuild",
                "This deletes and recreates the complete generated Chamber Geometry hierarchy. Use it only for structural changes that the normal sync cannot reconcile.",
                "Full Rebuild",
                "Cancel"))
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        BuildScene(scene, true);
    }

    // Entry point for headless verification.
    public static void RebuildMainSceneFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        BuildScene(scene, false);
    }

    public static void RebuildActiveMainSceneFromBridge()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MainScenePath)
        {
            throw new System.InvalidOperationException(
                $"The active scene must be {MainScenePath}, but it is {scene.path}.");
        }

        BuildScene(scene, false);
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
            BuildScene(scene, false);
        }
    }

    private static void BuildScene(Scene scene, bool fullRebuild)
    {
        GameObject existing = GameObject.Find(RootName);
        float preserveRoomOpacity = 100f;
        float preserveChamberOpacity = 100f;
        ChamberLightMode preserveChamberLightMode = ChamberLightMode.Auto;
        float preserveChamberLightTimeout = 30f;
        bool preserveFloodLightsOn = false;
        float preservePanDegrees = 0f;
        float preserveTiltDegrees = 0f;
        float preserveHeightMeters = 0.2f;
        float preservePolarityDegrees = 0f;
        if (existing != null)
        {
            Camera existingCamera = FindMainSceneCamera();
            if (existingCamera != null && existingCamera.transform.IsChildOf(existing.transform))
            {
                existingCamera.transform.SetParent(null, true);
            }

            ChamberShellVisibilityController existingController =
                existing.GetComponent<ChamberShellVisibilityController>();
            if (existingController != null)
            {
                preserveRoomOpacity = existingController.RoomOpacityPercent;
                preserveChamberOpacity = existingController.ChamberOpacityPercent;
            }

            MotionSensitiveChamberLights existingMotionLights =
                existing.GetComponent<MotionSensitiveChamberLights>();
            if (existingMotionLights != null)
            {
                preserveChamberLightMode = existingMotionLights.Mode;
                preserveChamberLightTimeout = existingMotionLights.TimeoutSeconds;
            }

            FloodLightController existingFloodLights =
                existing.GetComponentInChildren<FloodLightController>(true);
            if (existingFloodLights != null)
            {
                preserveFloodLightsOn = existingFloodLights.LightsOn;
            }

            TurntableController existingTable =
                existing.GetComponentInChildren<TurntableController>(true);
            if (existingTable != null)
            {
                preservePanDegrees = existingTable.PanDegrees;
                preserveTiltDegrees = existingTable.TiltDegrees;
                preserveHeightMeters = existingTable.HeightMeters;
            }
            SourceAntennaController existingSourceAntenna =
                existing.GetComponentInChildren<SourceAntennaController>(true);
            if (existingSourceAntenna != null)
            {
                preservePolarityDegrees = existingSourceAntenna.PolarityDegrees;
            }
            if (fullRebuild)
            {
                Object.DestroyImmediate(existing);
                existing = null;
            }
        }

        BeginSync(existing);

        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);
        EnsureFolder(RenderTextureFolder);
        RenderTexture monitorView = GetRenderTexture("ChamberWallCamera", 1280, 720);
        Material wall = GetMaterial("Wall", WallColor, 0f, 0f);
        Material floor = GetMaterial("Floor", FloorColor, 0f, 0f);
        Material table = GetMaterial("Table", TableColor, 0.15f, 0.2f);
        Material lift = GetMaterial("Lift", LiftColor, 0.4f, 0.45f);
        Material housing = GetMaterial("Housing", HousingColor, 0.3f, 0.35f);
        Material purple = GetMaterial("TurntablePurple", PanAssemblyColor, 0.35f, 0.45f);
        Material orange = GetMaterial("TiltOrange", TiltAssemblyColor, 0.35f, 0.45f);
        Material yellow = GetMaterial("AntennaYellow", YellowColor, 0.2f, 0.45f);
        Material stand = GetMaterial("FloodStandYellow", StandColor, 0.25f, 0.45f);
        Material dark = GetMaterial("FixtureDark", DarkColor, 0.35f, 0.35f);
        Material source = GetMaterial("SourceGreen", SourceColor, 0.15f, 0.5f);
        Material analyzerGray = GetMaterial("SpectrumAnalyzerGray", Hex(0x777973), 0.05f, 0.25f);
        Material analyzerScreen = GetRasterScreenMaterial(
            "SpectrumAnalyzerScreen", SpectrumAnalyzerTexturePath);
        Material concrete = GetMaterial("Concrete", ConcreteColor, 0f, 0.05f);
        Material roomConcreteTransparent = GetTransparentMaterial(
            "RoomConcreteTransparent", ConcreteColor, 0f, 0.05f);
        Material chamberWallTransparent = GetTransparentMaterial(
            "ChamberWallTransparent", WallColor, 0f, 0f);
        Material chamberFloorTransparent = GetTransparentMaterial(
            "ChamberFloorTransparent", FloorColor, 0f, 0f);
        Material playerMaterial = GetMaterial("Player", PlayerColor, 0f, 0.25f);
        Material lightPanel = GetMaterial("LightPanel", Color.white, 0f, 0.75f, true, 8f);
        Material cameraWhite = GetMaterial("CameraWhite", Color.white, 0f, 0.3f);
        Material controlRed = GetMaterial("ControlRed", Hex(0xc62828), 0.1f, 0.3f);
        Material monitorScreen = GetMonitorDisplayMaterial("MonitorScreen", monitorView);

        Transform root = NewGroup(RootName, null);
        List<Renderer> roomPhysicalRenderers = new();
        List<Renderer> roomCutawayRenderers = new();
        List<Renderer> chamberPhysicalRenderers = new();
        List<Renderer> chamberCutawayRenderers = new();
        BuildContainingRoom(NewGroup("Containing Room", root), concrete, lightPanel,
            roomPhysicalRenderers, roomCutawayRenderers);
        BuildArchitecture(NewGroup("Architecture", root), wall, floor,
            chamberPhysicalRenderers, chamberCutawayRenderers);
        BuildLightingFixtures(
            NewGroup("Lighting Fixtures", root),
            stand,
            dark,
            lightPanel,
            out Light[] chamberWallLights,
            out Renderer[] chamberWallPanels,
            out GameObject floodInteractionZone,
            out Light[] floodLights,
            out Renderer[] floodPanels);
        BuildEquipment(
            NewGroup("Equipment", root),
            table,
            lift,
            housing,
            purple,
            orange,
            yellow,
            source,
            preservePolarityDegrees,
            out SourceAntennaController sourceAntennaController);
        TurntableController tableController = root.GetComponentInChildren<TurntableController>();
        tableController.SetPose(
            preservePanDegrees,
            preserveTiltDegrees,
            preserveHeightMeters);
        BuildScissorLiftControl(
            NewGroup("Scissor Lift Wall Control", root),
            controlRed,
            out GameObject liftInteractionZone);
        BuildComputerConsole(
            NewGroup("Computer Console", root),
            table,
            dark,
            analyzerGray,
            analyzerScreen,
            out GameObject consoleInteractionZone,
            out Transform seatedCameraPose,
            out Transform signalGeneratorCablePort,
            out Transform spectrumAnalyzerCablePort);
        BuildSourceFeedHose(
            NewGroup("Source Feed Hose", root),
            signalGeneratorCablePort,
            sourceAntennaController.transform,
            dark);
        BuildSpectrumAnalyzerFeedHose(
            NewGroup("Spectrum Analyzer Feed Hose", root),
            spectrumAnalyzerCablePort,
            dark);
        BuildExteriorDisplays(
            NewGroup("Exterior Camera Displays", root),
            dark,
            monitorScreen,
            tableController,
            sourceAntennaController);
        ShellVisualBinding[] roomVisuals = CreateCameraVisuals(
            roomPhysicalRenderers, roomConcreteTransparent);
        ShellVisualBinding[] chamberVisuals = CreateCameraVisuals(
            chamberPhysicalRenderers,
            chamberWallTransparent,
            floor,
            chamberFloorTransparent);
        ChamberShellVisibilityController shellController =
            GetOrAddComponent<ChamberShellVisibilityController>(root.gameObject);
        shellController.Configure(
            roomPhysicalRenderers.ToArray(),
            roomVisuals,
            roomCutawayRenderers.ToArray(),
            chamberPhysicalRenderers.ToArray(),
            chamberVisuals,
            chamberCutawayRenderers.ToArray(),
            preserveRoomOpacity,
            preserveChamberOpacity);
        ConfigureSceneCameraAndLight();
        FirstPersonPlayerController playerController =
            BuildPlayer(NewGroup("Player", root), playerMaterial);
        tableController.enabled = false;
        ComputerConsoleController consoleController =
            GetOrAddComponent<ComputerConsoleController>(consoleInteractionZone);
        consoleController.Configure(
            playerController,
            tableController,
            sourceAntennaController,
            playerController.PlayerCamera,
            seatedCameraPose);
        MotionSensitiveChamberLights motionLights =
            GetOrAddComponent<MotionSensitiveChamberLights>(root.gameObject);
        motionLights.Configure(
            playerController.transform,
            chamberWallLights,
            chamberWallPanels,
            lightPanel,
            dark,
            preserveChamberLightMode,
            preserveChamberLightTimeout);
        FloodLightController floodController =
            GetOrAddComponent<FloodLightController>(floodInteractionZone);
        floodController.Configure(
            playerController,
            floodLights,
            floodPanels,
            lightPanel,
            dark,
            preserveFloodLightsOn);
        ScissorLiftStationController liftController =
            GetOrAddComponent<ScissorLiftStationController>(liftInteractionZone);
        liftController.Configure(playerController, tableController);
        BuildWallCamera(NewGroup("Chamber Wall Camera", root), cameraWhite, dark, monitorView);

        FinishSync();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log(fullRebuild
            ? "Fully rebuilt chamber geometry in Main.unity."
            : "Synchronized chamber geometry in Main.unity while preserving stable scene objects.");
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

        // The full-size rectangular section occupies the rear half (z = 0..5).
        // The front half is a rectangular frustum ending in a 1 x 1 m exterior
        // throat centered on the source antenna at (0, 2.5, -5).
        Transform doorWall = NewGroup("Left Wall - Door", parent);
        ShellBox("Rear Section", doorWall, new Vector3(2.5f + halfThickness, 1.75f, 4f),
            new Vector3(wallThickness, 3.5f, 2f), wall, shellRenderers);
        ShellBox("Front Section", doorWall, new Vector3(2.5f + halfThickness, 1.75f, 1f),
            new Vector3(wallThickness, 3.5f, 2f), wall, shellRenderers);
        ShellBox("Above Door", doorWall, new Vector3(2.5f + halfThickness, 2.75f, 2.5f),
            new Vector3(wallThickness, 1.5f, 1f), wall, shellRenderers);
        Box("Door Frame Front Jamb", doorWall, new Vector3(2.5f + halfThickness, 1f, 1.75f),
            new Vector3(wallThickness, 2f, 0.5f), wall);
        Box("Door Frame Rear Jamb", doorWall, new Vector3(2.5f + halfThickness, 1f, 3.25f),
            new Vector3(wallThickness, 2f, 0.5f), wall);
        Box("Door Frame Header", doorWall, new Vector3(2.5f + halfThickness, 2.25f, 2.5f),
            new Vector3(wallThickness, 0.5f, 2f), wall);

        Transform solidWall = NewGroup("Right Wall - Solid", parent);
        ShellBox("Wall", solidWall, new Vector3(-2.5f - halfThickness, 1.75f, 2.5f),
            new Vector3(wallThickness, 3.5f, 5f), wall, shellRenderers);

        Transform backWall = NewGroup("Back Wall", parent);
        ShellBox("Wall", backWall, new Vector3(0f, 1.75f, 5f + halfThickness),
            new Vector3(5f, 3.5f, wallThickness), wall, shellRenderers);

        Transform frustum = NewGroup("Frustum", parent);
        Vector3[] leftFrustum =
        {
            new(-2.5f, 0f, 0f), new(-0.5f, 2f, -5f),
            new(-0.5f, 3f, -5f), new(-2.5f, 3.5f, 0f),
        };
        Vector3[] rightFrustum =
        {
            new(2.5f, 0f, 0f), new(2.5f, 3.5f, 0f),
            new(0.5f, 3f, -5f), new(0.5f, 2f, -5f),
        };
        Vector3[] floorFrustum =
        {
            new(-2.5f, 0f, 0f), new(2.5f, 0f, 0f),
            new(0.5f, 2f, -5f), new(-0.5f, 2f, -5f),
        };
        Vector3[] ceilingFrustum =
        {
            new(-2.5f, 3.5f, 0f), new(-0.5f, 3f, -5f),
            new(0.5f, 3f, -5f), new(2.5f, 3.5f, 0f),
        };
        FrustumSlab("Left Wall", frustum, leftFrustum, Vector3.right,
            wallThickness, wall, shellRenderers);
        FrustumSlab("Right Wall", frustum, rightFrustum, Vector3.left,
            wallThickness, wall, shellRenderers);
        FrustumSlab("Floor", frustum, floorFrustum, Vector3.up,
            wallThickness, floor, shellRenderers);
        FrustumSlab("Ceiling", frustum, ceilingFrustum, Vector3.down,
            wallThickness, wall, shellRenderers);

        Transform throat = NewGroup("Throat", parent);
        float throatCenterZ = -5f - halfThickness;
        const float throatBorder = 0.125f;
        Box("Source Frame Right", throat, new Vector3(-0.4375f, 2.5f, throatCenterZ),
            new Vector3(throatBorder, 1f, wallThickness), wall);
        Box("Source Frame Left", throat, new Vector3(0.4375f, 2.5f, throatCenterZ),
            new Vector3(throatBorder, 1f, wallThickness), wall);
        Box("Source Frame Bottom", throat, new Vector3(0f, 2.0625f, throatCenterZ),
            new Vector3(0.75f, throatBorder, wallThickness), wall);
        Box("Source Frame Top", throat, new Vector3(0f, 2.9375f, throatCenterZ),
            new Vector3(0.75f, throatBorder, wallThickness), wall);

        ShellBox("Floor", parent, new Vector3(0f, -halfThickness, 2.5f),
            new Vector3(5f, wallThickness, 5f), floor, shellRenderers);
        ShellBox("Ceiling", parent, new Vector3(0f, 3.5f + halfThickness, 2.5f),
            new Vector3(5f, wallThickness, 5f), wall, shellRenderers);

        Transform cutaway = NewGroup("Cutaway Surfaces", parent);
        CutawayQuad("Door Wall Rear Section", cutaway,
            new Vector3(2.5f, 1.75f, 4f), 2f, 3.5f, Vector3.left, wall, cutawayRenderers);
        CutawayQuad("Door Wall Front Section", cutaway,
            new Vector3(2.5f, 1.75f, 1f), 2f, 3.5f, Vector3.left, wall, cutawayRenderers);
        CutawayQuad("Door Wall Above Door", cutaway,
            new Vector3(2.5f, 2.75f, 2.5f), 1f, 1.5f, Vector3.left, wall, cutawayRenderers);
        CutawayQuad("Right Wall", cutaway,
            new Vector3(-2.5f, 1.75f, 2.5f), 5f, 3.5f, Vector3.right, wall, cutawayRenderers);
        CutawayQuad("Back Wall", cutaway,
            new Vector3(0f, 1.75f, 5f), 5f, 3.5f, Vector3.back, wall, cutawayRenderers);
        CutawayQuad("Floor", cutaway,
            new Vector3(0f, 0f, 2.5f), 5f, 5f, Vector3.up, floor, cutawayRenderers);
        CutawayQuad("Ceiling", cutaway,
            new Vector3(0f, 3.5f, 2.5f), 5f, 5f, Vector3.down, wall, cutawayRenderers);
        CutawayPolygon("Frustum Left Wall", cutaway, leftFrustum, Vector3.right,
            wall, cutawayRenderers);
        CutawayPolygon("Frustum Right Wall", cutaway, rightFrustum, Vector3.left,
            wall, cutawayRenderers);
        CutawayPolygon("Frustum Floor", cutaway, floorFrustum, Vector3.up,
            floor, cutawayRenderers);
        CutawayPolygon("Frustum Ceiling", cutaway, ceilingFrustum, Vector3.down,
            wall, cutawayRenderers);
    }

    private static void BuildLightingFixtures(
        Transform parent,
        Material stand,
        Material dark,
        Material lightPanel,
        out Light[] chamberWallLights,
        out Renderer[] chamberWallPanels,
        out GameObject floodInteractionZone,
        out Light[] floodLights,
        out Renderer[] floodPanels)
    {
        List<Light> wallLightList = new();
        List<Renderer> wallPanelList = new();
        Transform backFixtures = NewGroup("Back Wall Fixtures", parent);
        foreach (float x in new[] { -1.5f, 1.5f })
        {
            GameObject fixture = Box("Light Fixture", backFixtures,
                new Vector3(x, 2.5f, 4.915f), new Vector3(0.1f, 0.3f, 0.02f), lightPanel);
            wallPanelList.Add(fixture.GetComponent<Renderer>());
            wallLightList.Add(SpotLight(
                "Illumination",
                fixture.transform,
                new Vector3(0f, 0f, -0.03f),
                Vector3.back,
                Color.white,
                10f,
                10f,
                100f,
                70f,
                true));
        }

        Transform floodStand = NewGroup("Flood Light Stand", parent);
        floodStand.localPosition = MirrorPosition(new Vector3(1.5f, 0f, 2f));
        Cylinder("Pole", floodStand, new Vector3(0f, 0.7f, 0f), 0.025f, 1.4f, stand);

        Vector3 legStart = new(0f, 0.28f, 0f);
        Rod("Foot Front Right", floodStand, legStart, new Vector3(0.3f, 0.02f, -0.45f), 0.018f, stand);
        Rod("Foot Rear Right", floodStand, legStart, new Vector3(0.3f, 0.02f, 0.45f), 0.018f, stand);
        Rod("Foot Left", floodStand, legStart, new Vector3(-0.5f, 0.02f, 0f), 0.018f, stand);
        Box("Crossbar", floodStand, new Vector3(0f, 1.38f, 0f), new Vector3(0.04f, 0.04f, 0.8f), stand);

        floodInteractionZone = AcquireObject(
            "Interaction Zone", floodStand, () => new GameObject("Interaction Zone"));
        floodInteractionZone.transform.localPosition = new Vector3(0f, 1f, 0f);
        BoxCollider floodTrigger = GetOrAddComponent<BoxCollider>(floodInteractionZone);
        floodTrigger.size = new Vector3(1.5f, 2f, 1.5f);
        floodTrigger.isTrigger = true;
        Rigidbody floodTriggerBody = GetOrAddComponent<Rigidbody>(floodInteractionZone);
        floodTriggerBody.isKinematic = true;
        floodTriggerBody.useGravity = false;

        List<Light> floodLightList = new();
        List<Renderer> floodPanelList = new();
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
            GameObject panel = Box("Panel", head, new Vector3(0f, 0f, 0.041f),
                new Vector3(0.26f, 0.16f, 0.012f), lightPanel);
            floodPanelList.Add(panel.GetComponent<Renderer>());
            floodLightList.Add(SpotLight(
                "Illumination",
                head,
                new Vector3(0f, 0f, 0.055f),
                Vector3.forward,
                Color.white,
                14f,
                10f,
                60f,
                42f,
                true));
        }

        chamberWallLights = wallLightList.ToArray();
        chamberWallPanels = wallPanelList.ToArray();
        floodLights = floodLightList.ToArray();
        floodPanels = floodPanelList.ToArray();
    }

    private static void BuildEquipment(
        Transform parent,
        Material table,
        Material lift,
        Material housing,
        Material purple,
        Material orange,
        Material yellow,
        Material source,
        float initialPolarityDegrees,
        out SourceAntennaController sourceAntennaController)
    {
        Transform sourceAntenna = NewGroup("Source Antenna Assembly", parent);
        sourceAntenna.localPosition = MirrorPosition(new Vector3(0f, 2.5f, -5f));
        Transform polarityAssembly = NewGroup("Polarity Assembly", sourceAntenna);
        Pyramid(
            "Rectangular Horn",
            polarityAssembly,
            Vector3.zero,
            0.15f,
            0.05f,
            0.10f,
            source);
        sourceAntennaController =
            GetOrAddComponent<SourceAntennaController>(sourceAntenna.gameObject);
        sourceAntennaController.Configure(polarityAssembly, initialPolarityDegrees);

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

    private static void BuildComputerConsole(
        Transform parent,
        Material tableMaterial,
        Material computerMaterial,
        Material analyzerMaterial,
        Material analyzerScreenMaterial,
        out GameObject interactionZone,
        out Transform seatedCameraPose,
        out Transform signalGeneratorCablePort,
        out Transform spectrumAnalyzerCablePort)
    {
        // Source-coordinate placement before the project's YZ-plane reflection.
        // The console sits outside and parallel to the door-side frustum wall.
        const float tableLength = 5f * 12f * MetersPerInch;
        const float tableDepth = 2.5f * 12f * MetersPerInch;
        const float tableHeight = 0.74f;
        const float tableTopThickness = 0.045f;
        const float legThickness = 0.055f;
        const float containingRoomFloor = -0.3f;

        Vector3 wallOutward = new Vector3(5f, 0f, -2f).normalized;
        const float consoleZ = -2.4f;
        float wallX = 2.5f + 0.4f * consoleZ;
        Vector3 consolePosition = new(
            wallX + wallOutward.x * (tableDepth / 2f + 0.18f),
            containingRoomFloor,
            consoleZ + wallOutward.z * (tableDepth / 2f + 0.18f));
        Quaternion consoleRotation = Quaternion.LookRotation(wallOutward, Vector3.up);
        parent.localPosition = MirrorPosition(consolePosition);
        parent.localRotation = MirrorRotation(consoleRotation);

        Transform desk = NewGroup("Table", parent);
        float topCenterY = tableHeight - tableTopThickness / 2f;
        Box("Tabletop", desk, new Vector3(0f, topCenterY, 0f),
            new Vector3(tableLength, tableTopThickness, tableDepth), tableMaterial);

        float legHeight = tableHeight - tableTopThickness;
        float legCenterY = legHeight / 2f;
        float legX = tableLength / 2f - 0.08f;
        float legZ = tableDepth / 2f - 0.08f;
        foreach (float x in new[] { -legX, legX })
        {
            foreach (float z in new[] { -legZ, legZ })
            {
                Box("Leg", desk, new Vector3(x, legCenterY, z),
                    new Vector3(legThickness, legHeight, legThickness), tableMaterial);
            }
        }

        Transform stool = NewGroup("Stool", parent);
        Vector3 stoolCenter = new(-0.08f, 0f, 0.72f);
        Cylinder("Floor Base", stool, stoolCenter + new Vector3(0f, 0.025f, 0f),
            0.18f, 0.05f, tableMaterial);
        Cylinder("Trunk", stool, stoolCenter + new Vector3(0f, 0.25f, 0f),
            0.035f, 0.45f, tableMaterial);
        Cylinder("Seat", stool, stoolCenter + new Vector3(0f, 0.49f, 0f),
            0.2f, 0.07f, tableMaterial);

        Transform computer = NewGroup("Computer", parent);
        float tabletopY = tableHeight;

        // A compact desktop tower at one end of the table.
        Box("PC", computer, new Vector3(0.65f, tabletopY + 0.18f, -0.12f),
            new Vector3(0.18f, 0.36f, 0.34f), computerMaterial);

        // A deliberately simple keyboard near the operator-facing edge.
        Box("Keyboard", computer, new Vector3(-0.08f, tabletopY + 0.018f, 0.18f),
            new Vector3(0.42f, 0.036f, 0.16f), computerMaterial,
            Quaternion.Euler(-4f, 0f, 0f));

        Transform monitor = NewGroup("23-inch LED Monitor", computer);
        monitor.localPosition = MirrorPosition(new Vector3(-0.33f, 0f, 0f));
        monitor.localRotation = MirrorRotation(Quaternion.Euler(0f, 12f, 0f));
        const float monitorDiagonal = 23f * MetersPerInch;
        float screenHeight = monitorDiagonal / Mathf.Sqrt(16f * 16f + 9f * 9f) * 9f;
        float screenWidth = screenHeight * 16f / 9f;
        const float bodyDepth = 0.055f;
        const float bezel = 0.022f;
        const float baseHeight = 0.025f;
        const float standHeight = 0.18f;
        const float monitorZ = -0.18f;
        float bodyWidth = screenWidth + bezel * 2f;
        float bodyHeight = screenHeight + bezel * 2f;
        float baseY = tabletopY + baseHeight / 2f;
        float standY = tabletopY + baseHeight + standHeight / 2f;
        float bodyY = tabletopY + baseHeight + standHeight + bodyHeight / 2f;

        Cylinder("Base", monitor, new Vector3(0f, baseY, monitorZ),
            0.12f, baseHeight, computerMaterial);
        Cylinder("Stand", monitor, new Vector3(0f, standY, monitorZ),
            0.02f, standHeight, computerMaterial);
        Box("Body", monitor, new Vector3(0f, bodyY, monitorZ),
            new Vector3(bodyWidth, bodyHeight, bodyDepth), computerMaterial);
        Box("Screen", monitor,
            new Vector3(0f, bodyY, monitorZ + bodyDepth / 2f + 0.003f),
            new Vector3(screenWidth, screenHeight, 0.006f), computerMaterial);
        CreateWorldDisplayText(
            "Console Instructions",
            monitor,
            new Vector3(0f, bodyY, monitorZ + bodyDepth / 2f + 0.008f),
            screenWidth * 0.92f,
            screenHeight * 0.86f,
            "WASD: turntable\nQ/E: source polarity\nWheel: zoom\n\nESC: stand up",
            50);

        BuildSpectrumAnalyzer(
            parent,
            tabletopY,
            computerMaterial,
            analyzerMaterial,
            analyzerScreenMaterial,
            out spectrumAnalyzerCablePort);

        BuildSignalGeneratorTable(
            parent,
            tableLength,
            tableDepth,
            tableHeight,
            tableTopThickness,
            legThickness,
            tableMaterial,
            analyzerMaterial,
            computerMaterial,
            out signalGeneratorCablePort);

        interactionZone = AcquireObject(
            "Interaction Zone", parent, () => new GameObject("Interaction Zone"));
        interactionZone.transform.localPosition = MirrorPosition(new Vector3(-0.08f, 0.9f, 0.75f));
        BoxCollider trigger = GetOrAddComponent<BoxCollider>(interactionZone);
        trigger.size = new Vector3(1.2f, 1.8f, 0.9f);
        trigger.isTrigger = true;
        Rigidbody triggerBody = GetOrAddComponent<Rigidbody>(interactionZone);
        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;

        seatedCameraPose = NewGroup("Seated Camera Pose", parent);
        Vector3 seatedPosition = new(-0.08f, 1.15f, 0.62f);
        Vector3 seatedTarget = new(-0.08f, 1.05f, monitorZ);
        seatedCameraPose.localPosition = MirrorPosition(seatedPosition);
        seatedCameraPose.localRotation = MirrorRotation(
            Quaternion.LookRotation(seatedTarget - seatedPosition, Vector3.up));
    }

    private static void BuildSignalGeneratorTable(
        Transform parent,
        float tableLength,
        float tableDepth,
        float tableHeight,
        float tableTopThickness,
        float legThickness,
        Material tableMaterial,
        Material signalGeneratorMaterial,
        Material standMaterial,
        out Transform cablePort)
    {
        const float tableGap = 6f * MetersPerInch;
        Transform signalTable = NewGroup("Signal Generator Table", parent);
        signalTable.localPosition = MirrorPosition(
            new Vector3(tableLength + tableGap, 0f, 0f));

        float topCenterY = tableHeight - tableTopThickness / 2f;
        Box("Tabletop", signalTable, new Vector3(0f, topCenterY, 0f),
            new Vector3(tableLength, tableTopThickness, tableDepth), tableMaterial);

        float legHeight = tableHeight - tableTopThickness;
        float legCenterY = legHeight / 2f;
        float legX = tableLength / 2f - 0.08f;
        float legZ = tableDepth / 2f - 0.08f;
        Box("Front Left Leg", signalTable, new Vector3(-legX, legCenterY, legZ),
            new Vector3(legThickness, legHeight, legThickness), tableMaterial);
        Box("Front Right Leg", signalTable, new Vector3(legX, legCenterY, legZ),
            new Vector3(legThickness, legHeight, legThickness), tableMaterial);
        Box("Rear Left Leg", signalTable, new Vector3(-legX, legCenterY, -legZ),
            new Vector3(legThickness, legHeight, legThickness), tableMaterial);
        Box("Rear Right Leg", signalTable, new Vector3(legX, legCenterY, -legZ),
            new Vector3(legThickness, legHeight, legThickness), tableMaterial);

        const float bodyWidth = 0.40f;
        const float bodyHeight = 0.20f;
        const float bodyDepth = 0.60f;
        const float generatorX = 0.34f;
        const float generatorYawDegrees = -20f;
        const float tiltDegrees = -20f / 3f;
        float tiltRadians = Mathf.Abs(tiltDegrees) * Mathf.Deg2Rad;
        float projectedHalfHeight =
            Mathf.Cos(tiltRadians) * bodyHeight / 2f
            + Mathf.Sin(tiltRadians) * bodyDepth / 2f;

        Transform stand = NewGroup("Signal Generator Stand", signalTable);
        stand.localPosition = MirrorPosition(new Vector3(generatorX, 0f, 0f));
        Box("Left Rail", stand, new Vector3(-0.15f, tableHeight + 0.012f, 0f),
            new Vector3(0.025f, 0.024f, 0.52f), standMaterial);
        Box("Right Rail", stand, new Vector3(0.15f, tableHeight + 0.012f, 0f),
            new Vector3(0.025f, 0.024f, 0.52f), standMaterial);
        Rod("Rear Prop", stand,
            new Vector3(0f, tableHeight + 0.02f, -0.23f),
            new Vector3(0f, tableHeight + 0.18f, -0.10f),
            0.012f,
            standMaterial,
            true);

        Transform signalGenerator = NewGroup("Signal Generator", signalTable);
        signalGenerator.localPosition = MirrorPosition(new Vector3(
            generatorX,
            tableHeight + projectedHalfHeight + 0.02f,
            0f));
        signalGenerator.localRotation = MirrorRotation(
            Quaternion.Euler(tiltDegrees, generatorYawDegrees, 0f));
        Box("Chassis", signalGenerator, Vector3.zero,
            new Vector3(bodyWidth, bodyHeight, bodyDepth), signalGeneratorMaterial);

        // This is the future anchor for the flexible source-antenna feed hose.
        GameObject port = Cylinder("Rear Cable Port", signalGenerator,
            new Vector3(0f, 0f, -bodyDepth / 2f - 0.012f),
            0.018f,
            0.024f,
            standMaterial,
            Quaternion.Euler(90f, 0f, 0f));
        cablePort = port.transform;
    }

    private static void BuildSourceFeedHose(
        Transform parent,
        Transform signalGeneratorCablePort,
        Transform sourceAntenna,
        Material material)
    {
        Vector3 start = signalGeneratorCablePort.position;
        Vector3 end = sourceAntenna.TransformPoint(new Vector3(0f, 0f, -0.065f));
        Vector3 exitDirection =
            signalGeneratorCablePort.parent.TransformDirection(Vector3.back).normalized;

        // A long, deliberately under-supported route makes this read as a loose,
        // heavy RF hose rather than a taut wire.
        Vector3[] worldControlPoints =
        {
            start,
            start + exitDirection * 0.28f + Vector3.down * 0.16f,
            Vector3.Lerp(start, end, 0.36f)
                + exitDirection * 0.18f
                + Vector3.down * 0.92f,
            Vector3.Lerp(start, end, 0.72f) + Vector3.down * 0.58f,
            end + Vector3.down * 0.18f + Vector3.back * 0.08f,
            end,
        };
        Vector3[] localControlPoints = new Vector3[worldControlPoints.Length];
        for (int index = 0; index < worldControlPoints.Length; index++)
        {
            localControlPoints[index] = parent.InverseTransformPoint(worldControlPoints[index]);
        }
        TubeAlongCatmullRom(
            "Loose Source Feed Hose",
            parent,
            localControlPoints,
            0.01f,
            12,
            8,
            material);
    }

    private static void BuildSpectrumAnalyzerFeedHose(
        Transform parent,
        Transform spectrumAnalyzerCablePort,
        Material material)
    {
        const float containingRoomFloor = -0.3f;
        Vector3 start = spectrumAnalyzerCablePort.position;
        Transform analyzer = spectrumAnalyzerCablePort.parent;
        Transform console = analyzer.parent;
        Vector3 end = new(2.65f, containingRoomFloor + 0.5f, 2.5f);

        Vector3[] worldControlPoints =
        {
            start,
            // Come forward and down from the lower-front connector.
            analyzer.TransformPoint(new Vector3(0.05f, -0.145f, 0.28f)),
            // Fold across the front toward the viewed-left side of the analyzer.
            analyzer.TransformPoint(new Vector3(0.14f, -0.17f, 0.30f)),
            analyzer.TransformPoint(new Vector3(0.27f, -0.18f, 0.24f)),
            // Wrap around that side and continue behind the chassis.
            analyzer.TransformPoint(new Vector3(0.31f, -0.12f, 0.05f)),
            analyzer.TransformPoint(new Vector3(0.31f, 0.02f, -0.20f)),
            analyzer.TransformPoint(new Vector3(0.31f, 0.04f, -0.30f)),
            // Rest on the tabletop before rounding over its rear edge.
            console.TransformPoint(new Vector3(-0.10f, 0.755f, -0.395f)),
            console.TransformPoint(new Vector3(-0.10f, 0.69f, -0.46f)),
            // Once clear of the edge, fall freely toward the under-chamber run.
            console.TransformPoint(new Vector3(-0.10f, 0.20f, -0.52f)),
            new(-1.55f, -0.12f, -2.15f),
            // Cross beneath the forward/frustum portion of the chamber.
            new(-1.05f, -0.22f, -1.75f),
            new(0.00f, -0.22f, -1.20f),
            new(1.15f, -0.22f, -0.75f),
            new(2.10f, -0.20f, -0.35f),
            // Emerge on the exterior left side near the frustum transition.
            new(2.52f, -0.08f, -0.05f),
            new(2.82f, 0.06f, 0.20f),
            // Hang visibly and loosely along the left side of the room.
            new(3.02f, 0.12f, 0.75f),
            new(3.05f, 0.14f, 1.45f),
            new(2.98f, 0.17f, 2.10f),
            new(2.82f, 0.20f, 2.55f),
            new(2.68f, 0.20f, 2.62f),
            end,
        };
        Vector3[] localControlPoints = new Vector3[worldControlPoints.Length];
        for (int index = 0; index < worldControlPoints.Length; index++)
        {
            localControlPoints[index] = parent.InverseTransformPoint(worldControlPoints[index]);
        }

        TubeAlongCatmullRom(
            "Loose Spectrum Analyzer Feed Hose",
            parent,
            localControlPoints,
            0.01f,
            12,
            8,
            material);

        // The endpoint is a fixed feedthrough on the chamber's exterior left wall.
        Cylinder(
            "Left Wall Feedthrough",
            parent,
            new Vector3(-2.67f, containingRoomFloor + 0.5f, 2.5f),
            0.025f,
            0.04f,
            material,
            Quaternion.Euler(0f, 0f, 90f));
    }

    private static void BuildSpectrumAnalyzer(
        Transform parent,
        float tabletopY,
        Material darkMaterial,
        Material bodyMaterial,
        Material screenMaterial,
        out Transform cablePort)
    {
        const float bodyWidth = 0.48f;
        const float bodyHeight = 0.27f;
        const float bodyDepth = 0.30f;
        const float analyzerX = 0.33f;
        Transform stand = NewGroup("Spectrum Analyzer Stand", parent);
        stand.localPosition = MirrorPosition(new Vector3(analyzerX, 0f, 0f));
        Box("Left Foot", stand, new Vector3(-0.18f, tabletopY + 0.012f, 0.02f),
            new Vector3(0.028f, 0.024f, 0.32f), darkMaterial);
        Box("Right Foot", stand, new Vector3(0.18f, tabletopY + 0.012f, 0.02f),
            new Vector3(0.028f, 0.024f, 0.32f), darkMaterial);
        Rod("Rear Prop", stand,
            new Vector3(0f, tabletopY + 0.02f, -0.13f),
            new Vector3(0f, tabletopY + 0.17f, 0.01f),
            0.012f,
            darkMaterial,
            true);

        Transform analyzer = NewGroup("Spectrum Analyzer", parent);
        analyzer.localPosition = MirrorPosition(new Vector3(analyzerX, tabletopY + 0.155f, 0f));
        analyzer.localRotation = MirrorRotation(Quaternion.Euler(-30f, -14f, 0f));
        Box("Chassis", analyzer, Vector3.zero,
            new Vector3(bodyWidth, bodyHeight, bodyDepth), bodyMaterial);
        Box("CRT Bezel", analyzer, new Vector3(-0.105f, 0.015f, bodyDepth / 2f + 0.008f),
            new Vector3(0.235f, 0.195f, 0.018f), darkMaterial);

        Transform controls = NewGroup("Controls", analyzer);
        int buttonIndex = 1;
        foreach (float x in new[] { 0.055f, 0.115f, 0.175f })
        {
            foreach (float y in new[] { 0.078f, 0.035f, -0.008f })
            {
                Box($"Button {buttonIndex++}", controls,
                    new Vector3(x, y, bodyDepth / 2f + 0.021f),
                    new Vector3(0.043f, 0.025f, 0.012f), darkMaterial);
            }
        }
        Cylinder("Tuning Knob", controls,
            new Vector3(0.085f, -0.078f, bodyDepth / 2f + 0.036f),
            0.036f,
            0.028f,
            darkMaterial,
            Quaternion.Euler(90f, 0f, 0f));
        foreach (float x in new[] { 0.145f, 0.19f })
        {
            Box("Lower Button", controls,
                new Vector3(x, -0.078f, bodyDepth / 2f + 0.022f),
                new Vector3(0.035f, 0.027f, 0.012f), darkMaterial);
        }

        Transform display = NewGroup("Display", analyzer);
        GameObject rasterScreen = Quad(
            "CRT Raster Screen",
            display,
            new Vector3(-0.105f, 0.015f, bodyDepth / 2f + 0.019f),
            0.205f,
            0.168f,
            Vector3.forward,
            screenMaterial);
        rasterScreen.transform.localRotation *= Quaternion.Euler(0f, 0f, 180f);
        Collider screenCollider = rasterScreen.GetComponent<Collider>();
        if (screenCollider != null)
        {
            Object.DestroyImmediate(screenCollider);
        }
        SpectrumAnalyzerDisplay displayController =
            GetOrAddComponent<SpectrumAnalyzerDisplay>(display.gameObject);
        displayController.Configure(
            rasterScreen.GetComponent<Renderer>(),
            screenMaterial.GetTexture("_BaseMap"));

        GameObject port = Cylinder(
            "Front Cable Port",
            analyzer,
            new Vector3(-0.05f, -0.105f, bodyDepth / 2f + 0.025f),
            0.018f,
            0.035f,
            darkMaterial,
            Quaternion.Euler(90f, 0f, 0f));
        cablePort = port.transform;
    }

    private static void BuildScissorLiftControl(
        Transform parent,
        Material controlMaterial,
        out GameObject interactionZone)
    {
        Vector3 controlPosition = new(2f, 1.2f, 4.88f);
        Box("Red Lift Control", parent, controlPosition,
            new Vector3(0.2f, 0.2f, 0.2f), controlMaterial);

        interactionZone = AcquireObject(
            "Interaction Zone", parent, () => new GameObject("Interaction Zone"));
        interactionZone.transform.localPosition = MirrorPosition(new Vector3(2f, 1f, 4.25f));
        BoxCollider trigger = GetOrAddComponent<BoxCollider>(interactionZone);
        trigger.size = new Vector3(1.2f, 2f, 1.25f);
        trigger.isTrigger = true;
        Rigidbody triggerBody = GetOrAddComponent<Rigidbody>(interactionZone);
        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;
    }

    private static void BuildWallCamera(
        Transform parent,
        Material housingMaterial,
        Material lensMaterial,
        RenderTexture targetTexture)
    {
        float panDiskCenterY =
            0.2f
            + TiltAxisZeroLiftHeight
            + PanDiskTopAboveTiltAxis
            - PanDiskThickness / 2f;
        Vector3 cameraPosition = new(-2.5f, 1.5f, 2.5f);
        Vector3 targetPosition = new(0f, panDiskCenterY, 3f);
        parent.localPosition = MirrorPosition(cameraPosition);
        parent.localRotation = MirrorRotation(
            Quaternion.LookRotation(targetPosition - cameraPosition, Vector3.up));

        const float housingLength = 0.3f;
        const float housingRadius = 0.05f;
        Cylinder("White Camera Housing", parent,
            new Vector3(0f, 0f, housingLength / 2f),
            housingRadius,
            housingLength,
            housingMaterial,
            Quaternion.Euler(90f, 0f, 0f));
        Cylinder("Lens", parent, new Vector3(0f, 0f, housingLength + 0.003f),
            0.04f, 0.006f, lensMaterial, Quaternion.Euler(90f, 0f, 0f));

        Transform view = NewGroup("Camera View", parent);
        view.localPosition = new Vector3(0f, 0f, housingLength + 0.012f);
        Camera camera = GetOrAddComponent<Camera>(view.gameObject);
        camera.targetTexture = targetTexture;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.fieldOfView = 55f;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 30f;
        camera.allowHDR = true;
        camera.allowMSAA = true;

        UniversalAdditionalCameraData cameraData =
            camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
    }

    private static void BuildExteriorDisplays(
        Transform parent,
        Material bodyMaterial,
        Material cameraScreenMaterial,
        TurntableController turntableController,
        SourceAntennaController sourceAntennaController)
    {
        const float roomFloorY = -0.3f;
        const float displayCenterAboveFloor = 1.75f;
        const float diagonal = 40f * MetersPerInch;
        const float bezel = 0.025f;
        const float bodyDepth = 0.06f;
        const float displayGap = 5f * MetersPerInch;
        const float wallThickness = 0.15f;

        float screenHeight = diagonal / Mathf.Sqrt(16f * 16f + 9f * 9f) * 9f;
        float screenWidth = screenHeight * 16f / 9f;
        float bodyWidth = screenWidth + bezel * 2f;
        float bodyHeight = screenHeight + bezel * 2f;
        float centerY = roomFloorY + displayCenterAboveFloor;

        Vector3 wallOutward = new Vector3(5f, 0f, -2f).normalized;
        const float centerZ = -2.4f;
        float wallX = 2.5f + 0.4f * centerZ;
        Vector3 displayCenter = new(wallX, centerY, centerZ);
        displayCenter += wallOutward * (wallThickness + bodyDepth / 2f + 0.01f);
        Quaternion displayRotation = Quaternion.LookRotation(wallOutward, Vector3.up);
        parent.localPosition = MirrorPosition(displayCenter);
        parent.localRotation = MirrorRotation(displayRotation);

        float centerOffset = (bodyWidth + displayGap) / 2f;
        for (int index = 0; index < 2; index++)
        {
            float x = index == 0 ? -centerOffset : centerOffset;
            Transform display = NewGroup($"40-inch TV {index + 1}", parent);
            display.localPosition = MirrorPosition(new Vector3(x, 0f, 0f));
            Box("Body", display, Vector3.zero,
                new Vector3(bodyWidth, bodyHeight, bodyDepth), bodyMaterial);
            Box("Screen", display, new Vector3(0f, 0f, bodyDepth / 2f + 0.003f),
                new Vector3(screenWidth, screenHeight, 0.006f),
                index == 0 ? cameraScreenMaterial : bodyMaterial);

            if (index == 1)
            {
                Text readout = CreateWorldDisplayText(
                    "Pan Tilt Readout",
                    display,
                    new Vector3(0f, 0f, bodyDepth / 2f + 0.008f),
                    screenWidth * 0.94f,
                    screenHeight * 0.88f,
                    "Pan: 0\u00B0\nTilt: 0\u00B0\nPolarity: 0\u00B0",
                    58);
                TurntableReadoutDisplay readoutDisplay =
                    GetOrAddComponent<TurntableReadoutDisplay>(display.gameObject);
                readoutDisplay.Configure(
                    turntableController,
                    sourceAntennaController,
                    readout);
            }
        }
    }

    private static Text CreateWorldDisplayText(
        string name,
        Transform parent,
        Vector3 position,
        float width,
        float height,
        string content,
        int fontSize,
        Color? textColor = null,
        TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        const float canvasPixelWidth = 1000f;
        float canvasPixelHeight = canvasPixelWidth * height / width;
        GameObject canvasObject = AcquireObject(
            name,
            parent,
            () => new GameObject(
                name,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler)));
        RectTransform canvasTransform = canvasObject.GetComponent<RectTransform>();
        canvasTransform.localPosition = MirrorPosition(position);
        canvasTransform.localRotation = MirrorRotation(Quaternion.Euler(0f, 180f, 0f));
        canvasTransform.localScale = Vector3.one * (width / canvasPixelWidth);
        canvasTransform.sizeDelta = new Vector2(canvasPixelWidth, canvasPixelHeight);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 2f;

        GameObject textObject = AcquireObject(
            "Text",
            canvasTransform,
            () => new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = new Vector2(24f, 18f);
        textTransform.offsetMax = new Vector2(-24f, -18f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = textColor ?? Color.white;
        text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
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
        HalfCylinder("Tilt Disk", tiltAssembly, new Vector3(tiltDiskX, 0f, 0f),
            TiltDiskDiameter / 2f, TiltDiskThickness, orange);
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

        TurntableController controller = GetOrAddComponent<TurntableController>(turntable.gameObject);
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

    private static FirstPersonPlayerController BuildPlayer(Transform player, Material material)
    {
        Camera camera = FindMainSceneCamera();
        if (camera == null)
        {
            throw new System.InvalidOperationException("The main scene requires a Camera for the player.");
        }

        Vector3 eyePosition = camera.transform.position;
        Vector3 planarForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        player.position = new Vector3(eyePosition.x, 0f, eyePosition.z);
        player.rotation = Quaternion.LookRotation(planarForward, Vector3.up);

        GameObject body = AcquireObject(
            "Capsule Body", player, () => GameObject.CreatePrimitive(PrimitiveType.Capsule));
        SetPrimitive(body, "Capsule Body", player, new Vector3(0f, 0.9f, 0f),
            Quaternion.identity, new Vector3(0.6f, 0.9f, 0.6f), material);
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
        {
            Object.DestroyImmediate(bodyCollider);
        }

        CharacterController characterController = GetOrAddComponent<CharacterController>(player.gameObject);
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.height = 1.8f;
        characterController.radius = 0.3f;
        characterController.stepOffset = 0.3f;
        characterController.skinWidth = 0.05f;

        camera.transform.SetParent(player, true);
        camera.transform.localScale = Vector3.one;
        FirstPersonPlayerController controller =
            GetOrAddComponent<FirstPersonPlayerController>(player.gameObject);
        controller.Configure(camera);
        return controller;
    }

    private static void ConfigureSceneCameraAndLight()
    {
        Camera camera = FindMainSceneCamera();
        if (camera != null)
        {
            // Start just outside the door (world -X) and look into the chamber.
            Vector3 sourcePosition = new(3f, 1.7f, 2.5f);
            Vector3 sourceTarget = new(0f, 1.5f, 2.5f);
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

    private static Camera FindMainSceneCamera()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            return camera;
        }

        GameObject namedCamera = GameObject.Find("Main Camera");
        return namedCamera != null ? namedCamera.GetComponent<Camera>() : null;
    }

    private static void BeginSync(GameObject existingRoot)
    {
        syncRoot = existingRoot;
        staleGeneratedObjects = new HashSet<GameObject>();
        claimedGeneratedObjects = new HashSet<GameObject>();
        if (existingRoot == null)
        {
            return;
        }

        foreach (Transform transform in existingRoot.GetComponentsInChildren<Transform>(true))
        {
            if (transform != existingRoot.transform)
            {
                staleGeneratedObjects.Add(transform.gameObject);
            }
        }
    }

    private static void FinishSync()
    {
        if (staleGeneratedObjects != null)
        {
            foreach (GameObject staleObject in staleGeneratedObjects)
            {
                if (staleObject != null)
                {
                    Object.DestroyImmediate(staleObject);
                }
            }
        }

        syncRoot = null;
        staleGeneratedObjects = null;
        claimedGeneratedObjects = null;
    }

    private static GameObject AcquireObject(
        string name,
        Transform parent,
        System.Func<GameObject> create)
    {
        GameObject gameObject = null;
        if (parent == null && syncRoot != null && syncRoot.name == name
            && !claimedGeneratedObjects.Contains(syncRoot))
        {
            gameObject = syncRoot;
        }
        else if (parent != null)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                GameObject candidate = parent.GetChild(index).gameObject;
                if (candidate.name == name && !claimedGeneratedObjects.Contains(candidate))
                {
                    gameObject = candidate;
                    break;
                }
            }
        }

        if (gameObject == null)
        {
            gameObject = create();
        }

        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.SetActive(true);
        claimedGeneratedObjects.Add(gameObject);
        staleGeneratedObjects.Remove(gameObject);
        return gameObject;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static Transform NewGroup(string name, Transform parent)
    {
        GameObject group = AcquireObject(name, parent, () => new GameObject(name));
        group.transform.localPosition = Vector3.zero;
        group.transform.localRotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
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
        GameObject gameObject = AcquireObject(
            name, parent, () => GameObject.CreatePrimitive(PrimitiveType.Cube));
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

    private static GameObject FrustumSlab(
        string name,
        Transform parent,
        Vector3[] interiorCorners,
        Vector3 desiredInwardNormal,
        float thickness,
        Material material,
        List<Renderer> shellRenderers)
    {
        Vector3[] inner = OrientPolygon(interiorCorners, desiredInwardNormal);
        Vector3 inwardNormal = Vector3.Cross(inner[1] - inner[0], inner[2] - inner[0]).normalized;
        Vector3[] vertices = new Vector3[8];
        for (int i = 0; i < 4; i++)
        {
            vertices[i] = inner[i];
            vertices[i + 4] = inner[i] - inwardNormal * thickness;
        }

        List<int> triangles = new()
        {
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
        };
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            triangles.Add(i);
            triangles.Add(next + 4);
            triangles.Add(next);
            triangles.Add(i);
            triangles.Add(i + 4);
            triangles.Add(next + 4);
        }

        Mesh mesh = GetGeneratedMesh($"Frustum_{name}_Volume", vertices, triangles.ToArray());
        GameObject gameObject = MeshObject(name, parent, mesh, material, true);
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
        GameObject gameObject = AcquireObject(
            name, parent, () => GameObject.CreatePrimitive(PrimitiveType.Quad));
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

    private static GameObject CutawayPolygon(
        string name,
        Transform parent,
        Vector3[] corners,
        Vector3 desiredInwardNormal,
        Material material,
        List<Renderer> cutawayRenderers)
    {
        Vector3[] vertices = OrientPolygon(corners, desiredInwardNormal);
        Mesh mesh = GetGeneratedMesh($"Frustum_{name}_Cutaway", vertices,
            new[] { 0, 1, 2, 0, 2, 3 });
        GameObject gameObject = MeshObject(name, parent, mesh, material, false);
        Renderer renderer = gameObject.GetComponent<Renderer>();
        renderer.enabled = false;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        cutawayRenderers.Add(renderer);
        return gameObject;
    }

    private static GameObject MeshObject(
        string name,
        Transform parent,
        Mesh mesh,
        Material material,
        bool addCollider)
    {
        GameObject gameObject = AcquireObject(name, parent, () => new GameObject(name));
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
        MeshFilter meshFilter = GetOrAddComponent<MeshFilter>(gameObject);
        meshFilter.sharedMesh = mesh;
        MeshRenderer renderer = GetOrAddComponent<MeshRenderer>(gameObject);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        if (addCollider)
        {
            MeshCollider collider = GetOrAddComponent<MeshCollider>(gameObject);
            collider.sharedMesh = mesh;
        }
        else
        {
            MeshCollider collider = gameObject.GetComponent<MeshCollider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }
        return gameObject;
    }

    private static ShellVisualBinding[] CreateCameraVisuals(
        List<Renderer> physicalRenderers,
        Material defaultTransparentMaterial,
        Material alternateOpaqueMaterial = null,
        Material alternateTransparentMaterial = null)
    {
        List<ShellVisualBinding> cameraVisuals = new();
        foreach (Renderer physicalRenderer in physicalRenderers)
        {
            if (physicalRenderer == null)
            {
                continue;
            }

            MeshFilter physicalMeshFilter = physicalRenderer.GetComponent<MeshFilter>();
            if (physicalMeshFilter == null || physicalMeshFilter.sharedMesh == null)
            {
                continue;
            }

            Material opaqueMaterial = physicalRenderer.sharedMaterial;
            Material transparentMaterial =
                alternateOpaqueMaterial != null && opaqueMaterial == alternateOpaqueMaterial
                    ? alternateTransparentMaterial
                    : defaultTransparentMaterial;

            GameObject cameraVisual = AcquireObject(
                "Camera Visual",
                physicalRenderer.transform,
                () => new GameObject("Camera Visual"));
            cameraVisual.transform.localPosition = Vector3.zero;
            cameraVisual.transform.localRotation = Quaternion.identity;
            cameraVisual.transform.localScale = Vector3.one;
            MeshFilter meshFilter = GetOrAddComponent<MeshFilter>(cameraVisual);
            meshFilter.sharedMesh = physicalMeshFilter.sharedMesh;
            MeshRenderer renderer = GetOrAddComponent<MeshRenderer>(cameraVisual);
            renderer.sharedMaterial = opaqueMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            physicalRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            cameraVisuals.Add(new ShellVisualBinding(
                renderer,
                opaqueMaterial,
                transparentMaterial));
        }

        return cameraVisuals.ToArray();
    }

    private static Vector3[] OrientPolygon(Vector3[] corners, Vector3 desiredNormal)
    {
        Vector3[] oriented = (Vector3[])corners.Clone();
        Vector3 normal = Vector3.Cross(oriented[1] - oriented[0], oriented[2] - oriented[0]);
        if (Vector3.Dot(normal, desiredNormal) < 0f)
        {
            (oriented[1], oriented[3]) = (oriented[3], oriented[1]);
        }
        return oriented;
    }

    private static Mesh GetGeneratedMesh(string name, Vector3[] vertices, int[] triangles)
    {
        string safeName = name.Replace(' ', '_');
        string path = $"{MeshFolder}/{safeName}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = safeName };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static GameObject TubeAlongCatmullRom(
        string name,
        Transform parent,
        Vector3[] controlPoints,
        float radius,
        int radialSegments,
        int samplesPerSpan,
        Material material)
    {
        List<Vector3> centerline = new();
        for (int span = 0; span < controlPoints.Length - 1; span++)
        {
            Vector3 p1 = controlPoints[span];
            Vector3 p2 = controlPoints[span + 1];
            Vector3 p0 = span > 0
                ? controlPoints[span - 1]
                : p1 + (p1 - p2);
            Vector3 p3 = span + 2 < controlPoints.Length
                ? controlPoints[span + 2]
                : p2 + (p2 - p1);
            for (int sample = 0; sample < samplesPerSpan; sample++)
            {
                centerline.Add(CatmullRom(
                    p0,
                    p1,
                    p2,
                    p3,
                    sample / (float)samplesPerSpan));
            }
        }
        centerline.Add(controlPoints[controlPoints.Length - 1]);

        List<Vector3> vertices = new(centerline.Count * radialSegments + 2);
        Vector3 previousNormal = Vector3.zero;
        for (int ring = 0; ring < centerline.Count; ring++)
        {
            Vector3 tangent;
            if (ring == 0)
            {
                tangent = centerline[1] - centerline[0];
            }
            else if (ring == centerline.Count - 1)
            {
                tangent = centerline[ring] - centerline[ring - 1];
            }
            else
            {
                tangent = centerline[ring + 1] - centerline[ring - 1];
            }
            tangent.Normalize();

            Vector3 ringNormal = previousNormal - tangent * Vector3.Dot(previousNormal, tangent);
            if (ringNormal.sqrMagnitude < 0.0001f)
            {
                Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.9f
                    ? Vector3.right
                    : Vector3.up;
                ringNormal = Vector3.Cross(tangent, reference);
            }
            ringNormal.Normalize();
            Vector3 ringBinormal = Vector3.Cross(tangent, ringNormal).normalized;
            previousNormal = ringNormal;

            for (int side = 0; side < radialSegments; side++)
            {
                float angle = side * Mathf.PI * 2f / radialSegments;
                Vector3 offset =
                    ringNormal * Mathf.Cos(angle) * radius
                    + ringBinormal * Mathf.Sin(angle) * radius;
                vertices.Add(centerline[ring] + offset);
            }
        }

        List<int> triangles = new();
        for (int ring = 0; ring < centerline.Count - 1; ring++)
        {
            int currentRing = ring * radialSegments;
            int nextRing = (ring + 1) * radialSegments;
            for (int side = 0; side < radialSegments; side++)
            {
                int nextSide = (side + 1) % radialSegments;
                triangles.Add(currentRing + side);
                triangles.Add(nextRing + nextSide);
                triangles.Add(nextRing + side);
                triangles.Add(currentRing + side);
                triangles.Add(currentRing + nextSide);
                triangles.Add(nextRing + nextSide);
            }
        }

        int startCenter = vertices.Count;
        vertices.Add(centerline[0]);
        int endCenter = vertices.Count;
        vertices.Add(centerline[centerline.Count - 1]);
        int lastRing = (centerline.Count - 1) * radialSegments;
        for (int side = 0; side < radialSegments; side++)
        {
            int nextSide = (side + 1) % radialSegments;
            triangles.Add(startCenter);
            triangles.Add(nextSide);
            triangles.Add(side);
            triangles.Add(endCenter);
            triangles.Add(lastRing + side);
            triangles.Add(lastRing + nextSide);
        }

        Mesh mesh = GetGeneratedMesh(
            $"{name}_Tube",
            vertices.ToArray(),
            triangles.ToArray());
        return MeshObject(name, parent, mesh, material, false);
    }

    private static Vector3 CatmullRom(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
    {
        // Centripetal Catmull-Rom avoids the loops and cusps that uniform
        // Catmull-Rom can produce when adjacent routing points are unevenly spaced.
        float t0 = 0f;
        float t1 = CatmullKnot(t0, p0, p1);
        float t2 = CatmullKnot(t1, p1, p2);
        float t3 = CatmullKnot(t2, p2, p3);
        float knot = Mathf.Lerp(t1, t2, t);

        Vector3 a1 = Vector3.LerpUnclamped(p0, p1, (knot - t0) / (t1 - t0));
        Vector3 a2 = Vector3.LerpUnclamped(p1, p2, (knot - t1) / (t2 - t1));
        Vector3 a3 = Vector3.LerpUnclamped(p2, p3, (knot - t2) / (t3 - t2));
        Vector3 b1 = Vector3.LerpUnclamped(a1, a2, (knot - t0) / (t2 - t0));
        Vector3 b2 = Vector3.LerpUnclamped(a2, a3, (knot - t1) / (t3 - t1));
        return Vector3.LerpUnclamped(b1, b2, (knot - t1) / (t2 - t1));
    }

    private static float CatmullKnot(float previousKnot, Vector3 first, Vector3 second)
    {
        return previousKnot + Mathf.Sqrt(Vector3.Distance(first, second));
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
        GameObject gameObject = AcquireObject(name, parent, () => new GameObject(name));
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = Quaternion.LookRotation(localDirection, Vector3.forward);

        Light light = GetOrAddComponent<Light>(gameObject);
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
            ?? GetOrAddComponent<UniversalAdditionalLightData>(gameObject);
        SerializedObject serializedLightData = new(lightData);
        serializedLightData.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue =
            UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
        serializedLightData.ApplyModifiedPropertiesWithoutUndo();
        return light;
    }

    private static GameObject Pyramid(
        string name,
        Transform parent,
        Vector3 position,
        float baseWidth,
        float baseHeight,
        float length,
        Material material)
    {
        float halfWidth = baseWidth / 2f;
        float halfHeight = baseHeight / 2f;
        float halfLength = length / 2f;
        Vector3[] vertices =
        {
            new(-halfWidth, -halfHeight, halfLength),
            new(halfWidth, -halfHeight, halfLength),
            new(halfWidth, halfHeight, halfLength),
            new(-halfWidth, halfHeight, halfLength),
            new(0f, 0f, -halfLength),
        };
        int[] triangles =
        {
            0, 1, 2, 0, 2, 3,
            4, 1, 0,
            4, 2, 1,
            4, 3, 2,
            4, 0, 3,
        };
        Mesh mesh = GetGeneratedMesh($"{name}_Pyramid", vertices, triangles);
        GameObject gameObject = MeshObject(name, parent, mesh, material, true);
        gameObject.transform.localPosition = MirrorPosition(position);
        return gameObject;
    }

    private static GameObject Sphere(string name, Transform parent, Vector3 position, float radius, Material material)
    {
        GameObject gameObject = AcquireObject(
            name, parent, () => GameObject.CreatePrimitive(PrimitiveType.Sphere));
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
        GameObject gameObject = AcquireObject(
            name, parent, () => GameObject.CreatePrimitive(PrimitiveType.Cylinder));
        Vector3 scale = new(radius * 2f, height / 2f, radius * 2f);
        SetPrimitive(gameObject, name, parent, position, rotation ?? Quaternion.identity, scale, material);
        return gameObject;
    }

    private static GameObject HalfCylinder(
        string name,
        Transform parent,
        Vector3 position,
        float radius,
        float thickness,
        Material material)
    {
        const int arcSegments = 32;
        float halfThickness = thickness / 2f;
        List<Vector3> vertices = new();
        List<int> triangles = new();

        int negativeCenter = vertices.Count;
        vertices.Add(new Vector3(-halfThickness, 0f, 0f));
        int negativeArc = vertices.Count;
        for (int index = 0; index <= arcSegments; index++)
        {
            float angle = Mathf.PI * index / arcSegments;
            vertices.Add(new Vector3(
                -halfThickness,
                -Mathf.Sin(angle) * radius,
                Mathf.Cos(angle) * radius));
        }

        int positiveCenter = vertices.Count;
        vertices.Add(new Vector3(halfThickness, 0f, 0f));
        int positiveArc = vertices.Count;
        for (int index = 0; index <= arcSegments; index++)
        {
            float angle = Mathf.PI * index / arcSegments;
            vertices.Add(new Vector3(
                halfThickness,
                -Mathf.Sin(angle) * radius,
                Mathf.Cos(angle) * radius));
        }

        for (int index = 0; index < arcSegments; index++)
        {
            int negativeCurrent = negativeArc + index;
            int negativeNext = negativeCurrent + 1;
            int positiveCurrent = positiveArc + index;
            int positiveNext = positiveCurrent + 1;

            // End caps.
            triangles.Add(negativeCenter);
            triangles.Add(negativeNext);
            triangles.Add(negativeCurrent);
            triangles.Add(positiveCenter);
            triangles.Add(positiveCurrent);
            triangles.Add(positiveNext);

            // Curved outer edge.
            triangles.Add(negativeCurrent);
            triangles.Add(negativeNext);
            triangles.Add(positiveNext);
            triangles.Add(negativeCurrent);
            triangles.Add(positiveNext);
            triangles.Add(positiveCurrent);
        }

        // Horizontal flat face along the diameter at y = 0. The arc hangs below it.
        int negativeTop = negativeArc;
        int negativeBottom = negativeArc + arcSegments;
        int positiveTop = positiveArc;
        int positiveBottom = positiveArc + arcSegments;
        triangles.Add(negativeTop);
        triangles.Add(positiveTop);
        triangles.Add(positiveBottom);
        triangles.Add(negativeTop);
        triangles.Add(positiveBottom);
        triangles.Add(negativeBottom);

        Mesh mesh = GetGeneratedMesh(
            $"{name}_HalfCylinder",
            vertices.ToArray(),
            triangles.ToArray());
        GameObject gameObject = MeshObject(name, parent, mesh, material, true);
        gameObject.transform.localPosition = MirrorPosition(position);
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
        GameObject gameObject = AcquireObject(
            name,
            parent,
            () => square
                ? GameObject.CreatePrimitive(PrimitiveType.Cube)
                : GameObject.CreatePrimitive(PrimitiveType.Cylinder));
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

    private static Material GetTransparentMaterial(
        string name,
        Color color,
        float metallic,
        float smoothness)
    {
        Material material = GetMaterial(name, color, metallic, smoothness);
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_SrcBlendAlpha"))
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (material.HasProperty("_DstBlendAlpha"))
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetMonitorDisplayMaterial(
        string name,
        RenderTexture monitorView)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Texture");
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        material.color = Color.white;
        material.mainTexture = monitorView;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", monitorView);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Back);
        material.renderQueue = (int)RenderQueue.Geometry;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetRasterScreenMaterial(string name, string texturePath)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            throw new System.InvalidOperationException(
                $"Spectrum analyzer screen texture was not imported at {texturePath}.");
        }

        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Texture");
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        material.color = Color.white;
        material.mainTexture = texture;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Back);
        material.renderQueue = (int)RenderQueue.Geometry;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static RenderTexture GetRenderTexture(string name, int width, int height)
    {
        string path = $"{RenderTextureFolder}/{name}.renderTexture";
        RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = name,
            };
            AssetDatabase.CreateAsset(renderTexture, path);
        }
        else if (renderTexture.width != width || renderTexture.height != height)
        {
            renderTexture.Release();
            renderTexture.width = width;
            renderTexture.height = height;
        }

        renderTexture.depth = 24;
        renderTexture.antiAliasing = 1;
        renderTexture.useMipMap = false;
        renderTexture.autoGenerateMips = false;
        renderTexture.wrapMode = TextureWrapMode.Clamp;
        renderTexture.filterMode = FilterMode.Bilinear;
        EditorUtility.SetDirty(renderTexture);
        return renderTexture;
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
