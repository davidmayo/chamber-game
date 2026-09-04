using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CodexEditorBridge
{
    private const string BridgeVersion = "1.0.0";
    private const string EnabledPreference = "ChamberGame.CodexBridge.Enabled";
    private const string TestRequestSessionKey = "ChamberGame.CodexBridge.TestRequest";
    private const double HeartbeatIntervalSeconds = 1.0;
    private const double PollIntervalSeconds = 0.2;

    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string BridgeRoot = Path.Combine(ProjectRoot, "Library", "CodexBridge");
    private static readonly string RequestFolder = Path.Combine(BridgeRoot, "Requests");
    private static readonly string ProcessingFolder = Path.Combine(BridgeRoot, "Processing");
    private static readonly string ResponseFolder = Path.Combine(BridgeRoot, "Responses");
    private static readonly string ArtifactFolder = Path.Combine(BridgeRoot, "Artifacts");
    private static readonly string StatusPath = Path.Combine(BridgeRoot, "status.json");
    private static readonly string PendingRefreshPath = Path.Combine(BridgeRoot, "pending-refresh.json");

    private static readonly List<string> CompilerDiagnostics = new();
    private static readonly Queue<string> RecentLogs = new();

    private static double nextHeartbeat;
    private static double nextPoll;
    private static string lastCommand = "startup";
    private static string lastError = string.Empty;
    private static string activeTestRequestId;
    private static TestRunnerApi testRunnerApi;
    private static TestCallbacks testCallbacks;

    static CodexEditorBridge()
    {
        EnsureFolders();
        EditorApplication.update += Update;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        Application.logMessageReceived += OnLogMessageReceived;
        // Play Mode tests reload the scripting domain. Restore the request and
        // callback so the client still receives the result after that reload.
        string pendingTest = SessionState.GetString(TestRequestSessionKey, string.Empty);
        if (!string.IsNullOrEmpty(pendingTest))
        {
            RegisterTestCallbacks(JsonUtility.FromJson<BridgeRequest>(pendingTest));
        }
        WriteStatus();
    }

    [MenuItem("Tools/Codex Bridge/Enabled", true)]
    private static bool ValidateToggleEnabled()
    {
        Menu.SetChecked("Tools/Codex Bridge/Enabled", IsEnabled);
        return true;
    }

    [MenuItem("Tools/Codex Bridge/Enabled")]
    private static void ToggleEnabled()
    {
        IsEnabled = !IsEnabled;
        lastCommand = IsEnabled ? "enabled" : "disabled";
        WriteStatus();
        Debug.Log($"Codex Editor Bridge {(IsEnabled ? "enabled" : "disabled")}.");
    }

    [MenuItem("Tools/Codex Bridge/Open Runtime Folder")]
    private static void OpenRuntimeFolder()
    {
        EnsureFolders();
        EditorUtility.RevealInFinder(BridgeRoot);
    }

    [MenuItem("Tools/Codex Bridge/Write Status Now")]
    private static void WriteStatusNow()
    {
        WriteStatus();
        Debug.Log($"Codex Editor Bridge status written to {StatusPath}.");
    }

    private static bool IsEnabled
    {
        get => EditorPrefs.GetBool(EnabledPreference, true);
        set => EditorPrefs.SetBool(EnabledPreference, value);
    }

    private static void Update()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now >= nextHeartbeat)
        {
            nextHeartbeat = now + HeartbeatIntervalSeconds;
            WriteStatus();
        }

        if (!IsEnabled || now < nextPoll)
        {
            return;
        }

        nextPoll = now + PollIntervalSeconds;
        TryCompletePendingRefresh();

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || activeTestRequestId != null)
        {
            return;
        }

        ProcessNextRequest();
    }

    private static void ProcessNextRequest()
    {
        string requestPath = Directory.GetFiles(RequestFolder, "*.json")
            .OrderBy(File.GetCreationTimeUtc)
            .FirstOrDefault();
        if (requestPath == null)
        {
            return;
        }

        string processingPath = Path.Combine(ProcessingFolder, Path.GetFileName(requestPath));
        try
        {
            File.Move(requestPath, processingPath);
        }
        catch (IOException)
        {
            return;
        }

        BridgeRequest request = null;
        bool responseDeferred = false;
        try
        {
            request = JsonUtility.FromJson<BridgeRequest>(File.ReadAllText(processingPath));
            if (request == null || string.IsNullOrWhiteSpace(request.id) || string.IsNullOrWhiteSpace(request.command))
            {
                throw new InvalidDataException("The request must contain non-empty id and command fields.");
            }

            request.command = request.command.Trim().ToLowerInvariant();
            lastCommand = request.command;
            lastError = string.Empty;
            responseDeferred = Execute(request);
        }
        catch (Exception exception)
        {
            lastError = exception.ToString();
            string id = request?.id ?? Path.GetFileNameWithoutExtension(processingPath);
            string command = request?.command ?? "invalid-request";
            WriteResponse(id, command, false, exception.Message, string.Empty, string.Empty);
            Debug.LogException(exception);
        }
        finally
        {
            if (!responseDeferred || File.Exists(PendingRefreshPath))
            {
                TryDelete(processingPath);
            }
            WriteStatus();
        }
    }

    // Returns true when the response will be completed asynchronously.
    private static bool Execute(BridgeRequest request)
    {
        switch (request.command)
        {
            case "ping":
                WriteResponse(request.id, request.command, true, "Unity Editor bridge is responsive.",
                    string.Empty, EditorStateJson());
                return false;

            case "editor_state":
                WriteResponse(request.id, request.command, true, "Editor state captured.",
                    string.Empty, EditorStateJson());
                return false;

            case "game_view_info":
                WriteGameViewInfo(request);
                return false;

            case "refresh":
                BeginRefresh(request);
                return true;

            case "save_scene":
                RequireSafeEditState(request);
                bool saved = EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
                WriteResponse(request.id, request.command, saved,
                    saved ? "Open scenes and assets saved." : "Unity could not save all open scenes.",
                    string.Empty, EditorStateJson());
                return false;

            case "hierarchy":
                WriteHierarchy(request);
                return false;

            case "audit_geometry":
                AuditGeneratedGeometry(request);
                return false;

            case "capture_game_view":
                CaptureGameView(request);
                return false;

            case "capture_scene_view":
                CaptureSceneView(request);
                return false;

            case "rebuild_chamber":
                RequireSafeEditState(request);
                if (!request.force && SceneManager.GetActiveScene().isDirty)
                {
                    throw new InvalidOperationException(
                        "The active scene has unsaved changes. Save it first or send the request with force=true.");
                }
                ChamberSceneBuilder.RebuildActiveMainSceneFromBridge();
                WriteResponse(request.id, request.command, true, "Continuous facility synchronized in place and Main.unity saved.",
                    string.Empty, EditorStateJson());
                return false;

            case "build_ground_ops":
                RequireSafeEditState(request);
                if (!request.force && SceneManager.GetActiveScene().isDirty)
                {
                    throw new InvalidOperationException(
                        "The active scene has unsaved changes. Save it first or send the request with force=true.");
                }
                GroundOpsSceneBuilder.SyncAndOpenSceneFromBridge();
                WriteResponse(request.id, request.command, true,
                    "Ground Ops blockout synchronized, saved, and opened.",
                    string.Empty, EditorStateJson());
                return false;

            case "enter_play_mode":
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    throw new InvalidOperationException("Unity is already playing or changing Play Mode state.");
                }
                EditorApplication.isPlaying = true;
                WriteResponse(request.id, request.command, true, "Requested Play Mode.",
                    string.Empty, EditorStateJson());
                return false;

            case "exit_play_mode":
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    throw new InvalidOperationException("Unity is not in Play Mode.");
                }
                EditorApplication.isPlaying = false;
                WriteResponse(request.id, request.command, true, "Requested exit from Play Mode.",
                    string.Empty, EditorStateJson());
                return false;

            case "run_tests":
                RequireSafeEditState(request);
                if (SceneManager.GetActiveScene().isDirty)
                {
                    throw new InvalidOperationException(
                        "The active scene has unsaved changes. Save it before running tests.");
                }
                BeginTests(request);
                return true;

            case "get_logs":
                WriteLogs(request);
                return false;

            case "clear_logs":
                RecentLogs.Clear();
                CompilerDiagnostics.Clear();
                WriteResponse(request.id, request.command, true, "Bridge-captured logs cleared.",
                    string.Empty, string.Empty);
                return false;

            default:
                throw new InvalidOperationException(
                    $"Unknown command '{request.command}'. Allowed commands: ping, editor_state, refresh, " +
                    "save_scene, hierarchy, audit_geometry, capture_game_view, rebuild_chamber, enter_play_mode, " +
                    "exit_play_mode, run_tests, get_logs, clear_logs.");
        }
    }

    private static void BeginRefresh(BridgeRequest request)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Asset refresh is disabled while entering or running Play Mode.");
        }

        CompilerDiagnostics.Clear();
        PendingRefresh pending = new()
        {
            id = request.id,
            command = request.command,
            notBeforeUtcTicks = DateTime.UtcNow.AddSeconds(2).Ticks,
        };
        WriteJsonAtomic(PendingRefreshPath, JsonUtility.ToJson(pending, true));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void TryCompletePendingRefresh()
    {
        if (!File.Exists(PendingRefreshPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        try
        {
            PendingRefresh pending = JsonUtility.FromJson<PendingRefresh>(File.ReadAllText(PendingRefreshPath));
            if (pending == null || DateTime.UtcNow.Ticks < pending.notBeforeUtcTicks)
            {
                return;
            }

            bool succeeded = CompilerDiagnostics.All(line => !line.StartsWith("Error", StringComparison.Ordinal));
            string details = CompilerDiagnostics.Count == 0
                ? "No compiler diagnostics."
                : string.Join("\n", CompilerDiagnostics);
            WriteResponse(pending.id, pending.command, succeeded,
                succeeded ? "Asset refresh and script compilation completed." : "Asset refresh completed with compiler errors.",
                string.Empty, details);
            TryDelete(PendingRefreshPath);
        }
        catch (Exception exception)
        {
            lastError = exception.ToString();
        }
    }

    private static void BeginTests(BridgeRequest request)
    {
        TestMode mode = string.Equals(request.argument, "play", StringComparison.OrdinalIgnoreCase)
            ? TestMode.PlayMode
            : TestMode.EditMode;

        SessionState.SetString(TestRequestSessionKey, JsonUtility.ToJson(request));
        RegisterTestCallbacks(request);
        try
        {
            testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            testRunnerApi.Execute(new ExecutionSettings(new Filter { testMode = mode }));
        }
        catch
        {
            ClearTestRequest();
            throw;
        }
    }

    private static void RegisterTestCallbacks(BridgeRequest request)
    {
        activeTestRequestId = request.id;
        TestMode mode = string.Equals(request.argument, "play", StringComparison.OrdinalIgnoreCase)
            ? TestMode.PlayMode
            : TestMode.EditMode;
        testCallbacks = new TestCallbacks(request.id, request.command, mode);
        TestRunnerApi.RegisterTestCallback(testCallbacks);
    }

    private static void ClearTestRequest()
    {
        SessionState.EraseString(TestRequestSessionKey);
        if (activeTestRequestId != null)
        {
            TryDelete(Path.Combine(ProcessingFolder, $"{activeTestRequestId}.json"));
        }
        activeTestRequestId = null;
        if (testCallbacks != null) TestRunnerApi.UnregisterTestCallback(testCallbacks);
        if (testRunnerApi != null) UnityEngine.Object.DestroyImmediate(testRunnerApi);
        testRunnerApi = null;
        testCallbacks = null;
    }

    private static void WriteHierarchy(BridgeRequest request)
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder output = new();
        output.AppendLine($"Scene: {scene.path}");
        output.AppendLine($"Dirty: {scene.isDirty}");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            AppendHierarchy(output, root.transform, 0);
        }

        string artifactPath = Path.Combine(ArtifactFolder, $"{request.id}-hierarchy.txt");
        File.WriteAllText(artifactPath, output.ToString(), Encoding.UTF8);
        WriteResponse(request.id, request.command, true, "Scene hierarchy captured.", artifactPath,
            $"{scene.rootCount} root objects; {CountSceneObjects(scene)} total GameObjects.");
    }

    private static void WriteGameViewInfo(BridgeRequest request)
    {
        Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        EditorWindow gameView = gameViewType == null
            ? null
            : Resources.FindObjectsOfTypeAll(gameViewType)
                .OfType<EditorWindow>()
                .OrderByDescending(window => window.hasFocus)
                .FirstOrDefault();
        if (gameView == null)
        {
            throw new InvalidOperationException("No open Game View was found.");
        }

        Rect rect = gameView.position;
        string details = JsonUtility.ToJson(new GameViewInfo
        {
            x = rect.x,
            y = rect.y,
            width = rect.width,
            height = rect.height,
            focused = gameView.hasFocus,
            title = gameView.titleContent.text,
        }, true);
        WriteResponse(request.id, request.command, true, "Game View bounds captured.",
            string.Empty, details);
    }

    private static void AppendHierarchy(StringBuilder output, Transform transform, int depth)
    {
        string components = string.Join(", ", transform.GetComponents<Component>()
            .Where(component => component != null)
            .Select(component => component.GetType().Name));
        Vector3 p = transform.localPosition;
        Vector3 r = transform.localEulerAngles;
        Vector3 s = transform.localScale;
        output.Append(' ', depth * 2)
            .Append(transform.gameObject.activeSelf ? "+ " : "- ")
            .Append(transform.name)
            .Append(SceneVisibilityManager.instance.IsHidden(transform.gameObject, false)
                ? " {SceneView hidden}"
                : string.Empty)
            .Append(" [").Append(components).Append("] ")
            .AppendFormat(CultureInfo.InvariantCulture,
                "pos=({0:0.###},{1:0.###},{2:0.###}) rot=({3:0.###},{4:0.###},{5:0.###}) scale=({6:0.###},{7:0.###},{8:0.###})",
                p.x, p.y, p.z, r.x, r.y, r.z, s.x, s.y, s.z)
            .AppendLine();

        foreach (Transform child in transform)
        {
            AppendHierarchy(output, child, depth + 1);
        }
    }

    private static int CountSceneObjects(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            count += root.GetComponentsInChildren<Transform>(true).Length;
        }
        return count;
    }

    private static void AuditGeneratedGeometry(BridgeRequest request)
    {
        Scene scene = SceneManager.GetActiveScene();
        List<Renderer> surfaces = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Where(renderer => renderer != null
                && renderer.enabled
                && renderer.gameObject.activeInHierarchy
                && renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                && renderer.sharedMaterial != null
                && renderer.sharedMaterial.renderQueue < 3000)
            .Where(renderer =>
            {
                string path = GetHierarchyPath(renderer.transform);
                bool architectural = path.StartsWith("Ground Ops Blockout/Architecture/", StringComparison.Ordinal)
                    || path.StartsWith("Chamber Geometry/Architecture/", StringComparison.Ordinal)
                    || path.StartsWith("Chamber Geometry/Containing Room/", StringComparison.Ordinal);
                bool decorativeJoin = path.Contains("/Curved Window/", StringComparison.Ordinal)
                    || path.Contains("/Frame/", StringComparison.Ordinal)
                    || path.Contains("/Ceiling Lights/", StringComparison.Ordinal);
                return architectural && !decorativeJoin;
            })
            .ToList();

        StringBuilder output = new();
        output.AppendLine($"Scene: {scene.path}");
        output.AppendLine($"Opaque architectural renderers checked: {surfaces.Count}");
        int issueCount = 0;
        const float planeTolerance = 0.0005f;
        const float overlapTolerance = 0.002f;
        for (int firstIndex = 0; firstIndex < surfaces.Count; firstIndex++)
        {
            Renderer first = surfaces[firstIndex];
            Bounds firstBounds = first.bounds;
            int firstAxis = ThinAxis(firstBounds.size);
            if (AxisSize(firstBounds.size, firstAxis) > 0.30f) continue;

            for (int secondIndex = firstIndex + 1; secondIndex < surfaces.Count; secondIndex++)
            {
                Renderer second = surfaces[secondIndex];
                Bounds secondBounds = second.bounds;
                int secondAxis = ThinAxis(secondBounds.size);
                if (firstAxis != secondAxis || AxisSize(secondBounds.size, secondAxis) > 0.30f)
                {
                    continue;
                }
                if (!HasCoplanarFace(firstBounds, secondBounds, firstAxis, planeTolerance)
                    || !HasProjectedAreaOverlap(firstBounds, secondBounds, firstAxis, overlapTolerance))
                {
                    continue;
                }

                issueCount++;
                output.AppendLine($"COPLANAR OVERLAP {issueCount}:");
                output.AppendLine($"  {GetHierarchyPath(first.transform)}");
                output.AppendLine($"  {GetHierarchyPath(second.transform)}");
            }
        }

        if (issueCount == 0)
        {
            output.AppendLine("No coplanar overlap was found among thin opaque architectural surfaces.");
        }

        string artifactPath = Path.Combine(ArtifactFolder, $"{request.id}-geometry-audit.txt");
        File.WriteAllText(artifactPath, output.ToString(), Encoding.UTF8);
        WriteResponse(request.id, request.command, issueCount == 0,
            issueCount == 0 ? "Generated geometry audit passed." : "Generated geometry audit found coplanar overlaps.",
            artifactPath, $"{issueCount} suspicious coplanar overlap(s).");
    }

    private static int ThinAxis(Vector3 size)
    {
        if (size.x <= size.y && size.x <= size.z) return 0;
        return size.y <= size.z ? 1 : 2;
    }

    private static float AxisSize(Vector3 value, int axis)
    {
        return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
    }

    private static bool HasCoplanarFace(Bounds first, Bounds second, int axis, float tolerance)
    {
        float firstMin = AxisSize(first.min, axis);
        float firstMax = AxisSize(first.max, axis);
        float secondMin = AxisSize(second.min, axis);
        float secondMax = AxisSize(second.max, axis);
        // Touching solids legitimately share opposite faces (max-to-min).
        // Z fighting is instead caused by surfaces occupying the same side of
        // the same plane, so only compare like faces here.
        return Mathf.Abs(firstMin - secondMin) <= tolerance
            || Mathf.Abs(firstMax - secondMax) <= tolerance;
    }

    private static bool HasProjectedAreaOverlap(Bounds first, Bounds second, int thinAxis, float tolerance)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == thinAxis) continue;
            float overlap = Mathf.Min(AxisSize(first.max, axis), AxisSize(second.max, axis))
                - Mathf.Max(AxisSize(first.min, axis), AxisSize(second.min, axis));
            if (overlap <= tolerance) return false;
        }
        return true;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        Stack<string> names = new();
        for (Transform current = transform; current != null; current = current.parent)
        {
            names.Push(current.name);
        }
        return string.Join("/", names);
    }

    private static void CaptureGameView(BridgeRequest request)
    {
        Camera camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            throw new InvalidOperationException("The active scene does not contain a camera.");
        }

        int width = request.width > 0 ? Mathf.Clamp(request.width, 64, 4096) : 1280;
        int height = request.height > 0 ? Mathf.Clamp(request.height, 64, 4096) : 720;
        RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new(width, height, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        Vector3 previousPosition = camera.transform.position;
        Quaternion previousRotation = camera.transform.rotation;
        float previousFieldOfView = camera.fieldOfView;
        try
        {
            string preset = request.argument?.Trim().ToLowerInvariant();
            if (preset == "hallway-seam")
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(new Vector3(6.8f, 1.6f, 6.5f));
                Vector3 target = groundOpsRoot.TransformPoint(new Vector3(6.8f, 1.45f, 12.5f));
                camera.transform.SetPositionAndRotation(
                    viewPosition,
                    Quaternion.LookRotation(target - viewPosition, Vector3.up));
                camera.fieldOfView = 72f;
            }
            else if (preset == "ground-ops-interior")
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(new Vector3(1.5f, 1.6f, -2.5f));
                Vector3 target = groundOpsRoot.TransformPoint(new Vector3(-2.5f, 2.6f, 3.5f));
                camera.transform.SetPositionAndRotation(
                    viewPosition,
                    Quaternion.LookRotation(target - viewPosition, Vector3.up));
                camera.fieldOfView = 72f;
            }
            else if (preset == "chamber-interior")
            {
                Vector3 viewPosition = new(1.8f, 1.55f, 4.35f);
                Vector3 target = new(-0.25f, 0.15f, -0.75f);
                camera.transform.SetPositionAndRotation(
                    viewPosition,
                    Quaternion.LookRotation(target - viewPosition, Vector3.up));
                camera.fieldOfView = 72f;
            }
            else if (preset == "high-bay-overlook")
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(new Vector3(7.45f, 1.65f, -1.9f));
                Vector3 target = groundOpsRoot.TransformPoint(new Vector3(28.7f, -4.35f, 9.5f));
                camera.transform.SetPositionAndRotation(
                    viewPosition,
                    Quaternion.LookRotation(target - viewPosition, Vector3.up));
                camera.fieldOfView = 72f;
            }
            else if (preset == "ridge-recorder")
            {
                Transform recorder = RequireGroundOpsRoot().Find(
                    "Exterior Landscape/Rail Truck Journey/Ridge Recorder 07");
                if (recorder == null) throw new InvalidOperationException("Sync the facility to create Recorder 07.");
                Vector3 viewPosition = recorder.TransformPoint(new Vector3(0f, 1.7f, -4f));
                camera.transform.SetPositionAndRotation(viewPosition,
                    Quaternion.LookRotation(recorder.TransformPoint(new Vector3(0f, 1.2f, 0f)) - viewPosition));
                camera.fieldOfView = 65f;
            }
            else if (preset.StartsWith("null-", StringComparison.Ordinal))
            {
                Transform operations = RequireGroundOpsRoot();
                Vector3 position;
                Vector3 target;
                switch (preset)
                {
                    case "null-stair": position = new Vector3(4.7f, 1.65f, 26.55f); target = new Vector3(-2.3f, -2.0f, 26.0f); break;
                    case "null-gallery": position = new Vector3(4.7f, -5.45f, 23.1f); target = new Vector3(4.5f, -5.45f, 13f); break;
                    case "null-cell": position = new Vector3(2.5f, -5.45f, 20f); target = new Vector3(-0.7f, -5.25f, 21.8f); break;
                    default: position = new Vector3(2.5f, -5.45f, 14.7f); target = new Vector3(-0.5f, -5.5f, 20.5f); break;
                }
                camera.transform.SetPositionAndRotation(operations.TransformPoint(position),
                    Quaternion.LookRotation(operations.TransformDirection(target - position)));
                camera.fieldOfView = 70f;
            }
            else if (preset == "hallway-directory")
            {
                Transform operations = RequireGroundOpsRoot();
                camera.transform.SetPositionAndRotation(
                    operations.TransformPoint(new Vector3(6f, 1.7f, 6.25f)),
                    operations.rotation * Quaternion.Euler(-12f, 90f, 0f));
                camera.fieldOfView = 65f;
            }
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            string artifactPath = Path.Combine(ArtifactFolder, $"{request.id}-game.png");
            File.WriteAllBytes(artifactPath, image.EncodeToPNG());
            WriteResponse(request.id, request.command, true, $"Captured {width}x{height} camera image.",
                artifactPath, camera.name);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
            camera.fieldOfView = previousFieldOfView;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void CaptureSceneView(BridgeRequest request)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null || sceneView.camera == null)
        {
            throw new InvalidOperationException("No active Scene View is available to capture.");
        }

        Camera camera = sceneView.camera;
        bool captureTopView = string.Equals(
            request.argument?.Trim(), "top", StringComparison.OrdinalIgnoreCase);
        bool captureGroundOpsWindow = string.Equals(
            request.argument?.Trim(), "ground-ops-window", StringComparison.OrdinalIgnoreCase);
        bool captureGroundOpsConsole = string.Equals(
            request.argument?.Trim(), "ground-ops-console", StringComparison.OrdinalIgnoreCase);
        bool captureGroundOpsDsnRack = string.Equals(
            request.argument?.Trim(), "ground-ops-dsn", StringComparison.OrdinalIgnoreCase);
        bool captureFacilityHallway = string.Equals(
            request.argument?.Trim(), "facility-hallway", StringComparison.OrdinalIgnoreCase);
        bool captureChamberDoor = string.Equals(
            request.argument?.Trim(), "chamber-door", StringComparison.OrdinalIgnoreCase);
        bool captureChamberInterior = string.Equals(
            request.argument?.Trim(), "chamber-interior", StringComparison.OrdinalIgnoreCase);
        bool captureHallwayDoor = string.Equals(
            request.argument?.Trim(), "hallway-door", StringComparison.OrdinalIgnoreCase);
        bool captureHallwayHeader = string.Equals(
            request.argument?.Trim(), "hallway-header", StringComparison.OrdinalIgnoreCase);
        bool captureFacilityPlan = string.Equals(
            request.argument?.Trim(), "facility-plan", StringComparison.OrdinalIgnoreCase);
        bool captureHallwaySeam = string.Equals(
            request.argument?.Trim(), "hallway-seam", StringComparison.OrdinalIgnoreCase);
        bool captureHallwayL = string.Equals(
            request.argument?.Trim(), "hallway-l", StringComparison.OrdinalIgnoreCase);
        bool captureHighBay = string.Equals(
            request.argument?.Trim(), "high-bay-overlook", StringComparison.OrdinalIgnoreCase);
        bool captureRailTruckRoute = string.Equals(
            request.argument?.Trim(), "rail-truck-route", StringComparison.OrdinalIgnoreCase);
        bool captureRailTruckStart = string.Equals(
            request.argument?.Trim(), "rail-truck-start", StringComparison.OrdinalIgnoreCase);
        bool captureRailTruckCab = string.Equals(
            request.argument?.Trim(), "rail-truck-cab", StringComparison.OrdinalIgnoreCase);
        bool captureBuildingExterior = string.Equals(
            request.argument?.Trim(), "building-exterior", StringComparison.OrdinalIgnoreCase);
        bool captureBuildingWest = string.Equals(
            request.argument?.Trim(), "building-west", StringComparison.OrdinalIgnoreCase);
        int width = request.width > 0 ? Mathf.Clamp(request.width, 64, 4096) : 1280;
        int height = request.height > 0 ? Mathf.Clamp(request.height, 64, 4096) : 720;
        RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new(width, height, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        Vector3 previousPosition = camera.transform.position;
        Quaternion previousRotation = camera.transform.rotation;
        bool previousOrthographic = camera.orthographic;
        float previousOrthographicSize = camera.orthographicSize;
        float previousFieldOfView = camera.fieldOfView;
        try
        {
            if (captureTopView)
            {
                Bounds sceneBounds = CalculateSceneRendererBounds();
                float aspect = width / (float)height;
                float framedSize = Mathf.Max(
                    sceneBounds.extents.z,
                    sceneBounds.extents.x / aspect) * 1.12f;
                camera.transform.position = new Vector3(
                    sceneBounds.center.x,
                    sceneBounds.max.y + 12f,
                    sceneBounds.center.z);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(1f, framedSize);
            }
            else if (captureGroundOpsWindow)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(
                    new Vector3(-1.5f, 1.65f, -2.5f));
                Vector3 ridgeTarget = groundOpsRoot.TransformPoint(
                    new Vector3(-55.2f, 12.8f, 37.8f));
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(
                    ridgeTarget - viewPosition,
                    Vector3.up);
                camera.orthographic = false;
            }
            else if (captureGroundOpsConsole)
            {
                GameObject poseObject = GameObject.Find(
                    "Ground Ops Blockout/Furniture Blockout/Hardware Control Station/Seated Camera Pose");
                if (poseObject == null)
                {
                    throw new InvalidOperationException(
                        "The Ground Ops front-left seated camera pose was not found.");
                }
                camera.transform.SetPositionAndRotation(
                    poseObject.transform.position,
                    poseObject.transform.rotation);
                camera.orthographic = false;
                camera.fieldOfView = 68f;
            }
            else if (captureGroundOpsDsnRack)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(
                    new Vector3(-2.49f, 1.30f, 5.55f));
                Vector3 rackTarget = groundOpsRoot.TransformPoint(
                    new Vector3(-2.49f, 1.18f, 6.83f));
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(
                    rackTarget - viewPosition,
                    Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 58f;
            }
            else if (captureFacilityHallway)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(
                    new Vector3(7.0f, 1.65f, 11.25f));
                Vector3 chamberTarget = new(-2.5f, 1.4f, 5.5f);
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(
                    chamberTarget - viewPosition,
                    Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 68f;
            }
            else if (captureChamberDoor)
            {
                Vector3 viewPosition = new(-3.8f, 1.6f, 2.5f);
                Vector3 target = new(-1.8f, 1.45f, 2.5f);
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(target - viewPosition, Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 68f;
            }
            else if (captureChamberInterior)
            {
                Vector3 viewPosition = new(1.8f, 1.55f, 4.35f);
                Vector3 target = new(-0.25f, 0.15f, -0.75f);
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(target - viewPosition, Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 72f;
            }
            else if (captureHallwayDoor)
            {
                Vector3 viewPosition = new(-6.1f, 1.6f, 5.5f);
                Vector3 target = new(-3.6f, 1.45f, 5.5f);
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(target - viewPosition, Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 68f;
            }
            else if (captureHallwayHeader)
            {
                Vector3 viewPosition = new(-7.2f, 6.7f, 5.5f);
                Vector3 target = new(-4.5f, 5.45f, 5.5f);
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(target - viewPosition, Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 55f;
            }
            else if (captureFacilityPlan)
            {
                camera.transform.position = new Vector3(-9f, 45f, 7f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = 21f;
            }
            else if (captureHallwaySeam)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(new Vector3(6.8f, 1.6f, 6.5f));
                Vector3 target = groundOpsRoot.TransformPoint(new Vector3(6.8f, 1.45f, 12.5f));
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(target - viewPosition, Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 72f;
            }
            else if (captureHallwayL)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(new Vector3(-4.2f, 1.6f, -6.8f));
                Vector3 target = groundOpsRoot.TransformPoint(new Vector3(7.2f, 1.45f, -6.8f));
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(target - viewPosition, Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 72f;
            }
            else if (captureHighBay)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(new Vector3(7.45f, 1.65f, -1.9f));
                Vector3 target = groundOpsRoot.TransformPoint(new Vector3(28.7f, -4.35f, 9.5f));
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(target - viewPosition, Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 72f;
            }
            else if (captureRailTruckRoute)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                camera.transform.position = groundOpsRoot.TransformPoint(
                    new Vector3(-35f, 58f, 16f));
                camera.transform.rotation = groundOpsRoot.rotation * Quaternion.Euler(90f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = 39f;
            }
            else if (captureRailTruckStart)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(
                    new Vector3(-22.0f, 4.5f, -18.0f));
                Vector3 target = groundOpsRoot.TransformPoint(
                    new Vector3(-9.5f, -3.8f, -5.5f));
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(
                    target - viewPosition,
                    Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 62f;
            }
            else if (captureRailTruckCab)
            {
                GameObject poseObject = GameObject.Find(
                    "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Rail Truck/Driver Camera Pose");
                if (poseObject == null)
                {
                    throw new InvalidOperationException(
                        "The generated rail-truck driver camera pose was not found.");
                }
                camera.transform.SetPositionAndRotation(
                    poseObject.transform.position,
                    poseObject.transform.rotation);
                camera.orthographic = false;
                camera.fieldOfView = 68f;
            }
            else if (captureBuildingExterior)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(
                    new Vector3(-20f, 10f, -35f));
                Vector3 target = groundOpsRoot.TransformPoint(
                    new Vector3(18f, -1.5f, 0f));
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(
                    target - viewPosition,
                    Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 58f;
            }
            else if (captureBuildingWest)
            {
                Transform groundOpsRoot = RequireGroundOpsRoot();
                Vector3 viewPosition = groundOpsRoot.TransformPoint(
                    new Vector3(-28f, 0.5f, -0.5f));
                Vector3 target = groundOpsRoot.TransformPoint(
                    new Vector3(-4.4f, -0.25f, -0.5f));
                camera.transform.position = viewPosition;
                camera.transform.rotation = Quaternion.LookRotation(
                    target - viewPosition,
                    Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 58f;
            }
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            string artifactPath = Path.Combine(ArtifactFolder, $"{request.id}-scene.png");
            File.WriteAllBytes(artifactPath, image.EncodeToPNG());
            WriteResponse(request.id, request.command, true,
                $"Captured {width}x{height} Scene View image.", artifactPath, sceneView.titleContent.text);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.transform.position = previousPosition;
            camera.transform.rotation = previousRotation;
            camera.orthographic = previousOrthographic;
            camera.orthographicSize = previousOrthographicSize;
            camera.fieldOfView = previousFieldOfView;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static Transform RequireGroundOpsRoot()
    {
        GameObject root = GameObject.Find("Ground Ops Blockout");
        if (root == null)
        {
            throw new InvalidOperationException("The Ground Ops region was not found.");
        }
        return root.transform;
    }

    private static Bounds CalculateSceneRendererBounds()
    {
        Scene scene = SceneManager.GetActiveScene();
        Renderer[] renderers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
            .ToArray();
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, new Vector3(10f, 1f, 10f));
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }
        return bounds;
    }

    private static void WriteLogs(BridgeRequest request)
    {
        StringBuilder output = new();
        output.AppendLine("Compiler diagnostics:");
        foreach (string diagnostic in CompilerDiagnostics) output.AppendLine(diagnostic);
        output.AppendLine();
        output.AppendLine("Recent Unity logs captured by the bridge:");
        foreach (string line in RecentLogs) output.AppendLine(line);

        string artifactPath = Path.Combine(ArtifactFolder, $"{request.id}-logs.txt");
        File.WriteAllText(artifactPath, output.ToString(), Encoding.UTF8);
        WriteResponse(request.id, request.command, true, "Bridge logs captured.", artifactPath,
            $"{CompilerDiagnostics.Count} compiler diagnostics; {RecentLogs.Count} log entries.");
    }

    private static void RequireSafeEditState(BridgeRequest request)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException($"'{request.command}' is disabled during Play Mode.");
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            throw new InvalidOperationException($"'{request.command}' is disabled while Unity compiles or imports assets.");
        }
    }

    private static string EditorStateJson()
    {
        return JsonUtility.ToJson(BuildStatus(), true);
    }

    private static BridgeStatus BuildStatus()
    {
        Scene scene = SceneManager.GetActiveScene();
        return new BridgeStatus
        {
            version = BridgeVersion,
            enabled = IsEnabled,
            timestampUtc = DateTime.UtcNow.ToString("O"),
            projectPath = ProjectRoot,
            activeScene = scene.path,
            sceneDirty = scene.isDirty,
            isPlaying = EditorApplication.isPlaying,
            isPaused = EditorApplication.isPaused,
            isCompiling = EditorApplication.isCompiling,
            isUpdating = EditorApplication.isUpdating,
            pendingRequests = Directory.Exists(RequestFolder) ? Directory.GetFiles(RequestFolder, "*.json").Length : 0,
            activeTestRequest = activeTestRequestId ?? string.Empty,
            compilerDiagnosticCount = CompilerDiagnostics.Count,
            lastCommand = lastCommand,
            lastError = lastError,
        };
    }

    private static void WriteStatus()
    {
        try
        {
            EnsureFolders();
            WriteJsonAtomic(StatusPath, JsonUtility.ToJson(BuildStatus(), true));
        }
        catch (Exception exception)
        {
            lastError = exception.ToString();
        }
    }

    private static void WriteResponse(
        string id,
        string command,
        bool success,
        string message,
        string artifactPath,
        string details)
    {
        BridgeResponse response = new()
        {
            id = id,
            command = command,
            success = success,
            message = message,
            timestampUtc = DateTime.UtcNow.ToString("O"),
            artifactPath = artifactPath,
            details = details,
        };
        string responsePath = Path.Combine(ResponseFolder, $"{id}.json");
        WriteJsonAtomic(responsePath, JsonUtility.ToJson(response, true));
    }

    private static void WriteJsonAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
        if (File.Exists(path)) File.Delete(path);
        File.Move(temporaryPath, path);
    }

    private static void EnsureFolders()
    {
        Directory.CreateDirectory(RequestFolder);
        Directory.CreateDirectory(ProcessingFolder);
        Directory.CreateDirectory(ResponseFolder);
        Directory.CreateDirectory(ArtifactFolder);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // A later editor update can clean up transient files.
        }
    }

    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        foreach (CompilerMessage message in messages)
        {
            string line = $"{message.type}: {message.file}({message.line},{message.column}): {message.message}";
            CompilerDiagnostics.Add(line);
        }
    }

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        string line = $"{DateTime.UtcNow:O} [{type}] {condition}";
        RecentLogs.Enqueue(line);
        while (RecentLogs.Count > 200) RecentLogs.Dequeue();
    }

    private sealed class TestCallbacks : ICallbacks
    {
        private readonly string requestId;
        private readonly string command;
        private readonly TestMode mode;

        public TestCallbacks(string requestId, string command, TestMode mode)
        {
            this.requestId = requestId;
            this.command = command;
            this.mode = mode;
        }

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            bool success = result.FailCount == 0;
            string details = $"mode={mode}; pass={result.PassCount}; fail={result.FailCount}; " +
                             $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}";
            string artifactPath = Path.Combine(ArtifactFolder, $"{requestId}-tests.xml");
            TestRunnerApi.SaveResultToFile(result, artifactPath);
            WriteResponse(requestId, command, success,
                success ? "Unity tests completed successfully." : "Unity tests completed with failures.",
                artifactPath, details);
            ClearTestRequest();
            WriteStatus();
        }

        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
    }

    [Serializable]
    private sealed class GameViewInfo
    {
        public float x;
        public float y;
        public float width;
        public float height;
        public bool focused;
        public string title;
    }

    [Serializable]
    private sealed class BridgeRequest
    {
        public string id;
        public string command;
        public string argument;
        public bool force;
        public int width;
        public int height;
    }

    [Serializable]
    private sealed class BridgeResponse
    {
        public string id;
        public string command;
        public bool success;
        public string message;
        public string timestampUtc;
        public string artifactPath;
        public string details;
    }

    [Serializable]
    private sealed class BridgeStatus
    {
        public string version;
        public bool enabled;
        public string timestampUtc;
        public string projectPath;
        public string activeScene;
        public bool sceneDirty;
        public bool isPlaying;
        public bool isPaused;
        public bool isCompiling;
        public bool isUpdating;
        public int pendingRequests;
        public string activeTestRequest;
        public int compilerDiagnosticCount;
        public string lastCommand;
        public string lastError;
    }

    [Serializable]
    private sealed class PendingRefresh
    {
        public string id;
        public string command;
        public long notBeforeUtcTicks;
    }
}
