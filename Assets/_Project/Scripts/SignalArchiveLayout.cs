using UnityEngine;

// Ground Ops-local coordinates; the archive stays inside the straight building
// facade beneath the DOC. Its passage joins the existing first-floor gallery.
public static class SignalArchiveLayout
{
    public const float FloorY = -7.1f;
    public const float CeilingY = -0.4f;
    public const uint RenderingLayer = 1u << 7;

    public static bool Contains(Vector3 local) => local.y >= FloorY - 0.2f && local.y < -0.4f
        && ((local.x >= -4.05f && local.x <= 5.65f && local.z >= -5.4f && local.z <= 4.45f)
        || (local.x >= 3.45f && local.x <= 5.65f && local.z > 4.35f && local.z < 11.9f));
}
