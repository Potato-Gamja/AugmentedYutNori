using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutNori.UI.Common
{
    /// <summary>서버가 알려 준 준비 상태에 맞춰 같은 Button의 주·보조 모양을 전환한다.</summary>
    public sealed class RoomReadyButtonPresentation : MonoBehaviour
    {
        [SerializeField] private Sprite[] primarySprites, secondarySprites;
        [SerializeField] private Image border;
        public void Configure(Sprite[] primary, Sprite[] secondary, Image outline)
        { primarySprites=primary;secondarySprites=secondary;border=outline; }
        public void SetReady(bool ready)
        {
            var button=GetComponent<Button>();var face=(Image)button.targetGraphic;var sprites=ready?secondarySprites:primarySprites;
            face.sprite=sprites[0];face.overrideSprite=null;
            button.spriteState=new SpriteState{highlightedSprite=sprites[1],pressedSprite=sprites[2],disabledSprite=sprites[3]};
            border.gameObject.SetActive(ready);
            GetComponent<ButtonPresentation>().Configure(button,face,border,transform.Find("Label").GetComponent<TMP_Text>(),null,null,!ready);
        }
    }
}
