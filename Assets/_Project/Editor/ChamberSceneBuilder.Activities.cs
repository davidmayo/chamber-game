using UnityEngine;
using UnityEngine.UI;

public static partial class ChamberSceneBuilder
{
    private static void BuildSignalWatch(Transform root, FirstPersonPlayerController player,
        TurntableController table, SourceAntennaController source, ComputerConsoleController console)
    {
        Transform activity = NewGroup("Signal Watch", root);
        ChamberReferenceSignal signal = GetOrAddComponent<ChamberReferenceSignal>(activity.gameObject);
        Text readout = root.Find("Computer Console").GetComponentInChildren<Text>(true);
        signal.Configure(table, source, readout);
        root.GetComponentInChildren<SpectrumAnalyzerDisplay>(true).ConfigureSignal(signal);
        FacilityShiftController shift = GetOrAddComponent<FacilityShiftController>(activity.gameObject);
        GetOrAddComponent<FacilityShiftDisplay>(activity.gameObject).Configure(shift);

        Transform beam = NewGroup("Inspection Light", player.PlayerCamera.transform);
        beam.localPosition = new Vector3(0.16f, -0.12f, 0.05f);
        Light light = GetOrAddComponent<Light>(beam.gameObject);
        light.type = LightType.Spot;
        light.color = new Color(0.86f, 0.94f, 1f);
        light.intensity = 35f;
        light.range = 16f;
        light.spotAngle = 42f;
        light.innerSpotAngle = 26f;
        light.shadows = LightShadows.Hard;
        light.shadowBias = 0.02f;
        light.shadowNormalBias = 0.05f;
        light.enabled = false;
        GetOrAddComponent<FacilityPlayerEffects>(player.gameObject).Configure(player, light, null);

        Material signMaterial = GetMaterial("Wayfinding Ink", new Color(0.035f, 0.07f, 0.075f), 0f, 0.7f);
        Transform sign = NewGroup("Chamber Identification", root.Find("Containing Room"));
        sign.localPosition = new Vector3(-4.59f, 2.65f, 5.5f);
        sign.localRotation = Quaternion.Euler(0f, 90f, 0f);
        Box("Backplate", sign, Vector3.zero, new Vector3(1.15f, 0.34f, 0.035f), signMaterial);
        Text signText = CreateWorldDisplayText("Legend", sign, new Vector3(0f, 0f, -0.022f),
            1.08f, 0.28f, "01 / ANECHOIC CHAMBER", 66);
        signText.transform.parent.localRotation = Quaternion.identity;
        signText.raycastTarget = false;
    }

    private static void ConnectSignalWatch(Transform root, FirstPersonPlayerController player,
        ComputerConsoleController console)
    {
        Transform operations = GameObject.Find("Ground Ops Blockout").transform;
        FacilityPlayerEffects effects = player.GetComponent<FacilityPlayerEffects>();
        effects.Configure(player, player.PlayerCamera.transform.Find("Inspection Light").GetComponent<Light>(), operations);
        FacilityShiftController shift = root.GetComponentInChildren<FacilityShiftController>(true);
        shift.Configure(root.GetComponentInChildren<ChamberReferenceSignal>(true), console,
            operations.GetComponentInChildren<GroundOpsDishConsoleController>(true),
            operations.Find("Server Room Equipment/DSN Server Rack").GetComponentInChildren<SimpleSeatedConsoleController>(true),
            operations.GetComponentInChildren<GroundOpsDishController>(true),
            operations.GetComponentInChildren<GroundOpsSatelliteTarget>(true),
            operations.GetComponentInChildren<RidgeRecorderController>(true), effects);
        root.GetComponentInChildren<FacilityShiftDisplay>(true).ConfigureLaboratory(
            operations.GetComponentInChildren<NullLaboratoryController>(true));
        root.GetComponentInChildren<FacilityShiftDisplay>(true).ConfigureArchive(
            operations.GetComponentInChildren<SignalArchiveController>(true));
        root.GetComponentInChildren<FacilityShiftDisplay>(true).ConfigureSkunkWorks(
            operations.GetComponentInChildren<SkunkWorksCommissioning>(true));
    }
}
