using UnityEngine;
using UnityEngine.UI;

// Independent session record: the original facility assignment is retained
// when the player takes the truck to this campus and returns.
public sealed class SkunkWorksCommissioning : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private HeliosForgeController forge;
    [SerializeField] private VectorGardenController garden;
    [SerializeField] private HorizonEngineController horizon;
    [SerializeField] private Transform emblem;
    [SerializeField] private Text[] records;
    private float displayTimer;
    public bool PlayerInArea => player!=null && SkunkWorksLayout.Contains(
        transform.InverseTransformPoint(player.PlayerCamera.transform.position)+SkunkWorksLayout.Origin);
    public int CompletedCount => (forge.Certified?1:0)+(garden.Certified?1:0)+(horizon.Completed?1:0);
    public string ObjectiveTitle => horizon.Completed ? "FIRST LIGHT / CAMPUS COMMISSIONED" : garden.Certified ? "FIRST LIGHT / ALIGN HORIZON"
        : forge.Certified ? "FIRST LIGHT / CALIBRATE VECTOR" : "FIRST LIGHT / STABILIZE HELIOS";
    public string Guidance => horizon.IsPerforming ? "First Light is running. You can leave the bench and watch the aperture open."
        : horizon.Completed ? "First Light recorded. Space at the Horizon bench replays the sequence."
        : garden.Certified ? "At the Horizon bench, match yaw +7.5 and pitch +4.0, then press Space."
        : forge.Certified ? "At each Vector anchor, F raises it and its neighbor. Match A 2 / B 1 / C 3."
        : "At the Helios bench: A / D phase, W / S containment. Match the reference and hold Space.";
    public string Measurement => garden.Certified ? $"{CompletedCount}/3 CERTIFIED / BEACON ERROR {horizon.AlignmentError:0.0} DEG"
        : forge.Certified ? $"1/3 CERTIFIED / VECTOR {garden.LevelA} : {garden.LevelB} : {garden.LevelC} / REF 2 : 1 : 3"
        : $"0/3 CERTIFIED / HELIOS STABILITY {forge.Stability01:P0}";
    public float CaptureProgress01 => garden.Certified ? horizon.CaptureProgress01 : forge.Certified ? garden.CaptureProgress01 : forge.CaptureProgress01;
    public string CaptureStatus => horizon.IsPerforming ? $"FIRST LIGHT / {CaptureProgress01:P0}"
        : horizon.Completed ? "FIRST LIGHT RECORDED" : CaptureProgress01>0f ? $"CERTIFYING / {CaptureProgress01:P0}" : "";
    public string Notes => "SKUNK WORKS / PROTOTYPE COMMISSIONING\n\n"
        +"01 / HELIOS FORGE\nMatch phase 126 and containment 0.680. A/D changes phase; W/S changes containment. Shift is fine control. Hold Space for three stable seconds.\n\n"
        +"02 / VECTOR GARDEN\nF advances an anchor and its next neighbor: A > B > C > A. Each wraps after 3. Match heights 2 / 1 / 3 and let the field settle.\n\n"
        +"03 / HORIZON ENGINE\nA/D adjusts yaw; W/S adjusts pitch. Match +7.5 / +4.0, then press Space for First Light. Watch from the bench or walk around. Space replays after completion.\n\n"
        +Measurement+"\n\nF/Escape leaves a bench. Tab closes notes. The truck returns to the SSC. Progress lasts for this session.\n\nThese are fictional prototypes and gameplay measurements.";
    public void Configure(FirstPersonPlayerController controller,HeliosForgeController source,VectorGardenController field,HorizonEngineController aperture)
    { player=controller; forge=source; garden=field; horizon=aperture; }
    public void ConfigureHall(Transform sculpture,Text[] displays) { emblem=sculpture; records=displays; }
    private void Update()
    {
        if(RuntimeSceneSwitcher.IsOpen) return;
        for(int i=0;i<emblem.childCount;i++)
            emblem.GetChild(i).Rotate(new Vector3(3f+i,5f-i,2f)*Time.deltaTime,Space.Self);
        displayTimer-=Time.deltaTime;
        if(displayTimer>0f) return;
        displayTimer=0.2f;
        records[0].text="01 / HELIOS FORGE\n"+(forge.Certified?"POWER BUS ONLINE":"SOURCE UNCERTIFIED");
        records[1].text="02 / VECTOR GARDEN\n"+(garden.Certified?"FIELD LOCKED":"ANCHORS UNCALIBRATED");
        records[2].text="03 / HORIZON ENGINE\n"+(horizon.Completed?"FIRST LIGHT RECORDED":"AWAITING FIRST LIGHT");
    }
}
