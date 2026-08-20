using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Positions the Ground Ops sun from a local Eastern civil date/time and the
/// real DOC coordinates. The site follows US Eastern daylight-saving rules.
/// </summary>
[ExecuteAlways]
public sealed class GroundOpsSkyController : MonoBehaviour
{
    [SerializeField] private Light sun;
    [SerializeField] private Material skyMaterial;
    [SerializeField] private Vector3 worldNorth = new(0f, 0f, 1f);
    [SerializeField] private Vector3 worldEast = new(1f, 0f, 0f);
    [SerializeField] private double latitudeDegrees = 38.1908805556;
    [SerializeField] private double longitudeDegrees = -83.4300361111;
    [SerializeField] private int year = 2026;
    [SerializeField] private int month = 8;
    [SerializeField] private int day = 19;
    [SerializeField] private int hour = 14;
    [SerializeField] private int minute;
    [SerializeField, HideInInspector] private float solarAzimuthDegrees;
    [SerializeField, HideInInspector] private float solarElevationDegrees;
    [SerializeField, HideInInspector] private bool easternDaylightTime;

    public int Year => year;
    public int Month => month;
    public int Day => day;
    public int Hour => hour;
    public int Minute => minute;
    public float SolarAzimuthDegrees => solarAzimuthDegrees;
    public float SolarElevationDegrees => solarElevationDegrees;
    public bool EasternDaylightTime => easternDaylightTime;
    public string TimeZoneAbbreviation => easternDaylightTime ? "EDT" : "EST";

    public void Configure(
        Light controlledSun,
        Material controlledSkyMaterial,
        Vector3 northDirection,
        Vector3 eastDirection,
        double latitude,
        double longitude)
    {
        sun = controlledSun;
        skyMaterial = controlledSkyMaterial;
        worldNorth = Vector3.ProjectOnPlane(northDirection, Vector3.up).normalized;
        worldEast = Vector3.ProjectOnPlane(eastDirection, Vector3.up).normalized;
        latitudeDegrees = latitude;
        longitudeDegrees = longitude;
        ApplySky();
    }

    public void SetLocalDateTime(
        int newYear,
        int newMonth,
        int newDay,
        int newHour,
        int newMinute)
    {
        year = Mathf.Clamp(newYear, 1900, 2200);
        month = Mathf.Clamp(newMonth, 1, 12);
        day = Mathf.Clamp(newDay, 1, DateTime.DaysInMonth(year, month));
        hour = Mathf.Clamp(newHour, 0, 23);
        minute = Mathf.Clamp(newMinute, 0, 59);
        ApplySky();
    }

