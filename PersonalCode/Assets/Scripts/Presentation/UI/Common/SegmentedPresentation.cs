using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace YutNori.UI.Common
{
    [ExecuteAlways] public sealed class SegmentedPresentation:MonoBehaviour
    {
        [SerializeField] private Image[] faces;
        [SerializeField] private TMP_Text[] labels;
        [SerializeField] private Button[] buttons;
        [SerializeField] private int selectedIndex;
        public int SelectedIndex=>selectedIndex;
        public void Configure(Image[] backgrounds,TMP_Text[] texts,Button[] targets){faces=backgrounds;labels=texts;buttons=targets;SetSelected(selectedIndex);}
        private void OnEnable(){if(buttons!=null&&buttons.Length==2){buttons[0].onClick.AddListener(SelectFirst);buttons[1].onClick.AddListener(SelectSecond);}SetSelected(selectedIndex);}
        private void OnDisable(){if(buttons!=null&&buttons.Length==2){buttons[0].onClick.RemoveListener(SelectFirst);buttons[1].onClick.RemoveListener(SelectSecond);}}
        private void SelectFirst()=>SetSelected(0);
        private void SelectSecond()=>SetSelected(1);
        public void SetSelected(int index){selectedIndex=Mathf.Clamp(index,0,1);if(faces==null||labels==null)return;for(int i=0;i<2;i++){bool selected=i==selectedIndex;faces[i].color=selected?new Color32(145,95,48,255):new Color(1,1,1,0);labels[i].color=selected?Color.white:new Color32(122,92,62,255);}}
    }
}
