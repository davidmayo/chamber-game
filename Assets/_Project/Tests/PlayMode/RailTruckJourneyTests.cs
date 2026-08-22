using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class RailTruckJourneyTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
    }

    [UnityTest]
    public IEnumerator PlayerCanEnterDriveAndExitAtTheAntennaComplex()
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null;
        yield return null;

        Type railTruckType = Type.GetType("RailTruckController, Assembly-CSharp");
        Type playerType = Type.GetType("FirstPersonPlayerController, Assembly-CSharp");
        Assert.That(railTruckType, Is.Not.Null);
        Assert.That(playerType, Is.Not.Null);
        Component railTruck =
            UnityEngine.Object.FindFirstObjectByType(railTruckType) as Component;
        Component player =
            UnityEngine.Object.FindFirstObjectByType(playerType) as Component;
        Assert.That(railTruck, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        Camera playerCamera = Camera.main;
        Assert.That(playerCamera, Is.Not.Null);
        float standingFieldOfView = playerCamera.fieldOfView;

        GameObject departure = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Hallway Exterior Interaction");
        GameObject exitPose = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Antenna Complex Player Exit");
        GameObject road = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Antenna Access Road");
        GameObject terrain = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Low-poly Mountain Ridge");
        Assert.That(departure, Is.Not.Null);
        Assert.That(exitPose, Is.Not.Null);
        Assert.That(road?.GetComponent<MeshCollider>(), Is.Not.Null,
            "The access road must be walkable after leaving the truck.");
        Assert.That(terrain?.GetComponent<MeshCollider>(), Is.Not.Null,
            "The antenna-complex terrain must be walkable after leaving the truck.");

        SetPrivateField(railTruck, "fadeHalfSeconds", 0.01f);
        // Keep the end-to-end test fast while still proving W drives the truck.
        SetPrivateField(railTruck, "speedMetersPerSecond", 500f);

        CharacterController characterController = player.GetComponent<CharacterController>();
        characterController.enabled = false;
        player.transform.SetPositionAndRotation(
            departure.transform.position,
            departure.transform.rotation);
        characterController.enabled = true;
        Physics.SyncTransforms();

        yield return Tap(keyboard.fKey);
        yield return WaitForState(railTruck, "Driving", 1.5f);
        Assert.That(((Behaviour)player).enabled, Is.False,
            "Standing movement must be disabled while the camera is in the truck.");

        GameObject driverCameraPose = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Rail Truck/Driver Camera Pose");
        Assert.That(driverCameraPose, Is.Not.Null);
        InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(120f, -60f));
        InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0f, 120f));
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(
            Quaternion.Angle(
                playerCamera.transform.rotation,
                driverCameraPose.transform.rotation),
            Is.GreaterThan(1f),
            "Mouse movement must rotate the view independently of the rail truck.");
        Assert.That(playerCamera.fieldOfView, Is.LessThan(standingFieldOfView),
            "Scrolling up must zoom the truck view in.");

        PropertyInfo progressProperty = railTruckType.GetProperty("Progress01");
        Press(keyboard.aKey);
        yield return null;
        yield return null;
        Release(keyboard.aKey);
        yield return null;
        Assert.That((float)progressProperty.GetValue(railTruck), Is.EqualTo(0f),
            "Keys other than W must not move or steer the rail truck.");

        Press(keyboard.wKey);
        yield return WaitForState(railTruck, "Arrived", 2f);
        Release(keyboard.wKey);
        yield return null;

        Assert.That((float)progressProperty.GetValue(railTruck), Is.EqualTo(1f).Within(0.001f));

        yield return Tap(keyboard.fKey);
        yield return WaitForState(railTruck, "Completed", 1.5f);
        Assert.That(((Behaviour)player).enabled, Is.True,
            "Standing movement must be restored after exiting the truck.");
        Assert.That(playerCamera.fieldOfView, Is.EqualTo(standingFieldOfView).Within(0.01f),
            "Exiting the truck must restore the standing field of view.");
        Assert.That(
            Vector3.Distance(player.transform.position, exitPose.transform.position),
            Is.LessThan(0.05f));
        Assert.That(
            Physics.Raycast(
                player.transform.position + Vector3.up,
                Vector3.down,
                out _,
                2f),
            Is.True,
            "The player exit pose must have walkable ground beneath it.");
    }

    private static void SetPrivateField(Component component, string name, object value)
    {
        FieldInfo field = component.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Private test field '{name}' was not found.");
        field.SetValue(component, value);
    }

    private static string GetState(Component railTruck)
    {
        PropertyInfo property = railTruck.GetType().GetProperty("StateName");
        Assert.That(property, Is.Not.Null);
        return property.GetValue(railTruck) as string;
    }

    private static IEnumerator WaitForState(
        Component railTruck,
        string expected,
        float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (GetState(railTruck) != expected && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        Assert.That(GetState(railTruck), Is.EqualTo(expected));
    }

    private IEnumerator Tap(ButtonControl button)
    {
        Press(button);
        yield return null;
        Release(button);
        yield return null;
    }
}
