using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class HorizonEngineController : MonoBehaviour
{
    public const float ReferenceYaw=7.5f;
    public const float ReferencePitch=4f;
    public const float CycleSeconds=14f;
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private Transform campus;
    [SerializeField] private HeliosForgeController forge;
    [SerializeField] private VectorGardenController garden;
    [SerializeField] private SimpleSeatedConsoleController console;
    [SerializeField] private Transform[] iris;
    [SerializeField] private Transform[] rings;
    [SerializeField] private Transform surveyor;
    [SerializeField] private Renderer window;
    [SerializeField] private Text readout;
    [SerializeField] private Text status;
    [SerializeField] private Light radiance;
    [SerializeField] private float yaw=-15f;
    [SerializeField] private float pitch=-10f;
    private MaterialPropertyBlock properties;
    private float elapsed;
    private float clock;
    private float displayTimer;
    private AudioSource hum;
    private AudioClip tone;
    public bool Ready => forge.Certified && garden.Certified;
    public bool Aligned => AlignmentError<0.7f;
    public float AlignmentError => Vector2.Distance(new Vector2(yaw,pitch),new Vector2(ReferenceYaw,ReferencePitch));
    public float Yaw => yaw;
    public float Pitch => pitch;
    public bool IsPerforming { get; private set; }
    public bool Completed { get; private set; }
    public float OpenFraction { get; private set; }
    public float CaptureProgress01 => IsPerforming ? Mathf.Clamp01(elapsed/CycleSeconds) : Completed ? 1f : 0f;

    public void Configure(FirstPersonPlayerController controller,Transform building,HeliosForgeController source,
        VectorGardenController field,SimpleSeatedConsoleController seat,Transform[] petals,Transform[] stators,
        Transform probe,Renderer sky,Text terminal,Text wall,Light light)
    {
        player=controller; campus=building; forge=source; garden=field; console=seat;
        iris=petals; rings=stators; surveyor=probe; window=sky; readout=terminal; status=wall; radiance=light;
    }
    private void Awake()
    {
        properties=new MaterialPropertyBlock();
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
        if(console.IsSeated && keys!=null && !IsPerforming)
        {
            if(Ready && !Completed)
            {
                float fine=keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed ? 0.2f : 1f;
                yaw=Mathf.Clamp(yaw+((keys.dKey.isPressed?1f:0f)-(keys.aKey.isPressed?1f:0f))*18f*fine*Time.deltaTime,-30f,30f);
                pitch=Mathf.Clamp(pitch+((keys.wKey.isPressed?1f:0f)-(keys.sKey.isPressed?1f:0f))*12f*fine*Time.deltaTime,-20f,20f);
            }
            if(Ready && (Aligned || Completed) && keys.spaceKey.wasPressedThisFrame)
            {
                elapsed=0f;
                IsPerforming=true;
            }
        }
        if(IsPerforming)
        {
            elapsed+=Time.deltaTime;
            if(elapsed>=CycleSeconds)
            {
                IsPerforming=false;
                Completed=true;
                player.GetComponent<FacilityPlayerEffects>()?.PlayConfirmation();
            }
        }
        float opening=IsPerforming ? Mathf.SmoothStep(0f,1f,Mathf.Clamp01((elapsed-2f)/5f)) : Completed ? 1f : 0f;
        OpenFraction=Mathf.MoveTowards(OpenFraction,opening,Time.deltaTime*0.7f);
        for(int i=0;i<iris.Length;i++)
        {
            float angle=i*Mathf.PI*2f/iris.Length;
            iris[i].localPosition=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f)*(2.15f+OpenFraction*2.95f)+Vector3.forward*(0.6f+i*0.025f);
            // The leaves slide outward and fold onto their radial hinges.
            // This clears the optical path while keeping them inside the room.
            iris[i].localRotation=Quaternion.AngleAxis(angle*Mathf.Rad2Deg+30f*(1f-OpenFraction),Vector3.forward)
                *Quaternion.Euler(0f,OpenFraction*80f,0f);
        }
        for(int i=0;i<rings.Length;i++)
            rings[i].localRotation=Quaternion.Euler(0f,0f,clock*(i%2==0?1f:-1f)*(4f+i*3f)*(0.2f+OpenFraction));
        surveyor.gameObject.SetActive(OpenFraction>0.7f);
        surveyor.localPosition=new Vector3(Mathf.Sin(clock*0.35f)*0.18f,Mathf.Sin(clock*0.7f)*0.15f,-1.7f+OpenFraction*5f);
        surveyor.localRotation=Quaternion.Euler(clock*6f,clock*18f,20f);
        window.GetPropertyBlock(properties);
        properties.SetFloat("_Open",OpenFraction);
        properties.SetFloat("_Clock",clock);
        properties.SetVector("_Aim",new Vector4((yaw-ReferenceYaw)/30f,(pitch-ReferencePitch)/25f,0f,0f));
        window.SetPropertyBlock(properties);
        radiance.intensity=15f+OpenFraction*220f;
        displayTimer-=Time.deltaTime;
        if(displayTimer<=0f) { displayTimer=0.1f; UpdateReadouts(); }
        Vector3 local=campus.InverseTransformPoint(player.PlayerCamera.transform.position);
        bool nearby=Mathf.Abs(local.x)<13f && local.z<-10f && local.z>-35f && local.y<13f;
        if(nearby && tone==null) { tone=CreateHum(); hum.clip=tone; hum.Play(); }
        hum.volume=Mathf.MoveTowards(hum.volume,nearby ? 0.025f+OpenFraction*0.08f : 0f,Time.deltaTime*0.2f);
        hum.pitch=0.75f+OpenFraction*0.55f;
    }
    private void UpdateReadouts()
    {
        string stage=!Ready ? "INTERLOCK / CERTIFY HELIOS + VECTOR" : IsPerforming ? $"FIRST LIGHT / {CaptureProgress01:P0}"
            : Completed ? "FIRST LIGHT RECORDED / SPACE TO REPLAY" : Aligned ? "BEACON LOCK / SPACE TO INITIATE" : "ACQUIRE THE REFERENCE BEACON";
        readout.text=$"HORIZON / {(Completed?"COMMISSIONED":"ALIGNMENT")}\nYAW {yaw:0.0} / REF +7.5\nPITCH {pitch:0.0} / REF +4.0\n{stage}";
        status.text=stage;
    }
    private static AudioClip CreateHum()
    {
        const int rate=22050;
        float[] samples=new float[rate*2];
        for(int i=0;i<samples.Length;i++)
        {
            float t=i/(float)rate;
            samples[i]=Mathf.Sin(t*Mathf.PI*2f*55f)*0.18f+Mathf.Sin(t*Mathf.PI*2f*82.5f)*0.09f;
        }
        AudioClip clip=AudioClip.Create("Horizon resonator",samples.Length,1,rate,false);
        clip.SetData(samples,0);
        return clip;
    }
    private void OnDisable() { if(hum!=null) hum.Stop(); }
    private void OnDestroy() { if(tone!=null) Destroy(tone); }
}
