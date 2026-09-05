using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Each anchor advances itself and its clockwise neighbor, modulo four.
// A small coupled puzzle, expressed by the actual height of each sculpture.
public sealed class VectorGardenController : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private HeliosForgeController forge;
    [SerializeField] private Transform[] sculptures;
    [SerializeField] private Transform[] controls;
    [SerializeField] private Text[] labels;
    [SerializeField] private Text status;
    [SerializeField] private Material fieldMaterial;
    private readonly int[] levels = new int[3];
    private readonly int[] targets = { 2, 1, 3 };
    private readonly LineRenderer[] links = new LineRenderer[3];
    private readonly Vector3[][] points = { new Vector3[72], new Vector3[72], new Vector3[72] };
    private float stableSeconds;
    private float clock;
    private float displayTimer;
    public bool Certified { get; private set; }
    public bool Aligned => levels[0]==2 && levels[1]==1 && levels[2]==3;
    public int LevelA => levels[0];
    public int LevelB => levels[1];
    public int LevelC => levels[2];
    public float CaptureProgress01 => Certified ? 1f : Mathf.Clamp01(stableSeconds/2.5f);

    public void Configure(FirstPersonPlayerController controller, HeliosForgeController source,
        Transform[] gems, Transform[] anchors, Text[] displays, Text wall, Material glow)
    {
        player=controller; forge=source; sculptures=gems; controls=anchors;
        labels=displays; status=wall; fieldMaterial=glow;
    }

    private void Awake()
    {
        for(int i=0;i<3;i++)
        {
            GameObject item=new($"Runtime Vector Link {i}");
            item.transform.SetParent(transform,false);
            LineRenderer line=item.AddComponent<LineRenderer>();
            line.sharedMaterial=fieldMaterial;
            line.useWorldSpace=false;
            line.positionCount=72;
            line.widthMultiplier=0.075f;
            line.renderingLayerMask=SkunkWorksLayout.GardenLayer;
            line.shadowCastingMode=ShadowCastingMode.Off;
            line.receiveShadows=false;
            links[i]=line;
        }
        UpdateLabels();
    }

    private void Update()
    {
        if(RuntimeSceneSwitcher.IsOpen) { InteractionPromptDisplay.Hide(this); return; }
        clock+=Time.deltaTime;
        int nearby=-1;
        if(player.enabled)
            for(int i=0;i<controls.Length;i++)
                if(Vector3.Distance(player.transform.position,controls[i].position)<1.3f) { nearby=i; break; }
        if(nearby>=0)
        {
            string hint=!forge.Certified ? "VECTOR / Source offline. Certify Helios first."
                : Certified ? "VECTOR / Field certified. Continue to the Horizon Engine."
                : $"F: advance anchor {(char)('A'+nearby)} and {(char)('A'+(nearby+1)%3)}\nMatch A 2 / B 1 / C 3. Four steps return an anchor to zero.";
            InteractionPromptDisplay.Show(this,hint);
            if(forge.Certified && !Certified && Keyboard.current!=null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                levels[nearby]=(levels[nearby]+1)%4;
                int neighbor=(nearby+1)%3;
                levels[neighbor]=(levels[neighbor]+1)%4;
                stableSeconds=0f;
                player.GetComponent<FacilityPlayerEffects>()?.PlayConfirmation();
            }
        }
        else InteractionPromptDisplay.Hide(this);
        if(!Certified)
        {
            stableSeconds=forge.Certified && Aligned ? stableSeconds+Time.deltaTime : 0f;
            if(stableSeconds>=2.5f)
            {
                Certified=true;
                player.GetComponent<FacilityPlayerEffects>()?.PlayConfirmation();
            }
        }
        for(int i=0;i<3;i++)
        {
            Vector3 position=sculptures[i].localPosition;
            float targetHeight=2.65f+levels[i]*0.95f+Mathf.Sin(clock*0.8f+i*2f)*0.12f;
            position.y=Mathf.Lerp(position.y,targetHeight,1f-Mathf.Exp(-Time.deltaTime*2.5f));
            sculptures[i].localPosition=position;
            sculptures[i].localRotation=Quaternion.Euler(Mathf.Sin(clock*0.3f+i)*7f,clock*(8f+i*3f),0f);
        }
        // Each side occupies a different part of the room. Looking away from
        // A-B must not freeze the other two visible sides of the field.
        if(links[0].isVisible || links[1].isVisible || links[2].isVisible || clock<0.2f) DrawField();
        displayTimer-=Time.deltaTime;
        if(displayTimer<=0f) { displayTimer=0.1f; UpdateLabels(); }
    }

    private void DrawField()
    {
        for(int i=0;i<3;i++)
        {
            Vector3 a=sculptures[i].localPosition;
            Vector3 b=sculptures[(i+1)%3].localPosition;
            Vector3 side=Vector3.Cross((b-a).normalized,Vector3.up).normalized;
            for(int j=0;j<points[i].Length;j++)
            {
                float t=j/(float)(points[i].Length-1);
                float envelope=Mathf.Sin(t*Mathf.PI);
                float angle=t*Mathf.PI*8f-clock*2f;
                points[i][j]=Vector3.Lerp(a,b,t)+envelope*(Vector3.up*1.2f
                    +(Vector3.up*Mathf.Cos(angle)+side*Mathf.Sin(angle))*(Certified?0.12f:0.32f));
            }
            links[i].SetPositions(points[i]);
            Color color=forge.Certified ? new Color(0.12f,1.5f,0.7f) : new Color(0.025f,0.28f,0.22f);
            links[i].startColor=links[i].endColor=color;
        }
    }

    private void UpdateLabels()
    {
        for(int i=0;i<3;i++)
            labels[i].text=$"ANCHOR {(char)('A'+i)} / {(levels[i]==targets[i]?"MATCH":"ADJUST")}\nHEIGHT {levels[i]} / REF {targets[i]}\nF ADVANCES {(char)('A'+i)} + {(char)('A'+(i+1)%3)}";
        status.text=Certified ? "VECTOR LOCK / HORIZON FIELD AVAILABLE" : !forge.Certified ? "AWAITING HELIOS POWER / ANCHORS ISOLATED"
            : Aligned ? $"FIELD SETTLING / {CaptureProgress01:P0}" : $"COUPLED ANCHORS / {levels[0]} : {levels[1]} : {levels[2]} / REF 2 : 1 : 3";
    }
    private void OnDisable() => InteractionPromptDisplay.Hide(this);
}