    public void SetToCurrentLocalTime()
    {
        DateTime now = DateTime.Now;
        SetLocalDateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute);
    }

    public void ApplySky()
    {
        if (sun == null || skyMaterial == null) return;

        DateTime localTime = SafeLocalDateTime();
        easternDaylightTime = IsEasternDaylightTime(localTime);
        double utcOffsetHours = easternDaylightTime ? -4.0 : -5.0;
        CalculateSolarPosition(
            localTime,
            utcOffsetHours,
            latitudeDegrees,
            longitudeDegrees,
            out double azimuth,
            out double elevation);
        solarAzimuthDegrees = (float)azimuth;
        solarElevationDegrees = (float)elevation;

        float azimuthRadians = solarAzimuthDegrees * Mathf.Deg2Rad;
        float elevationRadians = solarElevationDegrees * Mathf.Deg2Rad;
        Vector3 horizontalDirection =
            worldNorth * Mathf.Cos(azimuthRadians)
            + worldEast * Mathf.Sin(azimuthRadians);
        Vector3 directionToSun = (
            horizontalDirection * Mathf.Cos(elevationRadians)
            + Vector3.up * Mathf.Sin(elevationRadians)).normalized;
        sun.transform.rotation = Quaternion.LookRotation(-directionToSun, Vector3.up);

        float daylight = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(-6f, 10f, solarElevationDegrees));
        float warmSun = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(-2f, 18f, solarElevationDegrees));
        sun.enabled = solarElevationDegrees > -6f;
        sun.intensity = 1.15f * daylight;
        sun.color = Color.Lerp(
            new Color(1f, 0.48f, 0.22f),
            new Color(1f, 0.96f, 0.88f),
            warmSun);
        sun.shadows = LightShadows.Soft;

        Color skyTint = Color.Lerp(
            new Color(0.025f, 0.035f, 0.075f),
            new Color(0.32f, 0.55f, 0.82f),
            daylight);
        Color groundTint = Color.Lerp(
            new Color(0.015f, 0.018f, 0.028f),
            new Color(0.28f, 0.31f, 0.27f),
            daylight);
        if (skyMaterial.HasProperty("_SkyTint")) skyMaterial.SetColor("_SkyTint", skyTint);
        if (skyMaterial.HasProperty("_GroundColor")) skyMaterial.SetColor("_GroundColor", groundTint);
        if (skyMaterial.HasProperty("_Exposure")) skyMaterial.SetFloat("_Exposure", Mathf.Lerp(0.12f, 1.05f, daylight));
        if (skyMaterial.HasProperty("_AtmosphereThickness")) skyMaterial.SetFloat("_AtmosphereThickness", 0.75f);
        if (skyMaterial.HasProperty("_SunSize")) skyMaterial.SetFloat("_SunSize", 0.025f);
        if (skyMaterial.HasProperty("_SunSizeConvergence")) skyMaterial.SetFloat("_SunSizeConvergence", 5f);

        RenderSettings.skybox = skyMaterial;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = Mathf.Lerp(0.08f, 0.72f, daylight);
        DynamicGI.UpdateEnvironment();
    }

    private void OnEnable()
    {
        ApplySky();
    }

    private void OnValidate()
    {
        year = Mathf.Clamp(year, 1900, 2200);
        month = Mathf.Clamp(month, 1, 12);
        day = Mathf.Clamp(day, 1, DateTime.DaysInMonth(year, month));
        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);
        ApplySky();
    }

    private DateTime SafeLocalDateTime()
    {
        int safeYear = Mathf.Clamp(year, 1900, 2200);
        int safeMonth = Mathf.Clamp(month, 1, 12);
        int safeDay = Mathf.Clamp(day, 1, DateTime.DaysInMonth(safeYear, safeMonth));
        return new DateTime(safeYear, safeMonth, safeDay,
            Mathf.Clamp(hour, 0, 23), Mathf.Clamp(minute, 0, 59), 0,
            DateTimeKind.Unspecified);
    }

    private static bool IsEasternDaylightTime(DateTime localTime)
    {
        int marchStartDay = NthWeekdayOfMonth(localTime.Year, 3, DayOfWeek.Sunday, 2);
        int novemberEndDay = NthWeekdayOfMonth(localTime.Year, 11, DayOfWeek.Sunday, 1);
        DateTime start = new(localTime.Year, 3, marchStartDay, 2, 0, 0);
        DateTime end = new(localTime.Year, 11, novemberEndDay, 2, 0, 0);
        return localTime >= start && localTime < end;
    }

    private static int NthWeekdayOfMonth(
        int targetYear,
        int targetMonth,
        DayOfWeek weekday,
        int occurrence)
    {
        DateTime first = new(targetYear, targetMonth, 1);
        int offset = ((int)weekday - (int)first.DayOfWeek + 7) % 7;
        return 1 + offset + (occurrence - 1) * 7;
    }

    private static void CalculateSolarPosition(
        DateTime localTime,
        double utcOffsetHours,
        double latitude,
        double longitude,
        out double azimuthDegrees,
        out double elevationDegrees)
    {
        DateTime utc = DateTime.SpecifyKind(
            localTime.AddHours(-utcOffsetHours),
            DateTimeKind.Utc);
        double julianDay = 2451545.0
            + (utc - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays;
        double century = (julianDay - 2451545.0) / 36525.0;
        double meanLongitude = NormalizeDegrees(
            280.46646 + century * (36000.76983 + century * 0.0003032));
        double meanAnomaly = 357.52911
            + century * (35999.05029 - 0.0001537 * century);
        double eccentricity = 0.016708634
            - century * (0.000042037 + 0.0000001267 * century);
        double anomalyRadians = DegreesToRadians(meanAnomaly);
        double equationOfCenter =
            Math.Sin(anomalyRadians) * (1.914602 - century * (0.004817 + 0.000014 * century))
            + Math.Sin(2.0 * anomalyRadians) * (0.019993 - 0.000101 * century)
            + Math.Sin(3.0 * anomalyRadians) * 0.000289;
        double trueLongitude = meanLongitude + equationOfCenter;
        double apparentLongitude = trueLongitude - 0.00569
            - 0.00478 * Math.Sin(DegreesToRadians(125.04 - 1934.136 * century));
        double meanObliquity = 23.0 + (
            26.0 + (21.448 - century * (
                46.815 + century * (0.00059 - century * 0.001813))) / 60.0) / 60.0;
        double correctedObliquity = meanObliquity
            + 0.00256 * Math.Cos(DegreesToRadians(125.04 - 1934.136 * century));
        double obliquityRadians = DegreesToRadians(correctedObliquity);
        double declinationRadians = Math.Asin(
            Math.Sin(obliquityRadians) * Math.Sin(DegreesToRadians(apparentLongitude)));

        double y = Math.Tan(obliquityRadians / 2.0);
        y *= y;
        double longitudeRadians = DegreesToRadians(meanLongitude);
        double equationOfTime = 4.0 * RadiansToDegrees(
            y * Math.Sin(2.0 * longitudeRadians)
            - 2.0 * eccentricity * Math.Sin(anomalyRadians)
            + 4.0 * eccentricity * y * Math.Sin(anomalyRadians) * Math.Cos(2.0 * longitudeRadians)
            - 0.5 * y * y * Math.Sin(4.0 * longitudeRadians)
            - 1.25 * eccentricity * eccentricity * Math.Sin(2.0 * anomalyRadians));

        double localMinutes = localTime.Hour * 60.0 + localTime.Minute + localTime.Second / 60.0;
        double trueSolarMinutes = NormalizeMinutes(
            localMinutes + equationOfTime + 4.0 * longitude - 60.0 * utcOffsetHours);
        double hourAngleDegrees = trueSolarMinutes / 4.0 - 180.0;
        double hourAngleRadians = DegreesToRadians(hourAngleDegrees);
        double latitudeRadians = DegreesToRadians(latitude);
        double cosineZenith =
            Math.Sin(latitudeRadians) * Math.Sin(declinationRadians)
            + Math.Cos(latitudeRadians) * Math.Cos(declinationRadians) * Math.Cos(hourAngleRadians);
        cosineZenith = Math.Max(-1.0, Math.Min(1.0, cosineZenith));
        double zenithRadians = Math.Acos(cosineZenith);
        elevationDegrees = 90.0 - RadiansToDegrees(zenithRadians);
        azimuthDegrees = NormalizeDegrees(RadiansToDegrees(Math.Atan2(
            Math.Sin(hourAngleRadians),
            Math.Cos(hourAngleRadians) * Math.Sin(latitudeRadians)
            - Math.Tan(declinationRadians) * Math.Cos(latitudeRadians))) + 180.0);
    }

    private static double NormalizeDegrees(double value)
    {
        value %= 360.0;
        return value < 0.0 ? value + 360.0 : value;
    }

    private static double NormalizeMinutes(double value)
    {
        value %= 1440.0;
        return value < 0.0 ? value + 1440.0 : value;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}
