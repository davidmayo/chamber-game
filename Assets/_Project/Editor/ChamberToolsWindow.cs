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
        window.minSize = new Vector2(300f, 180f);
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

        SetCutaway(controller, !controller.CutawayView);
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

        bool cutaway = EditorGUILayout.ToggleLeft(
            "Cutaway shell",
            controller.CutawayView);
        if (cutaway != controller.CutawayView)
        {
            SetCutaway(controller, cutaway);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Current mode",
            controller.CutawayView ? "Cutaway" : "Opaque");
        EditorGUILayout.HelpBox(
            controller.CutawayView
                ? "Near shell faces are camera-transparent. Inward-facing far surfaces remain visible. The volumetric shell still blocks light and movement."
                : "The complete volumetric chamber and containing-room shells are visible.",
            MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private static void SetCutaway(
        ChamberShellVisibilityController controller,
        bool cutaway)
    {
        if (!Application.isPlaying)
        {
            Undo.RegisterFullObjectHierarchyUndo(
                controller.gameObject,
                "Change Chamber Shell Visualization");
        }

        controller.SetCutawayView(cutaway);
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
