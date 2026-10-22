using TMPro;
using UnityEngine;

namespace YutNori.UI.Common
{
    [ExecuteAlways]
    public sealed class ApplicationVersionLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        public void Configure(TMP_Text text) { label = text; Refresh(); }
        private void OnEnable() => Refresh();
        private void Refresh()
        {
            if (label != null) label.text = "v" + Application.version;
        }
    }
}
