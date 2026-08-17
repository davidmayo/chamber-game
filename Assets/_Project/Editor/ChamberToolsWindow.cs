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
        window.minSize = new Vector2(340f, 220f);
        window.Show();
    }

    [MenuItem("Tools/Chamber/Toggle Shell Visibility %#v")]
    private static void ToggleShellFromMenu()
    {
        ChamberShellVisibilityController controller = FindController();
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

        DrawShellSection();
    }

    private static void DrawShellSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Scene Visualization", EditorStyles.boldLabel);

        ChamberShellVisibilityController controller = FindController();
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

    private static void RecordShellUndo(
        ChamberShellVisibilityController controller,
        string description)
    {
        if (!Application.isPlaying)
        {
            Undo.RegisterFullObjectHierarchyUndo(
                controller.gameObject,
                description);
        }
    }

    private static void FinishShellChange(ChamberShellVisibilityController controller)
    {
        EditorUtility.SetDirty(controller);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        SceneView.RepaintAll();
    }

    private static ChamberShellVisibilityController FindController()
    {
        return Object.FindFirstObjectByType<ChamberShellVisibilityController>(
            FindObjectsInactive.Include);
    }
}
