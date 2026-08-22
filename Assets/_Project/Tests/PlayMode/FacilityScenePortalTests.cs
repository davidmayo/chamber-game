using System.Collections;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class FacilityContinuousWorldTests
{
    [UnityTest]
    public IEnumerator PlayerCanWalkFromChamberIntoHallwayWithoutLoadingAScene()
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null;

        Assert.That(GameObject.Find("Chamber Geometry"), Is.Not.Null);
        Assert.That(GameObject.Find("Ground Ops Blockout"), Is.Not.Null);
        Type playerType = Type.GetType("FirstPersonPlayerController, Assembly-CSharp");
        Assert.That(playerType, Is.Not.Null);
        Assert.That(
            UnityEngine.Object.FindObjectsByType(playerType, FindObjectsSortMode.None),
            Has.Length.EqualTo(1),
            "The continuous facility must have exactly one player and camera owner.");
        Assert.That(GameObject.Find("To Anechoic Chamber"), Is.Null);
        Assert.That(GameObject.Find("To Ground Ops Hallway"), Is.Null);
        Assert.That(GameObject.Find(
            "Chamber Geometry/Containing Room/Hallway Double Door/Front Open Leaf")
            ?.GetComponent<Collider>(), Is.Null,
            "A parked-open hallway door must not trap the player against its leaf.");
        Assert.That(GameObject.Find(
            "Chamber Geometry/Architecture/Left Wall - Door/Open Chamber Door")
            ?.GetComponent<Collider>(), Is.Null,
            "The parked-open chamber door must not obstruct its approach.");

        Component player = UnityEngine.Object.FindFirstObjectByType(playerType) as Component;
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = new Vector3(-3.75f, -0.3f, 5.5f);
        player.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        controller.enabled = true;
        Physics.SyncTransforms();

        for (int step = 0; step < 24; step++)
        {
            controller.Move(Vector3.left * 0.15f);
            Physics.SyncTransforms();
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
        Assert.That(player.transform.position.x, Is.LessThan(-6.0f));
        Assert.That(player.transform.position.y, Is.GreaterThan(-0.5f),
            "The player fell while crossing the chamber/hallway seam.");
        Assert.That(Physics.Raycast(
            player.transform.position + Vector3.up * 0.25f,
            Vector3.down,
            out _,
            1.0f), Is.True, "There is no floor beneath the player in the hallway.");

        Transform forest = GameObject.Find(
            "Ground Ops Blockout/Exterior Landscape/Low-poly Forest")?.transform;
        Assert.That(forest, Is.Not.Null);
        foreach (MeshFilter meshFilter in forest.GetComponentsInChildren<MeshFilter>())
        {
            foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
            {
                Vector3 worldVertex = meshFilter.transform.TransformPoint(vertex);
                bool insideChamberSite =
                    worldVertex.x >= -8.5f && worldVertex.x <= 5.5f
                    && worldVertex.z >= -10f && worldVertex.z <= 9f;
                Assert.That(insideChamberSite, Is.False,
                    $"Forest geometry intrudes into the chamber site at {worldVertex}.");
            }
        }
    }
}
