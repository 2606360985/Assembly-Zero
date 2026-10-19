using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    [CreateAssetMenu(menuName="Assembly Zero/EP04 Economy")]
    public sealed class EP04Config : ScriptableObject
    {
        public EconomySettings Economy=new EconomySettings();
        public Font Font;
        public Material BaseMaterial;
    }
}
