using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ChamberToolsWindow : EditorWindow
{
    [MenuItem("Window/Scene Tools")]
    private static void Open()
    {
        ChamberToolsWindow window = GetWindow<ChamberToolsWindow>();
        window.titleContent = new GUIContent("Scene Tools");
        window.minSize = new Vector2(380f, 500f);
        window.Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Scene Tools");
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("SCENE TOOLS", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Editor-only preview/debug controls. Walls, floors, and ceilings use ordinary opaque rendering; generated ceilings alone are hidden in Scene View.",
            MessageType.Info);
        EditorGUILayout.Space(6f);
        if (FindController<GroundOpsSkyController>() != null)
        {
            DrawSkySection();
            EditorGUILayout.Space(6f);
        }
        if (FindController<GroundOpsCeilingLightsController>() != null)
        {
            DrawGroundOpsLightingSection();
            EditorGUILayout.Space(6f);
        }
        if (FindController<GroundOpsSatelliteTarget>() != null)
        {
            DrawGroundOpsSatelliteTargetSection();
            EditorGUILayout.Space(6f);
        }
        if (FindController<RailTruckController>() != null)
        {
            DrawRailTruckSection();
            EditorGUILayout.Space(6f);
        }
        if (FindController<MotionSensitiveChamberLights>() != null)
        {
            DrawLightingSection();
            EditorGUILayout.Space(6f);
            DrawPositionerSection();
        }
    }

    private static void DrawSkySection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Ground Ops Sky", EditorStyles.boldLabel);

        GroundOpsSkyController controller = FindController<GroundOpsSkyController>();
        if (controller == null)
        {
            EditorGUILayout.HelpBox(
                "The generated Ground Ops sky controller was not found.",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.HelpBox(
            "Local civil time at the DOC. Eastern Standard/Daylight Time is selected automatically.",
            MessageType.Info);
        EditorGUI.BeginChangeCheck();
        int year = EditorGUILayout.IntField("Year", controller.Year);
        int month = EditorGUILayout.IntSlider("Month", controller.Month, 1, 12);
        int day = EditorGUILayout.IntSlider("Day", controller.Day, 1, 31);
        int hour = EditorGUILayout.IntSlider("Hour (0-23)", controller.Hour, 0, 23);
        int minute = EditorGUILayout.IntSlider("Minute", controller.Minute, 0, 59);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo(controller, "Change Ground Ops Date and Time");
            controller.SetLocalDateTime(year, month, day, hour, minute);
            FinishChange(controller);
        }

        if (GUILayout.Button("Use Current Local Date and Time"))
        {
            RecordUndo(controller, "Set Current Ground Ops Date and Time");
            controller.SetToCurrentLocalTime();
            FinishChange(controller);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Time zone", controller.TimeZoneAbbreviation);
        EditorGUILayout.LabelField("Sun azimuth", $"{controller.SolarAzimuthDegrees:0.00}° true");
        EditorGUILayout.LabelField("Sun elevation", $"{controller.SolarElevationDegrees:+0.00;-0.00;0.00}°");
        EditorGUILayout.EndVertical();
    }

    private static void DrawGroundOpsLightingSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Ground Ops Lighting", EditorStyles.boldLabel);
        GroundOpsCeilingLightsController controller =
            FindController<GroundOpsCeilingLightsController>();
        if (controller == null)
        {
            EditorGUILayout.HelpBox(
                "The generated Ground Ops ceiling-light controller was not found.",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        bool lightsOn = EditorGUILayout.Toggle("Ceiling lights", controller.LightsOn);
        if (lightsOn != controller.LightsOn)
        {
            RecordUndo(controller, "Toggle Ground Ops Ceiling Lights");
            controller.SetLightsOn(lightsOn);
            FinishChange(controller);
        }
        EditorGUILayout.EndVertical();
    }

    private static void DrawGroundOpsSatelliteTargetSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Ground Ops Target", EditorStyles.boldLabel);
        GroundOpsSatelliteTarget target = FindController<GroundOpsSatelliteTarget>();
        if (target == null)
        {
            EditorGUILayout.HelpBox(
                "The generated Ground Ops satellite target was not found.",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.HelpBox(
            "Azimuth is clockwise from true north. Power is the satellite's published minimum X-band EIRP, not received power at the DOC.",
            MessageType.Info);
        EditorGUI.BeginChangeCheck();
        string name = EditorGUILayout.TextField("Name", target.TargetName);
        float azimuth = EditorGUILayout.FloatField("Azimuth (deg)", target.AzimuthDegrees);
        float elevation = EditorGUILayout.FloatField("Elevation (deg)", target.ElevationDegrees);
        float range = EditorGUILayout.FloatField("Range (km)", target.RangeKilometers);
        float frequency = EditorGUILayout.FloatField("Frequency (MHz)", target.FrequencyMegahertz);
        float power = EditorGUILayout.FloatField("Power (dBmi EIRP)", target.PowerDbmiEirp);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo(target, "Change Ground Ops Satellite Target");
            target.Configure(name, azimuth, elevation, range, frequency, power);
            FinishChange(target);
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawRailTruckSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Rail Truck Journey", EditorStyles.boldLabel);
        RailTruckController controller = FindController<RailTruckController>();
        if (controller == null)
        {
            EditorGUILayout.HelpBox(
                "The generated rail-truck controller was not found.",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField("Route length", $"{controller.RouteLengthMeters:0.0} m");
        EditorGUILayout.LabelField("Drive speed", $"{controller.SpeedMetersPerSecond:0.0} m/s");
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Journey state", controller.StateName);
            EditorGUILayout.LabelField("Progress", $"{controller.Progress01 * 100f:0}%");
        }
        else
        {
            EditorGUILayout.HelpBox(
                "The preview moves only the truck in Edit Mode. Play Mode always starts it at the DOC end of the route.",
                MessageType.Info);
            float preview = EditorGUILayout.Slider(
                "Route preview",
                controller.EditorPreviewProgress,
                0f,
                1f);
            if (!Mathf.Approximately(preview, controller.EditorPreviewProgress))
            {
                RecordUndo(controller, "Preview Rail Truck Route");
                controller.SetEditorPreviewProgress(preview);
                FinishChange(controller);
            }
        }

        bool drawRoute = EditorGUILayout.Toggle(
            "Show route gizmo",
            controller.DrawRouteGizmos);
        if (drawRoute != controller.DrawRouteGizmos)
        {
            RecordUndo(controller, "Toggle Rail Truck Route Gizmo");
            controller.SetDrawRouteGizmos(drawRoute);
            FinishChange(controller);
        }
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
