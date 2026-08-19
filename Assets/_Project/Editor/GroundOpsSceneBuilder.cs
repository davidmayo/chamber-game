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
        BeginSync(existingRoot);

        Material wallMaterial = GetMaterial("GroundOpsWall", new Color(0.76f, 0.75f, 0.70f), 0f, 0.08f);
        Material trimMaterial = GetMaterial("GroundOpsWindowTrim", new Color(0.16f, 0.18f, 0.20f), 0.15f, 0.25f);
        Material glassMaterial = GetTransparentMaterial("GroundOpsWindowGlass", new Color(0.32f, 0.48f, 0.58f, 0.28f));
        Material carpetMaterial = GetMaterial("GroundOpsCarpet", new Color(0.12f, 0.14f, 0.16f), 0f, 0.02f);
        Material deskMaterial = GetMaterial("GroundOpsDesk", new Color(0.30f, 0.32f, 0.34f), 0.05f, 0.12f);
        Material rackMaterial = GetMaterial("GroundOpsDsnRack", new Color(0.25f, 0.27f, 0.28f), 0.15f, 0.18f);

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
            wallMaterial);
        BuildCurvedWindow(
            NewGroup("Curved Window", architecture),
            windowPoints,
            wallHeight,
            glassMaterial,
            trimMaterial);

        Transform furniture = NewGroup("Furniture Blockout", root);
        BuildStationDesk("Dish Station 1", furniture, new Vector3(-3.540f, 0f, -2.680f), 17.181f, deskMaterial);
        BuildStationDesk("Dish Station 2", furniture, new Vector3(-2.870f, 0f, -0.700f), 21.625f, deskMaterial);
        BuildStationDesk("Dish Station 3", furniture, new Vector3(-1.527f, 0f, -3.340f), 14.958f, deskMaterial);
        BuildStationDesk("Dish Station 4", furniture, new Vector3(-0.858f, 0f, -1.457f), 26.940f, deskMaterial);
        Box(
            "DSN Server Rack",
            NewGroup("Server Room Equipment", root),
            new Vector3(-2.160f, 1.05f, 7.30f),
            new Vector3(1.45f, 2.10f, 0.90f),
            Quaternion.identity,
            rackMaterial);

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
        Material material)
    {
        float centerY = wallHeight / 2f;

        // Straight right wall shared by the Ops Room and Server Room.
        Box("Right Wall", parent,
            new Vector3(rightWallX, centerY, (opsFrontZ + serverBackZ) / 2f),
            new Vector3(wallThickness, wallHeight, serverBackZ - opsFrontZ),
            Quaternion.identity, material);

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
            material);

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
            material);

        Box("Server Room Back Wall", parent,
            new Vector3((serverLeftX + rightWallX) / 2f, centerY, serverBackZ),
            new Vector3(rightWallX - serverLeftX, wallHeight, wallThickness),
            Quaternion.identity, material);
        Box("Server Room Left Wall", parent,
            new Vector3(serverLeftX, centerY, (partitionZ + serverBackZ) / 2f),
            new Vector3(wallThickness, wallHeight, serverBackZ - partitionZ),
            Quaternion.identity, material);
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
        Material material)
    {
        Transform wall = NewGroup(name, parent);
        float openingLeft = openingCenterX - openingWidth / 2f;
        float openingRight = openingCenterX + openingWidth / 2f;
        float leftWidth = openingLeft - minimumX;
        float rightWidth = maximumX - openingRight;
        float headerHeight = wallHeight - openingHeight;

        Box("Left Segment", wall,
            new Vector3(minimumX + leftWidth / 2f, wallHeight / 2f, z),
            new Vector3(leftWidth, wallHeight, thickness), Quaternion.identity, material);
        Box("Right Segment", wall,
            new Vector3(openingRight + rightWidth / 2f, wallHeight / 2f, z),
            new Vector3(rightWidth, wallHeight, thickness), Quaternion.identity, material);
        Box("Header", wall,
            new Vector3(openingCenterX, openingHeight + headerHeight / 2f, z),
            new Vector3(openingWidth, headerHeight, thickness), Quaternion.identity, material);
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
        Material material)
    {
        Box(name, parent,
            floorPosition + Vector3.up * 0.375f,
            new Vector3(0.90f, 0.75f, 1.80f),
            Quaternion.Euler(0f, yawDegrees, 0f),
            material);
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
