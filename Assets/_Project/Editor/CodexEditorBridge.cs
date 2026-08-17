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
                WriteResponse(request.id, request.command, true, "Chamber geometry synchronized in place and Main.unity saved.",
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
                    "save_scene, hierarchy, capture_game_view, rebuild_chamber, enter_play_mode, " +
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

        activeTestRequestId = request.id;
        testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        testCallbacks = new TestCallbacks(request.id, request.command, mode);
        testRunnerApi.RegisterCallbacks(testCallbacks);
        testRunnerApi.Execute(new ExecutionSettings(new Filter { testMode = mode }));
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
        try
        {
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
        try
        {
            if (captureTopView)
            {
                camera.transform.position = new Vector3(0f, 12f, 0f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = 7f;
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
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(image);
        }
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
            WriteResponse(requestId, command, success,
                success ? "Unity tests completed successfully." : "Unity tests completed with failures.",
                string.Empty, details);
            activeTestRequestId = null;
            if (testRunnerApi != null) UnityEngine.Object.DestroyImmediate(testRunnerApi);
            testRunnerApi = null;
            testCallbacks = null;
            WriteStatus();
        }

        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
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
