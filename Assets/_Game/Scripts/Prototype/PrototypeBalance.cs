using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    [CreateAssetMenu(menuName = "Crazy Rubbish/M1 Experimental Balance")]
    public sealed class PrototypeBalance : ScriptableObject
    {
        [TextArea(3, 5)] public string notice = "M2起步配置，采用2026-10-08合并版首版规则；白盒位置和未接入系统见开发说明。";
        public PrototypeSettings values = new PrototypeSettings();
    }
}
