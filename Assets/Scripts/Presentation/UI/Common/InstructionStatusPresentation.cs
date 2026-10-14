using TMPro;
using UnityEngine;
namespace YutNori.UI.Common
{
    /// <summary>고정 본문과 같은 초기 안내만 감춘다. 상태 문자열과 요청 처리에는 관여하지 않는다.</summary>
    [ExecuteAlways]
    public sealed class InstructionStatusPresentation : MonoBehaviour
    {
        [SerializeField] private TMP_Text status;
        [SerializeField] private string repeatedInstruction;
        public void Configure(TMP_Text text,string instruction){status=text;repeatedInstruction=instruction;Refresh();}
        private void LateUpdate()=>Refresh();
        public void Refresh(){if(status!=null)status.maxVisibleCharacters=status.text==repeatedInstruction?0:int.MaxValue;}
    }
}
