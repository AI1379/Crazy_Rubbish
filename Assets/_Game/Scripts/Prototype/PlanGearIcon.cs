using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed class PlanGearIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();float radius=Mathf.Min(r.width,r.height)/2;
            // Eight teeth and an open centre, drawn without relying on a font symbol.
            const int segments=32;
            for(int i=0;i<=segments;i++)
            {
                float angle=(i-.5f)*2*Mathf.PI/segments;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                float outer=(i%4==0||i%4==3) ? .75f : 1;
                mesh.AddVert(r.center+direction*(radius*outer),color,Vector2.zero);
                mesh.AddVert(r.center+direction*(radius*.34f),color,Vector2.zero);
                if(i==0)continue;int at=(i-1)*2;
                mesh.AddTriangle(at,at+1,at+2);mesh.AddTriangle(at+1,at+3,at+2);
            }
        }
    }
}
