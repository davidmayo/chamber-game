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
            "Chamber Geometry/Containing Room/Hallway Double Door"), Is.Null,
            "The hallway connection should be a plain wall opening for now.");
        Assert.That(GameObject.Find(
            "Chamber Geometry/Architecture/Left Wall - Door/Open Chamber Door"), Is.Null,
            "The chamber connection should be a plain wall opening for now.");
        Assert.That(GameObject.Find(
            "Chamber Geometry/Architecture/Left Wall - Door/Door Frame Header"), Is.Null,
            "The chamber connection should not retain formal door trim.");
        Assert.That(GameObject.Find(
            "Ground Ops Blockout/Architecture/Hallway and High Bay Blockout/Hallway End Door Wall"),
            Is.Null, "The former wall across the L turn must not return.");
        Assert.That(GameObject.Find(
            "Ground Ops Blockout/Architecture/Hallway and High Bay Blockout/Hallway L End Cap"),
            Is.Not.Null);
        Assert.That(GameObject.Find(
            "Ground Ops Blockout/Architecture/Hallway and High Bay Blockout/Server-to-Chamber Hall Wall Filler"),
            Is.Not.Null);

        foreach (Vector3 floorProbe in new[]
        {
            new Vector3(-2.0f, 0.5f, 2.5f),
            new Vector3(-2.5f, 0.5f, 2.5f),
            new Vector3(-3.0f, 0.5f, 2.5f),
            new Vector3(-4.0f, 0.5f, 5.5f),
            new Vector3(-4.5f, 0.5f, 5.5f),
            new Vector3(-5.0f, 0.5f, 5.5f),
        })
        {
            Assert.That(Physics.Raycast(floorProbe, Vector3.down, out RaycastHit hit, 1f), Is.True,
                $"No floor exists beneath chamber threshold probe {floorProbe}.");
            Assert.That(hit.point.y, Is.EqualTo(0f).Within(0.02f),
                $"The chamber threshold changes elevation at {floorProbe}.");
        }

        Component player = UnityEngine.Object.FindFirstObjectByType(playerType) as Component;
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = new Vector3(-3.75f, 0f, 5.5f);
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
        Assert.That(player.transform.position.y, Is.GreaterThan(-0.1f),
            "The player fell while crossing the chamber/hallway seam.");
        Assert.That(Physics.Raycast(
            player.transform.position + Vector3.up * 0.25f,
            Vector3.down,
            out RaycastHit hallwayHit,
            1.0f), Is.True, "There is no floor beneath the player in the hallway.");
        Assert.That(hallwayHit.point.y, Is.EqualTo(0f).Within(0.02f),
            "The containing room and hallway should share one floor elevation.");

        Transform groundOps = GameObject.Find("Ground Ops Blockout").transform;
        foreach (Vector3 localFloorProbe in new[]
        {
            new Vector3(6.8f, 0.5f, -7.0f),
            new Vector3(0f, 0.5f, -6.8f),
            new Vector3(6.8f, 0.5f, 26.5f),
        })
        {
            Vector3 worldProbe = groundOps.TransformPoint(localFloorProbe);
            Assert.That(Physics.Raycast(worldProbe, Vector3.down, out RaycastHit hit, 1f), Is.True,
                $"The completed L hallway has no floor under local point {localFloorProbe}.");
            Assert.That(hit.point.y, Is.EqualTo(0f).Within(0.02f));
        }

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
