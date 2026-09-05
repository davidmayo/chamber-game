using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class GroundOpsSceneBuilder
{
    private static HorizonEngineController BuildHorizon(Transform campus,Transform room,FirstPersonPlayerController player,
        HeliosForgeController source,VectorGardenController field,Material metal,Material dark,Material violet,Material white,Material cyan)
    {
        Transform machine=NewGroup("Horizon Aperture",room);
        machine.localPosition=new Vector3(0f,6f,-26f);
        SkunkRing("Aperture Chassis",machine,Vector3.zero,4.8f,0.38f,white,Quaternion.identity);
        SkunkRing("Chassis Inner Conductor",machine,Vector3.forward*0.3f,4.47f,0.065f,violet,Quaternion.identity);
        SkunkRing("Outer Field Winding",machine,Vector3.zero,5.28f,0.08f,violet,Quaternion.identity);
        Transform[] rings=new Transform[3];
        for(int ring=0;ring<3;ring++)
        {
            Transform stator=NewGroup($"Rotating Stator {ring}",machine);
            stator.localPosition=Vector3.back*(0.55f+ring*0.8f);
            rings[ring]=stator;
            SkunkRing("Stator Hoop",stator,Vector3.zero,4.4f,0.14f,metal,Quaternion.identity);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8f;
                GameObject blade=NullBox($"Conductor Segment {i}",stator,new Vector3(Mathf.Cos(a)*4.15f,Mathf.Sin(a)*4.15f,0f),
                    new Vector3(0.12f,0.55f,0.11f),ring%2==0?cyan:violet);
                blade.transform.localRotation=Quaternion.Euler(0f,0f,a*Mathf.Rad2Deg);
                Object.DestroyImmediate(blade.GetComponent<Collider>());
            }
        }
        Transform[] iris=new Transform[8];
        for(int i=0;i<iris.Length;i++)
        {
            Transform petal=NewGroup($"Iris Petal {i}",machine);
            float angle=i*Mathf.PI/4f;
            petal.localPosition=new Vector3(Mathf.Cos(angle)*2.15f,Mathf.Sin(angle)*2.15f,0.6f+i*0.025f);
            petal.localRotation=Quaternion.Euler(0f,0f,angle*Mathf.Rad2Deg+30f);
            iris[i]=petal;
            MeshObject("Ceramic Blade",petal,SkunkIrisBladeMesh(),metal,false);
            GameObject edge=NullBox("Blade Edge",petal,new Vector3(-1.86f,0f,0.13f),new Vector3(0.045f,2.5f,0.045f),violet);
            Object.DestroyImmediate(edge.GetComponent<Collider>());
            GameObject inset=NullBox("Blade Inset",petal,new Vector3(0.3f,0f,0.12f),new Vector3(1.7f,0.16f,0.035f),dark);
            Object.DestroyImmediate(inset.GetComponent<Collider>());
        }
        Material sky=ArchiveMaterial("Horizon Stellar Window","Chamber/Horizon Window");
        List<Vector3> vertices=new() { new(-4.35f,-4.35f,0f),new(4.35f,-4.35f,0f),new(4.35f,4.35f,0f),new(-4.35f,4.35f,0f) };
        Mesh screen=GetGeneratedMesh("HorizonWindow",vertices,new List<int> { 0,1,2,0,2,3 });
        screen.uv=new[] { new Vector2(0f,0f),new Vector2(1f,0f),new Vector2(1f,1f),new Vector2(0f,1f) };
        UnityEditor.EditorUtility.SetDirty(screen);
        GameObject window=MeshObject("Stellar Window",machine,screen,sky,false);
        window.transform.localPosition=Vector3.back*2.4f;
        // The prototype is an observation aperture, with a physical rear guard.
        NullBox("Rear Guard",room,new Vector3(0f,6f,-29f),new Vector3(10f,12f,0.2f),dark);
        foreach(float side in new[] {-1f,1f})
        {
            NullBox($"Engine Pier {side}",room,new Vector3(side*6.2f,2.8f,-26f),new Vector3(1.4f,5.6f,2.4f),dark);
            NullBox($"Pier Conductor {side}",room,new Vector3(side*6.2f,2.8f,-24.75f),new Vector3(0.12f,4.8f,0.055f),violet);
            NullRail($"Engine Brace {side}",room,new Vector3(side*6.2f,5.5f,-26f),new Vector3(side*3.7f,8.2f,-26f),metal);
            NullBox($"Runway Edge {side}",room,new Vector3(side*3.4f,0.022f,-21.5f),new Vector3(0.06f,0.025f,8f),violet);
            for(int i=0;i<7;i++)
                NullBox($"Runway Tick {side} {i}",room,new Vector3(side*3.15f,0.021f,-18f-i*1.1f),new Vector3(0.5f,0.02f,0.035f),cyan);
        }
        Transform surveyor=NewGroup("Horizon Surveyor",machine);
        surveyor.localPosition=Vector3.back*1.7f;
        GameObject heart=SkunkCrystal("Surveyor Core",surveyor,Vector3.zero,white);
        heart.transform.localScale=Vector3.one*0.65f;
        SkunkRing("Surveyor Orbit",surveyor,Vector3.zero,1.25f,0.035f,cyan,Quaternion.Euler(45f,20f,0f));
        for(int i=0;i<6;i++)
        {
            Transform wing=NewGroup($"Surveyor Sail {i}",surveyor);
            wing.localRotation=Quaternion.Euler(0f,i*60f,25f);
            GameObject sail=NullBox("Titanium Sail",wing,new Vector3(1.0f,0f,0f),new Vector3(0.8f,0.055f,0.4f),metal);
            Object.DestroyImmediate(sail.GetComponent<Collider>());
            GameObject rail=NullBox("Sail Filament",wing,new Vector3(1f,0.05f,0f),new Vector3(0.8f,0.04f,0.07f),violet);
            Object.DestroyImmediate(rail.GetComponent<Collider>());
        }
        Text status=NullSign("Horizon Status",room,new Vector3(0f,11.6f,-25.8f),Vector3.forward,
            "FIRST LIGHT / INTERLOCK",12f,0.55f,dark);
        NullSign("Horizon Interlock Instructions",room,new Vector3(-8.6f,2f,-18f),Vector3.forward,
            "FIRST LIGHT PROTOCOL\n01 CERTIFY HELIOS\n02 LOCK THE VECTOR FIELD\n03 ACQUIRE +7.5 / +4.0\n04 SPACE TO INITIATE",3.5f,1.75f,dark);
        SimpleSeatedConsoleController console=BuildSkunkConsole("Horizon Alignment Bench",room,player,new Vector3(0f,0f,-16f),Vector3.back,
            dark,violet,"A / D: yaw   W / S: pitch   Shift: fine   Space: First Light / replay",out Text readout);
        Light radiance=NullLamp("Aperture Radiance",machine,new Vector3(0f,0f,1.6f),Vector3.forward,
            new Color(0.40f,0.38f,1f),15f,20f,violet,false);
        HorizonEngineController controller=GetOrAddComponent<HorizonEngineController>(machine.gameObject);
        controller.Configure(player,campus,source,field,console,iris,rings,surveyor,window.GetComponent<Renderer>(),readout,status,radiance);
        return controller;
    }

    private static Mesh SkunkIrisBladeMesh()
    {
        Vector2[] outline={new(-1.9f,-1.25f),new(-1.4f,-1.75f),new(1.4f,-1.75f),new(1.9f,-1.25f),
            new(1.9f,1.25f),new(1.4f,1.75f),new(-1.4f,1.75f),new(-1.9f,1.25f)};
        List<Vector3> vertices=new();
        List<int> triangles=new();
        for(int i=0;i<outline.Length;i++)
        {
            Vector3 a=new(outline[i].x,outline[i].y,0.1f);
            Vector2 next=outline[(i+1)%outline.Length];
            Vector3 b=new(next.x,next.y,0.1f);
            Vector3 c=a-Vector3.forward*0.2f,d=b-Vector3.forward*0.2f;
            AddFlatTriangle(vertices,triangles,Vector3.forward*0.1f,a,b);
            AddFlatTriangle(vertices,triangles,Vector3.back*0.1f,d,c);
            AddFlatTriangle(vertices,triangles,a,c,b);
            AddFlatTriangle(vertices,triangles,b,c,d);
        }
        return GetGeneratedMesh("SkunkWorksIrisBlade",vertices,triangles);
    }
}
