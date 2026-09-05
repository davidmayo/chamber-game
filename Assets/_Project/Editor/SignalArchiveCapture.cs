using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// A repeatable photography pass through the actual running scene. Start a fresh
// Play session first: the injected demonstration state is discarded by exiting
// Play Mode on completion, cancellation, reload, or error. No scene is saved.
public static class SignalArchiveCapture
{
    private const int Width = 1920;
    private const int Height = 1080;
    private const int SettleFrames = 180;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] FileNames =
    {
        "afterglow-orbital.png", "afterglow-pulsar.png", "afterglow-aurora.png",
    };

    private static Action<bool, string, string> completed;
    private static SignalArchiveController archive;
    private static SignalArchiveSculpture sculpture;
    private static Camera camera;
    private static string outputFolder;
    private static int program;
    private static int frames;
    private static int previousFrame;
    private static float previousCaptureDeltaTime;
    private static double deadline;

    public static void Begin(Action<bool, string, string> onCompleted)
    {
        if (completed != null) throw new InvalidOperationException("An archive photography pass is already running.");
        if (!Application.isPlaying || EditorApplication.isPaused || RuntimeSceneSwitcher.IsOpen
            || !Mathf.Approximately(Time.timeScale, 1f))
            throw new InvalidOperationException("Start a fresh, unpaused Play session before capture_archive. It exits Play Mode when finished.");
        if (SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/Main.unity")
            throw new InvalidOperationException("Archive photography requires the continuous Main scene.");

        archive = Object.FindFirstObjectByType<SignalArchiveController>();
        if (archive == null || archive.PoweredCount != 0 || archive.CompletedProgramCount != 0
            || archive.IsPerforming || archive.SelectedProgram != 0)
            throw new InvalidOperationException("Start a fresh Play session with all archive receivers still isolated before capture_archive.");
        foreach (SimpleSeatedConsoleController seat in Object.FindObjectsByType<SimpleSeatedConsoleController>(FindObjectsSortMode.None))
            if (seat.IsSeated) throw new InvalidOperationException("The player must be standing before archive photography starts.");

        FirstPersonPlayerController player = ReadField<FirstPersonPlayerController>(archive, "player");
        Transform operations = ReadField<Transform>(archive, "operations");
        sculpture = ReadField<SignalArchiveSculpture>(archive, "sculpture");
        camera = player != null ? player.PlayerCamera : null;
        if (player == null || operations == null || sculpture == null || camera == null)
            throw new InvalidOperationException("The archive's player, facility transform, sculpture, and camera must be configured.");

        previousCaptureDeltaTime = Time.captureDeltaTime;
        completed = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        try
        {
            outputFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "screenshots"));
            Directory.CreateDirectory(outputFolder);
            deadline = EditorApplication.timeSinceStartup + 120d;
            Time.captureDeltaTime = 1f / 60f;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            player.enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = operations.TransformPoint(new Vector3(3.65f, -7.1f, 3.65f));
            Vector3 eye = operations.TransformPoint(new Vector3(3.65f, -5.35f, 3.65f));
            Vector3 target = operations.TransformPoint(new Vector3(0f, -4.55f, -0.4f));
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
            camera.fieldOfView = 76f;
            camera.aspect = Width / (float)Height;
            bool[] powered = ReadField<bool[]>(archive, "powered");
            for (int receiver = 0; receiver < powered.Length; receiver++) powered[receiver] = true;
            program = 0;
            BeginProgram();
        }
        catch (Exception exception)
        {
            Finish(false, "Archive photography failed: " + exception.Message);
        }
    }

    private static void BeginProgram()
    {
        // The normal controller drives the sculpture, room lights, and displays
        // for three seconds before the camera takes a picture of each recording.
        WriteProperty(archive, nameof(SignalArchiveController.SelectedProgram), program);
        WriteProperty(archive, nameof(SignalArchiveController.IsPerforming), true);
        WriteField(archive, "playbackElapsed", 6f);
        WriteField(sculpture, "clock", 0f);
        frames = 0;
        previousFrame = Time.frameCount;
    }

    private static void Update()
    {
        if (completed == null) return;
        try
        {
            if (!Application.isPlaying || archive == null || camera == null)
                throw new InvalidOperationException("The running scene was closed during photography.");
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("The archive did not finish its photography pass within 120 seconds.");
            if (EditorApplication.isPaused || RuntimeSceneSwitcher.IsOpen) return;
            int currentFrame = Time.frameCount;
            if (currentFrame == previousFrame) return;
            frames += currentFrame - previousFrame;
            previousFrame = currentFrame;
            if (frames < SettleFrames) return;

            Capture(Path.Combine(outputFolder, FileNames[program]));
            program++;
            if (program < FileNames.Length) BeginProgram();
            else Finish(true, "Captured three 1920x1080 live archive views; exiting Play Mode to discard demonstration state.");
        }
        catch (Exception exception)
        {
            Finish(false, "Archive photography failed: " + exception.Message);
        }
    }

    private static void Capture(string path)
    {
        RenderTexture target = new(Width, Height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new(Width, Height, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            // Camera rendering includes the world-space displays and live floor
            // reflection. Screen-space overlay HUDs stay out of these hero views.
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }

    private static T ReadField<T>(object target, string name) => (T)RequireField(target, name).GetValue(target);
    private static void WriteField(object target, string name, object value) => RequireField(target, name).SetValue(target, value);

    private static FieldInfo RequireField(object target, string name) => target.GetType().GetField(name, PrivateInstance)
        ?? throw new MissingFieldException(target.GetType().Name, name);

    private static void WriteProperty(object target, string name, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        MethodInfo setter = property?.GetSetMethod(true);
        if (setter == null) throw new MissingMemberException(target.GetType().Name, name);
        setter.Invoke(target, new[] { value });
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
            Finish(false, "Archive photography cancelled because Play Mode ended.");
    }

    private static void BeforeAssemblyReload() => Finish(false, "Archive photography cancelled by script reload.");

    private static void Finish(bool success, string message)
    {
        Action<bool, string, string> callback = completed;
        if (callback == null) return;
        completed = null;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        Time.captureDeltaTime = previousCaptureDeltaTime;
        try
        {
            callback(success, message, outputFolder);
        }
        finally
        {
            archive = null;
            sculpture = null;
            camera = null;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
