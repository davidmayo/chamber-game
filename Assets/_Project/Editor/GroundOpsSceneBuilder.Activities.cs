using UnityEngine;
using UnityEngine.UI;

public static partial class GroundOpsSceneBuilder
{
    private static void BuildActivityWayfinding(Transform hallway)
    {
        Transform signs = NewGroup("Wayfinding", hallway);
        BuildActivitySign(signs, "DOC Identification", new Vector3(4.15f, 2.7f, -5.59f),
            Vector3.back, "02 / DISH OPERATIONS", 1.65f);
        BuildActivitySign(signs, "Server Identification", new Vector3(5.67f, 2.65f, 6.25f),
            Vector3.right, "03 / SERVER ROOM\nDSN RACKS", 1.3f);
        BuildActivitySign(signs, "Ridge Access", new Vector3(-4.10f, 2.3f, -6.8f),
            Vector3.right, "RIDGE ACCESS\nF / TRUCK TRANSFER", 1.35f);
        BuildActivitySign(signs, "Hallway Directory", new Vector3(8.0f, 2.25f, 6.25f),
            Vector3.left, "SIGNAL WATCH\n< CHAMBER\nOPERATIONS / RIDGE >", 1.6f);
    }

    private static void BuildActivitySign(Transform parent, string name, Vector3 position,
        Vector3 facing, string legend, float width)
    {
        Transform sign = NewGroup(name, parent);
        sign.localPosition = position;
        sign.localRotation = Quaternion.LookRotation(-facing, Vector3.up);
        Material ink = GetMaterial("Wayfinding Ink", new Color(0.035f, 0.07f, 0.075f), 0f, 0.7f);
        Box("Backplate", sign, Vector3.zero, new Vector3(width, 0.48f, 0.035f), Quaternion.identity, ink);
        Text text = CreateWorldDisplayText("Legend", sign, new Vector3(0f, 0f, -0.023f),
            width - 0.06f, 0.41f, legend, 64, Quaternion.identity);
        text.color = new Color(0.75f, 0.95f, 0.88f);
        text.raycastTarget = false;
    }

    private static void BuildRidgeRecorder(Transform journey, FirstPersonPlayerController player, Vector3 truckStop)
    {
        Transform exit = journey.Find("Antenna Complex Player Exit");
        // A short walk from the truck, outside its interaction radius and route.
        Vector3 awayFromTruck = Vector3.ProjectOnPlane(exit.localPosition - truckStop, Vector3.up).normalized;
        Vector3 position = exit.localPosition + awayFromTruck * 5f;
        position.y = MountainHeight(position.x, position.z) + 0.13f;
        Transform recorder = NewGroup("Ridge Recorder 07", journey);
        recorder.localPosition = position;
        Vector3 facing = exit.localPosition - position;
        facing.y = 0f;
        recorder.localRotation = Quaternion.LookRotation(-facing.normalized, Vector3.up);
        Material metal = GetMaterial("Recorder Enclosure", new Color(0.28f, 0.34f, 0.35f), 0.55f, 0.35f);
        Material dark = GetMaterial("Recorder Panel", new Color(0.02f, 0.065f, 0.055f), 0f, 0.5f);
        Material yellow = GetMaterial("Recorder Safety Stripe", new Color(0.88f, 0.63f, 0.15f), 0f, 0.6f);
        Box("Concrete Plinth", recorder, new Vector3(0f, -0.03f, 0f),
            new Vector3(1.5f, 0.18f, 1.3f), Quaternion.identity, metal);
        Box("Pedestal", recorder, new Vector3(0f, 0.49f, 0f),
            new Vector3(0.22f, 0.96f, 0.26f), Quaternion.identity, metal);
        Box("Recorder Enclosure", recorder, new Vector3(0f, 1.19f, 0f),
            new Vector3(0.92f, 0.60f, 0.34f), Quaternion.identity, metal);
        Box("Display Face", recorder, new Vector3(0f, 1.19f, -0.177f),
            new Vector3(0.82f, 0.49f, 0.02f), Quaternion.identity, dark);
        Box("Safety Stripe", recorder, new Vector3(0f, 0.95f, -0.192f),
            new Vector3(0.80f, 0.035f, 0.012f), Quaternion.identity, yellow);
        Text status = CreateWorldDisplayText("Recorder Status", recorder,
            new Vector3(0f, 1.20f, -0.192f), 0.77f, 0.39f,
            "RIDGE RECORDER / 07\nLOCAL BUFFER READY\nF to copy snapshot", 72, Quaternion.identity);
        status.color = new Color(0.45f, 1f, 0.73f);
        status.raycastTarget = false;
        Transform interaction = NewGroup("Reader Position", recorder);
        Vector3 readerPosition = position + recorder.localRotation * new Vector3(0f, 0f, -1f);
        readerPosition.y = MountainHeight(readerPosition.x, readerPosition.z) + 0.12f;
        interaction.localPosition = Quaternion.Inverse(recorder.localRotation) * (readerPosition - position);
        GetOrAddComponent<RidgeRecorderController>(recorder.gameObject).Configure(player, interaction, status);
    }
}
