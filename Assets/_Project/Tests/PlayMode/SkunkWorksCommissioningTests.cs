using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class SkunkWorksCommissioningTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;
    private Behaviour player;
    private Transform campus;
    private float previousDelta;
    public override void Setup()
    {
        base.Setup();
        keyboard=InputSystem.AddDevice<Keyboard>();
        mouse=InputSystem.AddDevice<Mouse>();
        previousDelta=Time.captureDeltaTime;
        Time.captureDeltaTime=1f/60f;
    }
    public override void TearDown()
    {
        Behaviour menu=Find("RuntimeSceneSwitcher");
        if(menu!=null) UnityEngine.Object.DestroyImmediate(menu.gameObject);
        Time.captureDeltaTime=previousDelta;
        Time.timeScale=1f;
        AudioListener.pause=false;
        base.TearDown();
    }

    [UnityTest]
    public IEnumerator CommissionTheCampusWithActualConsoleAndAnchorControls()
    {
        SceneManager.LoadScene("Main",LoadSceneMode.Single);
        yield return Frames(3);
        player=Find("FirstPersonPlayerController");
        campus=GameObject.Find("Ground Ops Blockout/Level 02 - Space Science Center Skunk Works").transform;
        Behaviour forge=Find("HeliosForgeController");
        Behaviour console=Field(forge,"console") as Behaviour;
        Camera camera=player.GetComponentInChildren<Camera>();
        CharacterController body=player.GetComponent<CharacterController>();
        body.enabled=false;
        player.transform.position=campus.TransformPoint(new Vector3(-19.5f,0f,7.1f));
        body.enabled=true;
        Physics.SyncTransforms();
        yield return Frames(5);
        if(Find("RuntimeSceneSwitcher")==null) new GameObject("Skunk Works Test Pause Menu").AddComponent(Type.GetType("RuntimeSceneSwitcher, Assembly-CSharp"));
        float standingFov=camera.fieldOfView;
        yield return Tap(keyboard.tabKey);
        Text notes=Find("FacilityShiftDisplay").transform.Find("Signal Watch Canvas/Field Notebook/Notebook Entries").GetComponent<Text>();
        Assert.That(notes.text,Does.Contain("SKUNK WORKS"));
        Assert.That(notes.preferredHeight,Is.LessThanOrEqualTo(notes.rectTransform.rect.height));
        Assert.That(notes.raycastTarget,Is.False);
        yield return Tap(keyboard.tabKey);
        yield return Tap(keyboard.fKey);
        yield return WaitFor(()=>Property<bool>(console,"IsSeated"));
        Press(keyboard.spaceKey);
        yield return Frames(25);
        Release(keyboard.spaceKey);
        yield return Frames(2);
        Assert.That(Property<float>(forge,"CaptureProgress01"),Is.Zero,"An unstable source cannot be certified.");
        Press(keyboard.dKey);
        yield return WaitFor(()=>Property<float>(forge,"PhaseDegrees")>=125f);
        Release(keyboard.dKey);
        // Let the release reach the Input System before queuing another full
        // keyboard state; otherwise the test's W event reintroduces held D.
        yield return Frames(2);
        Press(keyboard.wKey);
        yield return WaitFor(()=>Property<float>(forge,"Containment")>=0.68f);
        Release(keyboard.wKey);
        yield return Frames(2);
        Assert.That(Property<bool>(forge,"Aligned"),Is.True,$"Phase {Property<float>(forge,"PhaseDegrees")}, containment {Property<float>(forge,"Containment")}");
        Press(keyboard.spaceKey);
        yield return Frames(30);
        Assert.That(Property<float>(forge,"CaptureProgress01"),Is.GreaterThan(0f));
        Release(keyboard.spaceKey);
        yield return Frames(3);
        Assert.That(Property<float>(forge,"CaptureProgress01"),Is.Zero,"Releasing Space interrupts source certification.");
        float seatedFov=camera.fieldOfView;
        Set(mouse.scroll,new Vector2(0f,120f));
        yield return Frames(8);
        Set(mouse.scroll,Vector2.zero);
        Assert.That(camera.fieldOfView,Is.LessThan(seatedFov-1f));
        Press(keyboard.spaceKey);
        yield return WaitFor(()=>Property<bool>(forge,"Certified"));
        Release(keyboard.spaceKey);
        yield return Frames(2);
        Assert.That(Property<float>(forge,"CaptureProgress01"),Is.EqualTo(1f));
        yield return Tap(keyboard.escapeKey);
        yield return WaitFor(()=>player.enabled);
        Assert.That(camera.fieldOfView,Is.EqualTo(standingFov).Within(0.01f));
        yield return Tap(keyboard.escapeKey);
        Assert.That(Time.timeScale,Is.Zero);
        Transform core=Field(forge,"core") as Transform;
        Quaternion rotation=core.localRotation;
        yield return Frames(20);
        Assert.That(Quaternion.Angle(rotation,core.localRotation),Is.LessThan(0.001f));
        yield return Tap(keyboard.escapeKey);
        yield return Frames(15);
        Assert.That(Quaternion.Angle(rotation,core.localRotation),Is.GreaterThan(0.1f));
        Assert.That(Property<bool>(forge,"Certified"),Is.True);
        Capture(camera,"skunk-helios-certified",new Vector3(-12.5f,2.5f,7.5f),new Vector3(-19.5f,4.5f,0.8f));
        yield return WalkTo(new Vector3(-12f,0f,0f));
        yield return WalkTo(new Vector3(0f,0f,0f));
        yield return WalkTo(new Vector3(12f,0f,0f));
        yield return WalkTo(new Vector3(15.1f,0f,2.6f));
        Behaviour garden=Find("VectorGardenController");
        yield return Tap(keyboard.escapeKey);
        yield return Tap(keyboard.fKey);
        Assert.That(Property<int>(garden,"LevelA"),Is.Zero,"Pause owns all input, including the anchors.");
        yield return Tap(keyboard.escapeKey);
        yield return Tap(keyboard.fKey);
        Assert.That(Property<int>(garden,"LevelA"),Is.EqualTo(1));
        Assert.That(Property<int>(garden,"LevelB"),Is.EqualTo(1),"An anchor must change its clockwise neighbor.");
        Assert.That(Property<int>(garden,"LevelC"),Is.Zero);
        for(int i=0;i<3;i++) yield return Tap(keyboard.fKey);
        Assert.That(Property<int>(garden,"LevelA"),Is.Zero,"Four steps wrap the anchor to zero.");
        Assert.That(Property<int>(garden,"LevelB"),Is.Zero);
        yield return WalkTo(new Vector3(15.1f,0f,7.1f));
        yield return WalkTo(new Vector3(19.5f,0f,7.1f));
        yield return Tap(keyboard.fKey);
        yield return WalkTo(new Vector3(23.9f,0f,7.1f));
        yield return WalkTo(new Vector3(23.9f,0f,2.6f));
        yield return Tap(keyboard.fKey);
        yield return Tap(keyboard.fKey);
        Assert.That(Property<bool>(garden,"Aligned"),Is.True);
        yield return WaitFor(()=>Property<bool>(garden,"Certified"));
        yield return Tap(keyboard.fKey);
        Assert.That(Property<bool>(garden,"Aligned"),Is.True,"Certification locks the stable field.");
        Transform[] sculptures=Field(garden,"sculptures") as Transform[];
        yield return Frames(120);
        Assert.That(sculptures[2].localPosition.y,Is.GreaterThan(sculptures[0].localPosition.y+0.5f));
        Capture(camera,"skunk-vector-certified",new Vector3(12.5f,2.5f,9f),new Vector3(19.5f,4.6f,1f));
    }

    private IEnumerator WalkTo(Vector3 local)
    {
        Vector3 target=campus.TransformPoint(local);
        int count=0;
        while(Vector3.ProjectOnPlane(target-player.transform.position,Vector3.up).magnitude>0.13f && count++<850)
        {
            player.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(target-player.transform.position,Vector3.up));
            Press(keyboard.wKey);
            yield return null;
        }
        Release(keyboard.wKey);
        yield return Frames(3);
        Assert.That(Vector3.Distance(player.transform.position,target),Is.LessThan(0.35f),
            $"Commissioning route blocked at {campus.InverseTransformPoint(player.transform.position)} toward {local}.");
        Assert.That(Physics.Raycast(player.transform.position+Vector3.up*0.3f,Vector3.down,0.7f,~0,QueryTriggerInteraction.Ignore),Is.True);
    }

    private void Capture(Camera camera,string name,Vector3 localEye,Vector3 localTarget)
    {
        Vector3 position=camera.transform.position;
        Quaternion rotation=camera.transform.rotation;
        float fov=camera.fieldOfView;
        RenderTexture previousTarget=camera.targetTexture;
        RenderTexture previousActive=RenderTexture.active;
        RenderTexture target=new(1920,1080,24,RenderTextureFormat.ARGB32);
        Texture2D image=new(1920,1080,TextureFormat.RGB24,false);
        try
        {
            camera.transform.SetPositionAndRotation(campus.TransformPoint(localEye),Quaternion.LookRotation(campus.TransformDirection(localTarget-localEye)));
            camera.fieldOfView=74f;
            camera.targetTexture=target;
            camera.Render();
            RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0);
            image.Apply();
            string folder=Path.Combine(Application.dataPath,"..","Library","CodexBridge","Artifacts");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previousTarget;
            RenderTexture.active=previousActive;
            camera.transform.SetPositionAndRotation(position,rotation);
            camera.fieldOfView=fov;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(image);
        }
    }
    private IEnumerator Tap(ButtonControl key) { Press(key); yield return null; Release(key); yield return Frames(2); }
    private static IEnumerator Frames(int count) { for(int i=0;i<count;i++) yield return null; }
    private static IEnumerator WaitFor(Func<bool> condition)
    {
        int count=0;
        while(!condition() && count++<650) yield return null;
        Assert.That(condition(),Is.True,"The expected commissioning state was not reached.");
        yield return null;
    }
    private static Behaviour Find(string type) => UnityEngine.Object.FindFirstObjectByType(Type.GetType(type+", Assembly-CSharp")) as Behaviour;
    private static object Field(object owner,string field) => owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    private static T Property<T>(object owner,string property) => (T)owner.GetType().GetProperty(property).GetValue(owner);
}
