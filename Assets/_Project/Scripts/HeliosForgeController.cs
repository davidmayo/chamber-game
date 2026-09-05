using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

// A fictional source-certification exercise. Its measurements are gameplay
// values; the glowing toroidal field is an imagined prototype visualization.
public sealed class HeliosForgeController : MonoBehaviour
{
    public const float ReferencePhase = 126f;
    public const float ReferenceContainment = 0.68f;
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private Transform campus;
    [SerializeField] private SimpleSeatedConsoleController console;
    [SerializeField] private Transform fieldOrigin;
    [SerializeField] private Transform phaseRing;
    [SerializeField] private Transform core;
    [SerializeField] private Material fieldMaterial;
    [SerializeField] private Text readout;
    [SerializeField] private Text wallStatus;
    [SerializeField] private Light sourceLight;
    [SerializeField] private float phaseDegrees;
    [SerializeField] private float containment = 0.25f;
    [SerializeField] private float captureSeconds = 3f;
    private readonly LineRenderer[] strands = new LineRenderer[10];
    private readonly Vector3[][] points = new Vector3[10][];
    private float captured;
    private float clock;
    private float displayTimer;
    private AudioSource hum;
    private AudioClip tone;
    public bool Certified { get; private set; }
    public float PhaseDegrees => phaseDegrees;
    public float Containment => containment;
    public bool Aligned => Mathf.Abs(Mathf.DeltaAngle(phaseDegrees,ReferencePhase)) <= 3f
        && Mathf.Abs(containment-ReferenceContainment) <= 0.018f;
    public float CaptureProgress01 => Certified ? 1f : Mathf.Clamp01(captured/captureSeconds);
    public float Stability01 => 1f-Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(phaseDegrees,ReferencePhase))/100f
        + Mathf.Abs(containment-ReferenceContainment)*1.2f);

    public void Configure(FirstPersonPlayerController controller, Transform building, SimpleSeatedConsoleController seat,
        Transform field, Transform rotor, Transform source, Material glow, Text terminal, Text status, Light light)
    {
        player=controller; campus=building; console=seat; fieldOrigin=field; phaseRing=rotor;
        core=source; fieldMaterial=glow; readout=terminal; wallStatus=status; sourceLight=light;
    }

    private void Awake()
    {
        for(int i=0;i<strands.Length;i++)
        {
            GameObject item=new($"Runtime Helios Flux {i:00}");
            item.transform.SetParent(fieldOrigin,false);
            LineRenderer line=item.AddComponent<LineRenderer>();
            line.sharedMaterial=fieldMaterial;
            line.useWorldSpace=false;
            line.loop=true;
            line.positionCount=160;
            line.widthMultiplier=0.065f;
            line.renderingLayerMask=SkunkWorksLayout.ForgeLayer;
            line.shadowCastingMode=ShadowCastingMode.Off;
            line.receiveShadows=false;
            strands[i]=line;
            points[i]=new Vector3[160];
        }
        hum=gameObject.AddComponent<AudioSource>();
        hum.playOnAwake=false;
        hum.loop=true;
        hum.volume=0f;
        UpdateReadouts();
    }

    private void Update()
    {
        if(RuntimeSceneSwitcher.IsOpen) return;
        clock+=Time.deltaTime;
        Keyboard keys=Keyboard.current;
        if(console.IsSeated && keys!=null && !Certified)
        {
            float fine=keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed ? 0.2f : 1f;
            phaseDegrees=Mathf.Repeat(phaseDegrees+((keys.dKey.isPressed?1f:0f)-(keys.aKey.isPressed?1f:0f))*35f*fine*Time.deltaTime,360f);
            containment=Mathf.Clamp01(containment+((keys.wKey.isPressed?1f:0f)-(keys.sKey.isPressed?1f:0f))*0.18f*fine*Time.deltaTime);
            captured=Aligned && keys.spaceKey.isPressed ? captured+Time.deltaTime : 0f;
            if(captured>=captureSeconds)
            {
                Certified=true;
                player.GetComponent<FacilityPlayerEffects>()?.PlayConfirmation();
            }
        }
        else if(!Certified) captured=0f;
        phaseRing.localRotation=Quaternion.Euler(18f,phaseDegrees,35f);
        core.localScale=Vector3.one*(0.75f+Stability01*0.18f+Mathf.Sin(clock*1.7f)*0.025f);
        core.localRotation=Quaternion.Euler(clock*5f,clock*13f,20f);
        sourceLight.intensity=90f+Stability01*110f+(Certified?80f:0f);
        if(strands[0].isVisible || clock<0.2f) DrawField();
        displayTimer-=Time.deltaTime;
        if(displayTimer<=0f) { displayTimer=0.1f; UpdateReadouts(); }
        Vector3 local=campus.InverseTransformPoint(player.PlayerCamera.transform.position);
        bool nearby=local.x < -9f && local.x > -30f && local.z > -8f && local.z < 12f && local.y<11f;
        if(nearby && tone==null)
        {
            tone=CreateHum(); hum.clip=tone; hum.Play();
        }
        hum.volume=Mathf.MoveTowards(hum.volume,nearby ? 0.07f+Stability01*0.04f : 0f,Time.deltaTime*0.3f);
        hum.pitch=0.8f+containment*0.35f;
    }

    private void DrawField()
    {
        for(int strand=0;strand<strands.Length;strand++)
        {
            float offset=strand*Mathf.PI*2f/strands.Length;
            for(int sample=0;sample<points[strand].Length;sample++)
            {
                float a=sample*Mathf.PI*2f/points[strand].Length;
                float wave=a*3f+offset+clock*0.5f;
                float radius=2.05f+Mathf.Cos(wave)*0.47f;
                float agitation=(1f-Stability01)*Mathf.Sin(a*7f-clock*1.3f)*0.12f;
                points[strand][sample]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(wave)*(0.48f+containment*0.25f)+agitation,Mathf.Sin(a)*radius);
            }
            strands[strand].SetPositions(points[strand]);
            Color hot=Color.Lerp(new Color(1.8f,0.2f,0.015f),new Color(2f,0.95f,0.16f),Stability01);
            strands[strand].startColor=hot;
            strands[strand].endColor=Color.Lerp(hot,new Color(1f,0.1f,0.025f),0.3f);
        }
    }

    private void UpdateReadouts()
    {
        readout.text=$"HELIOS / {(Certified?"SOURCE CERTIFIED":"SOURCE TUNING")}\nPHASE {phaseDegrees:000.0} / REF 126.0\nCONTAINMENT {containment:0.000} / REF 0.680\n"
            +(Certified?"POWER BUS ONLINE":Aligned?$"HOLD SPACE / CERTIFY {CaptureProgress01:P0}":"A D / PHASE    W S / CONTAINMENT");
        wallStatus.text=Certified ? "HELIOS ONLINE / POWER BUS CERTIFIED"
            : $"SOURCE STABILITY {Stability01:P0} / {(Aligned?"READY TO CERTIFY":"TUNING REQUIRED")}";
    }

    private static AudioClip CreateHum()
    {
        const int rate=22050;
        float[] samples=new float[rate*2];
        for(int i=0;i<samples.Length;i++)
        {
            float t=i/(float)rate;
            samples[i]=Mathf.Sin(t*Mathf.PI*2f*73f)*0.22f+Mathf.Sin(t*Mathf.PI*2f*146f)*0.06f;
        }
        AudioClip clip=AudioClip.Create("Helios source hum",samples.Length,1,rate,false);
        clip.SetData(samples,0);
        return clip;
    }
    private void OnDisable() { if(hum!=null) hum.Stop(); }
    private void OnDestroy() { if(tone!=null) Destroy(tone); }
}
