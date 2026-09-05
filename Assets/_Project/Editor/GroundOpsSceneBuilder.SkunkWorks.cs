using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class GroundOpsSceneBuilder
{
    private static void BuildSkunkWorks(Transform operations, Transform exterior, FirstPersonPlayerController player)
    {
        BuildSkunkWorksRoad(operations, exterior, player);
        Transform campus = NewGroup("Level 02 - Space Science Center Skunk Works", operations);
        campus.localPosition = SkunkWorksLayout.Origin;
        Material white = GetMaterial("Skunk Works Ceramic", new Color(0.65f, 0.74f, 0.78f), 0.25f, 0.65f);
        Material dark = GetMaterial("Skunk Works Obsidian", new Color(0.018f, 0.034f, 0.057f), 0.55f, 0.68f);
        Material floor = GetMaterial("Skunk Works Silver Floor", new Color(0.24f, 0.34f, 0.41f), 0.5f, 0.62f);
        Material metal = GetMaterial("Skunk Works Titanium", new Color(0.28f, 0.39f, 0.46f), 0.85f, 0.7f);
        Material cyan = GetEmissiveMaterial("Skunk Works Cyan", new Color(0.06f, 0.83f, 1f), 3.2f);
        Material amber = GetEmissiveMaterial("Skunk Works Amber", new Color(1f, 0.31f, 0.035f), 3.4f);
        Material mint = GetEmissiveMaterial("Skunk Works Mint", new Color(0.10f, 1f, 0.62f), 3f);
        Material violet = GetEmissiveMaterial("Skunk Works Violet", new Color(0.58f, 0.15f, 1f), 3.4f);
        Transform terrace = NewGroup("Arrival Terrace", campus);
        NullBox("Arrival Platform", terrace, new Vector3(0f, -0.22f, 20f), new Vector3(70f, 0.44f, 20f), floor);
        // This broad forecourt also contains the truck's forward turnaround.
        for (int i = 0; i < 9; i++)
            NullBox($"Arrival Inlay {i}", terrace, new Vector3(-31f + i * 7.8f, 0.008f, 20f), new Vector3(0.035f, 0.014f, 19f), metal);
        foreach (float x in new[] { -34f, 34f })
        {
            NullBox($"Terrace Edge {x}", terrace, new Vector3(x, 0.06f, 20f), new Vector3(0.12f, 0.12f, 20f), cyan);
            for (int i = 0; i < 5; i++)
                NullBox($"Terrace Bollard {x} {i}", terrace, new Vector3(x, 0.7f, 12f + i * 4f), new Vector3(0.15f, 1.4f, 0.15f), white);
        }
        NullBox("Entrance Runway", terrace, new Vector3(0f, 0.006f, 16.5f), new Vector3(4.8f, 0.01f, 13f), dark);
        foreach (float x in new[] { -2.5f, 2.5f })
            NullBox($"Entrance Guide {x}", terrace, new Vector3(x, 0.018f, 16.5f), new Vector3(0.05f, 0.012f, 13f), cyan);
        NullSign("Shuttle Stop", terrace, new Vector3(15f, 1.6f, 18.5f), Vector3.back,
            "SSC / TRANSIT\nF TO BOARD\nSPACE SCIENCE CENTER", 1.65f, 0.7f, dark);
        NullBox("Stop Sign Post", terrace, new Vector3(15f, 0.65f, 18.6f), new Vector3(0.10f, 1.3f, 0.10f), metal);
        // A pair of leaning blades frames the reveal from the road.
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject fin = NullBox($"Arrival Blade {side}", terrace, new Vector3(side * 9f, 5.4f, 12.5f), new Vector3(0.75f, 11f, 1.5f), white);
            fin.transform.localRotation = Quaternion.Euler(0f, 0f, side * -16f);
            GameObject glow = NullBox($"Arrival Blade Light {side}", terrace, new Vector3(side * 8.58f, 5.5f, 13.28f), new Vector3(0.09f, 10.5f, 0.04f), cyan);
            glow.transform.localRotation = fin.transform.localRotation;
        }
        NullSign("Campus Name", terrace, new Vector3(0f, 9.65f, 11.6f), Vector3.forward,
            "SPACE SCIENCE CENTER\nS K U N K   W O R K S", 16f, 2.5f, dark);
        NullSign("Campus Motto", terrace, new Vector3(0f, 5.85f, 10.25f), Vector3.forward,
            "LEVEL 02 / MAKE THE IMPOSSIBLE REPEATABLE", 9f, 0.55f, dark);
        for (int i=0;i<3;i++)
            NullLamp($"Facade Wash {i}", terrace, new Vector3(-25f+i*25f,9f,28f), new Vector3(0f,0.05f,-1f),
                new Color(0.72f,0.88f,1f),1500f,65f,cyan,false);
        SkunkRing("Crown Structure",terrace,new Vector3(0f,12.6f,-1f),13f,0.24f,white,Quaternion.Euler(64f,0f,12f));
        SkunkRing("Crown Light",terrace,new Vector3(0f,12.6f,-1f),13.28f,0.08f,cyan,Quaternion.Euler(64f,0f,12f));
        foreach(float x in new[] { -7f,7f })
            NullBox($"Crown Pylon {x}",terrace,new Vector3(x,12f,-1f),new Vector3(0.45f,4.5f,0.7f),white);
        SetRendererMask(terrace, ExteriorRenderingLayer);
        SetLightMask(terrace, ExteriorRenderingLayer);

        Transform atrium = NewGroup("Commissioning Hall", campus);
        BuildSkunkRoom(atrium, -8f, 8f, -8f, 10f, 12f, true, true, true, true, white, dark, floor, cyan);
        Transform forge = NewGroup("Helios Forge", campus);
        BuildSkunkRoom(forge, -29f, -10f, -7f, 11f, 10f, false, true, false, false, white, dark, floor, amber);
        Transform garden = NewGroup("Vector Garden", campus);
        BuildSkunkRoom(garden, 10f, 29f, -7f, 11f, 10f, true, false, false, false, white, dark, floor, mint);
        Transform horizon = NewGroup("Horizon Engine", campus);
        BuildSkunkRoom(horizon, -12f, 12f, -34f, -10f, 12f, false, false, false, true, white, dark, floor, violet);
        BuildSkunkLink("Forge Link", campus, new Vector3(-9f, 0f, 0f), new Vector2(2f, 4f), false, white, dark, amber);
        BuildSkunkLink("Garden Link", campus, new Vector3(9f, 0f, 0f), new Vector2(2f, 4f), false, white, dark, mint);
        BuildSkunkLink("Horizon Link", campus, new Vector3(0f, 0f, -9f), new Vector2(5f, 2f), true, white, dark, violet);
        NullSign("Hall Welcome", atrium, new Vector3(0f, 7.8f, -7.84f), Vector3.forward,
            "FIRST LIGHT\nPROTOTYPE COMMISSIONING / 02", 10f, 1.4f, dark);
        NullSign("Forge Directory", atrium, new Vector3(-7.82f, 5.2f, 0f), Vector3.right,
            "01 / HELIOS FORGE\nSTABILIZE THE SOURCE", 5.5f, 0.85f, dark);
        NullSign("Garden Directory", atrium, new Vector3(7.82f, 5.2f, 0f), Vector3.left,
            "02 / VECTOR GARDEN\nCALIBRATE THE FIELD", 5.5f, 0.85f, dark);
        NullSign("Horizon Directory", atrium, new Vector3(0f, 5.2f, -7.83f), Vector3.forward,
            "03 / HORIZON ENGINE\nOPEN A NEW HORIZON", 5.7f, 0.8f, dark);
        NullSign("Forge Identity", forge, new Vector3(-19.5f, 6.9f, -6.83f), Vector3.forward,
            "H E L I O S\nP R O T O T Y P E   E N E R G Y   F O R G E", 12f, 1.35f, dark);
        NullSign("Garden Identity", garden, new Vector3(19.5f, 6.9f, -6.83f), Vector3.forward,
            "V E C T O R\nL E V I T A T I O N   G A R D E N", 12f, 1.35f, dark);
        NullSign("Horizon Identity", horizon, new Vector3(0f, 9f, -33.83f), Vector3.forward,
            "H O R I Z O N\nE X P E R I M E N T A L   T R A N S I T", 15f, 1.6f, dark);
        // A suspended sculpture in the hall establishes a visibly different campus.
        Transform emblem = NewGroup("Suspended Commissioning Emblem", atrium);
        emblem.localPosition = new Vector3(0f, 7.2f, 2.4f);
        for (int i = 0; i < 3; i++)
            SkunkRing($"Emblem Orbit {i}", emblem, Vector3.zero, 2.0f + i * 0.38f, 0.045f, cyan,
                Quaternion.Euler(25f + i * 58f, i * 60f, i * 24f));
        for (int i = 0; i < 3; i++)
            NullSign($"Commissioning Pillar {i}", atrium, new Vector3(-7.76f, 1.8f, 4f + i*2f), Vector3.right,
                i == 0 ? "01 / POWER\nHELIOS FORGE" : i == 1 ? "02 / FIELD\nVECTOR GARDEN" : "03 / FIRST LIGHT\nHORIZON ENGINE", 1.75f, 0.85f, dark);

        HeliosForgeController source=BuildHelios(campus,forge,player,metal,dark,amber,white);
        GetOrAddComponent<SkunkWorksCommissioning>(campus.gameObject).Configure(player,source);
        ConfigureSkunkZone(atrium, SkunkWorksLayout.AtriumLayer, new Vector3(0f, 6f, 1f), new Vector3(16f, 12f, 18f), new Color(0.25f, 0.72f, 1f));
        ConfigureSkunkZone(forge, SkunkWorksLayout.ForgeLayer, new Vector3(-19.5f, 5f, 2f), new Vector3(19f, 10f, 18f), new Color(1f, 0.42f, 0.15f));
        ConfigureSkunkZone(garden, SkunkWorksLayout.GardenLayer, new Vector3(19.5f, 5f, 2f), new Vector3(19f, 10f, 18f), new Color(0.12f, 1f, 0.75f));
        ConfigureSkunkZone(horizon, SkunkWorksLayout.HorizonLayer, new Vector3(0f, 6f, -22f), new Vector3(24f, 12f, 24f), new Color(0.48f, 0.30f, 1f));
        // Stable walking markers are used by the end-to-end route checks.
        Transform walk = NewGroup("Walking Route", campus);
        Vector3[] points = { new(13f,0f,19f), new(0f,0f,18f), new(0f,0f,12f), new(0f,0f,8f),
            new(-5f,0f,0f), new(-12f,0f,0f), new(-19.5f,0f,7.1f), new(-12f,0f,0f), new(0f,0f,0f),
            new(12f,0f,0f), new(19.5f,0f,6.5f), new(12f,0f,0f), new(0f,0f,0f), new(0f,0f,-12f), new(0f,0f,-16f) };
        for (int i = 0; i < points.Length; i++) NewGroup($"Route {i:00}", walk).localPosition = points[i];
        foreach(Text label in campus.GetComponentsInChildren<Text>(true))
        {
            label.resizeTextMinSize=18;
            label.resizeTextMaxSize=label.fontSize;
            label.resizeTextForBestFit=true;
            label.verticalOverflow=VerticalWrapMode.Truncate;
            label.rectTransform.offsetMin=new Vector2(8f,4f);
            label.rectTransform.offsetMax=new Vector2(-8f,-4f);
        }
    }

    private static void BuildSkunkRoom(Transform room, float x0, float x1, float z0, float z1, float height,
        bool westDoor, bool eastDoor, bool southDoor, bool northDoor, Material wall, Material dark, Material floor, Material glow)
    {
        Vector3 center = new((x0+x1)/2f, 0f, (z0+z1)/2f);
        NullBox("Floor", room, center + Vector3.down * 0.14f, new Vector3(x1-x0, 0.28f, z1-z0), floor);
        NullBox("Skunk Works Ceiling", room, center + Vector3.up * (height+0.18f), new Vector3(x1-x0+0.5f, 0.36f, z1-z0+0.5f), dark);
        SkunkWall("West", room, true, x0-0.125f, z0, z1, height, westDoor, wall);
        SkunkWall("East", room, true, x1+0.125f, z0, z1, height, eastDoor, wall);
        SkunkWall("South", room, false, z0-0.125f, x0, x1, height, southDoor, wall);
        if (x1 < 0f || x0 > 0f) BuildSkunkWindowWall(room,x0,x1,z1,height,wall,dark);
        else SkunkWall("North", room, false, z1+0.125f, x0, x1, height, northDoor, wall);
        for (int i = 0; i < 5; i++)
        {
            float z = Mathf.Lerp(z0+1f, z1-1f, i/4f);
            foreach (float x in new[] { x0+0.35f, x1-0.35f })
            {
                if (Mathf.Abs(z)<2.6f && (x<center.x ? westDoor : eastDoor)) continue;
                NullBox($"Wall Fin {i} {x}", room, new Vector3(x, height*0.48f, z), new Vector3(0.30f, height*0.96f, 0.30f), dark);
                NullBox($"Fin Light {i} {x}", room, new Vector3(x+(x<center.x?0.16f:-0.16f), height*0.6f, z), new Vector3(0.045f, height*0.6f, 0.095f), glow);
            }
            NullBox($"Ceiling Blade {i}", room, new Vector3(center.x, height-0.15f, z), new Vector3(x1-x0-0.7f, 0.20f, 0.20f), wall);
            NullBox($"Ceiling Blade Light {i}", room, new Vector3(center.x, height-0.27f, z), new Vector3(x1-x0-2f, 0.045f, 0.07f), glow);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new(Mathf.Lerp(x0+3f,x1-3f,i%2), height-0.6f, Mathf.Lerp(z0+3f,z1-3f,i/2));
            NullLamp($"Ceiling Wash {i}", room, p, Vector3.down, new Color(0.60f, 0.80f, 1f), 170f, height+7f, glow, false);
        }
        // A dark lower wall band and floor inlays give the bright shell scale.
        foreach (float z in new[] { z0+0.3f, z1-0.3f })
            NullBox($"Floor Seam {z}", room, new Vector3(center.x, 0.009f, z), new Vector3(x1-x0-1f, 0.015f, 0.035f), glow);
    }

    private static void SkunkWall(string name, Transform room, bool alongZ, float fixedCoordinate,
        float min, float max, float height, bool door, Material material)
    {
        void Segment(string suffix, float a, float b, float bottom, float top)
        {
            Vector3 p = alongZ ? new(fixedCoordinate,(bottom+top)/2f,(a+b)/2f) : new((a+b)/2f,(bottom+top)/2f,fixedCoordinate);
            Vector3 size = alongZ ? new(0.25f,top-bottom,b-a) : new(b-a,top-bottom,0.25f);
            NullBox(name+suffix, room, p, size, material);
        }
        if (!door) { Segment(" Wall", min, max, 0f, height); return; }
        float half = alongZ ? 2f : 2.5f;
        Segment(" Left", min, -half, 0f, height);
        Segment(" Right", half, max, 0f, height);
        Segment(" Header", -half, half, 4.5f, height);
    }

    private static void BuildSkunkWindowWall(Transform room,float x0,float x1,float z,float height,Material wall,Material dark)
    {
        float center=(x0+x1)/2f;
        NullBox("North Window Sill",room,new Vector3(center,1.15f,z+0.125f),new Vector3(x1-x0,2.3f,0.25f),wall);
        NullBox("North Window Header",room,new Vector3(center,(height+6.5f)/2f,z+0.125f),new Vector3(x1-x0,height-6.5f,0.25f),wall);
        const int panes=6;
        float width=(x1-x0)/panes;
        for(int i=0;i<panes;i++)
        {
            ReusablePrefabInstance($"Facade Window {i}",room,reusablePrefabs.WindowPane,
                new Vector3(x0+(i+0.5f)*width,4.4f,z+0.125f),Quaternion.identity,new Vector3(width-0.08f,4.2f,0.035f));
            NullBox($"Facade Mullion {i}",room,new Vector3(x0+i*width,4.4f,z+0.125f),new Vector3(0.08f,4.2f,0.35f),dark);
        }
    }

    private static void BuildSkunkLink(string name, Transform campus, Vector3 position, Vector2 size, bool alongZ,
        Material white, Material dark, Material glow)
    {
        Transform link = NewGroup(name, campus);
        link.localPosition = position;
        NullBox("Floor", link, Vector3.down*0.14f, new Vector3(size.x,0.28f,size.y), dark);
        NullBox("Skunk Works Ceiling", link, Vector3.up*4.65f, new Vector3(size.x,0.3f,size.y), white);
        foreach (float side in new[] { -1f, 1f })
        {
            NullBox($"Link Wall {side}", link, alongZ ? new Vector3(side*(size.x/2f+0.125f),2.25f,0f) : new Vector3(0f,2.25f,side*(size.y/2f+0.125f)),
                alongZ ? new Vector3(0.25f,4.5f,size.y) : new Vector3(size.x,4.5f,0.25f), dark);
            NullBox($"Link Light {side}", link, alongZ ? new Vector3(side*(size.x/2f-0.05f),1.6f,0f) : new Vector3(0f,1.6f,side*(size.y/2f-0.05f)),
                alongZ ? new Vector3(0.05f,0.08f,size.y) : new Vector3(size.x,0.08f,0.05f), glow);
        }
        SetRendererMask(link, SkunkWorksLayout.AllLayers);
    }

    private static void ConfigureSkunkZone(Transform room, uint mask, Vector3 center, Vector3 size, Color color)
    {
        SetRendererMask(room, mask | ExteriorRenderingLayer);
        SetLightMask(room, mask);
        Transform zones = NewGroup("Local Lighting", room);
        BuildLocalVolume(room.name+" Camera Volume", zones, center, Quaternion.identity, size, 28f, 0.35f,
            GetVolumeProfile("SkunkWorks"+room.name.Replace(" ",""), 0.3f, 17f, 4f, 0.45f, 0.9f));
        BuildLocalReflectionProbe(room.name+" Reflection", zones, center, Quaternion.identity, size, 0.5f,
            GetSolidCubemap("SkunkWorks"+room.name.Replace(" ",""), color*0.23f));
    }

    private static GameObject SkunkRing(string name, Transform parent, Vector3 position, float radius, float thickness,
        Material material, Quaternion rotation)
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();
        const int segments = 80;
        const int sides = 8;
        for (int i = 0; i <= segments; i++)
        {
            float a = i*Mathf.PI*2f/segments;
            for (int j = 0; j <= sides; j++)
            {
                float b = j*Mathf.PI*2f/sides;
                float r = radius+Mathf.Cos(b)*thickness;
                vertices.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Mathf.Sin(b)*thickness));
                if (i == segments || j == sides) continue;
                int v = i*(sides+1)+j;
                triangles.AddRange(new[] { v,v+1,v+sides+1,v+1,v+sides+2,v+sides+1 });
            }
        }
        GameObject ring = MeshObject(name, parent, GetGeneratedMesh($"SkunkRing_{radius:0.000}_{thickness:0.000}",vertices,triangles),material,false);
        ring.transform.localPosition = position;
        ring.transform.localRotation = rotation;
        return ring;
    }
}
