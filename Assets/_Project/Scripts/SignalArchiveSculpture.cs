using UnityEngine;
using UnityEngine.Rendering;

// Fictional telemetry made into light: eight orbits, one ribbon mesh, one star mesh.
// Everything is built only in Play Mode. The supplied materials remain shared assets.
public sealed class SignalArchiveSculpture : MonoBehaviour
{
    private const int OrbitCount = 8;
    private const int OrbitSamples = 192;
    private const int FilamentCount = 32;
    private const int FilamentSamples = 96;
    private const int CloudCount = 112;
    private const int TrailLength = 7;
    private const int StarCount = CloudCount + OrbitCount * TrailLength + 1;
    private const float Tau = Mathf.PI * 2f;

    private static readonly Color Cyan = new(0.035f, 0.75f, 1.25f, 1f);
    private static readonly Color Amber = new(1.55f, 0.46f, 0.055f, 1f);
    private static readonly Color Ember = new(1.2f, 0.13f, 0.025f, 1f);
    private static readonly Color Violet = new(0.8f, 0.055f, 1.25f, 1f);
    private static readonly Color Mint = new(0.035f, 1.1f, 0.45f, 1f);

    [SerializeField] private Material filamentMaterial;
    [SerializeField] private Material starMaterial;
    [SerializeField] private uint renderingLayerMask = 1u << 7;

    private Transform runtimeRoot;
    private readonly LineRenderer[] orbits = new LineRenderer[OrbitCount];
    private readonly Vector3[][] orbitPoints = new Vector3[OrbitCount][];
    private readonly Quaternion[] orbitRotations = new Quaternion[OrbitCount];
    private readonly Vector3[] cloudPositions = new Vector3[CloudCount];
    private readonly float[] cloudSizes = new float[CloudCount];
    private readonly Renderer[] renderers = new Renderer[OrbitCount + 2];
    private Mesh filamentMesh;
    private Mesh starsMesh;
    private Vector3[] filamentVertices;
    private Color[] filamentColors;
    private Vector3[] starVertices;
    private Color[] starColors;
    private Camera viewingCamera;
    private float activation;
    private float targetActivation;
    private float pulsarWeight;
    private float auroraWeight;
    private float clock;
    private int program;
    private float performance;
    private bool performing;

    public uint RenderingLayerMask
    {
        get => renderingLayerMask;
        set
        {
            renderingLayerMask = value;
            foreach (Renderer item in renderers)
                if (item != null) item.renderingLayerMask = value;
        }
    }

