using UnityEngine;

public static partial class GroundOpsSceneBuilder
{
    private static Vector3[] SkunkWorksRoadPoints() => SampleTerrainPath(new[]
    {
        new Vector3(TruckRoundaboutRoadFork.x, 0f, TruckRoundaboutRoadFork.y),
        new Vector3(-23f, 0f, -3f), new Vector3(-31f, 0f, -11f),
        new Vector3(-40f, 0f, -21f), new Vector3(-50f, 0f, -25f),
        new Vector3(-57f, 0f, -25f), new Vector3(-64f, 0f, -25f),
    }, 0f);

    private static void BuildSkunkWorksRoad(Transform operations, Transform exterior, FirstPersonPlayerController player)
    {
        Transform journey = exterior.Find("Rail Truck Journey");
        RailTruckController truck = journey.GetComponent<RailTruckController>();
        MeshCollider terrain = exterior.Find("Low-poly Mountain Ridge").GetComponent<MeshCollider>();
        Material pavement = GetMaterial("Skunk Works Approach Asphalt", new Color(0.075f, 0.105f, 0.13f), 0.15f, 0.5f);
        Material metal = GetMaterial("Skunk Works Road Furniture", new Color(0.11f, 0.18f, 0.22f), 0.6f, 0.5f);
        Material cyan = GetEmissiveMaterial("Skunk Works Road Cyan", new Color(0.07f, 0.7f, 1f), 3f);
        Vector3[] main = SkunkWorksRoadPoints();
        Vector3[] outbound = GetRailTruckRoundaboutConnectorPoints(true, 0f);
        Vector3[] inbound = GetRailTruckRoundaboutConnectorPoints(false, 0f);
        foreach (Vector3[] path in new[] { main, outbound, inbound }) ConformPathAboveTerrain(path, exterior, terrain, 0.25f);
        Transform branch = NewGroup("Skunk Works Road", journey);
        MeshObject("Skunk Works Access Road", branch,
            GetRailRoadMesh("SkunkWorksAccessRoad", main, 4.2f, exterior, terrain, 0.25f), pavement, true);
        Vector3[] routePoints = GetRailTruckJourneyRoutePoints(0f, main, outbound, inbound, out int stop);
        ConformPathAboveTerrain(routePoints, exterior, terrain, 0.25f);
        Transform route = NewGroup("Skunk Works Route Waypoints", branch);
        Transform[] waypoints = new Transform[routePoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
        {
            waypoints[i] = NewGroup($"Waypoint {i + 1:000}", route);
            waypoints[i].localPosition = routePoints[i];
        }
        Transform exit = NewGroup("Skunk Works Player Exit", branch);
        exit.localPosition = SkunkWorksLayout.Origin + new Vector3(13f, 0.02f, 19f);
        exit.localRotation = Quaternion.LookRotation(new Vector3(-13f, 0f, -7f));
        truck.ConfigureSkunkWorks(waypoints, stop, exit);
        for (int i = 5; i < main.Length - 1; i += 5)
        {
            Vector3 heading = (main[i + 1] - main[i - 1]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, heading).normalized;
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector3 point = main[i] + side * sign * 2.45f;
                point.y = MountainHeight(point.x, point.z);
                NullBox($"Approach Bollard {i} {sign}", branch, point + Vector3.up * 0.6f, new Vector3(0.12f, 1.2f, 0.12f), metal);
                NullBox($"Approach Marker {i} {sign}", branch, point + Vector3.up * 1.13f, new Vector3(0.15f, 0.10f, 0.15f), cyan);
            }
        }
        NullSign("Campus Road Sign", branch, main[12] + new Vector3(2.5f, 1.6f, 0f), new Vector3(1f, 0f, 1f),
            "SPACE SCIENCE CENTER\nSKUNK WORKS / LEVEL 02\nPROTOTYPE COMMISSIONING CAMPUS", 3.1f, 1.0f, metal);
        Transform directory = NewGroup("Skunk Works Travel Directory", operations);
        NullSign("Departure Directory", directory, new Vector3(-3.91f, 1.8f, -7.97f), Vector3.forward,
            "TRUCK DESTINATIONS\n1 / ANTENNA RIDGE\n2 / SKUNK WORKS\nSELECT IN CAB / W TO DEPART", 1.7f, 0.8f, metal);
        SetRendererMask(directory, HallwayRenderingLayer);
    }

    private static float SkunkWorksPadDistance(float x, float z) => DistanceOutsideRectangle(
        x - SkunkWorksLayout.Origin.x, z - SkunkWorksLayout.Origin.z, -35f, 35f, -38f, 32f);
}
