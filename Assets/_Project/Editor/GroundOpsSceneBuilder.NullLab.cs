using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static partial class GroundOpsSceneBuilder
{
    private static void BuildNullLaboratory(Transform operations, FirstPersonPlayerController player)
    {
        Transform root = NewGroup("Level 01 - Null Reference Laboratory", operations);
        Material wall = GetMaterial("NullLab Painted Concrete", new Color(0.29f, 0.34f, 0.34f), 0f, 0.24f);
        Material floor = GetMaterial("NullLab Sealed Floor", new Color(0.15f, 0.20f, 0.21f), 0.10f, 0.48f);
        Material dark = GetMaterial("NullLab Graphite", new Color(0.035f, 0.052f, 0.061f), 0.30f, 0.36f);
        Material metal = GetMaterial("NullLab Brushed Metal", new Color(0.40f, 0.47f, 0.48f), 0.72f, 0.60f);
        Material copper = GetMaterial("NullLab Copper", new Color(0.57f, 0.28f, 0.12f), 0.76f, 0.62f);
        Material yellow = GetMaterial("NullLab Safety Ochre", new Color(0.88f, 0.60f, 0.19f), 0.08f, 0.30f);
        Material warm = GetEmissiveMaterial("NullLab Amber Lamp", new Color(1f, 0.54f, 0.18f), 3f);
        Material cool = GetEmissiveMaterial("NullLab Reference Lamp", new Color(0.35f, 0.92f, 0.87f), 3f);
        Transform stairs = NewGroup("Stair 01", root);
        BuildNullStairs(stairs, wall, floor, metal, yellow, warm);
        Transform rooms = NewGroup("Rooms", root);
        const float y = NullLabLayout.FloorY;
        const float ceiling = NullLabLayout.CeilingY;
        float height = ceiling - y;
        NullBox("Lab Floor", rooms, new Vector3(0.15f, y - 0.08f, 17.9f), new Vector3(6.8f, 0.16f, 11.8f), floor);
        NullBox("Cable Gallery Floor", rooms, new Vector3(4.525f, y - 0.08f, 18.025f), new Vector3(1.95f, 0.16f, 12.05f), floor);
        NullBox("Lab Ceiling", rooms, new Vector3(0.15f, ceiling + 0.08f, 17.9f), new Vector3(6.8f, 0.16f, 11.8f), wall);
        NullBox("Cable Gallery Ceiling", rooms, new Vector3(4.525f, ceiling + 0.08f, 18.025f), new Vector3(1.95f, 0.16f, 12.05f), wall);
        NullBox("West Wall", rooms, new Vector3(-3.325f, y + height / 2f, 17.9f), new Vector3(0.15f, height, 11.95f), wall);
        NullBox("Gallery East Wall", rooms, new Vector3(5.575f, y + height / 2f, 18.025f), new Vector3(0.15f, height, 12.2f), wall);
        NullBox("South Wall", rooms, new Vector3(1.125f, y + height / 2f, 11.925f), new Vector3(8.75f, height, 0.15f), wall);
        NullBox("Lab North Wall", rooms, new Vector3(0.15f, y + height / 2f, 23.875f), new Vector3(6.8f, height, 0.15f), wall);
        // Independent internal partition: a real opening into the lab, with a
        // fixed observation pane farther along the cable gallery.
        NullBox("Gallery Partition South", rooms, new Vector3(3.55f, y + height / 2f, 13.0f), new Vector3(0.15f, height, 2f), wall);
        NullBox("Gallery Partition North", rooms, new Vector3(3.55f, y + height / 2f, 19.75f), new Vector3(0.15f, height, 8.1f), wall);
        NullBox("Lab Entrance Header", rooms, new Vector3(3.55f, y + 2.825f, 14.85f), new Vector3(0.15f, 1.25f, 1.7f), wall);
        // The test cell remains explorable through its eastern opening.
        NullBox("Cell Window Sill", rooms, new Vector3(-0.95f, y + 0.43f, 19.3f), new Vector3(4.6f, 0.86f, 0.15f), dark);
        NullBox("Cell Window Header", rooms, new Vector3(0.15f, y + 3.08f, 19.3f), new Vector3(6.8f, 0.74f, 0.15f), dark);
        NullBox("Cell Door Jamb", rooms, new Vector3(3.35f, y + 1.355f, 19.3f), new Vector3(0.25f, 2.71f, 0.15f), dark);
        for (int i = 0; i < 3; i++)
        {
            float x = -2.47f + i * 1.53f;
            ReusablePrefabInstance($"Cell Window {i + 1}", rooms, reusablePrefabs.WindowPane,
                new Vector3(x, y + 1.785f, 19.3f), Quaternion.identity, new Vector3(1.50f, 1.85f, 0.02f));
            NullBox($"Window Mullion {i + 1}", rooms, new Vector3(x + 0.765f, y + 1.785f, 19.3f), new Vector3(0.045f, 1.85f, 0.07f), metal);
        }

        Transform gallery = NewGroup("Cable Gallery", root);
        for (int i = 0; i < 3; i++)
        {
            NullBox($"Cable Trunk {i + 1}", gallery, new Vector3(5.40f - i * 0.09f, y + 2.5f + i * 0.10f, 18f), new Vector3(0.065f, 0.065f, 11.5f), i == 1 ? copper : dark);
        }
        for (int i = 0; i < 6; i++)
        {
            NullBox($"Cable Support {i + 1}", gallery, new Vector3(5.28f, y + 2.38f, 13f + i * 2f), new Vector3(0.42f, 0.035f, 0.07f), metal);
            NullBox($"Gallery Guide {i + 1}", gallery, new Vector3(5.48f, y + 0.2f, 13f + i * 2f), new Vector3(0.025f, 0.055f, 0.7f), warm);
        }
        for (int i = 0; i < 4; i++)
            NullLamp($"Gallery Lamp {i + 1}", gallery, new Vector3(4.55f, y + 2.7f, 13.2f + i * 3.1f),
                new Vector3(0f, -1f, 0.1f), new Color(1f, 0.64f, 0.30f), 18f, 5f, warm, false);
        NullSign("Gallery Entrance Sign", gallery, new Vector3(4.55f, y + 2.6f, 23.77f), Vector3.forward,
            "LEVEL 01 / QUIET SERVICES\nNULL REFERENCE LAB  /  FOLLOW AMBER LINE", 1.70f, 0.38f, dark);
        NullSign("Lab Door Sign", gallery, new Vector3(3.65f, y + 2.6f, 14.85f), Vector3.right,
            "N-01 / NULL REFERENCE\nSIGNAL BALANCE BENCH", 1.6f, 0.38f, dark);
        NullSign("Return Sign", gallery, new Vector3(5.46f, y + 1.7f, 22.2f), Vector3.left,
            "STAIR 01 >\nCHAMBER / OPERATIONS", 1.45f, 0.4f, dark);

        Transform isolator = NewGroup("Bench Supply Isolator", gallery);
        isolator.localPosition = new Vector3(4.5f, y, 12.15f);
        NullBox("Supply Cabinet", isolator, new Vector3(0f, 1.35f, 0f), new Vector3(0.68f, 0.9f, 0.22f), metal);
        Text supplyLabel = NullSign("Supply Instructions", isolator, new Vector3(0f, 1.5f, 0.13f), Vector3.forward,
            "N-01 / BENCH SUPPLY\nISOLATED\nF / ENERGIZE", 0.62f, 0.48f, dark);
        Transform supplyHandle = NewGroup("Supply Handle", isolator);
        supplyHandle.localPosition = new Vector3(0f, 1.04f, 0.20f);
        NullBox("Lever", supplyHandle, Vector3.zero, new Vector3(0.09f, 0.25f, 0.07f), yellow);
        Transform supplyPoint = NewGroup("Supply Interaction", isolator);
        supplyPoint.localPosition = new Vector3(0f, 0f, 1.05f);

        Transform lab = NewGroup("Null Reference Lab", root);
        for (int i = 0; i < 3; i++)
            NullBox($"Floor Inlay {i + 1}", lab, new Vector3(-2.7f + i * 2.4f, y + 0.003f, 15.6f), new Vector3(0.028f, 0.004f, 6.3f), metal);
        NullSign("Lab Identity", lab, new Vector3(-3.23f, y + 2.15f, 15.3f), Vector3.right,
            "N U L L\nREFERENCE LABORATORY\nQUIET IS A MEASUREMENT", 2.8f, 1.25f, dark);
        NullSign("Bench Procedure", lab, new Vector3(1.65f, y + 2.2f, 12.03f), Vector3.forward,
            "01 / ENERGIZE THE BENCH SUPPLY\n02 / BALANCE PHASE AND AMPLITUDE\n03 / HOLD SPACE TO CERTIFY THE NULL", 2.1f, 0.9f, dark);
        NullLamp("Lab Safety Lamp", lab, new Vector3(2.8f, y + 3.1f, 14.1f), new Vector3(-0.3f, -1f, 0f),
            new Color(1f, 0.65f, 0.33f), 30f, 8f, warm, false);
        Light benchLight = NullLamp("Bench Task Lamp", lab, new Vector3(-0.5f, y + 2.8f, 16.3f),
            new Vector3(0f, -1f, 0.2f), new Color(0.62f, 0.88f, 1f), 45f, 7f, cool, true);
        Transform bench = NewGroup("Balance Bench", lab);
        bench.localPosition = new Vector3(-0.4f, y, 17.1f);
        ReusablePrefabInstance("Work Table", bench, reusablePrefabs.DocDesk, Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
        ReusablePrefabInstance("Chair", bench, reusablePrefabs.ChairBlack, new Vector3(0f, 0f, -0.9f), Quaternion.identity);
        NullBox("Instrument Case", bench, new Vector3(0f, 1.02f, 0.12f), new Vector3(1.25f, 0.50f, 0.45f), dark);
        GameObject scope = Quad("Cancellation Scope", bench, new Vector3(-0.18f, 1.03f, -0.111f), 0.77f, 0.36f, Vector3.back,
            GetMaterial("NullLab Scope Screen", Color.white, 0f, 0.3f));
        // An unlit monitor material keeps the trace legible with the bench off.
        scope.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("Universal Render Pipeline/Unlit");
        Transform phaseKnob = NullKnob("Phase Dial", bench, new Vector3(0.44f, 1.13f, -0.145f), metal, yellow);
        Transform amplitudeKnob = NullKnob("Amplitude Dial", bench, new Vector3(0.44f, 0.94f, -0.145f), metal, yellow);
        NullBox("Readout Housing", bench, new Vector3(0f, 1.40f, 0.12f), new Vector3(1.25f, 0.26f, 0.45f), dark);
        Text readout = CreateWorldDisplayText("Bench Readout", bench, new Vector3(0f, 1.40f, -0.112f), 1.18f, 0.22f,
            "NULL REFERENCE / BENCH ISOLATED", 38, Quaternion.identity);
        readout.color = new Color(0.50f, 1f, 0.86f);
        Transform seat = NewGroup("Seated Camera Pose", bench);
        seat.localPosition = new Vector3(0f, 1.23f, -1.0f);
        seat.localRotation = Quaternion.LookRotation(new Vector3(0f, 0.0f, 1f));
        Transform trigger = NewGroup("Null Bench Interaction", bench);
        trigger.localPosition = new Vector3(0f, 0.9f, -1.0f);
        BoxCollider bounds = GetOrAddComponent<BoxCollider>(trigger.gameObject);
        bounds.isTrigger = true;
        bounds.size = new Vector3(1.2f, 1.8f, 1.2f);
        Rigidbody body = GetOrAddComponent<Rigidbody>(trigger.gameObject);
        body.isKinematic = true;
        body.useGravity = false;
        SimpleSeatedConsoleController console = GetOrAddComponent<SimpleSeatedConsoleController>(trigger.gameObject);
        console.Configure(player, player.PlayerCamera, seat);
        console.ConfigurePrompts("Press F to sit at the null balance bench",
            "A / D phase   W / S amplitude   Shift: fine   Space: certify\nMouse: look   Wheel: zoom   F / Esc: stand up");

        Transform cell = NewGroup("Null Cell", root);
        NullBox("Apparatus Plinth", cell, new Vector3(-0.7f, y + 0.12f, 21.6f), new Vector3(2.8f, 0.24f, 2.5f), dark);
        for (int i = 0; i < 9; i++)
            NullBox($"Absorber Fin {i + 1}", cell, new Vector3(-2.95f + i * 0.65f, y + 1.45f, 23.45f), new Vector3(0.12f, 2.8f, 0.40f), dark);
        Transform rotor = NewGroup("Reference Rotor", cell);
        rotor.localPosition = new Vector3(-0.7f, y + 1.65f, 21.55f);
        for (int ring = 0; ring < 2; ring++)
        {
            for (int segment = 0; segment < 32; segment++)
            {
                float angle = segment * 360f / 32f;
                float radians = angle * Mathf.Deg2Rad;
                GameObject piece = NullBox($"Ring {ring + 1} Segment {segment + 1:00}", rotor,
                    new Vector3(Mathf.Cos(radians) * 1.04f, Mathf.Sin(radians) * 1.04f, ring * 0.44f),
                    new Vector3(0.21f, 0.075f, 0.075f), copper);
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
            }
        }
        NullBox("Phase Vane", rotor, new Vector3(0f, 0f, 0.22f), new Vector3(1.76f, 0.045f, 0.055f), metal);
        for (int side = -1; side <= 1; side += 2)
            NullBox($"Suspension {side}", cell, new Vector3(-0.7f + side * 0.8f, y + 2.72f, 21.78f), new Vector3(0.025f, 1.45f, 0.025f), metal);
        Light cellLight = NullLamp("Cell Grazing Light", cell, new Vector3(-2.6f, y + 2.7f, 20.0f),
            new Vector3(1.5f, -0.9f, 2.7f), new Color(0.42f, 0.88f, 1f), 100f, 8f, cool, true);
        Text cellStatus = NullSign("Cell Status", cell, new Vector3(-0.7f, y + 2.9f, 23.18f), Vector3.back,
            "NULL CELL / STANDBY", 2.7f, 0.35f, dark);
        NullLamp("Cell Standby Lamp", cell, new Vector3(2.8f, y + 2.6f, 22.3f), new Vector3(-1f, -0.5f, -0.1f),
            new Color(1f, 0.44f, 0.14f), 14f, 6f, warm, false);

        NullLaboratoryController controller = GetOrAddComponent<NullLaboratoryController>(root.gameObject);
        controller.Configure(player, operations, console, supplyPoint, supplyHandle, supplyLabel, readout,
            cellStatus, scope.GetComponent<Renderer>(), phaseKnob, amplitudeKnob, rotor,
            new[] { benchLight, cellLight });
        BuildNullRouteMarkers(root);
        SetRendererMask(root, NullLabLayout.RenderingLayer);
        SetLightMask(root, NullLabLayout.RenderingLayer);
        Transform zones = NewGroup("Local Lighting", root);
        BuildLocalVolume("Null Lab Camera Volume", zones, new Vector3(1.05f, -5.2f, 17.95f), Quaternion.identity,
            new Vector3(9.15f, 3.9f, 12.15f), 24f, 0.3f,
            GetVolumeProfile("NullLaboratory", 0.15f, 16f, -8f, 0.18f, 0.95f));
        BuildLocalVolume("Stair Camera Volume", zones, new Vector3(1.1f, -2.1f, 25.6f), Quaternion.identity,
            new Vector3(9.2f, 10.1f, 3.45f), 25f, 0.2f,
            GetVolumeProfile("NullStair", 0.05f, 8f, -8f, 0.12f, 1f));
        BuildLocalReflectionProbe("Null Lab Reflection Probe", zones, new Vector3(0.2f, -5.35f, 17.9f), Quaternion.identity,
            new Vector3(6.9f, 3.5f, 11.9f), 0.2f,
            GetSolidCubemap("NullLabReflection", new Color(0.11f, 0.17f, 0.18f)));
    }

    private static void BuildNullStairs(Transform root, Material wall, Material floor, Material metal, Material yellow, Material lamp)
    {
        const float floorY = NullLabLayout.FloorY;
        const float midY = floorY / 2f;
        NullBox("Upper Landing", root, new Vector3(4.8875f, -0.08f, 26.55f), new Vector3(1.375f, 0.16f, 1.3f), floor);
        NullBox("Half Landing", root, new Vector3(-2.4f, midY - 0.08f, 25.6f), new Vector3(1.6f, 0.16f, 3.2f), floor);
        NullBox("Lower Landing", root, new Vector3(4.85f, floorY - 0.08f, 25.6f), new Vector3(1.3f, 0.16f, 3.2f), floor);
        for (int flight = 0; flight < 2; flight++)
        {
            bool upper = flight == 0;
            float startX = upper ? NullLabLayout.StairTopX : NullLabLayout.StairTurnX;
            float endX = upper ? NullLabLayout.StairTurnX : NullLabLayout.StairTopX;
            float startY = upper ? 0f : midY;
            float z = upper ? NullLabLayout.UpperFlightZ : NullLabLayout.LowerFlightZ;
            float run = Mathf.Abs(endX - startX) / NullLabLayout.StepsPerFlight;
            float rise = -midY / NullLabLayout.StepsPerFlight;
            for (int step = 0; step < NullLabLayout.StepsPerFlight; step++)
            {
                float t = (step + 0.5f) / NullLabLayout.StepsPerFlight;
                float top = startY - (step + 1) * rise;
                float x = Mathf.Lerp(startX, endX, t);
                NullBox($"Flight {flight + 1} Tread {step + 1:00}", root, new Vector3(x, top - rise / 2f, z),
                    new Vector3(run + 0.004f, rise, NullLabLayout.StairWidth), floor);
                NullBox($"Flight {flight + 1} Nosing {step + 1:00}", root,
                    new Vector3(x + (upper ? run / 2f - 0.025f : -run / 2f + 0.025f), top + 0.003f, z),
                    new Vector3(0.05f, 0.006f, NullLabLayout.StairWidth - 0.04f), yellow);
            }
            // Sloping handrails follow the flight rather than each individual tread.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 first = new(startX, startY + 0.95f, z + side * 0.61f);
                Vector3 last = new(endX, startY + midY + 0.95f, z + side * 0.61f);
                NullRail($"Flight {flight + 1} Handrail {side}", root, first, last, metal);
                for (int post = 0; post <= 5; post++)
                {
                    Vector3 top = Vector3.Lerp(first, last, post / 5f);
                    NullBox($"Flight {flight + 1} Post {side} {post}", root, top - Vector3.up * 0.47f,
                        new Vector3(0.035f, 0.94f, 0.035f), metal);
                }
            }
        }
        NullBox("Stair North Wall", root, new Vector3(1.125f, -2.15f, 27.325f), new Vector3(8.95f, 9.9f, 0.15f), wall);
        NullBox("Stair West Wall", root, new Vector3(-3.325f, -3.63f, 25.625f), new Vector3(0.15f, 6.94f, 3.55f), wall);
        NullBox("Stair West Upper Wall", root, new Vector3(-3.325f, 1.32f, 26.6f), new Vector3(0.15f, 2.96f, 1.6f), wall);
        NullBox("Stair South Wall", root, new Vector3(0.45f, -3.63f, 23.925f), new Vector3(7.55f, 6.94f, 0.15f), wall);
        NullBox("Lower Exit Header", root, new Vector3(4.875f, -2.43f, 23.925f), new Vector3(1.3f, 4.54f, 0.15f), wall);
        NullBox("Stair East Lower Wall", root, new Vector3(5.575f, -3.55f, 25.65f), new Vector3(0.15f, 7.1f, 3.4f), wall);
        // Only the upper flight rises beside the containing room. Its lower
        // return stays beneath that room's existing floor and never enters it.
        NullBox("Upper Flight South Wall", root, new Vector3(0.45f, 1.32f, 25.85f), new Vector3(7.55f, 2.96f, 0.04f), wall);
        NullBox("Stair Ceiling", root, new Vector3(1.125f, 2.88f, 26.6f), new Vector3(8.95f, 0.16f, 1.6f), wall);
        // Guard the open edge of the upper landing and the intermediate turn.
        NullRail("Upper Landing Guard", root, new Vector3(4.2f, 1f, 25.88f), new Vector3(5.5f, 1f, 25.88f), metal);
        NullBox("Upper Guard Infill", root, new Vector3(4.85f, 0.49f, 25.88f), new Vector3(1.3f, 0.98f, 0.05f), metal);
        NullRail("Half Landing Guard", root, new Vector3(-1.59f, midY + 1f, 25.34f), new Vector3(-1.59f, midY + 1f, 25.86f), metal);
        for (int i = 0; i < 3; i++)
        {
            Vector3 pos = i == 0 ? new Vector3(4.7f, 2.4f, 26.55f)
                : i == 1 ? new Vector3(-2.6f, -1.2f, 25.65f) : new Vector3(4.85f, -4.5f, 25.25f);
            NullLamp($"Stair Bulkhead {i + 1}", root, pos, Vector3.down, new Color(1f, 0.62f, 0.29f), 32f, 8f, lamp, false);
        }
        NullSign("Half Landing Level", root, new Vector3(-3.23f, midY + 1.65f, 25.7f), Vector3.right,
            "01\nNULL REFERENCE LAB\nCONTINUE DOWN >", 1.5f, 1.1f, metal);
        NullSign("Upper Stair Sign", root, new Vector3(5.68f, 2.3f, 26.55f), Vector3.right,
            "STAIR 01\nNULL REFERENCE LAB / LEVEL 01", 1.35f, 0.45f, metal);
        NullSign("Lower Landing Level", root, new Vector3(4.85f, floorY + 1.7f, 27.22f), Vector3.back,
            "LEVEL 01\nQUIET SERVICES\nCHAMBER ABOVE", 1.1f, 0.9f, metal);
    }

    private static GameObject NullBox(string name, Transform parent, Vector3 position, Vector3 size, Material material) =>
        Box(name, parent, position, size, Quaternion.identity, material);

    private static void NullRail(string name, Transform parent, Vector3 first, Vector3 last, Material material)
    {
        GameObject rail = NullBox(name, parent, (first + last) / 2f, new Vector3(0.055f, 0.055f, Vector3.Distance(first, last)), material);
        rail.transform.localRotation = Quaternion.LookRotation(last - first);
    }

    private static Text NullSign(string name, Transform parent, Vector3 position, Vector3 facing,
        string content, float width, float height, Material plate)
    {
        Transform sign = NewGroup(name, parent);
        sign.localPosition = position;
        sign.localRotation = Quaternion.LookRotation(-facing);
        NullBox("Plate", sign, Vector3.zero, new Vector3(width, height, 0.025f), plate);
        Text text = CreateWorldDisplayText("Legend", sign, new Vector3(0f, 0f, -0.017f), width - 0.06f, height - 0.04f,
            content, 64, Quaternion.identity);
        text.color = new Color(0.76f, 0.92f, 0.87f);
        return text;
    }

    private static Light NullLamp(string name, Transform parent, Vector3 position, Vector3 direction,
        Color color, float intensity, float range, Material diffuser, bool shadows)
    {
        Transform lamp = NewGroup(name, parent);
        lamp.localPosition = position;
        lamp.localRotation = Quaternion.LookRotation(direction);
        NullBox("Diffuser", lamp, Vector3.zero, new Vector3(0.42f, 0.09f, 0.06f), diffuser);
        Transform bulb = NewGroup("Light", lamp);
        bulb.localPosition = new Vector3(0f, 0f, 0.08f);
        Light light = GetOrAddComponent<Light>(bulb.gameObject);
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = shadows ? 65f : 105f;
        light.innerSpotAngle = shadows ? 45f : 68f;
        light.shadows = shadows ? LightShadows.Hard : LightShadows.None;
        light.shadowBias = 0.025f;
        light.shadowNormalBias = 0.10f;
        return light;
    }

    private static Transform NullKnob(string name, Transform parent, Vector3 position, Material metal, Material marker)
    {
        Transform knob = NewGroup(name, parent);
        knob.localPosition = position;
        GameObject dial = Cylinder("Dial", knob, Vector3.zero, 0.065f, 0.06f, metal);
        dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        NullBox("Index", knob, new Vector3(0f, 0.031f, -0.034f), new Vector3(0.014f, 0.038f, 0.01f), marker);
        return knob;
    }

    private static void BuildNullRouteMarkers(Transform root)
    {
        Transform route = NewGroup("Walking Route", root);
        Vector3[] positions =
        {
            new(6.8f, 0f, 11.25f), new(6.8f, 0f, 26.55f), new(4.65f, 0f, 26.55f),
            new(-2.35f, -3.55f, 26.55f), new(-2.35f, -3.55f, 24.65f),
            new(4.85f, -7.1f, 24.65f), new(4.85f, -7.1f, 22.8f),
            new(4.5f, -7.1f, 13.2f), new(4.5f, -7.1f, 14.85f),
            new(2.7f, -7.1f, 14.85f), new(-0.4f, -7.1f, 15.6f),
        };
        for (int i = 0; i < positions.Length; i++)
            NewGroup($"Route {i:00}", route).localPosition = positions[i];
    }
}
