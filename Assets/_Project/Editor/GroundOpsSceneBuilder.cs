using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the deliberately simple Dish Operations Center blockout from the
/// photographic references and sketch under .local/reference.
/// Dimensions are estimates and are kept together near the top of BuildBlockout.
/// </summary>
public static class GroundOpsSceneBuilder
{
    public const string ScenePath = "Assets/_Project/Scenes/GroundOps.unity";

    private const string RootName = "Ground Ops Blockout";
    private const string MaterialFolder = "Assets/_Project/Materials";
    private const string MeshFolder = "Assets/_Project/Generated/Meshes";
    private const double DocLatitudeDegrees = 38.1908805555556;
    private const double DocLongitudeDegrees = -83.4300361111111;
    private const double AntennaLatitudeDegrees = 38.1918583333333;
    private const double AntennaLongitudeDegrees = -83.438825;
    private static readonly Vector2 ExteriorViewOrigin = new(-1.5f, -2.5f);
    private static readonly Vector2 ExteriorViewDirection = new Vector2(-0.48f, 0.88f).normalized;
    private static readonly Vector2 ExteriorLateralDirection =
        new(ExteriorViewDirection.y, -ExteriorViewDirection.x);
    private static readonly Vector3 DishComplexOffset = new(-41.5f, 13.7f, 18.01f);
    private static readonly Vector3 SmallDishRootPosition = new(11.95f, 14.30f, -61.00f);
    private static readonly Vector3 LargeDishRootPosition = new(-6.35f, 11.97f, -65.97f);
    private const float SmallDishRootScale = 4.9419937f;
    private const float LargeDishRootScale = 4.981515f;
    private const float SmallDishReflectorOffsetY = 0.162f;
    private const float LargeDishReflectorOffsetY = 0.239f;
    private static readonly Vector2 DishTerrainCenter = new(-55.16f, 37.81f);
    private const float TerrainHorizontalScale = 0.08641784f;
    private const float TerrainVerticalScale = 0.14127464f;
    private const float TerrainRotationCos = 0.87581675f;
    private const float TerrainRotationSin = -0.48264379f;

    private static GameObject syncRoot;
    private static HashSet<GameObject> staleObjects;
    private static HashSet<GameObject> claimedObjects;

