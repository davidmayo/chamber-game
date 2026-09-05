using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class GroundOpsSceneBuilder
{
    private static void BuildSignalArchive(Transform operations, FirstPersonPlayerController player)
    {
        Transform root = NewGroup("Level 01 - Signal Archive", operations);
        const float y = SignalArchiveLayout.FloorY;
        const float ceiling = SignalArchiveLayout.CeilingY;
        float height = ceiling - y;
        Material wall = GetMaterial("Archive Midnight Concrete", new Color(0.075f, 0.105f, 0.14f), 0.12f, 0.35f);
        Material floor = GetMaterial("Archive Basalt Floor", new Color(0.045f, 0.065f, 0.085f), 0.20f, 0.55f);
        Material dark = GetMaterial("Archive Graphite", new Color(0.024f, 0.032f, 0.044f), 0.45f, 0.48f);
        Material brass = GetMaterial("Archive Aged Brass", new Color(0.48f, 0.27f, 0.105f), 0.72f, 0.65f);
        Material amber = GetEmissiveMaterial("Archive Amber Guide", new Color(1f, 0.48f, 0.12f), 3f);
        Material blue = GetEmissiveMaterial("Archive Ice Guide", new Color(0.18f, 0.75f, 1f), 3f);
        Material filaments = ArchiveMaterial("Archive Filaments", "Chamber/Signal Archive Glow");
        filaments.SetColor("_BaseColor", Color.white);
        filaments.SetFloat("_Intensity", 2.2f);
        filaments.SetFloat("_Radial", 0f);
        Material stars = ArchiveMaterial("Archive Stars", "Chamber/Signal Archive Glow");
        stars.SetColor("_BaseColor", Color.white);
        stars.SetFloat("_Intensity", 3f);
        stars.SetFloat("_Radial", 1f);
        Material mirror = ArchiveMaterial("Archive Polished Inset", "Chamber/Signal Archive Mirror");
        mirror.SetColor("_BaseColor", new Color(0.008f, 0.014f, 0.023f));
        mirror.SetFloat("_ReflectionStrength", 0.65f);

        Transform room = NewGroup("Room Shell", root);
        NullBox("Archive Floor", room, new Vector3(0.775f, y - 0.08f, -0.475f), new Vector3(9.45f, 0.16f, 9.65f), floor);
        NullBox("Archive Ceiling", room, new Vector3(0.775f, ceiling + 0.08f, -0.475f), new Vector3(9.6f, 0.16f, 9.8f), dark);
        NullBox("Archive West Wall", room, new Vector3(-4.025f, y + height / 2f, -0.475f), new Vector3(0.15f, height, 9.8f), wall);
        NullBox("Archive East Wall", room, new Vector3(5.575f, y + height / 2f, -0.475f), new Vector3(0.15f, height, 9.8f), wall);
        NullBox("Archive South Wall", room, new Vector3(0.775f, y + height / 2f, -5.375f), new Vector3(9.6f, height, 0.15f), wall);
        NullBox("Archive North Wall", room, new Vector3(-0.2f, y + height / 2f, 4.35f), new Vector3(7.5f, height, 0.15f), wall);
        NullBox("Archive Entrance Header", room, new Vector3(4.525f, y + 2.6f + (height - 2.6f) / 2f, 4.35f),
            new Vector3(1.95f, height - 2.6f, 0.15f), wall);

        Transform passage = NewGroup("Afterglow Passage", root);
        NullBox("Archive Passage Floor", passage, new Vector3(4.525f, y - 0.08f, 8.175f), new Vector3(1.95f, 0.16f, 7.65f), floor);
        NullBox("Archive Passage Ceiling", passage, new Vector3(4.525f, -3.57f, 8.175f), new Vector3(1.95f, 0.16f, 7.65f), wall);
        foreach (float x in new[] { 3.55f, 5.575f })
            NullBox($"Passage Wall {x}", passage, new Vector3(x, y + 1.725f, 8.175f), new Vector3(0.15f, 3.45f, 7.65f), wall);
        for (int i = 0; i < 4; i++)
        {
            float z = 5.2f + i * 1.8f;
            NullBox($"Passage Guide {i}", passage, new Vector3(5.48f, y + 0.22f, z), new Vector3(0.03f, 0.045f, 1.3f), amber);
            NullLamp($"Passage Lamp {i}", passage, new Vector3(4.6f, -3.85f, z), Vector3.down,
                new Color(1f, 0.58f, 0.24f), 14f, 4.5f, amber, false);
        }
        NullSign("Gallery Invitation", passage, new Vector3(3.66f, y + 1.65f, 10.7f), Vector3.right,
            "A F T E R G L O W\nSIGNAL ARCHIVE / CONTINUE >", 1.8f, 0.55f, dark);
        NullSign("Archive Threshold", passage, new Vector3(4.525f, y + 2.85f, 4.45f), Vector3.forward,
            "A F T E R G L O W\n01 / SIGNAL ARCHIVE", 1.65f, 0.43f, dark);
        NullSign("Archive Exit", room, new Vector3(4.5f, y + 2.87f, 4.25f), Vector3.back,
            "GALLERY / STAIR 01\nCHAMBER + OPERATIONS", 1.7f, 0.42f, dark);

        Transform architecture = NewGroup("Acoustic Ribs and Light", root);
        for (int i = 0; i < 7; i++)
        {
            float z = -4.8f + i * 1.4f;
            foreach (float x in new[] { -3.8f, 5.34f })
            {
                NullBox($"Pilaster {i} {x}", architecture, new Vector3(x, y + 2.6f, z), new Vector3(0.16f, 5.2f, 0.12f), brass);
                Vector3 shoulder = new(x, y + 5.2f, z);
                Vector3 crown = new(x < 0 ? -2.6f : 4.14f, ceiling - 0.19f, z);
                NullRail($"Canted Rib {i} {x}", architecture, shoulder, crown, brass);
                NullBox($"Wall Light {i} {x}", architecture, new Vector3(x + (x < 0 ? 0.095f : -0.095f), y + 3.8f, z),
                    new Vector3(0.025f, 2.1f, 0.042f), blue);
            }
        }
        // A little warm light preserves navigable edges before any receiver is on.
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new(i < 2 ? -3.4f : 4.9f, y + 2.7f, i % 2 == 0 ? -4.65f : 3.7f);
            NullLamp($"Perimeter Safety Light {i}", architecture, p, Vector3.down,
                new Color(1f, 0.54f, 0.24f), 27f, 6f, amber, false);
        }
        Light[] accents = new Light[4];
        for (int i = 0; i < accents.Length; i++)
        {
            float angle = i * Mathf.PI / 2f + Mathf.PI / 4f;
            Vector3 p = new(Mathf.Cos(angle) * 3.2f + 0.3f, ceiling - 0.5f, Mathf.Sin(angle) * 3.2f - 0.4f);
            accents[i] = NullLamp($"Archive Wash {i}", architecture, p, new Vector3(-p.x * 0.1f, -1f, 0f),
                new Color(0.24f, 0.82f, 1f), 110f, 10f, blue, i == 0);
        }
        NullSign("Archive Identity", architecture, new Vector3(0.5f, y + 4.25f, -5.27f), Vector3.forward,
            "A F T E R G L O W\nS I G N A L   A R C H I V E", 5.2f, 0.75f, dark);
        NullSign("Archive Curator Note", architecture, new Vector3(-3.68f, y + 1.8f, 0.6f), Vector3.right,
            "WHAT REMAINS\nAFTER THE SIGNAL?\n\nTHREE IMAGINED RECORDINGS\nTRANSLATED INTO LIGHT", 1.8f, 1.05f, dark);
        BuildArchiveCeilingStars(architecture, stars);

        Transform pool = NewGroup("Reflection Inset", root);
        pool.localPosition = new Vector3(0f, y, -0.4f);
        GameObject foundation = Cylinder("Inset Foundation", pool, new Vector3(0f, 0.025f, 0f), 2.5f, 0.05f, dark);
        // A flattened primitive's capsule expands to its diameter. Use the
        // actual shallow mesh so its invisible collider cannot fill the room.
        CapsuleCollider capsule = foundation.GetComponent<CapsuleCollider>();
        if (capsule != null) Object.DestroyImmediate(capsule);
        GetOrAddComponent<MeshCollider>(foundation).sharedMesh = foundation.GetComponent<MeshFilter>().sharedMesh;
        Renderer surface = MeshObject("Polished Surface", pool, ArchiveDisc(), mirror, false).GetComponent<Renderer>();
        for (int i = 0; i < 96; i++)
        {
            float a = i * Mathf.PI * 2f / 96f;
            GameObject rim = NullBox($"Brass Rim {i:00}", pool, new Vector3(Mathf.Cos(a) * 2.5f, 0.068f, Mathf.Sin(a) * 2.5f),
                new Vector3(0.17f, 0.038f, 0.045f), brass);
            rim.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            if (i % 4 == 0)
                NullBox($"Pool Index {i:00}", pool, new Vector3(Mathf.Cos(a) * 2.67f, 0.007f, Mathf.Sin(a) * 2.67f),
                    new Vector3(0.045f, 0.008f, 0.045f), amber);
        }
        GetOrAddComponent<SignalArchiveReflection>(pool.gameObject).Configure(player.PlayerCamera, surface, mirror);
        Transform sculptureRoot = NewGroup("Suspended Signal", root);
        sculptureRoot.localPosition = new Vector3(0f, -3.8f, -0.4f);
        SignalArchiveSculpture sculpture = GetOrAddComponent<SignalArchiveSculpture>(sculptureRoot.gameObject);
        sculpture.Configure(filaments, stars);
        sculpture.RenderingLayerMask = SignalArchiveLayout.RenderingLayer;

        Transform[] relayPoints = new Transform[3];
        Text[] labels = new Text[3];
        Renderer[] indicators = new Renderer[3];
        Vector3[] cabinets = { new(-3.62f, y, 2.6f), new(-0.6f, y, -4.97f), new(5.16f, y, -1.4f) };
        Vector3[] interactions = { new(-2.5f, y, 2.6f), new(-0.6f, y, -3.75f), new(3.95f, y, -1.4f) };
        for (int i = 0; i < 3; i++)
        {
            Transform receiver = NewGroup($"Receiver {i + 1:00}", root);
            receiver.localPosition = cabinets[i];
            receiver.localRotation = Quaternion.Euler(0f, i == 0 ? 90f : i == 2 ? -90f : 0f, 0f);
            NullBox("Cabinet", receiver, new Vector3(0f, 0.96f, 0f), new Vector3(1.02f, 1.92f, 0.44f), dark);
            for (int rack = 0; rack < 6; rack++)
            {
                NullBox($"Receiver Module {rack}", receiver, new Vector3(0f, 0.33f + rack * 0.20f, 0.235f), new Vector3(0.87f, 0.16f, 0.04f), brass);
                NullBox($"Signal Slot {rack}", receiver, new Vector3(-0.04f, 0.33f + rack * 0.20f, 0.26f), new Vector3(0.64f, 0.018f, 0.015f), blue);
            }
            labels[i] = NullSign("Receiver Readout", receiver, new Vector3(0f, 1.63f, 0.26f), Vector3.forward,
                $"RECEIVER {i + 1:00}\nISOLATED / F TO ENERGIZE", 0.92f, 0.38f, dark);
            indicators[i] = NullBox("Supply Indicator", receiver, new Vector3(0.40f, 1.63f, 0.29f),
                new Vector3(0.03f, 0.20f, 0.025f), amber).GetComponent<Renderer>();
            relayPoints[i] = NewGroup($"Receiver Interaction {i + 1:00}", root);
            relayPoints[i].localPosition = interactions[i];
        }

        Transform bench = NewGroup("Playback Bench", root);
        bench.localPosition = new Vector3(3.3f, y, 1.35f);
        bench.localRotation = Quaternion.Euler(0f, -90f, 0f);
        ReusablePrefabInstance("Work Table", bench, reusablePrefabs.UtilityDesk, new Vector3(0f, 0f, 0.25f), Quaternion.identity);
        ReusablePrefabInstance("Chair", bench, reusablePrefabs.ChairBlack, new Vector3(0f, 0f, -0.9f), Quaternion.identity);
        NullBox("Playback Terminal", bench, new Vector3(0f, 0.98f, 0.26f), new Vector3(1.08f, 0.4f, 0.32f), dark);
        Text readout = CreateWorldDisplayText("Archive Readout", bench, new Vector3(0f, 0.98f, 0.09f), 1f, 0.34f,
            "AFTERGLOW / SIGNAL ARCHIVE\nRECEIVERS OFFLINE", 48, Quaternion.identity);
        readout.color = new Color(0.46f, 0.92f, 1f);
        Transform pose = NewGroup("Seated Camera Pose", bench);
        pose.localPosition = new Vector3(0f, 1.3f, -0.9f);
        pose.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        Transform trigger = NewGroup("Archive Bench Interaction", bench);
        trigger.localPosition = new Vector3(0f, 0.9f, -0.9f);
        BoxCollider bounds = GetOrAddComponent<BoxCollider>(trigger.gameObject);
        bounds.isTrigger = true;
        bounds.size = new Vector3(1.2f, 1.8f, 1.2f);
        Rigidbody body = GetOrAddComponent<Rigidbody>(trigger.gameObject);
        body.isKinematic = true;
        body.useGravity = false;
        SimpleSeatedConsoleController console = GetOrAddComponent<SimpleSeatedConsoleController>(trigger.gameObject);
        console.Configure(player, player.PlayerCamera, pose);
        console.ConfigurePrompts("Press F to sit at the archive playback bench",
            "A / D: choose recording   Space: play\nMouse: look   Wheel: zoom   F / Esc: stand up");
        GetOrAddComponent<SignalArchiveController>(root.gameObject).Configure(player, operations, console,
            relayPoints, labels, indicators, accents, readout, sculpture);
        BuildArchiveRoute(root);
        SetRendererMask(root, SignalArchiveLayout.RenderingLayer);
        SetLightMask(root, SignalArchiveLayout.RenderingLayer);
        Transform zones = NewGroup("Local Lighting", root);
        BuildLocalVolume("Archive Camera Volume", zones, new Vector3(0.775f, -3.75f, -0.475f), Quaternion.identity,
            new Vector3(9.6f, 6.7f, 9.8f), 26f, 0.22f,
            GetVolumeProfile("SignalArchive", 0.35f, 18f, 4f, 0.65f, 0.8f));
        BuildLocalVolume("Archive Passage Camera Volume", zones, new Vector3(4.525f, -5.35f, 8.175f), Quaternion.identity,
            new Vector3(2f, 3.5f, 7.65f), 25f, 0.2f,
            GetVolumeProfile("ArchivePassage", 0.1f, 14f, -8f, 0.22f, 0.95f));
        BuildLocalReflectionProbe("Archive Reflection Probe", zones, new Vector3(0.775f, -3.75f, -0.475f), Quaternion.identity,
            new Vector3(9.6f, 6.7f, 9.8f), 0.2f, GetSolidCubemap("ArchiveReflection", new Color(0.08f, 0.15f, 0.22f)));
    }

    private static Material ArchiveMaterial(string name, string shaderName)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new System.InvalidOperationException($"Missing archive shader: {shaderName}");
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh ArchiveDisc()
    {
        List<Vector3> vertices = new() { new Vector3(0f, 0.06f, 0f) };
        List<int> triangles = new();
        for (int i = 0; i <= 96; i++)
        {
            float angle = i * Mathf.PI * 2f / 96f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * 2.48f, 0.06f, Mathf.Sin(angle) * 2.48f));
            if (i > 0) triangles.AddRange(new[] { 0, i + 1, i });
        }
        return GetGeneratedMesh("ArchiveReflectionDisc", vertices, triangles);
    }

    private static void BuildArchiveCeilingStars(Transform root, Material material)
    {
        System.Random random = new(19075);
        List<Vector3> vertices = new();
        List<int> triangles = new();
        List<Vector2> uv = new();
        List<Color> colors = new();
        for (int i = 0; i < 150; i++)
        {
            float x = -3.65f + (float)random.NextDouble() * 8.7f;
            float z = -4.9f + (float)random.NextDouble() * 8.9f;
            float r = 0.016f + (float)random.NextDouble() * 0.025f;
            int first = vertices.Count;
            vertices.AddRange(new[] { new Vector3(x-r, -0.425f, z-r), new Vector3(x+r, -0.425f, z-r),
                new Vector3(x+r, -0.425f, z+r), new Vector3(x-r, -0.425f, z+r) });
            uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            Color color = Color.Lerp(new Color(0.2f, 0.58f, 1f), new Color(1f, 0.7f, 0.35f), (float)random.NextDouble());
            for (int corner = 0; corner < 4; corner++) colors.Add(color);
            triangles.AddRange(new[] { first, first+1, first+2, first, first+2, first+3 });
        }
        Mesh mesh = GetGeneratedMesh("ArchiveCeilingStars", vertices, triangles);
        mesh.SetUVs(0, uv);
        mesh.SetColors(colors);
        MeshObject("Ceiling Star Field", root, mesh, material, false);
    }

    private static void BuildArchiveRoute(Transform root)
    {
        Transform route = NewGroup("Walking Route", root);
        const float y = SignalArchiveLayout.FloorY;
        Vector3[] points = { new(4.5f,y,14.85f), new(4.5f,y,11.5f), new(4.5f,y,5.4f), new(4.5f,y,3.5f),
            new(3.5f,y,2.6f), new(-2.5f,y,2.6f), new(-3.05f,y,-2.8f), new(-0.6f,y,-3.75f),
            new(3.95f,y,-3.75f), new(3.95f,y,-1.4f), new(3.95f,y,1.35f) };
        for (int i = 0; i < points.Length; i++) NewGroup($"Route {i:00}", route).localPosition = points[i];
    }
}
