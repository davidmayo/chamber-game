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
    public IEnumerator PlayerCanCompleteRoundTripBetweenBuildingAndAntennaComplex()
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
        GameObject apron = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Antenna Complex Apron");
        GameObject roundabout = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Building Roundabout");
        GameObject outboundConnector = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Roundabout Outbound Connector");
        GameObject returnConnector = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Roundabout Return Connector");
        GameObject terrain = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Low-poly Mountain Ridge");
        Assert.That(departure, Is.Not.Null);
        Assert.That(exitPose, Is.Not.Null);
        Assert.That(road?.GetComponent<MeshCollider>(), Is.Not.Null,
            "The access road must be walkable after leaving the truck.");
        Assert.That(terrain?.GetComponent<MeshCollider>(), Is.Not.Null,
            "The antenna-complex terrain must be walkable after leaving the truck.");
        Assert.That(apron?.GetComponent<MeshCollider>(), Is.Not.Null,
            "The enlarged antenna-complex apron must be walkable.");
        Assert.That(apron?.GetComponent<BoxCollider>(), Is.Null,
            "The terrain-following apron must not retain its obsolete box collider.");
        Assert.That(roundabout?.GetComponent<MeshCollider>(), Is.Not.Null,
            "The building roundabout must be visible and drivable terrain.");
        MeshCollider terrainCollider = terrain.GetComponent<MeshCollider>();
        foreach (GameObject pavement in new[]
                 {
                     road,
                     outboundConnector,
                     returnConnector,
                     roundabout,
                 })
        {
            Assert.That(pavement, Is.Not.Null);
            AssertPavementAboveTerrain(pavement, terrainCollider);
        }

        Transform[] routeWaypoints =
            (Transform[])GetPrivateField(railTruck, "routeWaypoints");
        int antennaStopWaypointIndex =
            (int)GetPrivateField(railTruck, "antennaStopWaypointIndex");
        Assert.That(routeWaypoints.Length, Is.GreaterThan(4));
        Assert.That(antennaStopWaypointIndex,
            Is.InRange(1, routeWaypoints.Length - 2),
            "The antenna must be an intermediate stop on the closed forward route.");
        Assert.That(Vector3.Distance(
                routeWaypoints[0].position,
                routeWaypoints[^1].position),
            Is.LessThan(0.05f),
            "The route must return to the same DOC roundabout parking point.");

        string truckPath =
            "Ground Ops Blockout/Exterior Landscape/Rail Truck Journey/Rail Truck/";
        foreach (string windowName in new[]
                 {
                     "Windshield",
                     "Rear Window",
                     "Left Window",
                     "Right Window",
                 })
        {
            Assert.That(GameObject.Find(truckPath + windowName), Is.Not.Null,
                $"The rail truck must retain its {windowName.ToLowerInvariant()}.");
        }

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
        yield return WaitForState(railTruck, "DrivingToAntennas", 1.5f);
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
        yield return WaitForState(railTruck, "ArrivedAtAntennas", 2f);
        Release(keyboard.wKey);
        yield return null;

        Assert.That((float)progressProperty.GetValue(railTruck),
            Is.EqualTo(1f).Within(0.001f));

        yield return Tap(keyboard.fKey);
        yield return WaitForState(railTruck, "ParkedAtAntennas", 1.5f);
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

        yield return Tap(keyboard.fKey);
        yield return WaitForState(railTruck, "DrivingToDoc", 1.5f);
        Assert.That(((Behaviour)player).enabled, Is.False,
            "Standing movement must be disabled during the return trip.");
        Assert.That((float)progressProperty.GetValue(railTruck),
            Is.EqualTo(1f).Within(0.001f));

        Press(keyboard.wKey);
        yield return WaitForState(railTruck, "ArrivedAtDoc", 2f);
        Release(keyboard.wKey);
        yield return null;
        Assert.That((float)progressProperty.GetValue(railTruck),
            Is.EqualTo(0f).Within(0.001f));

        // At either endpoint W starts the next leg without forcing an exit.
        // Prove the closed route can be circulated for another complete lap.
        yield return Tap(keyboard.wKey);
        yield return WaitForState(railTruck, "DrivingToAntennas", 1.5f);
        Press(keyboard.wKey);
        yield return WaitForState(railTruck, "ArrivedAtAntennas", 2f);
        Release(keyboard.wKey);
        yield return null;
        Assert.That((float)progressProperty.GetValue(railTruck),
            Is.EqualTo(1f).Within(0.001f));

        yield return Tap(keyboard.wKey);
        yield return WaitForState(railTruck, "DrivingToDoc", 1.5f);
        Press(keyboard.wKey);
        yield return WaitForState(railTruck, "ArrivedAtDoc", 2f);
        Release(keyboard.wKey);
        yield return null;
        Assert.That((float)progressProperty.GetValue(railTruck),
            Is.EqualTo(0f).Within(0.001f));

        yield return Tap(keyboard.fKey);
        yield return WaitForState(railTruck, "ParkedAtDoc", 1.5f);
        Assert.That(((Behaviour)player).enabled, Is.True,
            "Standing movement must be restored after returning to the building.");
        Assert.That(playerCamera.fieldOfView, Is.EqualTo(standingFieldOfView).Within(0.01f),
            "Returning to the building must restore the standing field of view.");
        Assert.That(
            Vector3.Distance(player.transform.position, departure.transform.position),
            Is.LessThan(0.05f),
            "The final F press must place the player back inside the hallway.");
    }

    private static void SetPrivateField(Component component, string name, object value)
    {
        FieldInfo field = component.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Private test field '{name}' was not found.");
        field.SetValue(component, value);
    }

    private static object GetPrivateField(Component component, string name)
    {
        FieldInfo field = component.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Private test field '{name}' was not found.");
        return field.GetValue(component);
    }

    private static void AssertPavementAboveTerrain(
        GameObject pavement,
        MeshCollider terrainCollider)
    {
        Mesh mesh = pavement.GetComponent<MeshFilter>()?.sharedMesh;
        Assert.That(mesh, Is.Not.Null, $"{pavement.name} must have a generated mesh.");
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        foreach (Vector3 vertex in vertices)
        {
            AssertPointAboveTerrain(pavement, vertex, terrainCollider);
        }

        for (int index = 0; index < triangles.Length; index += 3)
        {
            Vector3 centroid = (
                vertices[triangles[index]]
                + vertices[triangles[index + 1]]
                + vertices[triangles[index + 2]]) / 3f;
            AssertPointAboveTerrain(pavement, centroid, terrainCollider);
        }
    }

    private static void AssertPointAboveTerrain(
        GameObject pavement,
        Vector3 localPoint,
        MeshCollider terrainCollider)
    {
        Vector3 worldPoint = pavement.transform.TransformPoint(localPoint);
        Ray ray = new(worldPoint + Vector3.up * 30f, Vector3.down);
        Assert.That(terrainCollider.Raycast(ray, out RaycastHit hit, 80f), Is.True,
            $"Terrain was not found beneath {pavement.name} at {worldPoint}.");
        Assert.That(worldPoint.y - hit.point.y, Is.GreaterThan(0.05f),
            $"{pavement.name} intersects terrain at {worldPoint}.");
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
