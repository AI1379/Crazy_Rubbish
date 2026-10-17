using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeHud
    {
        [SerializeField] private RectTransform constructionNotice;
        [SerializeField] private Text constructionText,powerSummary;
        private static int FacilityKind(int page)=>page==1?1:page==2?2:page==3?4:page==4?8:page==5?16:page==6?32:page==7?64:128;
        private int SelectedOwnedId=>SelectedFacility==0?0:game.Model.SelectedFacility(FacilityKind(SelectedFacility)).Id;
        private void UpgradeConstructionLayout()
        {
            var content=pages[0].GetComponent<ScrollRect>().content;
            var oldChildren=new System.Collections.Generic.List<RectTransform>();
            foreach(Transform child in content)if(child is RectTransform rect)oldChildren.Add(rect);
            float shift=BuildConstructionControls(content,360)-370;
            foreach(var rect in oldChildren)if(rect.anchoredPosition.y<=-350)rect.anchoredPosition-=new Vector2(0,shift);
            for(int i=1;i<onsiteSectionY.Length;i++)onsiteSectionY[i]+=shift;
            content.sizeDelta+=new Vector2(0,shift);facilityPanel.sizeDelta=new Vector2(396,660);
            ButtonAt(facilityPanel,"MoveOwned","搬迁 / 旋转",20,610,112,36);
            ButtonAt(facilityPanel,"RemoveOwned","拆除返料",142,610,112,36);
            ButtonAt(facilityPanel,"SellOwned","出售",264,610,112,36);
            BuildConstructionNotice();
        }
        private float BuildConstructionControls(Transform parent,float y)
        {
            SectionHeading(parent,"自由建造与电网","02",y);y+=40;
            int[] kinds={1,2,4,8,16,32};string[] names={"新建木工","新建拆解","新建工作台","新建发电机","新建住房","新建路灯"};
            for(int i=0;i<kinds.Length;i++)ButtonAt(parent,"PlaceKind"+kinds[i],names[i],12+i%2*176,y+i/2*44,166,36);
            y+=140;
            ButtonAt(parent,"LayWire","铺设电线",12,y,166,36);ButtonAt(parent,"EraseWire","拆除电线",188,y,166,36);y+=44;
            ButtonAt(parent,"ConnectCommunity","接线到旧住房 / 路灯",12,y,342,36);y+=44;
            powerSummary=TextAt(parent,"Power Network Summary","",12,y,342,96,14,Muted);y+=104;
            TextAt(parent,"Construction Help","绿色可放置，红色说明原因；R旋转。\n每件电线铺4格；线路接设备外沿。\n同网公平分电，完全空载不耗燃料。",12,y,342,72,14,Muted);return y+84;
        }
        private void BuildConstructionNotice()
        {
            constructionNotice=Surface(root,"Construction Notice",0,0,820,80,Panel);
            constructionNotice.anchorMin=constructionNotice.anchorMax=new Vector2(0.5f,0);constructionNotice.pivot=new Vector2(0.5f,0);
            constructionNotice.anchoredPosition=new Vector2(0,132);
            constructionText=TextAt(constructionNotice,"Hint","",16,8,788,64,16,Accent);
            foreach(var g in constructionNotice.GetComponentsInChildren<Graphic>())g.raycastTarget=false;
            constructionNotice.gameObject.SetActive(false);
        }
        private void RefreshConstructionHud(PrototypeSimulation m)
        {
            RefreshMachineryHud(m);
            var world=game.GetComponent<PrototypeWorld>();
            if(constructionNotice!=null){constructionNotice.gameObject.SetActive(world.ConstructionActive&&!game.MenuOpen&&!game.ResultVisible);constructionText.text=world.ConstructionHint;}
            if(powerSummary!=null)powerSummary.text=$"电网 {m.PowerNetworks.Count} 组 · 可用 {m.AvailablePower:0.#} / 请求 {m.RequestedPower:0.###}\n旧住房 {m.HousingPowerRatio:P0} · 社区路灯 {m.CommunityLampPowerRatio:P0}\n电线 {m.Wires.Count} 格 · 余料 {m.WireCredit}/4\n住房容量 {m.Residents.Count+1} / {m.HousingCapacity}";
            if(residentCards.Length<m.Residents.Count)
            {
                int first=residentCards.Length;System.Array.Resize(ref residentCards,m.Residents.Count);System.Array.Resize(ref residentLabels,m.Residents.Count);
                for(int i=first;i<residentCards.Length;i++)
                {
                    var card=Surface(peopleContent,"Resident "+i,8,residentStart+i*112,356,102,Inset);residentCards[i]=card;
                    Artwork(card,"Portrait",game.appearance.characters[2+i%4].idle[1],6,12,44,70);
                    residentLabels[i]=TextAt(card,"Resident Info","",60,10,284,84,14,Ink);residentLabels[i].alignment=TextAnchor.UpperLeft;
                }
            }
        }
        private bool ExecuteConstructionCommand(string command)
        {
            var world=game.GetComponent<PrototypeWorld>();
            if(command.StartsWith("PlaceKind")&&int.TryParse(command.Substring(9),out int kind)){world.BeginPlacement(kind);return true;}
            switch(command)
            {
                case "LayWire":world.BeginWire();return true;
                case "EraseWire":world.BeginWire(true);return true;
                case "ConnectCommunity":game.Report(game.Model.ConnectCommunityPower());return true;
                case "MoveOwned":if(SelectedOwnedId!=0){var f=game.Model.GetFacility(SelectedOwnedId);world.BeginPlacement(f.Kind,f.Id);}return true;
                case "RemoveOwned":game.Report(game.Model.RemoveFacilityById(SelectedOwnedId));return true;
                case "SellOwned":game.Report(game.Model.RemoveFacilityById(SelectedOwnedId,true));return true;
                default:return false;
            }
        }
        public void ShowOwnedFacility(FacilityInstance f)
        {
            if(f==null)return;var result=game.Model.SelectFacility(f.Id);if(!result.Success){game.Report(result);return;}
            ShowFacility(f.Kind==1?1:f.Kind==2?2:f.Kind==4?3:f.Kind==8?4:f.Kind==16?5:f.Kind==32?6:f.Kind==64?7:8);
            facilityTitle.text=PrototypeSimulation.FacilityName(f.Kind)+" #"+f.Id;
        }
    }
}
