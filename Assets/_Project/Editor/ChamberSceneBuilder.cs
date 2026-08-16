using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
        if (existing != null)
        {
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
        Material lightPanel = GetMaterial("LightPanel", Color.white, 0f, 0.75f, true);

        Transform root = NewGroup(RootName, null);
        BuildArchitecture(NewGroup("Architecture", root), wall, floor);
        BuildLightingFixtures(NewGroup("Lighting Fixtures", root), stand, dark, lightPanel);
        BuildEquipment(NewGroup("Equipment", root), table, lift, housing, purple, orange, yellow, source);
        ConfigureSceneCameraAndLight();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Built chamber geometry from the Three.js reference into Main.unity.");
    }

    private static void BuildArchitecture(Transform parent, Material wall, Material floor)
    {
        // The source Three.js planes are mirrored across x = 0 as they are built.
        // Each quad faces into the room and remains invisible from its back side.
        Transform doorWall = NewGroup("Left Wall - Door", parent);
        Quad("Rear Section", doorWall, new Vector3(2.5f, 1.75f, 4f), 2f, 3.5f, Vector3.left, wall);
        Quad("Front Section", doorWall, new Vector3(2.5f, 1.75f, -1.5f), 7f, 3.5f, Vector3.left, wall);
        Quad("Above Door", doorWall, new Vector3(2.5f, 2.75f, 2.5f), 1f, 1.5f, Vector3.left, wall);
        Box("Door Frame Front Jamb", doorWall, new Vector3(2.5f, 1f, 1.75f), new Vector3(0.15f, 2f, 0.5f), wall);
        Box("Door Frame Rear Jamb", doorWall, new Vector3(2.5f, 1f, 3.25f), new Vector3(0.15f, 2f, 0.5f), wall);
        Box("Door Frame Header", doorWall, new Vector3(2.5f, 2.25f, 2.5f), new Vector3(0.15f, 0.5f, 2f), wall);

        Transform solidWall = NewGroup("Right Wall - Solid", parent);
        Quad("Wall", solidWall, new Vector3(-2.5f, 1.75f, 0f), 10f, 3.5f, Vector3.right, wall);

        Transform backWall = NewGroup("Back Wall", parent);
        Quad("Wall", backWall, new Vector3(0f, 1.75f, 5f), 5f, 3.5f, Vector3.back, wall);

        Transform frontWall = NewGroup("Front Wall", parent);
        Quad("Right Section", frontWall, new Vector3(-1.4375f, 1.75f, -5f), 2.125f, 3.5f, Vector3.forward, wall);
        Quad("Left Section", frontWall, new Vector3(1.4375f, 1.75f, -5f), 2.125f, 3.5f, Vector3.forward, wall);
        Quad("Below Source Opening", frontWall, new Vector3(0f, 1.0625f, -5f), 0.75f, 2.125f, Vector3.forward, wall);
        Quad("Above Source Opening", frontWall, new Vector3(0f, 3.1875f, -5f), 0.75f, 0.625f, Vector3.forward, wall);
        Box("Source Frame Right", frontWall, new Vector3(-0.625f, 2.5f, -5f), new Vector3(0.5f, 1.75f, 0.15f), wall);
        Box("Source Frame Left", frontWall, new Vector3(0.625f, 2.5f, -5f), new Vector3(0.5f, 1.75f, 0.15f), wall);
        Box("Source Frame Bottom", frontWall, new Vector3(0f, 1.875f, -5f), new Vector3(0.75f, 0.5f, 0.15f), wall);
        Box("Source Frame Top", frontWall, new Vector3(0f, 3.125f, -5f), new Vector3(0.75f, 0.5f, 0.15f), wall);

        Quad("Floor", parent, new Vector3(0f, 0f, 0f), 5f, 10f, Vector3.up, floor);
        Quad("Ceiling", parent, new Vector3(0f, 3.5f, 0f), 5f, 10f, Vector3.down, wall);
    }

    private static void BuildLightingFixtures(Transform parent, Material stand, Material dark, Material lightPanel)
    {
        Transform backFixtures = NewGroup("Back Wall Fixtures", parent);
        foreach (float x in new[] { -1.5f, 1.5f })
        {
            Box("Light Fixture", backFixtures, new Vector3(x, 2.5f, 4.915f), new Vector3(0.1f, 0.3f, 0.02f), lightPanel);
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
        BuildScissorForks(positioner, lift, liftHeight);
        BuildHousing(heightAssembly, housing);
        BuildTurntable(heightAssembly, purple, orange, yellow);
    }

    private static void BuildScissorForks(Transform parent, Material material, float height)
    {
        Transform forks = NewGroup("Scissor Forks", parent);
        float horizontalSpan = Mathf.Sqrt(Mathf.Max(0.05f * 0.05f, 1.2f * 1.2f - height * height));
        float halfZ = horizontalSpan / 2f;
        foreach (float sideX in new[] { -0.3f, 0.3f })
        {
            Rod("Rising Forward", forks,
                new Vector3(sideX - 0.015f, 0.6f, 3.9f - halfZ),
                new Vector3(sideX - 0.015f, 0.6f + height, 3.9f + halfZ),
                0.025f, material, true);
            Rod("Rising Backward", forks,
                new Vector3(sideX + 0.015f, 0.6f, 3.9f + halfZ),
                new Vector3(sideX + 0.015f, 0.6f + height, 3.9f - halfZ),
                0.025f, material, true);
        }
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

    private static void BuildTurntable(Transform parent, Material purple, Material orange, Material yellow)
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

        Light directional = Object.FindFirstObjectByType<Light>();
        if (directional != null)
        {
            directional.transform.rotation = MirrorRotation(Quaternion.Euler(50f, -30f, 0f));
            directional.intensity = 1.2f;
        }
    }

    private static Transform NewGroup(string name, Transform parent)
    {
        GameObject group = new(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        SetPrimitive(gameObject, name, parent, position, Quaternion.identity, size, material);
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
        bool emissive = false)
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
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 3f);
        }
        else
        {
            material.DisableKeyword("_EMISSION");
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
