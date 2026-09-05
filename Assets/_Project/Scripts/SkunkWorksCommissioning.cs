using UnityEngine;

// The campus keeps its own session record while the original Signal Watch
// assignment remains available back at the Space Science Center.
public sealed class SkunkWorksCommissioning : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private HeliosForgeController forge;
    public bool PlayerInArea => player!=null && SkunkWorksLayout.Contains(
        transform.InverseTransformPoint(player.PlayerCamera.transform.position)+SkunkWorksLayout.Origin);
    public string ObjectiveTitle => forge.Certified ? "FIRST LIGHT / POWER SOURCE CERTIFIED" : "FIRST LIGHT / STABILIZE HELIOS";
    public string Guidance => forge.Certified ? "The Helios power bus is online. Continue commissioning the prototype systems."
        : "At the Helios bench: A / D phase, W / S containment. Match the reference and hold Space.";
    public string Measurement => $"HELIOS {(forge.Certified?"ONLINE":"UNCERTIFIED")} / STABILITY {forge.Stability01:P0}";
    public float CaptureProgress01 => forge.CaptureProgress01;
    public string Notes => "FIRST LIGHT / SKUNK WORKS / LEVEL 02\n\n"
        +"A fictional campus for prototype commissioning. The measurements and machinery are imagined gameplay systems.\n\n"
        +"01 / HELIOS FORGE\nCertify the source at phase 126 degrees and containment 0.680. A / D changes phase; W / S changes containment. Shift gives fine control. Hold Space for three stable seconds.\n\n"
        +"Three lab wings surround the commissioning hall. Follow their colored light guides.\n\n"
        +Measurement+"\n\nF / Escape leaves a console. Tab closes notes. The truck waits on the arrival terrace for the return trip.";
    public void Configure(FirstPersonPlayerController controller,HeliosForgeController source) { player=controller; forge=source; }
}
