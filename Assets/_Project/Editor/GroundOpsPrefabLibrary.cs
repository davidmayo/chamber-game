using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the reusable authored building blocks used by the Ground Ops
/// generator. Assets are created only when missing: once present, the prefab
/// itself is the editable source of truth and ordinary scene syncs only place
/// instances of it.
/// </summary>
public static class GroundOpsPrefabLibrary
{
    public const string RootFolder = "Assets/_Project/Prefabs";

    public sealed class PrefabSet
    {
        public GameObject Monitor27;
        public GameObject Keyboard;
        public GameObject Mouse;
        public GameObject ChairGray;
        public GameObject ChairBlack;
        public GameObject DocDesk;
        public GameObject UtilityDesk;
        public GameObject DocDishStation;
        public GameObject GeneralWorkstation;
        public GameObject ServerRack;
        public GameObject HallwayLight;
        public GameObject SuspendedUplight;
        public GameObject RecessedCanLight;
        public GameObject HighBayLight;
        public GameObject CleanroomLight;
        public GameObject WindowPane;
        public GameObject InstitutionalDoor;
    }

    public static PrefabSet Ensure(
        Material deskMaterial,
        Material deskBaseMaterial,
        Material grayChairMaterial,
        Material blackChairMaterial,
        Material monitorMaterial,
        Material monitorScreenMaterial,
        Material rackMaterial,
        Material hallwayHousingMaterial,
        Material warmLightMaterial,
        Material highBayHousingMaterial,
        Material highBayLightMaterial,
        Material cleanroomLightMaterial,
        Material glassMaterial,
        Material woodDoorMaterial,
        Material trimMaterial)
    {
        EnsureFolder(RootFolder);
        EnsureFolder($"{RootFolder}/Components");
        EnsureFolder($"{RootFolder}/Furniture");
        EnsureFolder($"{RootFolder}/Stations");
        EnsureFolder($"{RootFolder}/Fixtures");

        PrefabSet assets = new();
        assets.Monitor27 = EnsurePrefab(
            $"{RootFolder}/Components/Monitor 27-inch.prefab",
            "Monitor 27-inch",
            root => BuildMonitor27(root, monitorMaterial, monitorScreenMaterial));
        assets.Keyboard = EnsurePrefab(
            $"{RootFolder}/Components/Keyboard.prefab",
            "Keyboard",
            root => Primitive(root, "Body", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.46f, 0.044f, 0.18f), Quaternion.identity, monitorMaterial));
        assets.Mouse = EnsurePrefab(
            $"{RootFolder}/Components/Mouse.prefab",
            "Mouse",
            root => Primitive(root, "Body", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.10f, 0.052f, 0.16f), Quaternion.identity, monitorMaterial));
        assets.ChairGray = EnsurePrefab(
            $"{RootFolder}/Components/Chair.prefab",
            "Chair",
            root => BuildChair(root, grayChairMaterial, deskBaseMaterial));
        assets.ChairBlack = EnsureVariant(
            $"{RootFolder}/Components/Chair Black.prefab",
            assets.ChairGray,
            "Chair Black",
            instance =>
            {
                instance.transform.Find("Seat").GetComponent<Renderer>().sharedMaterial = blackChairMaterial;
                instance.transform.Find("Back").GetComponent<Renderer>().sharedMaterial = blackChairMaterial;
            });

        assets.DocDesk = EnsurePrefab(
            $"{RootFolder}/Furniture/DOC Desk.prefab",
            "DOC Desk",
            root => BuildDocDesk(root, deskMaterial, deskBaseMaterial));
        assets.UtilityDesk = EnsurePrefab(
            $"{RootFolder}/Furniture/Utility Desk 2x4.prefab",
            "Utility Desk 2x4",
            root => BuildUtilityDesk(root, deskMaterial, deskBaseMaterial));
        assets.ServerRack = EnsurePrefab(
            $"{RootFolder}/Furniture/Server Rack.prefab",
            "Server Rack",
            root => BuildServerRack(root, rackMaterial, deskBaseMaterial));

        assets.DocDishStation = EnsurePrefab(
            $"{RootFolder}/Stations/DOC Dish Station.prefab",
            "DOC Dish Station",
            root => BuildDocStation(root, assets));
        assets.GeneralWorkstation = EnsurePrefab(
            $"{RootFolder}/Stations/General Workstation.prefab",
            "General Workstation",
            root => BuildGeneralStation(root, assets));

        assets.HallwayLight = EnsurePrefab(
            $"{RootFolder}/Fixtures/Hallway Light.prefab",
            "Hallway Light",
            root => BuildHallwayLight(root, hallwayHousingMaterial, warmLightMaterial));
        assets.SuspendedUplight = EnsurePrefab(
            $"{RootFolder}/Fixtures/Suspended Uplight.prefab",
            "Suspended Uplight",
            root => BuildSuspendedUplight(root, hallwayHousingMaterial, warmLightMaterial));
        assets.RecessedCanLight = EnsurePrefab(
            $"{RootFolder}/Fixtures/Recessed Can Light.prefab",
            "Recessed Can Light",
            root => BuildRecessedCan(root, warmLightMaterial));
        assets.HighBayLight = EnsurePrefab(
            $"{RootFolder}/Fixtures/High Bay Light.prefab",
            "High Bay Light",
            root => BuildHighBayLight(root, highBayHousingMaterial, highBayLightMaterial));
        assets.CleanroomLight = EnsurePrefab(
            $"{RootFolder}/Fixtures/Cleanroom Light.prefab",
            "Cleanroom Light",
            root => BuildCleanroomLight(root, cleanroomLightMaterial));
        assets.WindowPane = EnsurePrefab(
            $"{RootFolder}/Fixtures/Plate Glass Pane.prefab",
            "Plate Glass Pane",
            root => Primitive(root, "Glass", PrimitiveType.Cube, Vector3.zero,
                new Vector3(1f, 1f, 0.045f), Quaternion.identity, glassMaterial));
        assets.InstitutionalDoor = EnsurePrefab(
            $"{RootFolder}/Fixtures/Institutional Door.prefab",
            "Institutional Door",
            root => BuildDoor(root, woodDoorMaterial, trimMaterial));

        AssetDatabase.SaveAssets();
        return assets;
    }

    private static GameObject EnsurePrefab(
        string path,
        string rootName,
        Action<Transform> build)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject root = new(rootName);
        try
        {
            build(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static GameObject EnsureVariant(
        string path,
        GameObject basePrefab,
        string rootName,
        Action<GameObject> configure)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        try
        {
            instance.name = rootName;
            configure(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void BuildMonitor27(
        Transform root,
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
        float bodyY = baseHeight + standHeight + bodyHeight / 2f;

        Cylinder(root, "Base", new Vector3(0f, baseHeight / 2f, 0f),
            0.12f, baseHeight, bodyMaterial);
        Cylinder(root, "Stand", new Vector3(0f, baseHeight + standHeight / 2f, 0f),
            0.02f, standHeight, bodyMaterial);
        Primitive(root, "Body", PrimitiveType.Cube, new Vector3(0f, bodyY, 0f),
            new Vector3(bodyWidth, bodyHeight, bodyDepth), Quaternion.identity, bodyMaterial);
        Primitive(root, "Screen", PrimitiveType.Cube,
            new Vector3(0f, bodyY, bodyDepth / 2f + 0.003f),
            new Vector3(screenWidth, screenHeight, 0.006f), Quaternion.identity, screenMaterial);
    }

    private static void BuildChair(Transform root, Material upholstery, Material baseMaterial)
    {
        Cylinder(root, "Floor Base", new Vector3(0f, 0.025f, 0f), 0.24f, 0.05f, baseMaterial);
        Cylinder(root, "Pedestal", new Vector3(0f, 0.27f, 0f), 0.035f, 0.46f, baseMaterial);
        Primitive(root, "Seat", PrimitiveType.Cube, new Vector3(0f, 0.53f, 0f),
            new Vector3(0.48f, 0.11f, 0.48f), Quaternion.identity, upholstery);
        Primitive(root, "Back", PrimitiveType.Cube, new Vector3(0f, 0.80f, 0.21f),
            new Vector3(0.50f, 0.58f, 0.10f), Quaternion.identity, upholstery);
        RemoveColliders(root);
    }

    private static void BuildDocDesk(Transform root, Material topMaterial, Material baseMaterial)
    {
        const float width = 0.95f;
        const float length = 1.90f;
        const float height = 0.76f;
        const float top = 0.08f;
        Primitive(root, "Worktop", PrimitiveType.Cube, new Vector3(0f, height - top / 2f, 0f),
            new Vector3(width, top, length), Quaternion.identity, topMaterial);
        foreach ((string name, float z) in new[] { ("Forward Pedestal", 0.66f), ("Rear Pedestal", -0.66f) })
        {
            Primitive(root, name, PrimitiveType.Cube, new Vector3(-0.04f, 0.34f, z),
                new Vector3(0.72f, 0.68f, 0.42f), Quaternion.identity, baseMaterial);
        }
        Primitive(root, "Window-side Modesty Panel", PrimitiveType.Cube,
            new Vector3(-width / 2f + 0.045f, 0.39f, 0f),
            new Vector3(0.09f, 0.62f, 1.56f), Quaternion.identity, baseMaterial);
        Primitive(root, "Rear Console Rail", PrimitiveType.Cube,
            new Vector3(-width / 2f + 0.10f, height + 0.055f, 0f),
            new Vector3(0.20f, 0.11f, 1.68f), Quaternion.identity, topMaterial);
    }

    private static void BuildUtilityDesk(Transform root, Material topMaterial, Material legMaterial)
    {
        const float width = 4f * 0.3048f;
        const float depth = 2f * 0.3048f;
        const float height = 0.76f;
        const float top = 0.07f;
        Primitive(root, "Tabletop", PrimitiveType.Cube, new Vector3(0f, height - top / 2f, 0f),
            new Vector3(width, top, depth), Quaternion.identity, topMaterial);
        float legY = (height - top) / 2f;
        Vector2[] legPositions =
        {
            new(-width / 2f + 0.10f, -depth / 2f + 0.10f),
            new(-width / 2f + 0.10f, depth / 2f - 0.10f),
            new(width / 2f - 0.10f, -depth / 2f + 0.10f),
            new(width / 2f - 0.10f, depth / 2f - 0.10f),
        };
        for (int index = 0; index < legPositions.Length; index++)
        {
            Vector2 position = legPositions[index];
            Primitive(root, $"Leg {index + 1}", PrimitiveType.Cube,
                new Vector3(position.x, legY, position.y),
                new Vector3(0.065f, height - top, 0.065f),
                Quaternion.identity, legMaterial);
        }
    }

    private static void BuildDocStation(Transform root, PrefabSet assets)
    {
        Nested(root, assets.DocDesk, "DOC Desk", Vector3.zero, Quaternion.identity);
        Nested(root, assets.Monitor27, "Left 27-inch Monitor",
            new Vector3(-0.11f, 0.76f, -0.35f), Quaternion.Euler(0f, 75f, 0f));
        Nested(root, assets.Monitor27, "Right 27-inch Monitor",
            new Vector3(-0.11f, 0.76f, 0.35f), Quaternion.Euler(0f, 105f, 0f));
        Nested(root, assets.Keyboard, "Keyboard",
            new Vector3(0.20f, 0.782f, -0.048f), Quaternion.Euler(0f, 85.122f, 0f));
        Nested(root, assets.Mouse, "Mouse",
            new Vector3(0.22f, 0.786f, 0.36f), Quaternion.Euler(0f, 103.302f, 0f));
        Nested(root, assets.ChairGray, "Chair", new Vector3(0.86f, 0f, 0f),
            Quaternion.Euler(0f, 90f, 0f));
    }

    private static void BuildGeneralStation(Transform root, PrefabSet assets)
    {
        Nested(root, assets.UtilityDesk, "2-by-4-foot Desk", Vector3.zero, Quaternion.identity);
        Nested(root, assets.Monitor27, "Left 27-inch Monitor",
            new Vector3(-0.34f, 0.76f, 0.08f), Quaternion.Euler(0f, 165f, 0f));
        Nested(root, assets.Monitor27, "Right 27-inch Monitor",
            new Vector3(0.34f, 0.76f, 0.08f), Quaternion.Euler(0f, 195f, 0f));
        Nested(root, assets.Keyboard, "Keyboard",
            new Vector3(-0.048f, 0.782f, -0.184f), Quaternion.Euler(0f, -4.878f, 0f));
        Nested(root, assets.Mouse, "Mouse",
            new Vector3(0.34f, 0.786f, -0.20f), Quaternion.Euler(0f, 13.302f, 0f));
        Nested(root, assets.ChairBlack, "Chair", new Vector3(0f, 0f, -0.76f),
            Quaternion.Euler(0f, 180f, 0f));
    }

    private static void BuildServerRack(Transform root, Material rackMaterial, Material faceMaterial)
    {
        Primitive(root, "Cabinet", PrimitiveType.Cube, new Vector3(0f, 1.10f, 0f),
            new Vector3(0.70f, 2.20f, 0.90f), Quaternion.identity, rackMaterial);
        Primitive(root, "Front Face", PrimitiveType.Cube, new Vector3(0f, 1.10f, -0.458f),
            new Vector3(0.62f, 2.08f, 0.018f), Quaternion.identity, faceMaterial);
    }

    private static void BuildHallwayLight(Transform root, Material housing, Material luminous)
    {
        Primitive(root, "Housing", PrimitiveType.Cube, Vector3.zero,
            new Vector3(1.20f, 0.10f, 0.24f), Quaternion.identity, housing);
        Primitive(root, "Diffuser", PrimitiveType.Cube, Vector3.down * 0.061f,
            new Vector3(1.08f, 0.025f, 0.18f), Quaternion.identity, luminous);
        AddLight(root, "Diffuse Light", Vector3.down * 0.14f, LightType.Point,
            new Color(1f, 0.98f, 0.94f), 1.15f, 5.6f, 0f, false, Vector3.down);
    }

    private static void BuildSuspendedUplight(Transform root, Material housing, Material luminous)
    {
        Primitive(root, "Housing", PrimitiveType.Cube, Vector3.zero,
            new Vector3(4f, 0.16f, 0.28f), Quaternion.identity, housing);
        Primitive(root, "Upward Luminous Strip", PrimitiveType.Cube, Vector3.up * 0.095f,
            new Vector3(3.6f, 0.035f, 0.19f), Quaternion.identity, luminous);
        Cylinder(root, "Hanger -1", new Vector3(-1.36f, 0.29f, 0f), 0.018f, 0.58f, housing);
        Cylinder(root, "Hanger 1", new Vector3(1.36f, 0.29f, 0f), 0.018f, 0.58f, housing);
        AddLight(root, "Upward Light", Vector3.up * 0.12f, LightType.Spot,
            new Color(1f, 0.86f, 0.67f), 0.16f, 2.8f, 160f, false, Vector3.up);
        AddLight(root, "Reflected Fill", Vector3.up * 0.08f, LightType.Point,
            new Color(1f, 0.86f, 0.67f), 0.70f, 7.2f, 0f, false, Vector3.down);
    }

    private static void BuildRecessedCan(Transform root, Material luminous)
    {
        Cylinder(root, "Lens", Vector3.zero, 0.115f, 0.045f, luminous);
        AddLight(root, "Downlight", Vector3.down * 0.08f, LightType.Spot,
            new Color(1f, 0.86f, 0.67f), 5.8f, 9.5f, 72f, false, Vector3.down);
    }

    private static void BuildHighBayLight(Transform root, Material housing, Material luminous)
    {
        Primitive(root, "Housing", PrimitiveType.Cube, Vector3.zero,
            new Vector3(0.34f, 0.18f, 3.4f), Quaternion.identity, housing);
        Primitive(root, "Luminous Panel", PrimitiveType.Cube, Vector3.down * 0.105f,
            new Vector3(0.22f, 0.035f, 3.16f), Quaternion.identity, luminous);
        AddLight(root, "Diffuse Light", Vector3.down * 0.24f, LightType.Point,
            new Color(0.82f, 0.91f, 1f), 70f, 20f, 0f, false, Vector3.down);
    }

    private static void BuildCleanroomLight(Transform root, Material luminous)
    {
        Primitive(root, "Luminous Panel", PrimitiveType.Cube, Vector3.zero,
            new Vector3(2.8f, 0.035f, 1.25f), Quaternion.identity, luminous);
        AddLight(root, "Brutal White Fill", Vector3.down * 0.255f, LightType.Point,
            new Color(0.94f, 0.98f, 1f), 150f, 10f, 0f, false, Vector3.down);
    }

    private static void BuildDoor(Transform root, Material wood, Material hardware)
    {
        Primitive(root, "Leaf", PrimitiveType.Cube, new Vector3(0f, 1.075f, 0f),
            new Vector3(1.05f, 2.15f, 0.055f), Quaternion.identity, wood);
        Primitive(root, "Handle", PrimitiveType.Cube, new Vector3(0.34f, 1.02f, -0.04f),
            new Vector3(0.05f, 0.12f, 0.05f), Quaternion.identity, hardware);
    }

    private static GameObject Nested(
        Transform parent,
        GameObject prefab,
        string name,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static GameObject Primitive(
        Transform parent,
        string name,
        PrimitiveType type,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = localRotation;
        primitive.transform.localScale = localScale;
        Renderer renderer = primitive.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.receiveShadows = true;
        return primitive;
    }

    private static GameObject Cylinder(
        Transform parent,
        string name,
        Vector3 localPosition,
        float radius,
        float height,
        Material material)
    {
        return Primitive(parent, name, PrimitiveType.Cylinder, localPosition,
            new Vector3(radius * 2f, height / 2f, radius * 2f), Quaternion.identity, material);
    }

    private static Light AddLight(
        Transform parent,
        string name,
        Vector3 localPosition,
        LightType type,
        Color color,
        float intensity,
        float range,
        float spotAngle,
        bool shadows,
        Vector3 direction)
    {
        GameObject objectWithLight = new(name, typeof(Light));
        objectWithLight.transform.SetParent(parent, false);
        objectWithLight.transform.localPosition = localPosition;
        if (type == LightType.Spot || type == LightType.Directional)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
            objectWithLight.transform.localRotation = Quaternion.LookRotation(direction, up);
        }
        Light light = objectWithLight.GetComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        if (type == LightType.Spot)
        {
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.62f;
        }
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return light;
    }

    private static void RemoveColliders(Transform root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            UnityEngine.Object.DestroyImmediate(collider);
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
}
