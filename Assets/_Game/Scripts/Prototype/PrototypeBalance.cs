using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    [CreateAssetMenu(menuName = "Crazy Rubbish/M1 Experimental Balance")]
    public sealed class PrototypeBalance : ScriptableObject
    {
        [TextArea(3, 5)] public string notice = "M1 实验配置。数量、耗时、售价、容量、压力曲线和独立木工配方仅用于原型试玩，未成为正式策划结论。";
        public PrototypeSettings values = new PrototypeSettings();
    }
}