    [MenuItem("Tools/Ground Ops/Sync and Open Ground Ops Blockout")]
    public static void SyncAndOpenFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        SyncAndOpenScene();
    }

    public static void SyncAndOpenSceneFromBridge()
    {
        SyncAndOpenScene();
    }

    private static void SyncAndOpenScene()
    {
        EnsureFolder("Assets/_Project/Scenes");
        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);

        Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildBlockout(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = GameObject.Find(RootName);
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("Synchronized and opened GroundOps.unity blockout.");
    }

    private static void BuildBlockout(Scene scene)
    {
        // Approximate dimensions inferred from standard doors, racks, ceiling
        // tiles, and the supplied plan. Replace these as measurements become known.
        const float opsFrontZ = -5.5f;
        const float partitionZ = 4.5f;
        const float serverBackZ = 8.0f;
        const float rightWallX = 5.5f;
        const float serverLeftX = -4.2f;
        const float wallHeight = 12f * 0.3048f;
        const float wallThickness = 0.12f;
        const float doorWidth = 1.05f;
        const float doorHeight = 2.15f;

        GameObject existingRoot = scene.GetRootGameObjects()
            .FirstOrDefault(candidate => candidate.name == RootName);
        float preserveWallOpacity = 100f;
        bool preserveCeilingLightsOn = true;
        float preserveDishAzimuth = 0f;
        float preserveDishElevation = 90f;
        string preserveTargetName = "GOES-19";
        float preserveTargetAzimuth = 166.823f;
        float preserveTargetElevation = 44.946f;
        float preserveTargetRange = 37409.234f;
        float preserveTargetFrequency = 8220f;
        float preserveTargetPower = 69.6f;
        if (existingRoot != null)
        {
            ChamberShellVisibilityController existingVisibility =
                existingRoot.GetComponent<ChamberShellVisibilityController>();
            if (existingVisibility != null)
            {
                preserveWallOpacity = existingVisibility.RoomOpacityPercent;
            }
            GroundOpsCeilingLightsController existingCeilingLights =
                existingRoot.GetComponentInChildren<GroundOpsCeilingLightsController>(true);
            if (existingCeilingLights != null)
            {
                preserveCeilingLightsOn = existingCeilingLights.LightsOn;
            }
            GroundOpsDishController existingDishes =
                existingRoot.GetComponentInChildren<GroundOpsDishController>(true);
            if (existingDishes != null)
            {
                bool oldDefaultPose =
                    Mathf.Approximately(existingDishes.AzimuthDegrees, 98f)
                    && Mathf.Approximately(existingDishes.ElevationDegrees, 20f);
                if (!oldDefaultPose)
                {
                    preserveDishAzimuth = existingDishes.AzimuthDegrees;
                    preserveDishElevation = existingDishes.ElevationDegrees;
                }
            }
            GroundOpsSatelliteTarget existingTarget =
                existingRoot.GetComponentInChildren<GroundOpsSatelliteTarget>(true);
            if (existingTarget != null)
            {
                preserveTargetName = existingTarget.TargetName == "GOES-19 (GOES East)"
                    ? "GOES-19"
                    : existingTarget.TargetName;
                preserveTargetAzimuth = existingTarget.AzimuthDegrees;
                preserveTargetElevation = existingTarget.ElevationDegrees;
                preserveTargetRange = existingTarget.RangeKilometers;
                preserveTargetFrequency = existingTarget.FrequencyMegahertz;
                preserveTargetPower = existingTarget.PowerDbmiEirp;
            }
        }
        BeginSync(existingRoot);

        Material wallMaterial = GetMaterial("GroundOpsWall", new Color(0.76f, 0.75f, 0.70f), 0f, 0.08f);
        Material transparentWallMaterial = GetTransparentMaterial(
            "GroundOpsWallTransparent", new Color(0.76f, 0.75f, 0.70f, 0.50f));
        Material ceilingMaterial = GetMaterial(
            "GroundOpsCeilingPlaster", new Color(0.92f, 0.90f, 0.84f), 0f, 0.04f);
        Material transparentCeilingMaterial = GetTransparentMaterial(
            "GroundOpsCeilingPlasterTransparent", new Color(0.92f, 0.90f, 0.84f, 0.50f));
        Material trimMaterial = GetMaterial("GroundOpsWindowTrim", new Color(0.16f, 0.18f, 0.20f), 0.15f, 0.25f);
        Material glassMaterial = GetTransparentMaterial(
            "GroundOpsWindowGlass", new Color(0.32f, 0.48f, 0.58f, 0.12f));
        Material carpetMaterial = GetMaterial("GroundOpsCarpet", new Color(0.12f, 0.14f, 0.16f), 0f, 0.02f);
        Material deskMaterial = GetMaterial("GroundOpsDesk", new Color(0.30f, 0.32f, 0.34f), 0.05f, 0.12f);
        Material deskBaseMaterial = GetMaterial("GroundOpsDeskBase", new Color(0.12f, 0.13f, 0.14f), 0.08f, 0.10f);
        Material chairMaterial = GetMaterial("GroundOpsChair", new Color(0.42f, 0.41f, 0.38f), 0f, 0.16f);
        Material blackChairMaterial = GetMaterial("GroundOpsBlackChair", new Color(0.035f, 0.040f, 0.045f), 0f, 0.12f);
        Material monitorMaterial = GetMaterial("GroundOpsMonitor", new Color(0.055f, 0.060f, 0.065f), 0.02f, 0.20f);
        Material monitorScreenMaterial = GetMaterial("GroundOpsMonitorScreen", new Color(0.010f, 0.016f, 0.022f), 0f, 0.38f);
        Material rackMaterial = GetMaterial("GroundOpsDsnRack", new Color(0.25f, 0.27f, 0.28f), 0.15f, 0.18f);
        Material woodDoorMaterial = GetMaterial(
            "GroundOpsWoodDoor", new Color(0.36f, 0.19f, 0.08f), 0f, 0.20f);
        Material legacyBeigeMaterial = GetMaterial(
            "GroundOpsLegacyComputerBeige", new Color(0.68f, 0.65f, 0.53f), 0f, 0.08f);
        Material kvmScreenMaterial = GetMaterial("GroundOpsKvmScreen", new Color(0.015f, 0.025f, 0.030f), 0f, 0.35f);
        Material terrainMaterial = GetMaterial("GroundOpsMountainTerrain", new Color(0.22f, 0.31f, 0.17f), 0f, 0.02f);
        Material forestTrunkMaterial = GetMaterial("GroundOpsForestTrunks", new Color(0.16f, 0.11f, 0.065f), 0f, 0.02f);
        Material[] forestCrownMaterials =
        {
            GetMaterial("GroundOpsForestCrown1", new Color(0.13f, 0.25f, 0.10f), 0f, 0.01f),
            GetMaterial("GroundOpsForestCrown2", new Color(0.18f, 0.32f, 0.13f), 0f, 0.01f),
            GetMaterial("GroundOpsForestCrown3", new Color(0.23f, 0.38f, 0.16f), 0f, 0.01f),
            GetMaterial("GroundOpsForestCrown4", new Color(0.29f, 0.43f, 0.19f), 0f, 0.01f),
        };
        Material dishMaterial = GetMaterial("GroundOpsExteriorDish", new Color(0.66f, 0.68f, 0.65f), 0.05f, 0.15f);
        Material playerMaterial = GetMaterial("GroundOpsPlayer", new Color(0.12f, 0.32f, 0.58f), 0f, 0.18f);
        Material lightHousingMaterial = GetMaterial("GroundOpsLightHousing", new Color(0.44f, 0.46f, 0.46f), 0.25f, 0.38f);
        Material lightsOnMaterial = GetEmissiveMaterial(
            "GroundOpsLightsOn", new Color(1f, 0.89f, 0.70f), 1.35f);
        Material lightsOffMaterial = GetMaterial(
            "GroundOpsLightsOff", new Color(0.22f, 0.22f, 0.20f), 0f, 0.18f);
        Material northMaterial = GetMaterial("GroundOpsTrueNorth", new Color(0.16f, 0.40f, 0.95f), 0f, 0.12f);
        Material eastMaterial = GetMaterial("GroundOpsTrueEast", new Color(0.95f, 0.20f, 0.14f), 0f, 0.12f);
        Material skyMaterial = GetSkyMaterial("GroundOpsSky");

        CalculateWorldCardinalAxes(out Vector3 worldNorth, out Vector3 worldEast);

        Transform root = NewGroup(RootName, null);
        Transform architecture = NewGroup("Architecture", root);

        Vector3[] windowPoints = BuildWindowPoints(
            new Vector3(serverLeftX, 0f, partitionZ),
            new Vector3(-5.1f, 0f, opsFrontZ),
            new Vector3(-7.0f, 0f, -0.2f),
            5);

        BuildFloor(
            NewGroup("Floors", architecture),
            windowPoints,
            opsFrontZ,
            partitionZ,
            serverBackZ,
            rightWallX,
            serverLeftX,
            carpetMaterial);
        List<Renderer> wallPhysicalRenderers = new();
        List<Renderer> ceilingPhysicalRenderers = new();
        List<Renderer> wallCutawayRenderers = new();
        BuildWalls(
            NewGroup("Walls", architecture),
            windowPoints,
            opsFrontZ,
            partitionZ,
            serverBackZ,
            rightWallX,
            serverLeftX,
            wallHeight,
            wallThickness,
            doorWidth,
            doorHeight,
            wallMaterial,
            wallPhysicalRenderers,
            wallCutawayRenderers);
        BuildCeiling(
            NewGroup("Ceiling", architecture),
            windowPoints,
            partitionZ,
            serverBackZ,
            rightWallX,
            serverLeftX,
            wallHeight,
            ceilingMaterial,
            ceilingPhysicalRenderers);
        BuildCurvedWindow(
            NewGroup("Curved Window", architecture),
            windowPoints,
            wallHeight,
            glassMaterial,
            trimMaterial);
        BuildOpsRoomDoor(
            NewGroup("Ops Room Door", architecture),
            opsFrontZ,
            doorWidth,
            doorHeight,
            woodDoorMaterial,
            trimMaterial);

        Transform exteriorLandscape = NewGroup("Exterior Landscape", root);
        GroundOpsDishController dishController = BuildExteriorLandscape(
            exteriorLandscape,
            terrainMaterial,
            forestTrunkMaterial,
            forestCrownMaterials,
            dishMaterial,
            worldNorth,
            worldEast,
            preserveDishAzimuth,
            preserveDishElevation);
        GroundOpsSatelliteTarget satelliteTarget =
            GetOrAddComponent<GroundOpsSatelliteTarget>(exteriorLandscape.gameObject);
        satelliteTarget.Configure(
            preserveTargetName,
            preserveTargetAzimuth,
            preserveTargetElevation,
            preserveTargetRange,
            preserveTargetFrequency,
            preserveTargetPower);

        ShellVisualBinding[] wallVisuals = CreateCameraVisuals(
            wallPhysicalRenderers, transparentWallMaterial)
            .Concat(CreateCameraVisuals(ceilingPhysicalRenderers, transparentCeilingMaterial))
            .ToArray();
        Renderer[] roomPhysicalRenderers = wallPhysicalRenderers
            .Concat(ceilingPhysicalRenderers)
            .ToArray();
        ChamberShellVisibilityController wallVisibility =
            GetOrAddComponent<ChamberShellVisibilityController>(root.gameObject);
        wallVisibility.Configure(
            roomPhysicalRenderers,
            wallVisuals,
            wallCutawayRenderers.ToArray(),
            System.Array.Empty<Renderer>(),
            System.Array.Empty<ShellVisualBinding>(),
            System.Array.Empty<Renderer>(),
            preserveWallOpacity,
            100f);

        BuildCeilingLights(
            NewGroup("Ceiling Lighting", root),
            wallHeight,
            lightHousingMaterial,
            lightsOnMaterial,
            lightsOffMaterial,
            preserveCeilingLightsOn);

        Transform furniture = NewGroup("Furniture Blockout", root);
        Transform frontLeftStation = BuildStationDesk(
            "Hardware Control Station", furniture, new Vector3(-3.540f, 0f, -2.680f), 17.181f,
            deskMaterial, deskBaseMaterial, chairMaterial, monitorMaterial, monitorScreenMaterial);
        BuildStationDesk("Dish Station 2", furniture, new Vector3(-2.870f, 0f, -0.700f), 21.625f,
            deskMaterial, deskBaseMaterial, chairMaterial, monitorMaterial, monitorScreenMaterial);
        Transform dishStation3 = BuildStationDesk(
            "Dish Station 3", furniture, new Vector3(-1.527f, 0f, -3.340f), 14.958f,
            deskMaterial, deskBaseMaterial, chairMaterial, monitorMaterial, monitorScreenMaterial);
        BuildStationDesk("Dish Station 4", furniture, new Vector3(-0.858f, 0f, -1.457f), 26.940f,
            deskMaterial, deskBaseMaterial, chairMaterial, monitorMaterial, monitorScreenMaterial);
        BuildNonDishStations(
            furniture,
            deskMaterial,
            deskBaseMaterial,
            blackChairMaterial,
            monitorMaterial,
            monitorScreenMaterial);
        Transform serverRoomEquipment = NewGroup("Server Room Equipment", root);
        Transform dsnRackPair = BuildDsnRackPair(
            serverRoomEquipment,
            new Vector3(-2.160f, 0f, 7.30f),
            rackMaterial,
            deskBaseMaterial,
            kvmScreenMaterial,
            legacyBeigeMaterial,
            dishController,
            satelliteTarget);
        BuildServerRackRow(
            serverRoomEquipment,
            rackMaterial,
            deskBaseMaterial);

        BuildGeographicReference(
            NewGroup("Geographic Reference", root),
            worldNorth,
            worldEast,
            northMaterial,
            eastMaterial,
            trimMaterial);
        BuildCameraAndLight(
            NewGroup("Scene Setup", root),
            skyMaterial,
            worldNorth,
            worldEast);
        FirstPersonPlayerController playerController =
            BuildPlayer(
                NewGroup("Player", root),
                playerMaterial,
                dishStation3.TransformPoint(new Vector3(1.50f, 0f, 0f)),
                new Vector3(-6.0f, 1.65f, -0.2f));
        BuildDishStationConsole(frontLeftStation, playerController, dishController);
        BuildDsnRackConsole(dsnRackPair, playerController);
        FinishSync();

        RenderSettings.skybox = skyMaterial;
        RenderSettings.ambientMode = AmbientMode.Skybox;
    }

    private static Vector3[] BuildWindowPoints(
        Vector3 back,
        Vector3 front,
        Vector3 control,
        int segmentCount)
    {
        Vector3[] points = new Vector3[segmentCount + 1];
        for (int index = 0; index <= segmentCount; index++)
        {
            float t = index / (float)segmentCount;
            float inverse = 1f - t;
            points[index] =
                inverse * inverse * back
                + 2f * inverse * t * control
                + t * t * front;
        }
        return points;
    }

    private static void BuildFloor(
        Transform parent,
        Vector3[] windowPoints,
        float opsFrontZ,
        float partitionZ,
        float serverBackZ,
        float rightWallX,
        float serverLeftX,
        Material material)
    {
        List<Vector3> opsBoundary = new()
        {
            new Vector3(rightWallX, 0f, opsFrontZ),
        };
        for (int index = windowPoints.Length - 1; index >= 0; index--)
        {
            opsBoundary.Add(windowPoints[index]);
        }
        opsBoundary.Add(new Vector3(rightWallX, 0f, partitionZ));

        Mesh opsFloor = GetFloorMesh("GroundOps_OpsFloor", opsBoundary.ToArray());
        MeshObject("Operations Room Floor", parent, opsFloor, material);
        Box(
            "Server Room Floor",
            parent,
            new Vector3(
                (serverLeftX + rightWallX) / 2f,
                -0.04f,
                (partitionZ + serverBackZ) / 2f),
            new Vector3(rightWallX - serverLeftX, 0.08f, serverBackZ - partitionZ),
            Quaternion.identity,
            material);
    }

    private static void BuildCeiling(
        Transform parent,
        Vector3[] windowPoints,
        float partitionZ,
        float serverBackZ,
        float rightWallX,
        float serverLeftX,
        float wallHeight,
        Material material,
        List<Renderer> physicalRenderers)
    {
        const float slabThickness = 0.16f;
        const float lightSealOverlap = 0.18f;
        List<Vector3> opsBoundary = new()
        {
            new Vector3(rightWallX, 0f, windowPoints[windowPoints.Length - 1].z),
        };
        for (int index = windowPoints.Length - 1; index >= 0; index--)
        {
            opsBoundary.Add(windowPoints[index]);
        }
        opsBoundary.Add(new Vector3(rightWallX, 0f, partitionZ));

        // The ceiling must overlap the wall centerlines instead of merely
        // touching them. Exact edge contact can leave a one-pixel directional
        // shadow-map seam that reads as a bright diagonal crack in a corner.
        Vector3 boundaryCenter = Vector3.zero;
        foreach (Vector3 point in opsBoundary)
        {
            boundaryCenter += point;
        }
        boundaryCenter /= opsBoundary.Count;
        for (int index = 0; index < opsBoundary.Count; index++)
        {
            Vector3 outward = opsBoundary[index] - boundaryCenter;
            outward.y = 0f;
            opsBoundary[index] += outward.normalized * lightSealOverlap;
        }

        Mesh opsCeilingMesh = GetSlabMesh(
            "GroundOps_OpsCeiling",
            opsBoundary.ToArray(),
            wallHeight,
            wallHeight + slabThickness);
        GameObject opsCeiling = MeshObject(
            "Operations Room Ceiling Slab",
            parent,
            opsCeilingMesh,
            material);
        physicalRenderers.Add(opsCeiling.GetComponent<Renderer>());

        GameObject serverCeiling = Box(
            "Server Room Ceiling Slab",
            parent,
            new Vector3(
                (serverLeftX + rightWallX) / 2f,
                wallHeight + slabThickness / 2f,
                (partitionZ + serverBackZ) / 2f),
            new Vector3(
                rightWallX - serverLeftX + lightSealOverlap * 2f,
                slabThickness,
                serverBackZ - partitionZ + lightSealOverlap * 2f),
            Quaternion.identity,
            material);
        physicalRenderers.Add(serverCeiling.GetComponent<Renderer>());
    }

    private static GroundOpsCeilingLightsController BuildCeilingLights(
        Transform parent,
        float wallHeight,
        Material housingMaterial,
        Material lightsOnMaterial,
        Material lightsOffMaterial,
        bool initialState)
    {
        List<Light> lights = new();
        List<Renderer> luminousRenderers = new();
        Color warmLight = new(1f, 0.86f, 0.67f);

        (Vector3 position, float yaw, float length)[] hangingFixtures =
        {
            (new Vector3(-2.5f, wallHeight - 0.58f, -2.75f), -9f, 3.8f),
            (new Vector3(1.35f, wallHeight - 0.58f, -0.35f), 8f, 4.2f),
            (new Vector3(-1.05f, wallHeight - 0.58f, 2.35f), -6f, 4.0f),
        };
        Transform hangingGroup = NewGroup("Suspended Uplights", parent);
        for (int index = 0; index < hangingFixtures.Length; index++)
        {
            (Vector3 position, float yaw, float length) fixture = hangingFixtures[index];
            Transform fixtureRoot = NewGroup($"Uplight {index + 1}", hangingGroup);
            Quaternion rotation = Quaternion.Euler(0f, fixture.yaw, 0f);
            Vector3 fixtureRight = rotation * Vector3.right;
            Box("Housing", fixtureRoot,
                fixture.position,
                new Vector3(fixture.length, 0.16f, 0.28f),
                rotation,
                housingMaterial);
            GameObject luminousStrip = Box("Upward Luminous Strip", fixtureRoot,
                fixture.position + Vector3.up * 0.095f,
                new Vector3(fixture.length * 0.90f, 0.035f, 0.19f),
                rotation,
                lightsOnMaterial);
            luminousRenderers.Add(luminousStrip.GetComponent<Renderer>());

            float hangerHeight = wallHeight - fixture.position.y;
            for (int side = -1; side <= 1; side += 2)
            {
                Cylinder($"Hanger {side}", fixtureRoot,
                    fixture.position
                    + fixtureRight * (fixture.length * 0.34f * side)
                    + Vector3.up * (hangerHeight * 0.5f),
                    0.018f,
                    hangerHeight,
                    housingMaterial);
            }

            lights.Add(CreateLight(
                "Upward Light",
                fixtureRoot,
                fixture.position + Vector3.up * 0.12f,
                Vector3.up,
                LightType.Spot,
                warmLight,
                0.16f,
                2.8f,
                160f,
                false,
                0.08f));
            lights.Add(CreateLight(
                "Reflected Fill",
                fixtureRoot,
                fixture.position + Vector3.up * 0.08f,
                Vector3.down,
                LightType.Point,
                warmLight,
                0.70f,
                7.2f,
                0f,
                false));
        }

        Vector2[] canPositions =
        {
            new(-3.8f, -4.15f), new(-1.0f, -4.15f), new(1.8f, -4.15f), new(4.35f, -4.15f),
            new(-3.5f, 0.15f), new(0.0f, 0.15f), new(3.5f, 0.15f),
            new(-2.7f, 3.55f), new(0.8f, 3.55f), new(4.1f, 3.55f),
            new(-1.6f, 6.25f), new(2.2f, 6.25f),
        };
        Transform canGroup = NewGroup("Recessed Can Lights", parent);
        for (int index = 0; index < canPositions.Length; index++)
        {
            Vector3 position = new(canPositions[index].x, wallHeight - 0.035f, canPositions[index].y);
            GameObject lens = Cylinder(
                $"Can Light {index + 1}",
                canGroup,
                position,
                0.115f,
                0.045f,
                lightsOnMaterial);
            luminousRenderers.Add(lens.GetComponent<Renderer>());
            lights.Add(CreateLight(
                "Downlight",
                lens.transform,
                new Vector3(0f, -0.08f, 0f),
                Vector3.down,
                LightType.Spot,
                warmLight,
                5.8f,
                9.5f,
                72f,
                index % 3 == 0));
        }

        Transform roomFillGroup = NewGroup("Diffuse Room Fill", parent);
        Vector3[] fillPositions =
        {
            new(-2.6f, wallHeight - 0.85f, -2.4f),
            new(1.4f, wallHeight - 0.85f, -1.0f),
            new(-0.5f, wallHeight - 0.85f, 2.5f),
            new(2.8f, wallHeight - 0.85f, 3.2f),
        };
        for (int index = 0; index < fillPositions.Length; index++)
        {
            lights.Add(CreateLight(
                $"Room Fill {index + 1}",
                roomFillGroup,
                fillPositions[index],
                Vector3.down,
                LightType.Point,
                warmLight,
                0.55f,
                8.5f,
                0f,
                false));
        }

        GroundOpsCeilingLightsController controller =
            GetOrAddComponent<GroundOpsCeilingLightsController>(parent.gameObject);
        controller.Configure(
            lights.ToArray(),
            luminousRenderers.ToArray(),
            lightsOnMaterial,
            lightsOffMaterial,
            initialState);
        return controller;
    }

    private static Light CreateLight(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localDirection,
        LightType type,
        Color color,
        float intensity,
        float range,
        float spotAngle,
        bool castShadows,
        float innerSpotRatio = 0.62f)
    {
        GameObject gameObject = AcquireObject(name, parent, () => new GameObject(name));
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = type == LightType.Spot
            ? Quaternion.LookRotation(localDirection, Vector3.forward)
            : Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
        Light light = GetOrAddComponent<Light>(gameObject);
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        if (type == LightType.Spot)
        {
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * innerSpotRatio;
        }
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        return light;
    }

    private static void BuildWalls(
        Transform parent,
        Vector3[] windowPoints,
        float opsFrontZ,
        float partitionZ,
        float serverBackZ,
        float rightWallX,
        float serverLeftX,
        float wallHeight,
        float wallThickness,
        float doorWidth,
        float doorHeight,
        Material material,
        List<Renderer> physicalRenderers,
        List<Renderer> cutawayRenderers)
    {
        float centerY = wallHeight / 2f;
        Transform cutaway = NewGroup("Cutaway Surfaces", parent);

        // Straight right wall shared by the Ops Room and Server Room.
        WallBox("Right Wall", parent, cutaway,
            new Vector3(rightWallX, centerY, (opsFrontZ + serverBackZ) / 2f),
            new Vector3(wallThickness, wallHeight, serverBackZ - opsFrontZ),
            material, Vector3.left, physicalRenderers, cutawayRenderers);

        // Main Ops entrance opening near the front-right corner.
        const float opsDoorCenterX = 4.15f;
        BuildWallWithOpeningAlongX(
            "Ops Entrance",
            parent,
            windowPoints[windowPoints.Length - 1].x,
            rightWallX,
            opsFrontZ,
            opsDoorCenterX,
            doorWidth,
            doorHeight,
            wallHeight,
            wallThickness,
            material,
            Vector3.forward,
            cutaway,
            physicalRenderers,
            cutawayRenderers);

        // Doorway between the Ops Room and Server Room near the window end.
        const float serverDoorCenterX = -2.70f;
        BuildWallWithOpeningAlongX(
            "Server Room Door",
            parent,
            serverLeftX,
            rightWallX,
            partitionZ,
            serverDoorCenterX,
            doorWidth,
            doorHeight,
            wallHeight,
            wallThickness,
            material,
            Vector3.back,
            cutaway,
            physicalRenderers,
            cutawayRenderers);

        WallBox("Server Room Back Wall", parent, cutaway,
            new Vector3((serverLeftX + rightWallX) / 2f, centerY, serverBackZ),
            new Vector3(rightWallX - serverLeftX, wallHeight, wallThickness),
            material, Vector3.back, physicalRenderers, cutawayRenderers);
        WallBox("Server Room Left Wall", parent, cutaway,
            new Vector3(serverLeftX, centerY, (partitionZ + serverBackZ) / 2f),
            new Vector3(wallThickness, wallHeight, serverBackZ - partitionZ),
            material, Vector3.right, physicalRenderers, cutawayRenderers);
    }

    private static void BuildWallWithOpeningAlongX(
        string name,
        Transform parent,
        float minimumX,
        float maximumX,
        float z,
        float openingCenterX,
        float openingWidth,
        float openingHeight,
        float wallHeight,
        float thickness,
        Material material,
        Vector3 inwardNormal,
        Transform cutawayParent,
        List<Renderer> physicalRenderers,
        List<Renderer> cutawayRenderers)
    {
        Transform wall = NewGroup(name, parent);
        float openingLeft = openingCenterX - openingWidth / 2f;
        float openingRight = openingCenterX + openingWidth / 2f;
        float leftWidth = openingLeft - minimumX;
        float rightWidth = maximumX - openingRight;
        float headerHeight = wallHeight - openingHeight;

        WallBox("Left Segment", wall, cutawayParent,
            new Vector3(minimumX + leftWidth / 2f, wallHeight / 2f, z),
            new Vector3(leftWidth, wallHeight, thickness), material, inwardNormal,
            physicalRenderers, cutawayRenderers,
            $"{name} Left Segment");
        WallBox("Right Segment", wall, cutawayParent,
            new Vector3(openingRight + rightWidth / 2f, wallHeight / 2f, z),
            new Vector3(rightWidth, wallHeight, thickness), material, inwardNormal,
            physicalRenderers, cutawayRenderers,
            $"{name} Right Segment");
        WallBox("Header", wall, cutawayParent,
            new Vector3(openingCenterX, openingHeight + headerHeight / 2f, z),
            new Vector3(openingWidth, headerHeight, thickness), material, inwardNormal,
            physicalRenderers, cutawayRenderers,
            $"{name} Header");
    }

    private static void BuildCurvedWindow(
        Transform parent,
        Vector3[] points,
        float wallHeight,
        Material glassMaterial,
        Material trimMaterial)
    {
        const float sillHeight = 0.18f;
        const float topGap = 0.18f;
        const float frameThickness = 0.08f;
        float glassHeight = wallHeight - sillHeight - topGap;
        float glassCenterY = sillHeight + glassHeight / 2f;

        for (int index = 0; index < points.Length - 1; index++)
        {
            Vector3 first = points[index];
            Vector3 second = points[index + 1];
            Vector3 delta = second - first;
            float length = delta.magnitude;
            Vector3 midpoint = (first + second) / 2f;
            Quaternion rotation = Quaternion.Euler(
                0f,
                -Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg,
                0f);

            Box($"Window Panel {index + 1}", parent,
                new Vector3(midpoint.x, glassCenterY, midpoint.z),
                new Vector3(length, glassHeight, 0.045f), rotation, glassMaterial);
            Box($"Bottom Rail {index + 1}", parent,
                new Vector3(midpoint.x, sillHeight / 2f, midpoint.z),
                new Vector3(length, sillHeight, frameThickness), rotation, trimMaterial);
            Box($"Top Rail {index + 1}", parent,
                new Vector3(midpoint.x, wallHeight - topGap / 2f, midpoint.z),
                new Vector3(length, topGap, frameThickness), rotation, trimMaterial);
        }

        for (int index = 0; index < points.Length; index++)
        {
            Box($"Mullion {index + 1}", parent,
                new Vector3(points[index].x, wallHeight / 2f, points[index].z),
                new Vector3(frameThickness, wallHeight, frameThickness),
                Quaternion.identity,
                trimMaterial);
        }
    }

    private static void BuildOpsRoomDoor(
        Transform parent,
        float wallZ,
        float doorWidth,
        float doorHeight,
        Material woodMaterial,
        Material hardwareMaterial)
    {
        const float centerX = 4.15f;
        const float thickness = 0.055f;
        Box("Wooden Door", parent,
            new Vector3(centerX, doorHeight / 2f, wallZ),
            new Vector3(doorWidth, doorHeight, thickness),
            Quaternion.identity,
            woodMaterial);
        Box("Door Handle", parent,
            new Vector3(centerX - doorWidth * 0.34f, 1.02f, wallZ - thickness * 0.62f),
            new Vector3(0.05f, 0.12f, 0.05f),
            Quaternion.identity,
            hardwareMaterial);
    }

    private static Transform BuildStationDesk(
        string name,
        Transform parent,
        Vector3 floorPosition,
        float yawDegrees,
        Material topMaterial,
        Material baseMaterial,
        Material chairMaterial,
        Material monitorMaterial,
        Material screenMaterial)
    {
        const float deskWidth = 0.95f;
        const float deskLength = 1.90f;
        const float deskHeight = 0.76f;
        const float topThickness = 0.08f;

        Transform station = NewGroup(name, parent);
        station.localPosition = floorPosition;
        station.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

        Box("Worktop", station,
            new Vector3(0f, deskHeight - topThickness / 2f, 0f),
            new Vector3(deskWidth, topThickness, deskLength),
            Quaternion.identity,
            topMaterial);

        // Two broad equipment pedestals and a window-side modesty panel make
        // these read as heavy operations consoles rather than ordinary tables.
        foreach ((string pedestalName, float z) in new[]
                 {
                     ("Forward Pedestal", 0.66f),
                     ("Rear Pedestal", -0.66f),
                 })
        {
            Box(pedestalName, station,
                new Vector3(-0.04f, 0.34f, z),
                new Vector3(0.72f, 0.68f, 0.42f),
                Quaternion.identity,
                baseMaterial);
        }
        Box("Window-side Modesty Panel", station,
            new Vector3(-deskWidth / 2f + 0.045f, 0.39f, 0f),
            new Vector3(0.09f, 0.62f, 1.56f),
            Quaternion.identity,
            baseMaterial);
        Box("Rear Console Rail", station,
            new Vector3(-deskWidth / 2f + 0.10f, deskHeight + 0.055f, 0f),
            new Vector3(0.20f, 0.11f, 1.68f),
            Quaternion.identity,
            topMaterial);

        BuildDesktopMonitor("Left 27-inch Monitor", station,
            new Vector3(-0.11f, 0f, -0.35f), 75f, deskHeight,
            monitorMaterial, screenMaterial);
        BuildDesktopMonitor("Right 27-inch Monitor", station,
            new Vector3(-0.11f, 0f, 0.35f), 105f, deskHeight,
            monitorMaterial, screenMaterial);
        Box("Keyboard", station,
            new Vector3(0.20f, deskHeight + 0.022f, -0.048f),
            new Vector3(0.18f, 0.044f, 0.46f),
            Quaternion.Euler(0f, -4.878f, 0f), monitorMaterial);
        Box("Mouse", station,
            new Vector3(0.22f, deskHeight + 0.026f, 0.36f),
            new Vector3(0.16f, 0.052f, 0.10f),
            Quaternion.Euler(0f, 13.302f, 0f), monitorMaterial);

        Transform chair = NewGroup("Chair", station);
        chair.localPosition = new Vector3(0.86f, 0f, 0f);
        Cylinder("Floor Base", chair, new Vector3(0f, 0.025f, 0f),
            0.24f, 0.05f, baseMaterial);
        Cylinder("Pedestal", chair, new Vector3(0f, 0.27f, 0f),
            0.035f, 0.46f, baseMaterial);
        Box("Seat", chair, new Vector3(0f, 0.53f, 0f),
            new Vector3(0.48f, 0.11f, 0.48f), Quaternion.identity, chairMaterial);
        Box("Back", chair, new Vector3(0.21f, 0.80f, 0f),
            new Vector3(0.10f, 0.58f, 0.50f), Quaternion.identity, chairMaterial);
        RemoveColliders(chair);
        return station;
    }

    private static void BuildNonDishStations(
        Transform parent,
        Material topMaterial,
        Material baseMaterial,
        Material chairMaterial,
        Material monitorMaterial,
        Material screenMaterial)
    {
        // Five individual 2-by-4-foot desks form the compact U shown in the
        // supplied plan: two rear, two front, and a 90-degree joining station.
        const float feetToMeters = 0.3048f;
        const float deskLength = 4f * feetToMeters;
        const float deskDepth = 2f * feetToMeters;
        const float rearRowZ = 1.35f;
        float frontRowZ = rearRowZ - deskLength - deskDepth;
        float leftDeskX = 2.65f;
        float rightDeskX = leftDeskX + deskLength;
        float joiningDeskX = leftDeskX - (deskLength + deskDepth) / 2f;
        float joiningDeskZ = (rearRowZ + frontRowZ) / 2f;
        Transform nonDish = NewGroup("Non-Dish Stations", parent);
        nonDish.localPosition = new Vector3(-0.72f, 0f, 1.13f);

        // Square corner modules turn the three perpendicular runs into one
        // continuous horseshoe instead of leaving point-contact joints.
        BuildSimpleTable("Rear 2-by-2-foot Corner", nonDish,
            new Vector3(joiningDeskX, 0f, rearRowZ),
            new Vector2(deskDepth, deskDepth), topMaterial, baseMaterial);
        BuildSimpleTable("Front 2-by-2-foot Corner", nonDish,
            new Vector3(joiningDeskX, 0f, frontRowZ),
            new Vector2(deskDepth, deskDepth), topMaterial, baseMaterial);

        BuildNonDishStation("Non-Dish Station 1", nonDish,
            new Vector3(leftDeskX, 0f, rearRowZ), 0f,
            new Vector3(0f, 0f, -0.434f),
            topMaterial, baseMaterial, chairMaterial, monitorMaterial, screenMaterial);
        BuildNonDishStation("Non-Dish Station 2", nonDish,
            new Vector3(rightDeskX, 0f, rearRowZ), 0f,
            new Vector3(0f, 0f, -0.760f),
            topMaterial, baseMaterial, chairMaterial, monitorMaterial, screenMaterial);
        BuildNonDishStation("Non-Dish Station 3", nonDish,
            new Vector3(leftDeskX, 0f, frontRowZ), 0f,
            new Vector3(0f, 0f, -0.760f),
            topMaterial, baseMaterial, chairMaterial, monitorMaterial, screenMaterial);
        BuildNonDishStation("Non-Dish Station 4", nonDish,
            new Vector3(rightDeskX, 0f, frontRowZ), 0f,
            new Vector3(0f, 0f, -0.760f),
            topMaterial, baseMaterial, chairMaterial, monitorMaterial, screenMaterial);
        BuildNonDishStation("Non-Dish Station 5", nonDish,
            new Vector3(joiningDeskX, 0f, joiningDeskZ), -90f,
            new Vector3(-0.243f, 0f, -0.507f),
            topMaterial, baseMaterial, chairMaterial, monitorMaterial, screenMaterial);
    }

    private static void BuildSimpleTable(
        string name,
        Transform parent,
        Vector3 floorPosition,
        Vector2 size,
        Material topMaterial,
        Material legMaterial)
    {
        const float height = 0.76f;
        const float topThickness = 0.07f;
        const float legThickness = 0.065f;
        Transform table = NewGroup(name, parent);
        table.localPosition = floorPosition;
        Box("Tabletop", table,
            new Vector3(0f, height - topThickness / 2f, 0f),
            new Vector3(size.x, topThickness, size.y), Quaternion.identity, topMaterial);
        float legY = (height - topThickness) / 2f;
        float legX = size.x / 2f - 0.10f;
        float legZ = size.y / 2f - 0.10f;
        foreach (float x in new[] { -legX, legX })
        {
            foreach (float z in new[] { -legZ, legZ })
            {
                Box("Leg", table, new Vector3(x, legY, z),
                    new Vector3(legThickness, height - topThickness, legThickness),
                    Quaternion.identity, legMaterial);
            }
        }
    }

    private static void BuildNonDishStation(
        string name,
        Transform parent,
        Vector3 floorPosition,
        float yawDegrees,
        Vector3 chairPosition,
        Material topMaterial,
        Material tableBaseMaterial,
        Material chairMaterial,
        Material monitorMaterial,
        Material screenMaterial)
    {
        const float feetToMeters = 0.3048f;
        const float tabletopY = 0.76f;
        Transform station = NewGroup(name, parent);
        station.localPosition = floorPosition;
        station.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
        BuildSimpleTable("2-by-4-foot Desk", station, Vector3.zero,
            new Vector2(4f * feetToMeters, 2f * feetToMeters),
            topMaterial, tableBaseMaterial);
        BuildDesktopMonitor("Left 27-inch Monitor", station,
            new Vector3(-0.34f, 0f, 0.08f), 165f, tabletopY,
            monitorMaterial, screenMaterial);
        BuildDesktopMonitor("Right 27-inch Monitor", station,
            new Vector3(0.34f, 0f, 0.08f), 195f, tabletopY,
            monitorMaterial, screenMaterial);
        Box("Keyboard", station,
            new Vector3(-0.048f, tabletopY + 0.022f, -0.184f),
            new Vector3(0.46f, 0.044f, 0.18f),
            Quaternion.Euler(0f, -4.878f, 0f), monitorMaterial);
        Box("Mouse", station,
            new Vector3(0.34f, tabletopY + 0.026f, -0.20f),
            new Vector3(0.10f, 0.052f, 0.16f),
            Quaternion.Euler(0f, 13.302f, 0f), monitorMaterial);
        BuildSimpleChair("Chair", station, chairPosition,
            chairMaterial, tableBaseMaterial, Vector3.back);
    }

    private static void BuildDesktopMonitor(
        string name,
        Transform parent,
        Vector3 floorPosition,
        float yawDegrees,
        float tabletopY,
        Material bodyMaterial,
        Material screenMaterial)
    {
        const float metersPerInch = 0.0254f;
        const float diagonal = 27f * metersPerInch;
        const float bezel = 0.022f;
        const float bodyDepth = 0.055f;
        const float baseHeight = 0.025f;
        const float standHeight = 0.16f;
        float screenHeight = diagonal / Mathf.Sqrt(16f * 16f + 9f * 9f) * 9f;
        float screenWidth = screenHeight * 16f / 9f;
        float bodyWidth = screenWidth + bezel * 2f;
        float bodyHeight = screenHeight + bezel * 2f;

        Transform monitor = NewGroup(name, parent);
        monitor.localPosition = floorPosition;
        monitor.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
        float baseY = tabletopY + baseHeight / 2f;
        float standY = tabletopY + baseHeight + standHeight / 2f;
        float bodyY = tabletopY + baseHeight + standHeight + bodyHeight / 2f;
        Cylinder("Base", monitor, new Vector3(0f, baseY, 0f),
            0.12f, baseHeight, bodyMaterial);
        Cylinder("Stand", monitor, new Vector3(0f, standY, 0f),
            0.02f, standHeight, bodyMaterial);
        Box("Body", monitor, new Vector3(0f, bodyY, 0f),
            new Vector3(bodyWidth, bodyHeight, bodyDepth), Quaternion.identity, bodyMaterial);
        Box("Screen", monitor, new Vector3(0f, bodyY, bodyDepth / 2f + 0.003f),
            new Vector3(screenWidth, screenHeight, 0.006f), Quaternion.identity, screenMaterial);
    }

    private static void BuildDishStationConsole(
        Transform station,
        FirstPersonPlayerController playerController,
        GroundOpsDishController dishController)
    {
        const float metersPerInch = 0.0254f;
        const float diagonal = 27f * metersPerInch;
        const float tabletopY = 0.76f;
        const float baseHeight = 0.025f;
        const float standHeight = 0.16f;
        const float bezel = 0.022f;
        const float bodyDepth = 0.055f;
        float screenHeight = diagonal / Mathf.Sqrt(16f * 16f + 9f * 9f) * 9f;
        float screenWidth = screenHeight * 16f / 9f;
        float bodyHeight = screenHeight + bezel * 2f;
        float bodyY = tabletopY + baseHeight + standHeight + bodyHeight / 2f;
        Vector3 textPosition = new(0f, bodyY, bodyDepth / 2f + 0.008f);

        Transform leftMonitor = station.Find("Left 27-inch Monitor");
        Transform rightMonitor = station.Find("Right 27-inch Monitor");
        CreateWorldDisplayText(
            "Movement Instructions",
            leftMonitor,
            textPosition,
            screenWidth * 0.92f,
            screenHeight * 0.86f,
            "W/S: elevation +/-\nA/D: azimuth -/+\nShift: 1/5 speed\nCtrl: 5x speed\nMouse: look\nWheel: zoom\n\nF or ESC: stand up",
            48);
        Text readout = CreateWorldDisplayText(
            "Dish Pointing Readout",
            rightMonitor,
            textPosition,
            screenWidth * 0.92f,
            screenHeight * 0.86f,
            "Azimuth: +98°\nElevation: +20°",
            52);
        GroundOpsDishReadoutDisplay display =
            GetOrAddComponent<GroundOpsDishReadoutDisplay>(rightMonitor.gameObject);
        display.Configure(dishController, readout);

        Transform seatedCameraPose = NewGroup("Seated Camera Pose", station);
        Vector3 seatedPosition = new(0.82f, 1.16f, 0f);
        Vector3 seatedTarget = new(-0.10f, 1.12f, 0f);
        seatedCameraPose.localPosition = seatedPosition;
        seatedCameraPose.localRotation = Quaternion.LookRotation(
            seatedTarget - seatedPosition,
            Vector3.up);

        Transform interactionZone = NewGroup("Console Interaction Zone", station);
        interactionZone.localPosition = new Vector3(0.88f, 0.9f, 0f);
        BoxCollider trigger = GetOrAddComponent<BoxCollider>(interactionZone.gameObject);
        trigger.size = new Vector3(1.15f, 1.8f, 1.55f);
        trigger.isTrigger = true;
        Rigidbody triggerBody = GetOrAddComponent<Rigidbody>(interactionZone.gameObject);
        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;
        GroundOpsDishConsoleController consoleController =
            GetOrAddComponent<GroundOpsDishConsoleController>(interactionZone.gameObject);
        consoleController.Configure(
            playerController,
            dishController,
            playerController.PlayerCamera,
            seatedCameraPose);
    }

    private static Text CreateWorldDisplayText(
        string name,
        Transform parent,
        Vector3 localPosition,
        float width,
        float height,
        string content,
        int fontSize,
        Quaternion? localRotation = null)
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
        RectTransform canvasTransform = GetOrAddComponent<RectTransform>(canvasObject);
        canvasTransform.localPosition = localPosition;
        canvasTransform.localRotation = localRotation ?? Quaternion.Euler(0f, 180f, 0f);
        canvasTransform.localScale = Vector3.one * (width / canvasPixelWidth);
        canvasTransform.sizeDelta = new Vector2(canvasPixelWidth, canvasPixelHeight);

        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
        scaler.dynamicPixelsPerUnit = 2f;

        GameObject textObject = AcquireObject(
            "Text",
            canvasTransform,
            () => new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)));
        RectTransform textTransform = GetOrAddComponent<RectTransform>(textObject);
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = new Vector2(22f, 16f);
        textTransform.offsetMax = new Vector2(-22f, -16f);

        Text text = GetOrAddComponent<Text>(textObject);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void BuildSimpleChair(
        string name,
        Transform parent,
        Vector3 floorPosition,
        Material chairMaterial,
        Material baseMaterial,
        Vector3 backDirection)
    {
        Transform chair = NewGroup(name, parent);
        chair.localPosition = floorPosition;
        Cylinder("Floor Base", chair, new Vector3(0f, 0.025f, 0f),
            0.24f, 0.05f, baseMaterial);
        Cylinder("Pedestal", chair, new Vector3(0f, 0.27f, 0f),
            0.035f, 0.46f, baseMaterial);
        Box("Seat", chair, new Vector3(0f, 0.53f, 0f),
            new Vector3(0.48f, 0.11f, 0.48f), Quaternion.identity, chairMaterial);
        Vector3 backPosition = backDirection.normalized * 0.21f + Vector3.up * 0.80f;
        Vector3 backSize = Mathf.Abs(backDirection.x) > 0.5f
            ? new Vector3(0.10f, 0.58f, 0.50f)
            : new Vector3(0.50f, 0.58f, 0.10f);
        Box("Back", chair, backPosition, backSize, Quaternion.identity, chairMaterial);
        RemoveColliders(chair);
    }

    private static void BuildServerRackRow(
        Transform parent,
        Material rackMaterial,
        Material faceMaterial)
    {
        const int rackCount = 7;
        const float width = 0.70f;
        const float height = 2.20f;
        const float depth = 0.90f;
        const float gap = 0.055f;
        const float firstX = -1.65f;
        const float centerZ = 4.88f;
        Transform row = NewGroup("Seven Server Racks", parent);
        for (int index = 0; index < rackCount; index++)
        {
            Transform rack = NewGroup($"Server Rack {index + 1}", row);
            rack.localPosition = new Vector3(firstX + index * (width + gap), 0f, centerZ);
            Box("Cabinet", rack, new Vector3(0f, height / 2f, 0f),
                new Vector3(width, height, depth), Quaternion.identity, rackMaterial);
            Box("Front Face", rack, new Vector3(0f, height / 2f, -depth / 2f - 0.008f),
                new Vector3(width - 0.08f, height - 0.12f, 0.018f), Quaternion.identity, faceMaterial);
        }
    }

    private static Transform BuildDsnRackPair(
        Transform parent,
        Vector3 floorPosition,
        Material rackMaterial,
        Material trimMaterial,
        Material screenMaterial,
        Material legacyBeigeMaterial,
        GroundOpsDishController dishController,
        GroundOpsSatelliteTarget satelliteTarget)
    {
        const float rackWidth = 0.62f;
        const float rackHeight = 2.10f;
        const float rackDepth = 0.90f;
        const float rackGap = 0.04f;
        float rackOffset = (rackWidth + rackGap) / 2f;

        Transform pair = NewGroup("DSN Server Rack", parent);
        pair.localPosition = floorPosition;

        Transform leftRack = NewGroup("DSN Uplink Rack", pair);
        leftRack.localPosition = new Vector3(-rackOffset, 0f, 0f);
        Box("Plain Cabinet", leftRack,
            new Vector3(0f, rackHeight / 2f, 0f),
            new Vector3(rackWidth, rackHeight, rackDepth),
            Quaternion.identity,
            rackMaterial);

        // This is intentionally not a tidy rackmount KVM. The reference shows
        // an ordinary old monitor stuffed into an open bay, with a desktop
        // keyboard and mouse perched on a crude shelf bolted across the rack.
        // Preserve that improvised, mildly alarming character.
        const float frontZ = -rackDepth / 2f;
        const float monitorCenterY = 1.27f;
        Transform kludgedConsole = NewGroup("Kludged Beige Console", leftRack);
        Box("Open Rack Bay", kludgedConsole,
            new Vector3(0f, monitorCenterY - 0.02f, frontZ - 0.008f),
            new Vector3(0.54f, 0.52f, 0.018f),
            Quaternion.identity,
            trimMaterial);
        Box("Loose 4-by-3 Monitor Body", kludgedConsole,
            new Vector3(-0.025f, monitorCenterY, frontZ - 0.045f),
            new Vector3(0.48f, 0.39f, 0.10f),
            Quaternion.Euler(0f, -12f, -1.5f),
            legacyBeigeMaterial);
        Box("Loose Monitor Screen", kludgedConsole,
            new Vector3(-0.025f, monitorCenterY, frontZ - 0.101f),
            new Vector3(0.40f, 0.30f, 0.012f),
            Quaternion.Euler(0f, -12f, -1.5f),
            screenMaterial);
        Text signalReadout = CreateWorldDisplayText(
            "Signal Readout",
            kludgedConsole,
            new Vector3(-0.025f, monitorCenterY, frontZ - 0.109f),
            0.36f,
            0.24f,
            "Power: -60.0 dBm\nFrequency: 8220.000 MHz\nID: GOES-19",
            64,
            Quaternion.Euler(0f, -12f, 0f));
        GroundOpsSignalDisplay signalDisplay =
            GetOrAddComponent<GroundOpsSignalDisplay>(kludgedConsole.gameObject);
        signalDisplay.Configure(
            dishController,
            satelliteTarget,
            signalReadout,
            -60f,
            15f);
        Box("Improvised Shelf", kludgedConsole,
            new Vector3(0f, 0.92f, frontZ - 0.22f),
            new Vector3(0.58f, 0.045f, 0.43f),
            Quaternion.Euler(0f, -1.5f, 0f),
            legacyBeigeMaterial);
        Box("Desktop Keyboard", kludgedConsole,
            new Vector3(-0.035f, 0.955f, frontZ - 0.25f),
            new Vector3(0.40f, 0.035f, 0.18f),
            Quaternion.Euler(0f, 2f, 0f),
            legacyBeigeMaterial);
        Box("Desktop Mouse", kludgedConsole,
            new Vector3(0.215f, 0.962f, frontZ - 0.27f),
            new Vector3(0.09f, 0.045f, 0.12f),
            Quaternion.Euler(0f, -8f, 0f),
            legacyBeigeMaterial);

        Transform rightRack = NewGroup("DSN Downlink Rack", pair);
        rightRack.localPosition = new Vector3(rackOffset, 0f, 0f);
        Box("Cabinet", rightRack,
            new Vector3(0f, rackHeight / 2f, 0f),
            new Vector3(rackWidth, rackHeight, rackDepth),
            Quaternion.identity,
            rackMaterial);

        // The rack faces world -Z. This simplified KVM follows the reference:
        // inset monitor above a pull-out keyboard shelf on the right cabinet.
        Box("KVM Bezel", rightRack,
            new Vector3(0f, 1.24f, frontZ - 0.022f),
            new Vector3(0.50f, 0.39f, 0.045f),
            Quaternion.identity,
            trimMaterial);
        Box("KVM Screen", rightRack,
            new Vector3(0f, 1.25f, frontZ - 0.048f),
            new Vector3(0.43f, 0.31f, 0.012f),
            Quaternion.identity,
            screenMaterial);
        Box("Pull-out Tray", rightRack,
            new Vector3(0f, 0.96f, frontZ - 0.18f),
            new Vector3(0.54f, 0.045f, 0.34f),
            Quaternion.identity,
            trimMaterial);
        Box("KVM Keyboard", rightRack,
            new Vector3(0f, 0.99f, frontZ - 0.20f),
            new Vector3(0.45f, 0.025f, 0.20f),
            Quaternion.identity,
            rackMaterial);

        Transform stool = NewGroup("DSN Console Stool", pair);
        stool.localPosition = new Vector3(0f, 0f, -1.08f);
        Cylinder("Floor Base", stool, new Vector3(0f, 0.025f, 0f),
            0.25f, 0.05f, trimMaterial);
        Cylinder("Trunk", stool, new Vector3(0f, 0.29f, 0f),
            0.04f, 0.50f, trimMaterial);
        Cylinder("Seat", stool, new Vector3(0f, 0.57f, 0f),
            0.23f, 0.08f, legacyBeigeMaterial);
        RemoveColliders(stool);
        return pair;
    }

    private static void BuildDsnRackConsole(
        Transform rackPair,
        FirstPersonPlayerController playerController)
    {
        Transform seatedCameraPose = NewGroup("Seated Camera Pose", rackPair);
        Vector3 seatedPosition = new(0f, 1.22f, -1.08f);
        Vector3 seatedTarget = new(0f, 1.34f, -0.48f);
        seatedCameraPose.localPosition = seatedPosition;
        seatedCameraPose.localRotation = Quaternion.LookRotation(
            seatedTarget - seatedPosition,
            Vector3.up);

        Transform interactionZone = NewGroup("Console Interaction Zone", rackPair);
        interactionZone.localPosition = new Vector3(0f, 0.9f, -1.08f);
        BoxCollider trigger = GetOrAddComponent<BoxCollider>(interactionZone.gameObject);
        trigger.size = new Vector3(1.25f, 1.8f, 1.25f);
        trigger.isTrigger = true;
        Rigidbody triggerBody = GetOrAddComponent<Rigidbody>(interactionZone.gameObject);
        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;
        SimpleSeatedConsoleController consoleController =
            GetOrAddComponent<SimpleSeatedConsoleController>(interactionZone.gameObject);
        consoleController.Configure(
            playerController,
            playerController.PlayerCamera,
            seatedCameraPose);
    }

    private static GroundOpsDishController BuildExteriorLandscape(
        Transform parent,
        Material terrainMaterial,
        Material forestTrunkMaterial,
        Material[] forestCrownMaterials,
        Material dishMaterial,
        Vector3 worldNorth,
        Vector3 worldEast,
        float initialAzimuth,
        float initialElevation)
    {
        Mesh terrainMesh = GetMountainTerrainMesh("GroundOps_MountainTerrain");
        MeshObject("Low-poly Mountain Ridge", parent, terrainMesh, terrainMaterial, false);

        BuildForest(
            NewGroup("Low-poly Forest", parent),
            forestTrunkMaterial,
            forestCrownMaterials);

        // The actual antennas are roughly 2,500 feet (762 m) from the DOC. Their
        // diameters remain at 1:10 scale, but the complex is deliberately staged
        // only 25.4 m away so it reads about three times closer/larger than reality.
        Vector2 smallDishPosition =
            ExteriorViewOrigin
            + ExteriorViewDirection * 25.4f
            - ExteriorLateralDirection * 3.0f;
        Vector2 largeDishPosition =
            ExteriorViewOrigin
            + ExteriorViewDirection * 25.4f
            + ExteriorLateralDirection * 3.0f;
        Transform smallReflector = BuildDishProxy("13-meter Dish Proxy", parent,
            smallDishPosition, 1.30f, 2.0f,
            new Vector3(0.68f, 0.70f, 0.18f),
            SmallDishRootPosition, SmallDishRootScale, SmallDishReflectorOffsetY,
            dishMaterial);
        Transform largeReflector = BuildDishProxy("21-meter Dish Proxy", parent,
            largeDishPosition, 2.10f, 2.6f,
            new Vector3(0.58f, 0.79f, -0.20f),
            LargeDishRootPosition, LargeDishRootScale, LargeDishReflectorOffsetY,
            dishMaterial);
        GroundOpsDishController controller =
            GetOrAddComponent<GroundOpsDishController>(parent.gameObject);
        controller.Configure(
            new[] { smallReflector, largeReflector },
            worldNorth,
            worldEast,
            initialAzimuth,
            initialElevation);
        return controller;
    }

    private static void BuildForest(
        Transform parent,
        Material trunkMaterial,
        Material[] crownMaterials)
    {
        const float spacing = 1.55f;
        const float forestRadius = 112f;
        const float docClearingRadius = 24f;
        const float windowSideBackfill = 5f;
        const int gridRadius = 73;

        List<Vector3> trunkVertices = new();
        List<int> trunkTriangles = new();
        List<Vector3>[] crownVertices = crownMaterials.Select(_ => new List<Vector3>()).ToArray();
        List<int>[] crownTriangles = crownMaterials.Select(_ => new List<int>()).ToArray();

        for (int zIndex = -gridRadius; zIndex <= gridRadius; zIndex++)
        {
            for (int xIndex = -gridRadius; xIndex <= gridRadius; xIndex++)
            {
                float jitterX = (StableNoise01(xIndex, zIndex, 1) - 0.5f) * spacing * 0.72f;
                float jitterZ = (StableNoise01(xIndex, zIndex, 2) - 0.5f) * spacing * 0.72f;
                float x = ExteriorViewOrigin.x + xIndex * spacing + jitterX;
                float z = ExteriorViewOrigin.y + zIndex * spacing + jitterZ;
                Vector2 horizontal = new(x, z);

                if ((horizontal - ExteriorViewOrigin).sqrMagnitude > forestRadius * forestRadius) continue;
                if ((horizontal - ExteriorViewOrigin).sqrMagnitude < docClearingRadius * docClearingRadius) continue;
                // Only the broad -X half of the landscape is visible through the
                // curved window. Retain a little backfill beyond the DOC center
                // so the forest never ends visibly at an exact radial boundary.
                if (x > ExteriorViewOrigin.x + windowSideBackfill) continue;
                if (StableNoise01(xIndex, zIndex, 3) < 0.04f) continue;

                float groundY = MountainHeight(x, z);
                float height = Mathf.Lerp(1.30f, 2.35f, StableNoise01(xIndex, zIndex, 4));
                float crownRadius = Mathf.Lerp(0.82f, 1.22f, StableNoise01(xIndex, zIndex, 5));
                float slopeBlend = Mathf.InverseLerp(0.08f, 0.75f, TerrainSlope(x, z));
                crownRadius *= Mathf.Lerp(1f, 1.95f, slopeBlend);
                float trunkHeight = height
                    * Mathf.Lerp(0.38f, 0.52f, StableNoise01(xIndex, zIndex, 6))
                    * Mathf.Lerp(1f, 0.68f, slopeBlend);
                float crownHeight = (height - trunkHeight) * Mathf.Lerp(1f, 1.35f, slopeBlend);
                float trunkRadius = Mathf.Lerp(0.055f, 0.095f, StableNoise01(xIndex, zIndex, 7));
                int paletteIndex = Mathf.Min(
                    crownMaterials.Length - 1,
                    Mathf.FloorToInt(StableNoise01(xIndex, zIndex, 8) * crownMaterials.Length));

                AddTreeTrunk(
                    trunkVertices,
                    trunkTriangles,
                    new Vector3(x, groundY, z),
                    trunkRadius,
                    trunkHeight);
                AddTreeCrown(
                    crownVertices[paletteIndex],
                    crownTriangles[paletteIndex],
                    new Vector3(x, groundY + trunkHeight, z),
                    crownRadius,
                    crownHeight,
                    xIndex,
                    zIndex);
            }
        }

        Mesh trunkMesh = GetGeneratedMesh("GroundOps_ForestTrunks", trunkVertices, trunkTriangles);
        MeshObject("Forest Trunks", parent, trunkMesh, trunkMaterial, false);
        for (int index = 0; index < crownMaterials.Length; index++)
        {
            Mesh crownMesh = GetGeneratedMesh(
                $"GroundOps_ForestCrowns{index + 1}",
                crownVertices[index],
                crownTriangles[index]);
            MeshObject($"Forest Crowns {index + 1}", parent, crownMesh, crownMaterials[index], false);
        }
    }

    private static float TerrainSlope(float x, float z)
    {
        const float sampleOffset = 0.65f;
        float xGradient = (
            MountainHeight(x + sampleOffset, z)
            - MountainHeight(x - sampleOffset, z)) / (sampleOffset * 2f);
        float zGradient = (
            MountainHeight(x, z + sampleOffset)
            - MountainHeight(x, z - sampleOffset)) / (sampleOffset * 2f);
        return Mathf.Sqrt(xGradient * xGradient + zGradient * zGradient);
    }

    private static void AddTreeTrunk(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3 baseCenter,
        float radius,
        float height)
    {
        const int sides = 5;
        int start = vertices.Count;
        for (int ring = 0; ring < 2; ring++)
        {
            float y = baseCenter.y + ring * height;
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(
                    baseCenter.x + Mathf.Cos(angle) * radius,
                    y,
                    baseCenter.z + Mathf.Sin(angle) * radius));
            }
        }

        for (int side = 0; side < sides; side++)
        {
            int next = (side + 1) % sides;
            triangles.Add(start + side);
            triangles.Add(start + sides + side);
            triangles.Add(start + sides + next);
            triangles.Add(start + side);
            triangles.Add(start + sides + next);
            triangles.Add(start + next);
        }
    }

    private static void AddTreeCrown(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3 baseCenter,
        float radius,
        float height,
        int xIndex,
        int zIndex)
    {
        const int sides = 7;
        int start = vertices.Count;
        vertices.Add(baseCenter + Vector3.up * height);
        vertices.Add(baseCenter - Vector3.up * height * 0.08f);
        float lowerRingY = baseCenter.y + height * 0.38f;
        float upperRingY = baseCenter.y + height * 0.76f;
        float yaw = StableNoise01(xIndex, zIndex, 9) * Mathf.PI * 2f;
        for (int side = 0; side < sides; side++)
        {
            float angle = yaw + side * Mathf.PI * 2f / sides;
            float irregularity = Mathf.Lerp(
                0.82f,
                1.16f,
                StableNoise01(xIndex * 11 + side, zIndex * 13 - side, 10));
            vertices.Add(new Vector3(
                baseCenter.x + Mathf.Cos(angle) * radius * irregularity * 0.72f,
                upperRingY,
                baseCenter.z + Mathf.Sin(angle) * radius * irregularity * 0.72f));
        }
        for (int side = 0; side < sides; side++)
        {
            float angle = yaw + side * Mathf.PI * 2f / sides;
            float irregularity = Mathf.Lerp(
                0.82f,
                1.16f,
                StableNoise01(xIndex * 11 + side, zIndex * 13 - side, 10));
            vertices.Add(new Vector3(
                baseCenter.x + Mathf.Cos(angle) * radius * irregularity,
                lowerRingY,
                baseCenter.z + Mathf.Sin(angle) * radius * irregularity));
        }

        for (int side = 0; side < sides; side++)
        {
            int upperCurrent = start + 2 + side;
            int upperNext = start + 2 + (side + 1) % sides;
            int lowerCurrent = start + 2 + sides + side;
            int lowerNext = start + 2 + sides + (side + 1) % sides;
            triangles.Add(start);
            triangles.Add(upperNext);
            triangles.Add(upperCurrent);

            triangles.Add(upperCurrent);
            triangles.Add(upperNext);
            triangles.Add(lowerNext);
            triangles.Add(upperCurrent);
            triangles.Add(lowerNext);
            triangles.Add(lowerCurrent);

            triangles.Add(start + 1);
            triangles.Add(lowerCurrent);
            triangles.Add(lowerNext);
        }
    }

    private static float StableNoise01(int x, int z, int salt)
    {
        unchecked
        {
            uint value = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(salt * 83492791);
            value ^= value >> 13;
            value *= 1274126177u;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215f;
        }
    }

    private static Mesh GetGeneratedMesh(
        string name,
        List<Vector3> vertices,
        List<int> triangles)
    {
        string path = $"{MeshFolder}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
        }

        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Transform BuildDishProxy(
        string name,
        Transform parent,
        Vector2 horizontalPosition,
        float diameter,
        float postHeight,
        Vector3 dishNormal,
        Vector3 rootPosition,
        float rootScale,
        float reflectorOffsetY,
        Material material)
    {
        Transform dish = NewGroup(name, parent);
        dish.localPosition = rootPosition;
        dish.localScale = Vector3.one * rootScale;

        // The user scaled each whole proxy around its visible geometry. Retain
        // the pre-scale terrain reference for child placement so the recorded
        // compensating root positions and the enlarged geometry reproduce that
        // exact Editor composition.
        Vector2 worldHorizontalPosition =
            horizontalPosition + new Vector2(DishComplexOffset.x, DishComplexOffset.z);
        float groundY =
            MountainHeight(worldHorizontalPosition.x, worldHorizontalPosition.y)
            - DishComplexOffset.y;
        Cylinder("Post", dish,
            new Vector3(horizontalPosition.x, groundY + postHeight / 2f, horizontalPosition.y),
            0.075f, postHeight, material);
        Transform pointingAssembly = NewGroup("Pointing Assembly", dish);
        pointingAssembly.localPosition =
            new Vector3(horizontalPosition.x, groundY + postHeight, horizontalPosition.y);
        pointingAssembly.localRotation =
            Quaternion.FromToRotation(Vector3.up, dishNormal.normalized);
        GameObject reflector = MeshObject(
            "Dish Circle",
            pointingAssembly,
            GetDishReflectorMesh($"GroundOps_{name.Replace(' ', '_')}_Reflector", diameter),
            material,
            false);
        reflector.transform.localPosition = new Vector3(0f, reflectorOffsetY, 0f);
        foreach (Collider collider in reflector.GetComponents<Collider>())
        {
            Object.DestroyImmediate(collider);
        }

        float subreflectorHeight = diameter * 0.34f;
        float subreflectorRadius = diameter * 0.085f;
        Cylinder("Subreflector", pointingAssembly,
            new Vector3(0f, subreflectorHeight, 0f),
            subreflectorRadius, 0.045f, material);

        float supportRadius = diameter * 0.36f;
        float innerRadius = subreflectorRadius * 0.60f;
        Vector3[] supportDirections =
        {
            Vector3.right,
            Vector3.left,
            Vector3.forward,
            Vector3.back,
        };
        for (int index = 0; index < supportDirections.Length; index++)
        {
            Vector3 radial = supportDirections[index];
            Vector3 start = radial * supportRadius + Vector3.up * 0.04f;
            Vector3 end = radial * innerRadius + Vector3.up * subreflectorHeight;
            BoxBetween(
                $"Subreflector Leg {index + 1}",
                pointingAssembly,
                start,
                end,
                Mathf.Max(0.036f, diameter * 0.036f),
                material);
        }

        return pointingAssembly;
    }

    private static Mesh GetDishReflectorMesh(string name, float diameter)
    {
        string path = $"{MeshFolder}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
        }

        const int radialSegments = 8;
        const int angularSegments = 32;
        float apertureRadius = diameter * 0.5f;
        float depth = diameter * 0.16f;
        float shellThickness = Mathf.Max(0.025f, diameter * 0.025f);
        float sphereRadius =
            (apertureRadius * apertureRadius + depth * depth) / (2f * depth);
        float sphereCenterY = sphereRadius - depth;

        List<Vector3> vertices = new(2 + radialSegments * angularSegments * 2);
        List<int> triangles = new(radialSegments * angularSegments * 12);

        vertices.Add(new Vector3(0f, -depth, 0f));
        for (int ring = 1; ring <= radialSegments; ring++)
        {
            float radius = apertureRadius * ring / radialSegments;
            float y = sphereCenterY - Mathf.Sqrt(
                Mathf.Max(0f, sphereRadius * sphereRadius - radius * radius));
            for (int side = 0; side < angularSegments; side++)
            {
                float angle = side * Mathf.PI * 2f / angularSegments;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    y,
                    Mathf.Sin(angle) * radius));
            }
        }

        int backCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -depth - shellThickness, 0f));
        for (int ring = 1; ring <= radialSegments; ring++)
        {
            float radius = apertureRadius * ring / radialSegments;
            float y = sphereCenterY - Mathf.Sqrt(
                Mathf.Max(0f, sphereRadius * sphereRadius - radius * radius));
            for (int side = 0; side < angularSegments; side++)
            {
                float angle = side * Mathf.PI * 2f / angularSegments;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    y - shellThickness,
                    Mathf.Sin(angle) * radius));
            }
        }

        int FrontIndex(int ring, int side) =>
            1 + (ring - 1) * angularSegments + side % angularSegments;
        int BackIndex(int ring, int side) =>
            backCenter + 1 + (ring - 1) * angularSegments + side % angularSegments;

        for (int side = 0; side < angularSegments; side++)
        {
            int next = (side + 1) % angularSegments;
            triangles.Add(0);
            triangles.Add(FrontIndex(1, next));
            triangles.Add(FrontIndex(1, side));

            triangles.Add(backCenter);
            triangles.Add(BackIndex(1, side));
            triangles.Add(BackIndex(1, next));
        }

        for (int ring = 1; ring < radialSegments; ring++)
        {
            for (int side = 0; side < angularSegments; side++)
            {
                int next = (side + 1) % angularSegments;
                int frontInner = FrontIndex(ring, side);
                int frontInnerNext = FrontIndex(ring, next);
                int frontOuter = FrontIndex(ring + 1, side);
                int frontOuterNext = FrontIndex(ring + 1, next);
                triangles.Add(frontInner);
                triangles.Add(frontOuterNext);
                triangles.Add(frontOuter);
                triangles.Add(frontInner);
                triangles.Add(frontInnerNext);
                triangles.Add(frontOuterNext);

                int backInner = BackIndex(ring, side);
                int backInnerNext = BackIndex(ring, next);
                int backOuter = BackIndex(ring + 1, side);
                int backOuterNext = BackIndex(ring + 1, next);
                triangles.Add(backInner);
                triangles.Add(backOuter);
                triangles.Add(backOuterNext);
                triangles.Add(backInner);
                triangles.Add(backOuterNext);
                triangles.Add(backInnerNext);
            }
        }

        for (int side = 0; side < angularSegments; side++)
        {
            int next = (side + 1) % angularSegments;
            int frontCurrent = FrontIndex(radialSegments, side);
            int frontNext = FrontIndex(radialSegments, next);
            int backCurrent = BackIndex(radialSegments, side);
            int backNext = BackIndex(radialSegments, next);
            triangles.Add(frontCurrent);
            triangles.Add(backNext);
            triangles.Add(frontNext);
            triangles.Add(frontCurrent);
            triangles.Add(backCurrent);
            triangles.Add(backNext);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Mesh GetMountainTerrainMesh(string name)
    {
        string path = $"{MeshFolder}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
        }

        // This is a complete, rotated USGS-derived landscape around the DOC,
        // rather than a backdrop aimed at one window. It extends beyond the
        // camera's useful view distance in every horizontal direction.
        const int segmentsPerAxis = 48;
        float halfExtent = GroundOpsTerrainElevationData.HalfExtentMeters;
        List<Vector3> vertices = new(segmentsPerAxis * segmentsPerAxis * 6);
        List<int> triangles = new(segmentsPerAxis * segmentsPerAxis * 6);

        for (int northIndex = 0; northIndex < segmentsPerAxis; northIndex++)
        {
            float north0 = Mathf.Lerp(-halfExtent, halfExtent,
                northIndex / (float)segmentsPerAxis);
            float north1 = Mathf.Lerp(-halfExtent, halfExtent,
                (northIndex + 1) / (float)segmentsPerAxis);
            for (int eastIndex = 0; eastIndex < segmentsPerAxis; eastIndex++)
            {
                float east0 = Mathf.Lerp(-halfExtent, halfExtent,
                    eastIndex / (float)segmentsPerAxis);
                float east1 = Mathf.Lerp(-halfExtent, halfExtent,
                    (eastIndex + 1) / (float)segmentsPerAxis);
                Vector3 p00 = TerrainPointFromMeters(east0, north0);
                Vector3 p10 = TerrainPointFromMeters(east1, north0);
                Vector3 p11 = TerrainPointFromMeters(east1, north1);
                Vector3 p01 = TerrainPointFromMeters(east0, north1);
                if ((eastIndex + northIndex) % 2 == 0)
                {
                    AddFlatTriangle(vertices, triangles, p00, p01, p11);
                    AddFlatTriangle(vertices, triangles, p00, p11, p10);
                }
                else
                {
                    AddFlatTriangle(vertices, triangles, p00, p01, p10);
                    AddFlatTriangle(vertices, triangles, p10, p01, p11);
                }
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static float MountainHeight(float x, float z)
    {
        Vector2 worldOffset = new Vector2(x, z) - ExteriorViewOrigin;
        Vector2 unscaled = worldOffset / TerrainHorizontalScale;
        float eastMeters =
            TerrainRotationCos * unscaled.x + TerrainRotationSin * unscaled.y;
        float northMeters =
            -TerrainRotationSin * unscaled.x + TerrainRotationCos * unscaled.y;
        float elevationMeters = SampleRealTerrain(eastMeters, northMeters);
        float naturalHeight = -4.2f + (
            elevationMeters - GroundOpsTerrainElevationData.DocElevationMeters)
            * TerrainVerticalScale;
        float docGradeWeight = 1f - Mathf.SmoothStep(
            0f, 1f, Mathf.InverseLerp(8f, 22f, worldOffset.magnitude));
        naturalHeight = Mathf.Lerp(naturalHeight, -4.2f, docGradeWeight);

        // The real complex follows a ridge, not an isolated summit. Extend the
        // crest through both dishes along their shared lateral axis, then blend
        // it gradually into the sampled terrain across and beyond the complex.
        Vector2 ridgeOffset = new Vector2(x, z) - DishTerrainCenter;
        Vector2 ridgeAxis = ExteriorLateralDirection.normalized;
        Vector2 ridgeAcrossAxis = new(-ridgeAxis.y, ridgeAxis.x);
        float alongRidge = Vector2.Dot(ridgeOffset, ridgeAxis);
        float acrossRidge = Vector2.Dot(ridgeOffset, ridgeAcrossAxis);
        float acrossWeight = 1f - Mathf.SmoothStep(
            0f, 1f, Mathf.InverseLerp(5f, 20f, Mathf.Abs(acrossRidge)));
        float alongWeight = 1f - Mathf.SmoothStep(
            0f, 1f, Mathf.InverseLerp(36f, 78f, Mathf.Abs(alongRidge)));
        float ridgeWeight = acrossWeight * alongWeight;
        float crestHeight =
            12.56f
            - 0.010f * Mathf.Abs(alongRidge)
            + 0.18f * Mathf.Sin(alongRidge * 0.10f);
        return Mathf.Lerp(naturalHeight, crestHeight, ridgeWeight);
    }

    private static float SampleRealTerrain(float eastMeters, float northMeters)
    {
        int sampleCount = GroundOpsTerrainElevationData.SampleCount;
        float halfExtent = GroundOpsTerrainElevationData.HalfExtentMeters;
        float sampleX = Mathf.InverseLerp(-halfExtent, halfExtent, eastMeters)
            * (sampleCount - 1);
        float sampleZ = Mathf.InverseLerp(-halfExtent, halfExtent, northMeters)
            * (sampleCount - 1);
        int x0 = Mathf.Clamp(Mathf.FloorToInt(sampleX), 0, sampleCount - 2);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(sampleZ), 0, sampleCount - 2);
        float tx = Mathf.Clamp01(sampleX - x0);
        float tz = Mathf.Clamp01(sampleZ - z0);
        float south = Mathf.Lerp(
            SmoothedElevation(z0, x0),
            SmoothedElevation(z0, x0 + 1),
            tx);
        float north = Mathf.Lerp(
            SmoothedElevation(z0 + 1, x0),
            SmoothedElevation(z0 + 1, x0 + 1),
            tx);
        return Mathf.Lerp(south, north, tz);
    }

    private static float SmoothedElevation(int z, int x)
    {
        int sampleCount = GroundOpsTerrainElevationData.SampleCount;
        float weightedSum = 0f;
        float totalWeight = 0f;
        for (int dz = -1; dz <= 1; dz++)
        {
            int sampleZ = Mathf.Clamp(z + dz, 0, sampleCount - 1);
            float zWeight = dz == 0 ? 2f : 1f;
            for (int dx = -1; dx <= 1; dx++)
            {
                int sampleX = Mathf.Clamp(x + dx, 0, sampleCount - 1);
                float weight = zWeight * (dx == 0 ? 2f : 1f);
                weightedSum += GroundOpsTerrainElevationData.ElevationsMeters[
                    sampleZ, sampleX] * weight;
                totalWeight += weight;
            }
        }
        return weightedSum / totalWeight;
    }

    private static Vector3 TerrainPointFromMeters(float eastMeters, float northMeters)
    {
        Vector2 horizontal = ExteriorViewOrigin + TerrainHorizontalScale * new Vector2(
            TerrainRotationCos * eastMeters - TerrainRotationSin * northMeters,
            TerrainRotationSin * eastMeters + TerrainRotationCos * northMeters);
        return new Vector3(
            horizontal.x,
            MountainHeight(horizontal.x, horizontal.y),
            horizontal.y);
    }

    private static void AddFlatTriangle(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3 first,
        Vector3 second,
        Vector3 third)
    {
        int start = vertices.Count;
        vertices.Add(first);
        vertices.Add(second);
        vertices.Add(third);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
    }

    private static void CalculateWorldCardinalAxes(
        out Vector3 worldNorth,
        out Vector3 worldEast)
    {
        const double earthRadiusMeters = 6371000.0;
        double meanLatitudeRadians = (
            DocLatitudeDegrees + AntennaLatitudeDegrees) * 0.5 * Mathf.Deg2Rad;
        double trueNorthMeters = (
            AntennaLatitudeDegrees - DocLatitudeDegrees) * Mathf.Deg2Rad * earthRadiusMeters;
        double trueEastMeters = (
            AntennaLongitudeDegrees - DocLongitudeDegrees) * Mathf.Deg2Rad
            * earthRadiusMeters * System.Math.Cos(meanLatitudeRadians);
        float realDirectionAngle = Mathf.Atan2((float)trueNorthMeters, (float)trueEastMeters);
        Vector2 stagedAntennaDirection = (DishTerrainCenter - ExteriorViewOrigin).normalized;
        float worldDirectionAngle = Mathf.Atan2(
            stagedAntennaDirection.y,
            stagedAntennaDirection.x);
        float cardinalRotation = worldDirectionAngle - realDirectionAngle;

        worldEast = new Vector3(
            Mathf.Cos(cardinalRotation),
            0f,
            Mathf.Sin(cardinalRotation)).normalized;
        worldNorth = new Vector3(-worldEast.z, 0f, worldEast.x).normalized;
    }

    private static void BuildGeographicReference(
        Transform parent,
        Vector3 worldNorth,
        Vector3 worldEast,
        Material northMaterial,
        Material eastMaterial,
        Material centerMaterial)
    {
        const float axisLength = 5f;
        const float axisWidth = 0.16f;
        const float markerY = -0.32f;
        Box("Cardinal Origin", parent,
            new Vector3(0f, markerY, 0f),
            new Vector3(0.44f, 0.08f, 0.44f),
            Quaternion.identity,
            centerMaterial);
        BuildCardinalAxis("TRUE NORTH (+N)", parent, worldNorth,
            axisLength, axisWidth, markerY, northMaterial);
        BuildCardinalAxis("EAST (+E)", parent, worldEast,
            axisLength, axisWidth, markerY, eastMaterial);
    }

    private static void BuildCardinalAxis(
        string name,
        Transform parent,
        Vector3 direction,
        float length,
        float width,
        float y,
        Material material)
    {
        Transform axis = NewGroup(name, parent);
        float yaw = -Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Box("Shaft", axis,
            direction * (length * 0.5f) + Vector3.up * y,
            new Vector3(length, 0.08f, width),
            rotation,
            material);
        Box("Arrowhead", axis,
            direction * length + Vector3.up * y,
            new Vector3(0.52f, 0.10f, 0.52f),
            Quaternion.Euler(0f, yaw + 45f, 0f),
            material);
    }

    private static void BuildCameraAndLight(
        Transform parent,
        Material skyMaterial,
        Vector3 worldNorth,
        Vector3 worldEast)
    {
        Transform skyAndSun = NewGroup("Sky and Sun", parent);
        GameObject lightObject = AcquireObject("Sun", skyAndSun, () => new GameObject("Sun"));
        Light light = GetOrAddComponent<Light>(lightObject);
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        GroundOpsSkyController skyController =
            GetOrAddComponent<GroundOpsSkyController>(skyAndSun.gameObject);
        skyController.Configure(
            light,
            skyMaterial,
            worldNorth,
            worldEast,
            DocLatitudeDegrees,
            DocLongitudeDegrees);
    }

    private static FirstPersonPlayerController BuildPlayer(
        Transform player,
        Material material,
        Vector3 startPosition,
        Vector3 lookTarget)
    {
        // Start behind Dish Station 3, facing the curved window wall.
        player.localPosition = startPosition;
        Vector3 lookDirection = Vector3.ProjectOnPlane(
            lookTarget - (startPosition + Vector3.up * 1.65f),
            Vector3.up);
        player.localRotation = Quaternion.LookRotation(
            lookDirection.normalized,
            Vector3.up);

        GameObject body = AcquireObject(
            "Capsule Body",
            player,
            () => GameObject.CreatePrimitive(PrimitiveType.Capsule));
        body.transform.SetParent(player, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
        body.GetComponent<Renderer>().sharedMaterial = material;
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null) Object.DestroyImmediate(bodyCollider);

        CharacterController characterController =
            GetOrAddComponent<CharacterController>(player.gameObject);
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.height = 1.8f;
        characterController.radius = 0.3f;
        characterController.stepOffset = 0.3f;
        characterController.skinWidth = 0.05f;

        GameObject cameraObject = AcquireObject(
            "Main Camera", player, () => new GameObject("Main Camera"));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.transform.localScale = Vector3.one;
        Camera camera = GetOrAddComponent<Camera>(cameraObject);
        camera.fieldOfView = 68f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 180f;
        camera.clearFlags = CameraClearFlags.Skybox;

        FirstPersonPlayerController controller =
            GetOrAddComponent<FirstPersonPlayerController>(player.gameObject);
        controller.Configure(camera);
        return controller;
    }

    private static Mesh GetFloorMesh(string name, Vector3[] boundary)
    {
        string path = $"{MeshFolder}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
        }

        int[] triangles = new int[(boundary.Length - 2) * 3];
        for (int index = 0; index < boundary.Length - 2; index++)
        {
            triangles[index * 3] = 0;
            triangles[index * 3 + 1] = index + 1;
            triangles[index * 3 + 2] = index + 2;
        }

        mesh.vertices = boundary;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Mesh GetSlabMesh(
        string name,
        Vector3[] boundary,
        float bottomY,
        float topY)
    {
        string path = $"{MeshFolder}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
        }

        int count = boundary.Length;
        List<Vector3> vertices = new(count * 2);
        for (int index = 0; index < count; index++)
        {
            vertices.Add(new Vector3(boundary[index].x, bottomY, boundary[index].z));
        }
        for (int index = 0; index < count; index++)
        {
            vertices.Add(new Vector3(boundary[index].x, topY, boundary[index].z));
        }

        List<int> triangles = new((count - 2) * 6 + count * 6);
        for (int index = 1; index < count - 1; index++)
        {
            triangles.Add(0);
            triangles.Add(index + 1);
            triangles.Add(index);
            triangles.Add(count);
            triangles.Add(count + index);
            triangles.Add(count + index + 1);
        }
        for (int index = 0; index < count; index++)
        {
            int next = (index + 1) % count;
            int bottomCurrent = index;
            int bottomNext = next;
            int topCurrent = count + index;
            int topNext = count + next;
            triangles.Add(bottomCurrent);
            triangles.Add(topCurrent);
            triangles.Add(topNext);
            triangles.Add(bottomCurrent);
            triangles.Add(topNext);
            triangles.Add(bottomNext);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static GameObject MeshObject(
        string name,
        Transform parent,
        Mesh mesh,
        Material material,
        bool addCollider = true)
    {
        GameObject gameObject = AcquireObject(name, parent, () => new GameObject(name));
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
        MeshFilter filter = GetOrAddComponent<MeshFilter>(gameObject);
        filter.sharedMesh = mesh;
        MeshRenderer renderer = GetOrAddComponent<MeshRenderer>(gameObject);
        renderer.sharedMaterial = material;
        MeshCollider collider = gameObject.GetComponent<MeshCollider>();
        if (addCollider)
        {
            collider = collider != null ? collider : gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
        else if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
        return gameObject;
    }

    private static GameObject Box(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 size,
        Quaternion rotation,
        Material material)
    {
        GameObject gameObject = AcquireObject(
            name,
            parent,
            () => GameObject.CreatePrimitive(PrimitiveType.Cube));
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localRotation = rotation;
        gameObject.transform.localScale = size;
        Renderer renderer = gameObject.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return gameObject;
    }

    private static GameObject BoxBetween(
        string name,
        Transform parent,
        Vector3 start,
        Vector3 end,
        float thickness,
        Material material)
    {
        Vector3 delta = end - start;
        return Box(
            name,
            parent,
            (start + end) * 0.5f,
            new Vector3(thickness, delta.magnitude, thickness),
            Quaternion.FromToRotation(Vector3.up, delta.normalized),
            material);
    }

    private static GameObject WallBox(
        string name,
        Transform parent,
        Transform cutawayParent,
        Vector3 position,
        Vector3 size,
        Material material,
        Vector3 inwardNormal,
        List<Renderer> physicalRenderers,
        List<Renderer> cutawayRenderers,
        string cutawayName = null)
    {
        GameObject physical = Box(name, parent, position, size, Quaternion.identity, material);
        physicalRenderers.Add(physical.GetComponent<Renderer>());

        Vector3 normal = inwardNormal.normalized;
        float faceOffset = Mathf.Abs(normal.x) > 0.5f ? size.x / 2f : size.z / 2f;
        float width = Mathf.Abs(normal.x) > 0.5f ? size.z : size.x;
        GameObject cutaway = Quad(
            cutawayName ?? name,
            cutawayParent,
            position + normal * faceOffset,
            width,
            size.y,
            normal,
            material);
        Collider collider = cutaway.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        Renderer cutawayRenderer = cutaway.GetComponent<Renderer>();
        cutawayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        cutawayRenderers.Add(cutawayRenderer);
        return physical;
    }

    private static GameObject Quad(
        string name,
        Transform parent,
        Vector3 position,
        float width,
        float height,
        Vector3 normal,
        Material material)
    {
        GameObject gameObject = AcquireObject(
            name,
            parent,
            () => GameObject.CreatePrimitive(PrimitiveType.Quad));
        Mesh mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
        Vector3 primitiveNormal = mesh.normals.Length > 0
            ? mesh.normals[0].normalized
            : Vector3.back;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localRotation = Quaternion.FromToRotation(primitiveNormal, normal);
        gameObject.transform.localScale = new Vector3(width, height, 1f);
        Renderer renderer = gameObject.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return gameObject;
    }

    private static ShellVisualBinding[] CreateCameraVisuals(
        List<Renderer> physicalRenderers,
        Material transparentMaterial)
    {
        List<ShellVisualBinding> visuals = new();
        foreach (Renderer physicalRenderer in physicalRenderers)
        {
            if (physicalRenderer == null) continue;
            MeshFilter physicalMesh = physicalRenderer.GetComponent<MeshFilter>();
            if (physicalMesh == null || physicalMesh.sharedMesh == null) continue;

            GameObject cameraVisual = AcquireObject(
                "Camera Visual",
                physicalRenderer.transform,
                () => new GameObject("Camera Visual"));
            cameraVisual.transform.localPosition = Vector3.zero;
            cameraVisual.transform.localRotation = Quaternion.identity;
            cameraVisual.transform.localScale = Vector3.one;
            MeshFilter visualMesh = GetOrAddComponent<MeshFilter>(cameraVisual);
            visualMesh.sharedMesh = physicalMesh.sharedMesh;
            MeshRenderer visualRenderer = GetOrAddComponent<MeshRenderer>(cameraVisual);
            visualRenderer.sharedMaterial = physicalRenderer.sharedMaterial;
            visualRenderer.shadowCastingMode = ShadowCastingMode.Off;
            visualRenderer.receiveShadows = true;
            physicalRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            visuals.Add(new ShellVisualBinding(
                visualRenderer,
                physicalRenderer.sharedMaterial,
                transparentMaterial));
        }
        return visuals.ToArray();
    }

    private static GameObject Cylinder(
        string name,
        Transform parent,
        Vector3 position,
        float radius,
        float height,
        Material material)
    {
        GameObject gameObject = AcquireObject(
            name,
            parent,
            () => GameObject.CreatePrimitive(PrimitiveType.Cylinder));
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
        Renderer renderer = gameObject.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return gameObject;
    }

    private static Material GetMaterial(
        string name,
        Color color,
        float metallic,
        float smoothness)
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
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetEmissiveMaterial(
        string name,
        Color color,
        float emissionIntensity)
    {
        Material material = GetMaterial(name, color, 0f, 0.58f);
        Color emission = color * emissionIntensity;
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", emission);
        }
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetTransparentMaterial(string name, Color color)
    {
        Material material = GetMaterial(name, color, 0f, 0.45f);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetSkyMaterial(string name)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null)
        {
            throw new System.InvalidOperationException(
                "Unity's Skybox/Procedural shader is required for the Ground Ops sky.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_SunDisk")) material.SetFloat("_SunDisk", 2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureSceneInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(scene => scene.path != ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = $"{current}/{segments[index]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }
            current = next;
        }
    }

    private static void BeginSync(GameObject existingRoot)
    {
        syncRoot = existingRoot;
        staleObjects = new HashSet<GameObject>();
        claimedObjects = new HashSet<GameObject>();
        if (existingRoot == null) return;
        foreach (Transform transform in existingRoot.GetComponentsInChildren<Transform>(true))
        {
            if (transform != existingRoot.transform) staleObjects.Add(transform.gameObject);
        }
    }

    private static void FinishSync()
    {
        foreach (GameObject staleObject in staleObjects)
        {
            if (staleObject != null) Object.DestroyImmediate(staleObject);
        }
        syncRoot = null;
        staleObjects = null;
        claimedObjects = null;
    }

    private static GameObject AcquireObject(
        string name,
        Transform parent,
        System.Func<GameObject> create)
    {
        GameObject gameObject = null;
        if (parent == null && syncRoot != null && syncRoot.name == name && !claimedObjects.Contains(syncRoot))
        {
            gameObject = syncRoot;
        }
        else if (parent != null)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                GameObject candidate = parent.GetChild(index).gameObject;
                if (candidate.name == name && !claimedObjects.Contains(candidate))
                {
                    gameObject = candidate;
                    break;
                }
            }
        }

        if (gameObject == null) gameObject = create();
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.SetActive(true);
        claimedObjects.Add(gameObject);
        staleObjects.Remove(gameObject);
        return gameObject;
    }

    private static Transform NewGroup(string name, Transform parent)
    {
        GameObject gameObject = AcquireObject(name, parent, () => new GameObject(name));
        // A sync may promote an old primitive into a logical parent. Remove its
        // former visible/collidable shell while retaining the stable object path.
        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        bool wasPrimitive = meshRenderer != null || meshFilter != null;
        if (meshRenderer != null) Object.DestroyImmediate(meshRenderer);
        if (meshFilter != null) Object.DestroyImmediate(meshFilter);
        Collider collider = gameObject.GetComponent<Collider>();
        if (wasPrimitive && collider != null) Object.DestroyImmediate(collider);
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
        return gameObject.transform;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void RemoveColliders(Transform root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }
    }
}
