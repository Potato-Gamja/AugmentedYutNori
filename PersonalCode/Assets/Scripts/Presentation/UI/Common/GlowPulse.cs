using UnityEngine;
using UnityEngine.UI;
namespace YutNori.UI.Common
{
    [ExecuteAlways] public sealed class GlowPulse:MonoBehaviour
    {
        [SerializeField] private Graphic graphic;
        [SerializeField] private float period=1.2f;
        public void Configure(Graphic target)=>graphic=target;
        private void Update(){if(graphic==null)return;var color=graphic.color;color.a=Mathf.Lerp(0.35f,0.75f,0.5f+0.5f*Mathf.Sin(Time.realtimeSinceStartup*Mathf.PI*2/Mathf.Max(0.01f,period)));graphic.color=color;}
    }
}
