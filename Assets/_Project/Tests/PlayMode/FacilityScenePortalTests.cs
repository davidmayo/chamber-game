using System.Collections;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class FacilityScenePortalTests
{
    [UnityTest]
    public IEnumerator WalkingThroughChamberDoorsTransitionsBothWays()
    {
        yield return LoadSceneAndWait("GroundOps");
        yield return WalkPlayerThroughPortal("To Anechoic Chamber", true, 0f);
        yield return WaitForScene("Main");
        yield return new WaitForSecondsRealtime(3f);
        AssertPlayerAt("Main Ground Ops Arrival");
        AssertPlayerHasFloor();

        yield return WalkPlayerThroughPortal("To Ground Ops Hallway", true, -0.3f);
        yield return WaitForScene("GroundOps");
        yield return new WaitForSecondsRealtime(3f);
        AssertPlayerAt("Ground Ops Chamber Arrival");
        AssertPlayerHasFloor();
    }

    private static IEnumerator LoadSceneAndWait(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        yield return WaitForScene(sceneName);
        yield return null;
    }

    private static IEnumerator WalkPlayerThroughPortal(
        string portalName, bool travelTowardNegativeX, float floorY)
    {
        GameObject portal = GameObject.Find(portalName);
        Type playerType = Type.GetType("FirstPersonPlayerController, Assembly-CSharp");
        Component player = UnityEngine.Object.FindFirstObjectByType(playerType) as Component;
        Assert.That(portal, Is.Not.Null, $"Missing portal '{portalName}'.");
        Assert.That(player, Is.Not.Null, "Missing first-person player.");

        CharacterController controller = player.GetComponent<CharacterController>();
        Assert.That(controller, Is.Not.Null);
        controller.enabled = false;
        float startOffset = travelTowardNegativeX ? 1.4f : -1.4f;
        player.transform.position = new Vector3(
            portal.transform.position.x + startOffset,
            floorY,
            portal.transform.position.z);
        controller.enabled = true;
        Physics.SyncTransforms();

        Vector3 step = new(travelTowardNegativeX ? -0.16f : 0.16f, 0f, 0f);
        for (int index = 0; index < 20; index++)
        {
            controller.Move(step);
            Physics.SyncTransforms();
            // CharacterController movement and portal polling both happen on
            // rendered frames in the game, so exercise the same cadence here.
            yield return null;
            if (SceneManager.GetActiveScene().name
                != (portalName == "To Anechoic Chamber" ? "GroundOps" : "Main"))
            {
                yield break;
            }
        }
        // The portal fades to black before replacing the scene, so the scene
        // may intentionally remain unchanged when the physical crossing ends.
        // WaitForScene performs the bounded transition assertion afterward.
    }

    private static IEnumerator WaitForScene(string sceneName)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (SceneManager.GetActiveScene().name != sceneName
               && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
    }

    private static void AssertPlayerAt(string markerName)
    {
        GameObject marker = GameObject.Find(markerName);
        Type playerType = Type.GetType("FirstPersonPlayerController, Assembly-CSharp");
        Component player = UnityEngine.Object.FindFirstObjectByType(playerType) as Component;
        Assert.That(marker, Is.Not.Null, $"Missing arrival marker '{markerName}'.");
        Assert.That(player, Is.Not.Null);
        Vector2 playerHorizontal = new(player.transform.position.x, player.transform.position.z);
        Vector2 markerHorizontal = new(marker.transform.position.x, marker.transform.position.z);
        Assert.That(Vector2.Distance(playerHorizontal, markerHorizontal), Is.LessThan(0.05f));
    }

    private static void AssertPlayerHasFloor()
    {
        Type playerType = Type.GetType("FirstPersonPlayerController, Assembly-CSharp");
        Component player = UnityEngine.Object.FindFirstObjectByType(playerType) as Component;
        Assert.That(player, Is.Not.Null);
        Assert.That(player.transform.position.y, Is.GreaterThan(-0.5f),
            "Player fell below the facility floor after arriving.");
        Assert.That(Physics.Raycast(
            player.transform.position + Vector3.up * 0.25f,
            Vector3.down,
            out _,
            1.0f), Is.True, "No collider exists beneath the arrival point.");
    }
}
