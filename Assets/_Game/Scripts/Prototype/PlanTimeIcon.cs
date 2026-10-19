using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    // Small UI mesh icons stay crisp at different canvas scales and need no font glyphs.
    public sealed class PlanTimeIcon : MaskableGraphic
    {
        [Range(0,3)] public int speed;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();float h=Mathf.Min(14,r.height),y=r.center.y-h/2;
            if(speed==0)
            {
                Quad(mesh,r.center.x-7,y,5,h);Quad(mesh,r.center.x+2,y,5,h);return;
            }
            int count=Mathf.Clamp(speed,1,3);float gap=2,width=(r.width-gap*(count-1))/count;
            if(count==1)width=10;
            float x=r.center.x-(count*width+(count-1)*gap)/2;
            for(int i=0;i<count;i++,x+=width+gap)
            {
                int at=mesh.currentVertCount;
                mesh.AddVert(new Vector3(x,y),color,Vector2.zero);mesh.AddVert(new Vector3(x,y+h),color,Vector2.zero);mesh.AddVert(new Vector3(x+width,y+h/2),color,Vector2.zero);mesh.AddTriangle(at,at+1,at+2);
            }
        }
        private void Quad(VertexHelper mesh,float x,float y,float w,float h)
        {
            int at=mesh.currentVertCount;
            mesh.AddVert(new Vector3(x,y),color,Vector2.zero);mesh.AddVert(new Vector3(x,y+h),color,Vector2.zero);mesh.AddVert(new Vector3(x+w,y+h),color,Vector2.zero);mesh.AddVert(new Vector3(x+w,y),color,Vector2.zero);
            mesh.AddTriangle(at,at+1,at+2);mesh.AddTriangle(at,at+2,at+3);
        }
    }
}
