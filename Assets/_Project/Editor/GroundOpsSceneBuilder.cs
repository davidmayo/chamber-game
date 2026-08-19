using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
        const float wallHeight = 3.0f;
        const float wallThickness = 0.12f;
        const float doorWidth = 1.05f;
        const float doorHeight = 2.15f;

        GameObject existingRoot = scene.GetRootGameObjects()
            .FirstOrDefault(candidate => candidate.name == RootName);
        float preserveWallOpacity = 100f;
        if (existingRoot != null)
        {
            ChamberShellVisibilityController existingVisibility =
                existingRoot.GetComponent<ChamberShellVisibilityController>();
            if (existingVisibility != null)
            {
                preserveWallOpacity = existingVisibility.RoomOpacityPercent;
            }
        }
        BeginSync(existingRoot);

        Material wallMaterial = GetMaterial("GroundOpsWall", new Color(0.76f, 0.75f, 0.70f), 0f, 0.08f);
        Material transparentWallMaterial = GetTransparentMaterial(
            "GroundOpsWallTransparent", new Color(0.76f, 0.75f, 0.70f, 0.50f));
        Material trimMaterial = GetMaterial("GroundOpsWindowTrim", new Color(0.16f, 0.18f, 0.20f), 0.15f, 0.25f);
        Material glassMaterial = GetTransparentMaterial("GroundOpsWindowGlass", new Color(0.32f, 0.48f, 0.58f, 0.28f));
        Material carpetMaterial = GetMaterial("GroundOpsCarpet", new Color(0.12f, 0.14f, 0.16f), 0f, 0.02f);
        Material deskMaterial = GetMaterial("GroundOpsDesk", new Color(0.30f, 0.32f, 0.34f), 0.05f, 0.12f);
        Material deskBaseMaterial = GetMaterial("GroundOpsDeskBase", new Color(0.12f, 0.13f, 0.14f), 0.08f, 0.10f);
        Material chairMaterial = GetMaterial("GroundOpsChair", new Color(0.42f, 0.41f, 0.38f), 0f, 0.16f);
        Material blackChairMaterial = GetMaterial("GroundOpsBlackChair", new Color(0.035f, 0.040f, 0.045f), 0f, 0.12f);
        Material monitorMaterial = GetMaterial("GroundOpsMonitor", new Color(0.055f, 0.060f, 0.065f), 0.02f, 0.20f);
        Material monitorScreenMaterial = GetMaterial("GroundOpsMonitorScreen", new Color(0.010f, 0.016f, 0.022f), 0f, 0.38f);
        Material rackMaterial = GetMaterial("GroundOpsDsnRack", new Color(0.25f, 0.27f, 0.28f), 0.15f, 0.18f);
        Material kvmScreenMaterial = GetMaterial("GroundOpsKvmScreen", new Color(0.015f, 0.025f, 0.030f), 0f, 0.35f);

        Transform root = NewGroup(RootName, null);
        Transform architecture = NewGroup("Architecture", root);

        Vector3[] windowPoints = BuildWindowPoints(
            new Vector3(serverLeftX, 0f, partitionZ),
            new Vector3(-5.1f, 0f, opsFrontZ),
            new Vector3(-7.0f, 0f, -0.2f),
            8);

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
        BuildCurvedWindow(
            NewGroup("Curved Window", architecture),
            windowPoints,
            wallHeight,
            glassMaterial,
            trimMaterial);

        ShellVisualBinding[] wallVisuals = CreateCameraVisuals(
            wallPhysicalRenderers, transparentWallMaterial);
        ChamberShellVisibilityController wallVisibility =
            GetOrAddComponent<ChamberShellVisibilityController>(root.gameObject);
        wallVisibility.Configure(
            wallPhysicalRenderers.ToArray(),
            wallVisuals,
            wallCutawayRenderers.ToArray(),
            System.Array.Empty<Renderer>(),
            System.Array.Empty<ShellVisualBinding>(),
            System.Array.Empty<Renderer>(),
            preserveWallOpacity,
            100f);

        Transform furniture = NewGroup("Furniture Blockout", root);
        BuildStationDesk("Dish Station 1", furniture, new Vector3(-3.540f, 0f, -2.680f), 17.181f,
            deskMaterial, deskBaseMaterial, chairMaterial, monitorMaterial, monitorScreenMaterial);
        BuildStationDesk("Dish Station 2", furniture, new Vector3(-2.870f, 0f, -0.700f), 21.625f,
            deskMaterial, deskBaseMaterial, chairMaterial, monitorMaterial, monitorScreenMaterial);
        BuildStationDesk("Dish Station 3", furniture, new Vector3(-1.527f, 0f, -3.340f), 14.958f,
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
        BuildDsnRackPair(
            serverRoomEquipment,
            new Vector3(-2.160f, 0f, 7.30f),
            rackMaterial,
            deskBaseMaterial,
            kvmScreenMaterial);
        BuildServerRackRow(
            serverRoomEquipment,
            rackMaterial,
            deskBaseMaterial);

        BuildCameraAndLight(NewGroup("Scene Setup", root));
        FinishSync();

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.48f, 0.48f, 0.48f);
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

    private static void BuildStationDesk(
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
            new Vector3(-0.11f, 0f, -0.35f), 90f, deskHeight,
            monitorMaterial, screenMaterial);
        BuildDesktopMonitor("Right 27-inch Monitor", station,
            new Vector3(-0.11f, 0f, 0.35f), 90f, deskHeight,
            monitorMaterial, screenMaterial);
        Box("Keyboard", station,
            new Vector3(0.20f, deskHeight + 0.022f, 0f),
            new Vector3(0.18f, 0.044f, 0.46f), Quaternion.identity, monitorMaterial);
        Box("Mouse", station,
            new Vector3(0.22f, deskHeight + 0.026f, -0.36f),
            new Vector3(0.16f, 0.052f, 0.10f), Quaternion.identity, monitorMaterial);

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
    }

    private static void BuildNonDishStations(
        Transform parent,
        Material topMaterial,
        Material baseMaterial,
        Material chairMaterial,
        Material monitorMaterial,
        Material screenMaterial)
    {
        // The reference plan shows two three-operator rows on the right side,
        // connected at their left ends by a short table with one more station.
        const float rowCenterX = 3.15f;
        const float rowLength = 4.30f;
        const float rowDepth = 0.78f;
        const float rearRowZ = 1.55f;
        const float frontRowZ = -0.45f;
        const float tableHeight = 0.76f;

        Transform nonDish = NewGroup("Non-Dish Stations", parent);
        BuildSimpleTable("Rear Station Row", nonDish,
            new Vector3(rowCenterX, 0f, rearRowZ),
            new Vector2(rowLength, rowDepth), topMaterial, baseMaterial);
        BuildSimpleTable("Front Station Row", nonDish,
            new Vector3(rowCenterX, 0f, frontRowZ),
            new Vector2(rowLength, rowDepth), topMaterial, baseMaterial);

        float[] stationX = { 1.75f, 3.15f, 4.55f };
        int stationNumber = 1;
        foreach (float rowZ in new[] { rearRowZ, frontRowZ })
        {
            foreach (float x in stationX)
            {
                BuildNonDishStation($"Non-Dish Station {stationNumber++}", nonDish,
                    new Vector3(x, 0f, rowZ), tableHeight,
                    chairMaterial, baseMaterial, monitorMaterial, screenMaterial);
            }
        }

        // Wide enough for the seventh station's two displays while still
        // reading as the crosspiece that joins the two long rows.
        const float joinX = 1.13f;
        const float joinCenterZ = 0.55f;
        BuildSimpleTable("Joining Table", nonDish,
            new Vector3(joinX, 0f, joinCenterZ),
            new Vector2(1.35f, 1.22f), topMaterial, baseMaterial);
        BuildNonDishStation("Non-Dish Station 7", nonDish,
            new Vector3(joinX, 0f, joinCenterZ + 0.12f), tableHeight,
            chairMaterial, baseMaterial, monitorMaterial, screenMaterial,
            -0.70f);
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
        float tabletopY,
        Material chairMaterial,
        Material chairBaseMaterial,
        Material monitorMaterial,
        Material screenMaterial,
        float chairOffsetZ = -0.76f)
    {
        Transform station = NewGroup(name, parent);
        station.localPosition = floorPosition;
        BuildDesktopMonitor("Left 27-inch Monitor", station,
            new Vector3(-0.34f, 0f, 0.08f), 180f, tabletopY,
            monitorMaterial, screenMaterial);
        BuildDesktopMonitor("Right 27-inch Monitor", station,
            new Vector3(0.34f, 0f, 0.08f), 180f, tabletopY,
            monitorMaterial, screenMaterial);
        Box("Keyboard", station,
            new Vector3(0f, tabletopY + 0.022f, -0.18f),
            new Vector3(0.46f, 0.044f, 0.18f), Quaternion.identity, monitorMaterial);
        Box("Mouse", station,
            new Vector3(0.34f, tabletopY + 0.026f, -0.20f),
            new Vector3(0.10f, 0.052f, 0.16f), Quaternion.identity, monitorMaterial);
        BuildSimpleChair("Chair", station, new Vector3(0f, 0f, chairOffsetZ),
            chairMaterial, chairBaseMaterial, Vector3.back);
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

    private static void BuildDsnRackPair(
        Transform parent,
        Vector3 floorPosition,
        Material rackMaterial,
        Material trimMaterial,
        Material screenMaterial)
    {
        const float rackWidth = 0.62f;
        const float rackHeight = 2.10f;
        const float rackDepth = 0.90f;
        const float rackGap = 0.04f;
        float rackOffset = (rackWidth + rackGap) / 2f;

        Transform pair = NewGroup("DSN Server Rack", parent);
        pair.localPosition = floorPosition;

        Transform leftRack = NewGroup("Left 19-inch Rack", pair);
        leftRack.localPosition = new Vector3(-rackOffset, 0f, 0f);
        Box("Plain Cabinet", leftRack,
            new Vector3(0f, rackHeight / 2f, 0f),
            new Vector3(rackWidth, rackHeight, rackDepth),
            Quaternion.identity,
            rackMaterial);

        Transform rightRack = NewGroup("Right 19-inch Rack", pair);
        rightRack.localPosition = new Vector3(rackOffset, 0f, 0f);
        Box("Cabinet", rightRack,
            new Vector3(0f, rackHeight / 2f, 0f),
            new Vector3(rackWidth, rackHeight, rackDepth),
            Quaternion.identity,
            rackMaterial);

        // The rack faces world -Z. This simplified KVM follows the reference:
        // inset monitor above a pull-out keyboard shelf on the right cabinet.
        const float frontZ = -rackDepth / 2f;
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
    }

    private static void BuildCameraAndLight(Transform parent)
    {
        GameObject cameraObject = AcquireObject("Main Camera", parent, () => new GameObject("Main Camera"));
        cameraObject.tag = "MainCamera";
        Camera camera = GetOrAddComponent<Camera>(cameraObject);
        cameraObject.transform.position = new Vector3(10.5f, 9.0f, -12.0f);
        cameraObject.transform.rotation = Quaternion.LookRotation(
            new Vector3(-0.5f, 1.0f, 1.2f) - cameraObject.transform.position,
            Vector3.up);
        camera.fieldOfView = 58f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 80f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.065f, 0.075f);

        GameObject lightObject = AcquireObject("Directional Light", parent, () => new GameObject("Directional Light"));
        Light light = GetOrAddComponent<Light>(lightObject);
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.color = new Color(1f, 0.96f, 0.90f);
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
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

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
    {
        GameObject gameObject = AcquireObject(name, parent, () => new GameObject(name));
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
        MeshFilter filter = GetOrAddComponent<MeshFilter>(gameObject);
        filter.sharedMesh = mesh;
        MeshRenderer renderer = GetOrAddComponent<MeshRenderer>(gameObject);
        renderer.sharedMaterial = material;
        MeshCollider collider = GetOrAddComponent<MeshCollider>(gameObject);
        collider.sharedMesh = mesh;
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

    private static Material GetTransparentMaterial(string name, Color color)
    {
        Material material = GetMaterial(name, color, 0f, 0.45f);
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
        if (meshRenderer != null) Object.DestroyImmediate(meshRenderer);
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter != null) Object.DestroyImmediate(meshFilter);
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
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
}
