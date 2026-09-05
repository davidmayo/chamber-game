using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Stages only a disposable Play session, then photographs the real animated
// scene. Exiting Play Mode restores the user's scene, time, and audio setting.
public static class SkunkWorksCapture
{
    private readonly struct View
    {
        public readonly string Name;
        public readonly Vector3 Eye;
        public readonly Vector3 Target;
        public readonly float Fov;
        public readonly int Stage;
        public View(string name,Vector3 eye,Vector3 target,float fov,int stage=1)
        { Name=name; Eye=eye; Target=target; Fov=fov; Stage=stage; }
    }
    private static readonly View[] Views=
    {
        new("campus",new(46f,13f,51f),new(0f,5f,-3f),62f),
        new("arrival",new(9f,2f,27f),new(0f,6f,9f),74f),
        new("atrium",new(5.6f,2.2f,7f),new(0f,6f,-1f),80f),
        new("helios",new(-12.5f,2.5f,7.5f),new(-19.5f,4.5f,0.8f),74f),
        new("helios-bench",new(-19.5f,2.05f,9.2f),new(-19.5f,3.9f,0.8f),72f),
        new("helios-detail",new(-23.5f,4.6f,6f),new(-19.5f,4.5f,0.8f),65f),
        new("vector",new(12.5f,2.5f,9f),new(19.5f,4.6f,1f),76f),
        new("vector-front",new(19.5f,2.2f,10f),new(19.5f,4.3f,0f),76f),
        new("vector-anchor",new(23.9f,1.7f,5.4f),new(23.9f,3.2f,0.3f),80f),
        new("horizon-idle",new(0f,2f,-12.5f),new(0f,6f,-26f),66f),
        new("horizon-opening",new(6f,2.3f,-17.5f),new(0f,6f,-26f),70f,2),
        new("horizon-first-light",new(5f,2.7f,-15f),new(0f,6f,-26f),72f,3),
        new("horizon-bench",new(0f,2.05f,-13.1f),new(0f,4.6f,-26f),72f,3),
        new("horizon-detail",new(-4f,4.2f,-20f),new(0f,6f,-26f),74f,3),
        new("atrium-complete",new(0f,1.7f,9.4f),new(0f,5f,-4f),80f,3),
        new("night",new(38f,11f,42f),new(0f,5f,-3f),66f,4),
    };
    private static Action<bool,string,string> completed;
    private static FirstPersonPlayerController player;
    private static Transform campus;
    private static Camera camera;
    private static HeliosForgeController forge;
    private static VectorGardenController garden;
    private static HorizonEngineController horizon;
    private static int index;
    private static int startFrame;
    private static float previousDelta;
    private static double deadline;
    private static string folder;

