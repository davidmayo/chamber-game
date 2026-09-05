using UnityEngine;
using UnityEngine.UI;

public static partial class GroundOpsSceneBuilder
{
    private static VectorGardenController BuildVectorGarden(Transform room, FirstPersonPlayerController player,
        HeliosForgeController source, Material metal, Material dark, Material mint, Material white)
    {
        Transform apparatus=NewGroup("Vector Field",room);
        apparatus.localPosition=new Vector3(19.5f,0f,0.8f);
        Vector3[] positions={new(-4.4f,0f,-0.8f),new(0f,0f,3.7f),new(4.4f,0f,-0.8f)};
        Transform[] sculptures=new Transform[3];
        Transform[] controls=new Transform[3];
        Text[] labels=new Text[3];
        Material facet=GetMaterial("Vector Crystal",new Color(0.045f,0.42f,0.32f),0.82f,0.88f);
        Material glow=ArchiveMaterial("Vector Field Lines","Chamber/Signal Archive Glow");
        glow.SetColor("_BaseColor",Color.white);
        glow.SetFloat("_Intensity",1.8f);
        glow.SetFloat("_Radial",0f);
        for(int i=0;i<3;i++)
        {
            Transform pad=NewGroup($"Levitation Bed {(char)('A'+i)}",apparatus);
            pad.localPosition=positions[i];
            SkunkCylinderCollider(Cylinder("Bed Plinth",pad,Vector3.up*0.13f,1.65f,0.26f,dark));
            SkunkRing("Ground Conductor",pad,Vector3.up*0.29f,1.55f,0.055f,mint,Quaternion.Euler(90f,0f,0f));
            for(int spoke=0;spoke<8;spoke++)
            {
                float angle=spoke*Mathf.PI/4f;
                Vector3 p=new(Mathf.Cos(angle)*1.35f,0.55f,Mathf.Sin(angle)*1.35f);
                NullBox($"Emitter {spoke}",pad,p,new Vector3(0.13f,0.6f,0.13f),metal);
                NullBox($"Emitter Tip {spoke}",pad,p+Vector3.up*0.34f,new Vector3(0.18f,0.07f,0.18f),mint);
            }
            Transform sculpture=NewGroup($"Floating Anchor {(char)('A'+i)}",apparatus);
            sculpture.localPosition=positions[i]+Vector3.up*2.65f;
            sculptures[i]=sculpture;
            SkunkCrystal("Faceted Mass",sculpture,Vector3.zero,facet);
            GameObject seed=SkunkCrystal("Suspended Seed",sculpture,Vector3.up*1.82f,mint);
            seed.transform.localScale=Vector3.one*0.22f;
            SkunkRing("Equatorial Frame",sculpture,Vector3.zero,1.5f,0.07f,white,Quaternion.Euler(72f,0f,18f));
            SkunkRing("Field Winding",sculpture,Vector3.zero,1.7f,0.025f,mint,Quaternion.Euler(18f,55f,10f));
            SkunkRing("Polar Winding",sculpture,Vector3.zero,1.9f,0.025f,mint,Quaternion.Euler(45f,110f,30f));
            Transform control=NewGroup($"Anchor {(char)('A'+i)} Interaction",apparatus);
            control.localPosition=positions[i]+Vector3.forward*2.6f;
            controls[i]=control;
            Vector3 panel=positions[i]+new Vector3(0f,1.28f,1.88f);
            NullBox($"Anchor {i} Pedestal",apparatus,panel-new Vector3(0f,0.65f,0f),new Vector3(0.32f,1.26f,0.32f),metal);
            labels[i]=NullSign($"Anchor {i} Readout",apparatus,panel,Vector3.forward,"VECTOR / STANDBY",1.5f,0.62f,dark);
            NullLamp($"Levitation Wash {i}",apparatus,positions[i]+Vector3.up*8.7f,Vector3.down,
                new Color(0.12f,1f,0.65f),330f,12f,mint,false);
        }
        // Hanging ceiling ribbons frame the three independently moving masses.
        for(int i=0;i<7;i++)
        {
            Transform ribbon=NewGroup($"Canopy Ribbon {i}",apparatus);
            ribbon.localPosition=new Vector3(-6f+i*2f,8.9f,-0.2f);
            ribbon.localRotation=Quaternion.Euler(0f,i*6f-18f,0f);
            NullBox("Silver Blade",ribbon,Vector3.zero,new Vector3(0.2f,0.32f,10f),white);
            NullBox("Mint Underside",ribbon,Vector3.down*0.18f,new Vector3(0.055f,0.035f,9.6f),mint);
        }
        Text status=NullSign("Vector Status",room,new Vector3(19.5f,5.8f,-6.75f),Vector3.forward,
            "VECTOR / AWAITING SOURCE",10f,0.55f,dark);
        NullSign("Coupling Diagram",room,new Vector3(27.5f,1.8f,7.8f),Vector3.forward,
            "A > B > C > A\nEACH ANCHOR LIFTS ITS NEIGHBOR\nFOUR STEPS RETURN TO ZERO",2.3f,1f,dark);
        VectorGardenController controller=GetOrAddComponent<VectorGardenController>(apparatus.gameObject);
        controller.Configure(player,source,sculptures,controls,labels,status,glow);
        return controller;
    }
}
