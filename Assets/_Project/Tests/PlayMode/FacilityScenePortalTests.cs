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
        yield return WalkPlayerIntoPortal("To Anechoic Chamber");
        yield return WaitForScene("Main");
        yield return null;
        AssertPlayerAt("Main Ground Ops Arrival");

        yield return WalkPlayerIntoPortal("To Ground Ops Hallway");
        yield return WaitForScene("GroundOps");
        yield return null;
        AssertPlayerAt("Ground Ops Chamber Arrival");
    }

    private static IEnumerator LoadSceneAndWait(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        yield return WaitForScene(sceneName);
        yield return null;
    }

    private static IEnumerator WalkPlayerIntoPortal(string portalName)
    {
        GameObject portal = GameObject.Find(portalName);
        Type playerType = Type.GetType("FirstPersonPlayerController, Assembly-CSharp");
        Component player = UnityEngine.Object.FindFirstObjectByType(playerType) as Component;
        Assert.That(portal, Is.Not.Null, $"Missing portal '{portalName}'.");
        Assert.That(player, Is.Not.Null, "Missing first-person player.");

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        player.transform.position = portal.transform.position;
        if (controller != null)
        {
            controller.enabled = true;
        }
        Physics.SyncTransforms();

        yield return new WaitForFixedUpdate();
        yield return null;
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
        Assert.That(Vector3.Distance(player.transform.position, marker.transform.position),
            Is.LessThan(0.05f));
    }
}
