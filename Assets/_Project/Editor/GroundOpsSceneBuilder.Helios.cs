using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class GroundOpsSceneBuilder
{
    private static HeliosForgeController BuildHelios(Transform campus,Transform room,FirstPersonPlayerController player,
        Material metal,Material dark,Material amber,Material white)
    {
        Transform apparatus=NewGroup("Helios Prototype",room);
        apparatus.localPosition=new Vector3(-19.5f,0f,0.8f);
        GameObject foundation=Cylinder("Source Foundation",apparatus,new Vector3(0f,0.18f,0f),3.55f,0.36f,dark);
        SkunkCylinderCollider(foundation);
        SkunkRing("Foundation Halo",apparatus,new Vector3(0f,0.39f,0f),3.45f,0.035f,amber,Quaternion.Euler(90f,0f,0f));
        Transform field=NewGroup("Toroidal Field",apparatus);
        field.localPosition=new Vector3(0f,4.5f,0f);
        field.localRotation=Quaternion.Euler(24f,0f,12f);
        Material glow=ArchiveMaterial("Helios Flux","Chamber/Signal Archive Glow");
        glow.SetColor("_BaseColor",Color.white);
        glow.SetFloat("_Intensity",1.6f);
        glow.SetFloat("_Radial",0f);
        Transform phaseRing=NewGroup("Phase Stator",apparatus);
        phaseRing.localPosition=new Vector3(0f,4.5f,0f);
        SkunkRing("Outer Stator",phaseRing,Vector3.zero,3.5f,0.17f,metal,Quaternion.identity);
        SkunkRing("Stator Winding",phaseRing,Vector3.zero,3.29f,0.055f,amber,Quaternion.identity);
        SkunkRing("Tilted Containment Hoop",apparatus,new Vector3(0f,4.5f,0f),3.0f,0.16f,white,Quaternion.Euler(25f,64f,0f));
        SkunkRing("Hoop Conductor",apparatus,new Vector3(0f,4.5f,0f),2.80f,0.045f,amber,Quaternion.Euler(25f,64f,0f));
        Transform core=SkunkCrystal("Source Seed",apparatus,new Vector3(0f,4.5f,0f),amber).transform;
        for(int i=0;i<8;i++)
        {
            float a=i*Mathf.PI*2f/8f;
            Vector3 p=new(Mathf.Cos(a)*3.25f,0.85f,Mathf.Sin(a)*3.25f);
            NullBox($"Inductor Pedestal {i}",apparatus,p,new Vector3(0.45f,1.3f,0.45f),metal);
            GameObject crown=NullBox($"Inductor Cap {i}",apparatus,p+Vector3.up*0.73f,new Vector3(0.6f,0.16f,0.6f),amber);
            crown.transform.localRotation=Quaternion.Euler(0f,-a*Mathf.Rad2Deg,0f);
            NullRail($"Upper Suspension {i}",apparatus,new Vector3(Mathf.Cos(a)*2.2f,8.6f,Mathf.Sin(a)*2.2f),
                new Vector3(Mathf.Cos(a)*1.7f,6.5f,Mathf.Sin(a)*1.7f),metal);
        }
        SkunkRing("Suspension Crown",apparatus,new Vector3(0f,8.6f,0f),2.3f,0.20f,metal,Quaternion.Euler(90f,0f,0f));
        Light sourceLight=NullLamp("Source Radiance",apparatus,new Vector3(0f,5.4f,0f),Vector3.down,new Color(1f,0.4f,0.09f),150f,13f,amber,true);
        SetSkunkShadowBudget(sourceLight);
        Text wall=NullSign("Source Status",room,new Vector3(-19.5f,5.8f,-6.75f),Vector3.forward,
            "HELIOS / TUNING REQUIRED",9f,0.5f,dark);
        SimpleSeatedConsoleController console=BuildSkunkConsole("Helios Tuning Bench",room,player,
            new Vector3(-19.5f,0f,6f),Vector3.back,dark,amber,
            "A / D: phase   W / S: containment   Shift: fine   Hold Space: certify",out Text readout);
        HeliosForgeController controller=GetOrAddComponent<HeliosForgeController>(apparatus.gameObject);
        controller.Configure(player,campus,console,field,phaseRing,core,glow,readout,wall,sourceLight);
        return controller;
    }

    private static SimpleSeatedConsoleController BuildSkunkConsole(string name,Transform parent,FirstPersonPlayerController player,
        Vector3 position,Vector3 facing,Material dark,Material accent,string controls,out Text readout)
    {
        Transform bench=NewGroup(name,parent);
        bench.localPosition=position;
        bench.localRotation=Quaternion.LookRotation(facing);
        ReusablePrefabInstance("Work Table",bench,reusablePrefabs.DocDesk,Vector3.zero,Quaternion.Euler(0f,90f,0f));
        ReusablePrefabInstance("Chair",bench,reusablePrefabs.ChairBlack,new Vector3(0f,0f,-1f),Quaternion.identity);
        NullBox("Terminal",bench,new Vector3(0f,1.03f,0.12f),new Vector3(1.65f,0.48f,0.34f),dark);
        NullBox("Terminal Light",bench,new Vector3(0f,1.31f,0.12f),new Vector3(1.65f,0.045f,0.34f),accent);
        readout=CreateWorldDisplayText("Readout",bench,new Vector3(0f,1.03f,-0.06f),1.55f,0.44f,"PROTOTYPE SYSTEM / STANDBY",48,Quaternion.identity);
        readout.color=new Color(0.72f,0.94f,1f);
        Transform pose=NewGroup("Seated Camera Pose",bench);
        pose.localPosition=new Vector3(0f,1.45f,-1.1f);
        pose.localRotation=Quaternion.Euler(-10f,0f,0f);
        Transform trigger=NewGroup("Console Interaction",bench);
        trigger.localPosition=new Vector3(0f,0.9f,-1f);
        BoxCollider bounds=GetOrAddComponent<BoxCollider>(trigger.gameObject);
        bounds.isTrigger=true;
        bounds.size=new Vector3(1.6f,1.8f,1.5f);
        Rigidbody body=GetOrAddComponent<Rigidbody>(trigger.gameObject);
        body.isKinematic=true;
        body.useGravity=false;
        SimpleSeatedConsoleController console=GetOrAddComponent<SimpleSeatedConsoleController>(trigger.gameObject);
        console.Configure(player,player.PlayerCamera,pose);
        console.ConfigurePrompts("Press F to sit at the "+name.ToLowerInvariant(),controls+"\nMouse: look   Wheel: zoom   F / Esc: stand up");
        return console;
    }

    private static void SkunkCylinderCollider(GameObject cylinder)
    {
        CapsuleCollider capsule=cylinder.GetComponent<CapsuleCollider>();
        if(capsule!=null) Object.DestroyImmediate(capsule);
        GetOrAddComponent<MeshCollider>(cylinder).sharedMesh=cylinder.GetComponent<MeshFilter>().sharedMesh;
    }

    private static GameObject SkunkCrystal(string name,Transform parent,Vector3 position,Material material)
    {
        Vector3[] corners={Vector3.up*1.5f,Vector3.down*1.5f,Vector3.right,Vector3.forward,Vector3.left,Vector3.back};
        List<Vector3> vertices=new();
        List<int> triangles=new();
        for(int i=0;i<4;i++)
        {
            Vector3 a=corners[2+i],b=corners[2+(i+1)%4];
            AddFlatTriangle(vertices,triangles,corners[0],b,a);
            AddFlatTriangle(vertices,triangles,corners[1],a,b);
        }
        GameObject crystal=MeshObject(name,parent,GetGeneratedMesh("SkunkWorksCrystal",vertices,triangles),material,false);
        crystal.transform.localPosition=position;
        return crystal;
    }
}
