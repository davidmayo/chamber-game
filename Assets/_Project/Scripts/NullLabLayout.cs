using UnityEngine;

// All positions are in Ground Ops-local coordinates. The first floor shares
// the high bay's elevation; the lab and its stairs are independent room boxes.
public static class NullLabLayout
{
    public const float FloorY = -7.1f;
    public const float CeilingY = -3.65f;
    public const float StairTopX = 4.2f;
    public const float StairTurnX = -1.6f;
    public const float UpperFlightZ = 26.55f;
    public const float LowerFlightZ = 24.65f;
    public const float StairWidth = 1.3f;
    public const int StepsPerFlight = 20;
    public const uint RenderingLayer = 1u << 6;

    public static bool Contains(Vector3 local) => local.x >= -3.45f && local.x <= 5.65f
        && local.z >= 11.85f && local.z <= 27.4f && local.y >= FloorY - 0.2f && local.y <= 2.9f
        && (local.z > 25.85f || local.y < -0.4f);

    public static string Location(Vector3 local) => local.z >= 23.95f ? "STAIR 01 / QUIET SERVICES"
        : local.x > 3.55f ? "LEVEL 01 / CABLE GALLERY"
        : local.z > 19.3f ? "LEVEL 01 / NULL CELL" : "LEVEL 01 / NULL REFERENCE LAB";
}