    public void Configure(Material filaments, Material stars)
    {
        filamentMaterial = filaments;
        starMaterial = stars;
        if (Application.isPlaying && runtimeRoot == null) BuildSculpture();
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].sharedMaterial = i == renderers.Length - 1 ? stars : filaments;
    }

    public void SetPresentation(float activation01, int selectedProgram, float performance01, bool isPerforming)
    {
        targetActivation = Mathf.Clamp01(activation01);
        program = Mathf.Clamp(selectedProgram, 0, 2);
        performance = Mathf.Clamp01(performance01);
        performing = isPerforming;
    }

    private void Awake() => BuildSculpture();

    private void BuildSculpture()
    {
        if (runtimeRoot != null || filamentMaterial == null || starMaterial == null) return;
        runtimeRoot = new GameObject("Runtime light sculpture").transform;
        runtimeRoot.SetParent(transform, false);
        viewingCamera = Camera.main;
        for (int orbit = 0; orbit < OrbitCount; orbit++)
        {
            GameObject ring = new($"Orbit {orbit + 1:00}");
            ring.transform.SetParent(runtimeRoot, false);
            LineRenderer line = ring.AddComponent<LineRenderer>();
            line.sharedMaterial = filamentMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = OrbitSamples;
            line.widthMultiplier = orbit < 3 ? 0.034f : 0.025f;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.textureMode = LineTextureMode.Stretch;
            ConfigureRenderer(line);
            orbits[orbit] = line;
            renderers[orbit] = line;
            orbitPoints[orbit] = new Vector3[OrbitSamples];
        }

        filamentMesh = BuildFilamentMesh();
        renderers[OrbitCount] = CreateMeshObject("Woven signal field", filamentMesh, filamentMaterial);
        starsMesh = BuildStarMesh();
        renderers[OrbitCount + 1] = CreateMeshObject("Orbital sparks and distant points", starsMesh, starMaterial);
        System.Random random = new(73019);
        for (int star = 0; star < CloudCount; star++)
        {
            float vertical = (float)random.NextDouble() * 2f - 1f;
            float angle = (float)random.NextDouble() * Tau;
            float radius = Mathf.Lerp(0.7f, 2.26f, Mathf.Pow((float)random.NextDouble(), 0.3333f));
            float horizontal = Mathf.Sqrt(1f - vertical * vertical);
            cloudPositions[star] = new Vector3(Mathf.Cos(angle) * horizontal, vertical,
                Mathf.Sin(angle) * horizontal) * radius;
            cloudSizes[star] = Mathf.Lerp(0.009f, 0.024f, (float)random.NextDouble());
        }
        DrawSculpture();
        runtimeRoot.gameObject.SetActive(false);
    }

    private void ConfigureRenderer(Renderer item)
    {
        item.renderingLayerMask = renderingLayerMask;
        item.shadowCastingMode = ShadowCastingMode.Off;
        item.receiveShadows = false;
        item.lightProbeUsage = LightProbeUsage.Off;
        item.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private Renderer CreateMeshObject(string objectName, Mesh mesh, Material material)
    {
        GameObject item = new(objectName);
        item.transform.SetParent(runtimeRoot, false);
        item.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = item.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        ConfigureRenderer(renderer);
        return renderer;
    }

    private Mesh BuildFilamentMesh()
    {
        int verticesPerFilament = (FilamentSamples + 1) * 2;
        filamentVertices = new Vector3[FilamentCount * verticesPerFilament];
        filamentColors = new Color[filamentVertices.Length];
        int[] triangles = new int[FilamentCount * FilamentSamples * 6];
        Vector2[] uv = new Vector2[filamentVertices.Length];
        int index = 0;
        for (int filament = 0; filament < FilamentCount; filament++)
        {
            for (int sample = 0; sample <= FilamentSamples; sample++)
            {
                int vertex = filament * verticesPerFilament + sample * 2;
                // Match LineRenderer UVs: x runs along the ribbon, y across it.
                // One shared shader can then soften both sorts of luminous edge.
                uv[vertex] = new Vector2(sample / (float)FilamentSamples, 0f);
                uv[vertex + 1] = new Vector2(sample / (float)FilamentSamples, 1f);
                if (sample == FilamentSamples) continue;
                triangles[index++] = vertex;
                triangles[index++] = vertex + 1;
                triangles[index++] = vertex + 2;
                triangles[index++] = vertex + 1;
                triangles[index++] = vertex + 3;
                triangles[index++] = vertex + 2;
            }
        }
        Mesh mesh = new() { name = "Runtime archive filaments" };
        mesh.MarkDynamic();
        mesh.vertices = filamentVertices;
        mesh.colors = filamentColors;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4.7f);
        return mesh;
    }

    private Mesh BuildStarMesh()
    {
        // Each point has a bright center and two diamond-shaped falloff bands.
        // This gives a soft luminous point without textures or particle objects.
        starVertices = new Vector3[StarCount * 9];
        starColors = new Color[starVertices.Length];
        Vector2[] uv = new Vector2[starVertices.Length];
        int[] triangles = new int[StarCount * 36];
        int index = 0;
        for (int star = 0; star < StarCount; star++)
        {
            int start = star * 9;
            uv[start] = Vector2.one * 0.5f;
            for (int corner = 0; corner < 4; corner++)
            {
                int next = (corner + 1) % 4;
                Vector2 direction = new(Mathf.Cos(corner * Mathf.PI * 0.5f), Mathf.Sin(corner * Mathf.PI * 0.5f));
                uv[start + corner + 1] = Vector2.one * 0.5f + direction * 0.12f;
                uv[start + corner + 5] = Vector2.one * 0.5f + direction * 0.5f;
                triangles[index++] = start;
                triangles[index++] = start + corner + 1;
                triangles[index++] = start + next + 1;
                triangles[index++] = start + corner + 1;
                triangles[index++] = start + corner + 5;
                triangles[index++] = start + next + 5;
                triangles[index++] = start + corner + 1;
                triangles[index++] = start + next + 5;
                triangles[index++] = start + next + 1;
            }
        }
        Mesh mesh = new() { name = "Runtime archive points" };
        mesh.MarkDynamic();
        mesh.vertices = starVertices;
        mesh.colors = starColors;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4.7f);
        return mesh;
    }

    private void LateUpdate()
    {
        if (runtimeRoot == null || RuntimeSceneSwitcher.IsOpen || Time.deltaTime <= 0f) return;
        clock += Time.deltaTime;
        activation = Mathf.MoveTowards(activation, targetActivation, Time.deltaTime * 0.5f);
        pulsarWeight = Mathf.MoveTowards(pulsarWeight, program == 1 ? 1f : 0f, Time.deltaTime * 0.7f);
        auroraWeight = Mathf.MoveTowards(auroraWeight, program == 2 ? 1f : 0f, Time.deltaTime * 0.7f);
        bool wasActive = runtimeRoot.gameObject.activeSelf;
        bool hasLight = activation > 0f;
        if (wasActive != hasLight) runtimeRoot.gameObject.SetActive(hasLight);
        if (!hasLight) return;
        // Keep time progressing offscreen, but upload moving geometry only when
        // its fixed bounds are visible. The first activation always gets a draw.
        if (wasActive && !renderers[OrbitCount].isVisible && !renderers[OrbitCount + 1].isVisible) return;
        DrawSculpture();
    }

    private void DrawSculpture()
    {
        if (viewingCamera == null) viewingCamera = Camera.main;
        Vector3 right = viewingCamera != null ? transform.InverseTransformDirection(viewingCamera.transform.right) : Vector3.right;
        Vector3 up = viewingCamera != null ? transform.InverseTransformDirection(viewingCamera.transform.up) : Vector3.up;
        Vector3 towardViewer = Vector3.Cross(right, up);
        float crescendo = performing ? Mathf.Sin(performance * Mathf.PI) : 0f;
        float energy = 0.8f + crescendo * 0.65f;
        UpdateOrbits(energy);
        UpdateFilaments(towardViewer, energy);
        UpdateStars(right, up, energy, crescendo);
    }

    private void UpdateOrbits(float energy)
    {
        for (int orbit = 0; orbit < OrbitCount; orbit++)
        {
            float direction = orbit % 2 == 0 ? 1f : -1f;
            orbitRotations[orbit] = Quaternion.Euler(22f + orbit * 23f,
                orbit * 47f + clock * (3f + orbit * 0.3f) * direction, orbit * 17f);
            for (int sample = 0; sample < OrbitSamples; sample++)
                orbitPoints[orbit][sample] = OrbitPoint(orbit, sample * Tau / OrbitSamples);
            orbits[orbit].SetPositions(orbitPoints[orbit]);
            float revealed = Mathf.Clamp01(activation * 3f - (orbit < 3 ? 0f : 1f));
            Color primary = PresentationColor(orbit % 3 == 0 ? Amber : Cyan,
                orbit % 3 == 0 ? Ember : Amber, orbit % 2 == 0 ? Violet : Cyan);
            Color secondary = PresentationColor(Cyan, Color.Lerp(Amber, Ember, 0.4f), Violet);
            float focus = Mathf.Lerp(1f, 0.35f, auroraWeight);
            orbits[orbit].startColor = Glow(primary, revealed * energy * focus * 0.62f);
            orbits[orbit].endColor = Glow(secondary, revealed * energy * focus * 0.43f);
        }
    }

    private Vector3 OrbitPoint(int orbit, float angle)
    {
        float radius = 2.18f - orbit * 0.037f;
        Vector3 orbital = orbitRotations[orbit] * new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        float height = Mathf.Lerp(-1.6f, 1.6f, orbit / (float)(OrbitCount - 1));
        float pulsarRadius = Mathf.Sqrt(4.25f - height * height);
        Vector3 pulsar = new(Mathf.Cos(angle) * pulsarRadius, height + Mathf.Sin(angle * 3f + clock * 0.2f) * 0.06f,
            Mathf.Sin(angle) * pulsarRadius);
        float curtainRadius = 1.7f + 0.17f * Mathf.Sin(angle * 3f + clock * 0.2f + orbit);
        Vector3 aurora = new(Mathf.Cos(angle) * curtainRadius,
            height * 0.85f + Mathf.Sin(angle * 2f + clock * 0.18f + orbit * 0.2f) * 0.3f,
            Mathf.Sin(angle) * curtainRadius * 0.72f);
        return Vector3.ClampMagnitude(orbital * (1f - pulsarWeight - auroraWeight)
            + pulsar * pulsarWeight + aurora * auroraWeight, 2.26f);
    }

    private void UpdateFilaments(Vector3 towardViewer, float energy)
    {
        float revealed = Mathf.Clamp01(activation * 3f - 2f);
        int verticesPerFilament = (FilamentSamples + 1) * 2;
        for (int filament = 0; filament < FilamentCount; filament++)
        {
            Vector3 previous = FilamentPoint(filament, -1f / FilamentSamples);
            Vector3 position = FilamentPoint(filament, 0f);
            for (int sample = 0; sample <= FilamentSamples; sample++)
            {
                float along = sample / (float)FilamentSamples;
                Vector3 next = FilamentPoint(filament, along + 1f / FilamentSamples);
                Vector3 side = Vector3.Cross(next - previous, towardViewer).normalized;
                float curtainWidth = 0.135f + Mathf.Sin(along * Tau - clock * 0.2f + filament * 0.4f) * 0.014f;
                float width = 0.028f * (1f - pulsarWeight - auroraWeight)
                    + 0.04f * pulsarWeight + curtainWidth * auroraWeight;
                int vertex = filament * verticesPerFilament + sample * 2;
                filamentVertices[vertex] = position - side * width * 0.5f;
                filamentVertices[vertex + 1] = position + side * width * 0.5f;
                float wave = 0.75f + 0.25f * Mathf.Sin(along * Tau * 2f - clock * 0.55f + filament * 0.27f);
                float ends = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(along) * Mathf.PI)), 0.7f);
                float height = filament / (float)(FilamentCount - 1);
                Color pulse = Color.Lerp(Amber, Ember, Mathf.Abs(height - 0.5f) * 1.5f);
                Color curtain = along < 0.46f ? Color.Lerp(Mint, Cyan, along / 0.46f)
                    : Color.Lerp(Cyan, Violet, (along - 0.46f) / 0.54f);
                Color color = PresentationColor(filament % 8 == 0 ? Amber : Cyan, pulse, curtain);
                float brightness = Mathf.Lerp(0.57f, 0.42f, auroraWeight);
                Color glow = Glow(color, revealed * energy * wave * Mathf.Lerp(ends, 1f, pulsarWeight) * brightness);
                filamentColors[vertex] = glow;
                filamentColors[vertex + 1] = glow;
                previous = position;
                position = next;
            }
        }
        filamentMesh.vertices = filamentVertices;
        filamentMesh.colors = filamentColors;
    }

    private Vector3 FilamentPoint(int filament, float along)
    {
        float longitude = filament * Tau / FilamentCount;
        float latitude = (along - 0.5f) * Mathf.PI;
        float twist = latitude * 0.7f + clock * 0.12f;
        float radius = Mathf.Cos(latitude) * (1.55f + 0.07f * Mathf.Sin(along * Tau * 3f + clock * 0.3f));
        Vector3 orbital = new(Mathf.Cos(longitude + twist) * radius, Mathf.Sin(latitude) * 1.7f,
            Mathf.Sin(longitude + twist) * radius);

        float height = Mathf.Lerp(-1.8f, 1.8f, filament / (float)(FilamentCount - 1));
        float throat = 0.24f + Mathf.Pow(Mathf.Abs(height) / 1.8f, 1.6f) * 1.08f;
        throat += 0.035f * Mathf.Sin(height * 4f - clock * 0.6f);
        Vector3 pulsar = new(Mathf.Cos(along * Tau + clock * 0.1f) * throat, height,
            Mathf.Sin(along * Tau + clock * 0.1f) * throat);

        float spread = filament / (float)(FilamentCount - 1) * 2f - 1f;
        float y = along * 3.4f - 1.7f;
        float taper = 0.82f + 0.18f * Mathf.Sin(Mathf.Clamp01(along) * Mathf.PI);
        Vector3 aurora = new((spread * 1.62f + Mathf.Sin(y * 1.5f + clock * 0.22f + spread * 2f) * 0.12f) * taper,
            y, Mathf.Sin(spread * 5f + clock * 0.17f + y * 0.65f) * 0.78f * taper);
        return Vector3.ClampMagnitude(orbital * (1f - pulsarWeight - auroraWeight)
            + pulsar * pulsarWeight + aurora * auroraWeight, 2.26f);
    }

    private void UpdateStars(Vector3 right, Vector3 up, float energy, float crescendo)
    {
        Quaternion rotation = Quaternion.Euler(0f, clock * 1.7f, 0f);
        for (int star = 0; star < CloudCount; star++)
        {
            float breath = 0.7f + 0.3f * Mathf.Sin(clock * 0.35f + star * 2.17f);
            Color tint = PresentationColor(star % 9 == 0 ? Amber : Cyan,
                star % 9 == 0 ? Ember : Amber, star % 3 == 0 ? Mint : Violet);
            WriteStar(star, rotation * cloudPositions[star], cloudSizes[star], right, up,
                Glow(tint, activation * breath * energy * 0.45f));
        }
        for (int orbit = 0; orbit < OrbitCount; orbit++)
        {
            float head = clock * (0.28f + orbit * 0.031f) * (orbit % 2 == 0 ? 1f : -1f) + orbit * 2.4f;
            float revealed = Mathf.Clamp01(activation * 3f - (orbit < 3 ? 0f : 1f));
            Color tint = PresentationColor(orbit % 3 == 0 ? Amber : Cyan, Amber,
                orbit % 3 == 0 ? Violet : Cyan);
            for (int tail = 0; tail < TrailLength; tail++)
            {
                float fade = 1f - tail / (float)TrailLength;
                float angle = head - tail * 0.021f * (orbit % 2 == 0 ? 1f : -1f);
                WriteStar(CloudCount + orbit * TrailLength + tail, OrbitPoint(orbit, angle),
                    Mathf.Lerp(0.008f, 0.061f, fade * fade), right, up, Glow(tint, revealed * fade * energy));
            }
        }
        float core = Mathf.Clamp01(activation * 3f - 2f);
        WriteStar(StarCount - 1, Vector3.zero, 0.12f + crescendo * 0.05f, right, up,
            Glow(PresentationColor(Cyan, Amber, Violet), core * energy * 0.8f));
        starsMesh.vertices = starVertices;
        starsMesh.colors = starColors;
    }

    private void WriteStar(int star, Vector3 position, float size, Vector3 right, Vector3 up, Color color)
    {
        int start = star * 9;
        starVertices[start] = position;
        starColors[start] = color;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 direction = corner == 0 ? right : corner == 1 ? up : corner == 2 ? -right : -up;
            starVertices[start + corner + 1] = position + direction * size * 0.24f;
            starVertices[start + corner + 5] = position + direction * size;
            starColors[start + corner + 1] = Glow(color, 0.55f);
            starColors[start + corner + 5] = Color.clear;
        }
    }

    private static Color Glow(Color color, float brightness) => new(color.r * brightness, color.g * brightness,
        color.b * brightness, 1f);

    private Color PresentationColor(Color orbital, Color pulsar, Color aurora) =>
        orbital * (1f - pulsarWeight - auroraWeight) + pulsar * pulsarWeight + aurora * auroraWeight;

    private void OnDestroy()
    {
        if (filamentMesh != null) Destroy(filamentMesh);
        if (starsMesh != null) Destroy(starsMesh);
        if (runtimeRoot != null) Destroy(runtimeRoot.gameObject);
    }
}