    public static void Begin(Action<bool,string,string> callback)
    {
        if(completed!=null || !Application.isPlaying || EditorApplication.isPaused || RuntimeSceneSwitcher.IsOpen)
            throw new InvalidOperationException("Start a fresh unpaused Main Play session before capture_skunk_works.");
        forge=Object.FindFirstObjectByType<HeliosForgeController>();
        garden=Object.FindFirstObjectByType<VectorGardenController>();
        horizon=Object.FindFirstObjectByType<HorizonEngineController>();
        if(forge==null || garden==null || horizon==null || forge.Certified || horizon.IsPerforming)
            throw new InvalidOperationException("Photography requires fresh, uncommissioned Skunk Works equipment.");
        foreach(SimpleSeatedConsoleController seat in Object.FindObjectsByType<SimpleSeatedConsoleController>(FindObjectsSortMode.None))
            if(seat.IsSeated) throw new InvalidOperationException("Stand up before starting campus photography.");
        player=Read<FirstPersonPlayerController>(forge,"player");
        campus=Read<Transform>(forge,"campus");
        camera=player.PlayerCamera;
        previousDelta=Time.captureDeltaTime;
        completed=callback ?? throw new ArgumentNullException(nameof(callback));
        EditorApplication.update+=Update;
        EditorApplication.playModeStateChanged+=PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload+=BeforeReload;
        try
        {
            folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","docs","screenshots"));
            Directory.CreateDirectory(folder);
            Time.captureDeltaTime=1f/60f;
            deadline=EditorApplication.timeSinceStartup+240d;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            player.enabled=false;
            player.GetComponent<CharacterController>().enabled=false;
            Write(forge,"phaseDegrees",HeliosForgeController.ReferencePhase);
            Write(forge,"containment",HeliosForgeController.ReferenceContainment);
            Property(forge,nameof(HeliosForgeController.Certified),true);
            int[] levels=Read<int[]>(garden,"levels");
            levels[0]=2; levels[1]=1; levels[2]=3;
            Property(garden,nameof(VectorGardenController.Certified),true);
            Write(horizon,"yaw",HorizonEngineController.ReferenceYaw);
            Write(horizon,"pitch",HorizonEngineController.ReferencePitch);
            index=0;
            BeginView();
        }
        catch(Exception exception) { Finish(false,exception.Message); }
    }
    private static void BeginView()
    {
        View view=Views[index];
        player.transform.position=campus.TransformPoint(new Vector3(view.Eye.x,0f,view.Eye.z));
        camera.transform.SetPositionAndRotation(campus.TransformPoint(view.Eye),Quaternion.LookRotation(campus.TransformDirection(view.Target-view.Eye)));
        camera.fieldOfView=view.Fov;
        camera.aspect=1920f/1080f;
        Property(horizon,nameof(HorizonEngineController.IsPerforming),view.Stage==2);
        Property(horizon,nameof(HorizonEngineController.Completed),view.Stage>=3);
        if(view.Stage==2) Write(horizon,"elapsed",3.5f);
        if(view.Stage==4)
        {
            GroundOpsSkyController sky=Object.FindFirstObjectByType<GroundOpsSkyController>();
            sky.SetLocalDateTime(sky.Year,sky.Month,sky.Day,22,0);
        }
        startFrame=Time.frameCount;
    }
    private static void Update()
    {
        if(completed==null) return;
        try
        {
            if(!Application.isPlaying || camera==null) throw new InvalidOperationException("Play session ended during campus photography.");
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Campus photography exceeded four minutes.");
            if(EditorApplication.isPaused || RuntimeSceneSwitcher.IsOpen) return;
            if(Time.frameCount-startFrame<90) return;
            Capture(Path.Combine(folder,"skunk-works-"+Views[index].Name+".png"));
            index++;
            if(index==Views.Length) Finish(true,$"Captured {Views.Length} live 1920x1080 campus views; exiting Play Mode to discard demonstration state.");
            else BeginView();
        }
        catch(Exception exception) { Finish(false,"Campus photography failed: "+exception.Message); }
    }
    private static void Capture(string path)
    {
        RenderTexture hdr=new(1920,1080,24,RenderTextureFormat.DefaultHDR);
        RenderTexture output=new(1920,1080,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        Texture2D image=new(1920,1080,TextureFormat.RGB24,false);
        RenderTexture previousTarget=camera.targetTexture;
        RenderTexture previousActive=RenderTexture.active;
        try
        {
            // Preserve HDR through bloom and tonemapping. Convert the finished
            // camera image to an ordinary sRGB PNG only after post processing.
            camera.targetTexture=hdr;
            camera.Render();
            Graphics.Blit(hdr,output);
            RenderTexture.active=output;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0);
            image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previousTarget;
            RenderTexture.active=previousActive;
            hdr.Release(); output.Release();
            Object.DestroyImmediate(hdr); Object.DestroyImmediate(output); Object.DestroyImmediate(image);
        }
    }
    private static T Read<T>(object owner,string name) => (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    private static void Write(object owner,string name,object value) => owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
    private static void Property(object owner,string name,object value) => owner.GetType().GetProperty(name).GetSetMethod(true).Invoke(owner,new[] {value});
    private static void PlayModeChanged(PlayModeStateChange state) { if(state==PlayModeStateChange.ExitingPlayMode) Finish(false,"Campus photography cancelled."); }
    private static void BeforeReload() => Finish(false,"Campus photography cancelled by script reload.");
    private static void Finish(bool success,string message)
    {
        Action<bool,string,string> callback=completed;
        if(callback==null) return;
        completed=null;
        EditorApplication.update-=Update;
        EditorApplication.playModeStateChanged-=PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload-=BeforeReload;
        Time.captureDeltaTime=previousDelta;
        try { callback(success,message,folder); }
        finally
        {
            player=null; campus=null; camera=null; forge=null; garden=null; horizon=null;
            if(EditorApplication.isPlaying) EditorApplication.isPlaying=false;
        }
    }
}

