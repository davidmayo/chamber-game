using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ChamberToolsWindow : EditorWindow
{
    [MenuItem("Window/Chamber Tools")]
    private static void Open()
    {
        ChamberToolsWindow window = GetWindow<ChamberToolsWindow>();
        window.titleContent = new GUIContent("Chamber Tools");
        window.minSize = new Vector2(380f, 500f);
        window.Show();
    }

    [MenuItem("Tools/Chamber/Toggle Shell Visibility %#v")]
    private static void ToggleShellFromMenu()
    {
        ChamberShellVisibilityController controller = FindController<ChamberShellVisibilityController>();
        if (controller == null)
        {
            Debug.LogWarning("No ChamberShellVisibilityController was found in the active scene.");
            return;
        }

        RecordShellUndo(controller, "Toggle Chamber Shell Opacity");
        controller.ToggleVisibility();
        FinishShellChange(controller);
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Chamber Tools");
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("CHAMBER TOOLS", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Editor-only preview/debug controls. Standalone builds always start from ChamberBuildDefaults.",
            MessageType.Info);
        EditorGUILayout.Space(6f);

        DrawShellSection();
        EditorGUILayout.Space(6f);
        DrawLightingSection();
        EditorGUILayout.Space(6f);
        DrawPositionerSection();
    }

    private static void DrawShellSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Scene Visualization", EditorStyles.boldLabel);

        ChamberShellVisibilityController controller = FindController<ChamberShellVisibilityController>();
        if (controller == null)
        {
            EditorGUILayout.HelpBox(
                "No generated chamber shell was found in the active scene.",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        float roomOpacity = EditorGUILayout.Slider(
            "Room walls",
            controller.RoomOpacityPercent,
            0f,
            100f);
        if (!Mathf.Approximately(roomOpacity, controller.RoomOpacityPercent))
        {
            RecordShellUndo(controller, "Change Room Wall Opacity");
            controller.SetRoomOpacityPercent(roomOpacity);
            FinishShellChange(controller);
        }

        float chamberOpacity = EditorGUILayout.Slider(
            "Chamber walls",
            controller.ChamberOpacityPercent,
            0f,
            100f);
        if (!Mathf.Approximately(chamberOpacity, controller.ChamberOpacityPercent))
        {
            RecordShellUndo(controller, "Change Chamber Wall Opacity");
            controller.SetChamberOpacityPercent(chamberOpacity);
            FinishShellChange(controller);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Near walls use the selected opacity; far walls remain fully opaque. At 0% this is the one-sided cutaway view. Physical shells always block shadow-enabled lights and movement.",
            MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private static void DrawLightingSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);

        MotionSensitiveChamberLights chamberLights =
            FindController<MotionSensitiveChamberLights>();
        FloodLightController floodLights = FindController<FloodLightController>();
        if (chamberLights == null || floodLights == null)
        {
            EditorGUILayout.HelpBox(
                "The generated chamber lighting controllers were not found.",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        ChamberLightMode mode = (ChamberLightMode)EditorGUILayout.EnumPopup(
            "Chamber lights",
            chamberLights.Mode);
        if (mode != chamberLights.Mode)
        {
            RecordUndo(chamberLights, "Change Chamber Light Mode");
            chamberLights.SetMode(mode);
            FinishChange(chamberLights);
        }

        float timeout = EditorGUILayout.Slider(
            "Chamber lights timeout",
            chamberLights.TimeoutSeconds,
            1f,
            120f);
        if (!Mathf.Approximately(timeout, chamberLights.TimeoutSeconds))
        {
            RecordUndo(chamberLights, "Change Chamber Light Timeout");
            chamberLights.SetTimeoutSeconds(timeout);
            FinishChange(chamberLights);
        }

        string remaining = chamberLights.Mode switch
        {
            ChamberLightMode.On => "∞ (forced on)",
            ChamberLightMode.Off => "0.0 s (forced off)",
            _ => $"{chamberLights.RemainingSeconds:0.0} s",
        };
        EditorGUILayout.LabelField("Chamber lights remaining", remaining);

        bool floodEnabled = EditorGUILayout.Toggle("Flood lights", floodLights.LightsOn);
        if (floodEnabled != floodLights.LightsOn)
        {
            RecordUndo(floodLights, "Toggle Flood Lights");
            floodLights.SetLightsOn(floodEnabled);
            FinishChange(floodLights);
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawPositionerSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Positioner", EditorStyles.boldLabel);

        TurntableController controller = FindController<TurntableController>();
        if (controller == null)
        {
            EditorGUILayout.HelpBox(
                "The generated turntable controller was not found.",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        float pan = EditorGUILayout.Slider(
            "Pan angle",
            controller.PanDegrees,
            controller.PanLimitsDegrees.x,
            controller.PanLimitsDegrees.y);
        if (!Mathf.Approximately(pan, controller.PanDegrees))
        {
            RecordUndo(controller, "Change Pan Angle");
            controller.SetPanDegrees(pan);
            FinishChange(controller);
        }

        float tilt = EditorGUILayout.Slider(
            "Tilt angle",
            controller.TiltDegrees,
            controller.TiltLimitsDegrees.x,
            controller.TiltLimitsDegrees.y);
        if (!Mathf.Approximately(tilt, controller.TiltDegrees))
        {
            RecordUndo(controller, "Change Tilt Angle");
            controller.SetTiltDegrees(tilt);
            FinishChange(controller);
        }

        float height = EditorGUILayout.Slider(
            "Height",
            controller.HeightMeters,
            controller.HeightLimitsMeters.x,
            controller.HeightLimitsMeters.y);
        if (!Mathf.Approximately(height, controller.HeightMeters))
        {
            RecordUndo(controller, "Change Positioner Height");
            controller.SetHeightMeters(height);
            FinishChange(controller);
        }

        EditorGUILayout.EndVertical();
    }

    private static void RecordShellUndo(
        ChamberShellVisibilityController controller,
        string description)
    {
        RecordUndo(controller, description);
    }

    private static void FinishShellChange(ChamberShellVisibilityController controller)
    {
        FinishChange(controller);
    }

    private static void RecordUndo(Component controller, string description)
    {
        if (!Application.isPlaying)
        {
            Undo.RegisterFullObjectHierarchyUndo(
                controller.transform.root.gameObject,
                description);
        }
    }

    private static void FinishChange(Component controller)
    {
        EditorUtility.SetDirty(controller);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        SceneView.RepaintAll();
    }

    private static T FindController<T>() where T : Component
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}
