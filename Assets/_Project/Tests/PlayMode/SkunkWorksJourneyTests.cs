using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SkunkWorksJourneyTests : InputTestFixture
{
    private Keyboard keyboard;
    private Behaviour player;
    private Transform campus;
    private float previousDelta;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.AddDevice<Mouse>();
        previousDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f/60f;
    }

    public override void TearDown()
    {
        Time.captureDeltaTime = previousDelta;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        base.TearDown();
    }

    [UnityTest]
    public IEnumerator SelectSkunkWorksRideWalkAllThreeWingsAndReturn()
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return Frames(3);
        player = Find("FirstPersonPlayerController");
        Behaviour truck = Find("RailTruckController");
        Camera camera = player.GetComponentInChildren<Camera>();
        Transform departure = Field(truck,"departureInteractionPoint") as Transform;
        Transform exit = Field(truck,"skunkWorksExitPose") as Transform;
        Transform[] waypoints = Field(truck,"skunkWorksWaypoints") as Transform[];
        Assert.That(waypoints, Has.Length.GreaterThan(20));
        Assert.That(Vector3.Distance(waypoints[0].position,waypoints[^1].position), Is.LessThan(0.05f));
        Write(truck,"speedMetersPerSecond",120f);
        Write(truck,"fadeHalfSeconds",0.02f);
        CharacterController body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = departure.position;
        body.enabled = true;
        Physics.SyncTransforms();
        float fov = camera.fieldOfView;
        yield return Tap(keyboard.fKey);
        yield return State(truck,"ArrivedAtDoc");
        Vector3 parked = (Field(truck,"truckRoot") as Transform).position;
        yield return Tap(keyboard.digit2Key);
        Assert.That(Property<bool>(truck,"SkunkWorksSelected"), Is.True);
        Assert.That(Vector3.Distance(parked,(Field(truck,"truckRoot") as Transform).position), Is.LessThan(0.01f),
            "Selecting a destination must not teleport the parked truck.");
        yield return Tap(keyboard.wKey);
        yield return State(truck,"ArrivedAtSkunkWorks");
        yield return Tap(keyboard.fKey);
        yield return State(truck,"ParkedAtSkunkWorks");
        Assert.That(player.enabled, Is.True);
        Assert.That(camera.fieldOfView, Is.EqualTo(fov).Within(0.01f));
        Assert.That(Vector3.Distance(player.transform.position,exit.position), Is.LessThan(0.25f));
        campus = GameObject.Find("Ground Ops Blockout/Level 02 - Space Science Center Skunk Works").transform;
        Transform route = campus.Find("Walking Route");
        for (int i=1;i<route.childCount;i++) yield return WalkTo(route.GetChild(i).position);
        // Return by each doorway, then follow the terrace to the actual stop.
        for (int i=route.childCount-2;i>=0;i--) yield return WalkTo(route.GetChild(i).position);
        yield return Tap(keyboard.fKey);
        yield return State(truck,"ArrivedAtSkunkWorks");
        yield return Tap(keyboard.wKey);
        yield return State(truck,"ArrivedAtDoc");
        yield return Tap(keyboard.digit1Key);
        Assert.That(Property<bool>(truck,"SkunkWorksSelected"), Is.False);
        yield return Tap(keyboard.fKey);
        yield return State(truck,"ParkedAtDoc");
        Assert.That(player.enabled, Is.True);
        Assert.That(camera.fieldOfView, Is.EqualTo(fov).Within(0.01f));
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
        Assert.That(UnityEngine.Object.FindObjectsByType(player.GetType(),FindObjectsSortMode.None), Has.Length.EqualTo(1));
    }

    private IEnumerator WalkTo(Vector3 destination)
    {
        int count=0;
        while (Vector3.ProjectOnPlane(destination-player.transform.position,Vector3.up).magnitude>0.13f && count++<850)
        {
            player.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(destination-player.transform.position,Vector3.up));
            Press(keyboard.wKey);
            yield return null;
            Assert.That(player.transform.position.y, Is.GreaterThan(7.4f), "The campus route lost floor support.");
        }
        Release(keyboard.wKey);
        yield return Frames(3);
        Assert.That(Vector3.Distance(player.transform.position,destination), Is.LessThan(0.35f),
            $"Campus route blocked at {campus.InverseTransformPoint(player.transform.position)} toward {campus.InverseTransformPoint(destination)}.");
        Assert.That(Physics.Raycast(player.transform.position+Vector3.up*0.3f,Vector3.down,0.7f,~0,QueryTriggerInteraction.Ignore), Is.True);
    }
    private IEnumerator Tap(ButtonControl key) { Press(key); yield return null; Release(key); yield return Frames(2); }
    private static IEnumerator Frames(int count) { for(int i=0;i<count;i++) yield return null; }
    private static IEnumerator State(object truck,string expected)
    {
        int count=0;
        while(Property<string>(truck,"StateName")!=expected && count++<600) yield return null;
        Assert.That(Property<string>(truck,"StateName"), Is.EqualTo(expected));
        yield return Frames(2);
    }
    private static Behaviour Find(string type) => UnityEngine.Object.FindFirstObjectByType(Type.GetType(type+", Assembly-CSharp")) as Behaviour;
    private static object Field(object owner,string field) => owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    private static void Write(object owner,string field,object value) => owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(owner,value);
    private static T Property<T>(object owner,string property) => (T)owner.GetType().GetProperty(property).GetValue(owner);
}
