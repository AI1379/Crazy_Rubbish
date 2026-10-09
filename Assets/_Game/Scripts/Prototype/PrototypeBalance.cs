using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    [CreateAssetMenu(menuName = "Crazy Rubbish/M1 Experimental Balance")]
    public sealed class PrototypeBalance : ScriptableObject
    {
        [TextArea(3, 5)] public string notice = "S5最新策划基线，第20章优先；当前地图、交互、预约和运输采用新规则。";
        public PrototypeSettings values = new PrototypeSettings();
    }
}
