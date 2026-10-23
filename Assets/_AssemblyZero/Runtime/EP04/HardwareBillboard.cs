using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    public sealed class HardwareBillboard : MonoBehaviour
    {
        public Camera View;
        private void LateUpdate(){if(View!=null)transform.rotation=View.transform.rotation;}
    }
}
