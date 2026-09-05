using UnityEngine;

// The campus keeps its own session record while the original Signal Watch
// assignment remains available back at the Space Science Center.
public sealed class SkunkWorksCommissioning : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private HeliosForgeController forge;
    [SerializeField] private VectorGardenController garden;
    public bool PlayerInArea => player!=null && SkunkWorksLayout.Contains(
        transform.InverseTransformPoint(player.PlayerCamera.transform.position)+SkunkWorksLayout.Origin);
    public string ObjectiveTitle => garden.Certified ? "FIRST LIGHT / FIELD CERTIFIED" : forge.Certified ? "FIRST LIGHT / CALIBRATE VECTOR" : "FIRST LIGHT / STABILIZE HELIOS";
    public string Guidance => garden.Certified ? "The source and field are certified. Proceed to the Horizon Engine."
        : forge.Certified ? "At each Vector anchor, F raises it and its neighbor. Match A 2 / B 1 / C 3."
        : "At the Helios bench: A / D phase, W / S containment. Match the reference and hold Space.";
    public string Measurement => forge.Certified ? $"VECTOR {garden.LevelA} : {garden.LevelB} : {garden.LevelC} / REF 2 : 1 : 3"
        : $"HELIOS UNCERTIFIED / STABILITY {forge.Stability01:P0}";
    public float CaptureProgress01 => forge.Certified ? garden.CaptureProgress01 : forge.CaptureProgress01;
    public string Notes => "FIRST LIGHT / SKUNK WORKS / LEVEL 02\n\n"
        +"A fictional campus for prototype commissioning. The measurements and machinery are imagined gameplay systems.\n\n"
        +"01 / HELIOS FORGE\nCertify the source at phase 126 degrees and containment 0.680. A / D changes phase; W / S changes containment. Shift gives fine control. Hold Space for three stable seconds.\n\n"
        +"02 / VECTOR GARDEN\nF advances an anchor and its next neighbor: A > B > C > A. Each wraps after level 3. Match heights 2 / 1 / 3 and allow the field to settle.\n\n"
        +Measurement+"\n\nF / Escape leaves a console. Tab closes notes. The truck waits on the arrival terrace for the return trip.";
    public void Configure(FirstPersonPlayerController controller,HeliosForgeController source,VectorGardenController field)
    { player=controller; forge=source; garden=field; }
}
