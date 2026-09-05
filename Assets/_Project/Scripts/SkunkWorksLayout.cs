using UnityEngine;

// Campus coordinates are relative to this placement in Ground Ops space.
public static class SkunkWorksLayout
{
    public static readonly Vector3 Origin = new(-78f, 7.8f, -48f);
    public const float GroundY = 7.5f;
    public const uint AtriumLayer = 1u << 8;
    public const uint ForgeLayer = 1u << 9;
    public const uint GardenLayer = 1u << 10;
    public const uint HorizonLayer = 1u << 11;
    public const uint AllLayers = AtriumLayer | ForgeLayer | GardenLayer | HorizonLayer;

    public static bool Contains(Vector3 groundOpsPosition)
    {
        Vector3 p = groundOpsPosition - Origin;
        return p.x >= -34f && p.x <= 34f && p.z >= -36f && p.z <= 30f && p.y >= -0.5f && p.y < 15f;
    }
    public static uint Zone(Vector3 p) => p.z < -9f ? HorizonLayer
        : p.x < -9f ? ForgeLayer : p.x > 9f ? GardenLayer : AtriumLayer;
    public static string Location(Vector3 p) => p.z > 10f ? "SKUNK WORKS / ARRIVAL TERRACE"
        : p.z < -9f ? "SKUNK WORKS / HORIZON ENGINE"
        : p.x < -9f ? "SKUNK WORKS / HELIOS FORGE"
        : p.x > 9f ? "SKUNK WORKS / VECTOR GARDEN" : "SKUNK WORKS / COMMISSIONING HALL";
}
