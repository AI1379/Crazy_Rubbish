using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    [CreateAssetMenu(menuName = "Crazy Rubbish/M1 Experimental Balance")]
    public sealed class PrototypeBalance : ScriptableObject
    {
        [TextArea(3, 5)] public string notice = "10月11日v1数值基线；10月14日已确认的渐进开局与物品栏规则保留。未接入产线见开发说明。";
        public PrototypeSettings values = new PrototypeSettings();
    }
}
