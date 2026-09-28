using TMPro;
using UnityEngine;

public class YutResultPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;   // 비워두면 자기 자신을 숨김
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text remainingText;

    private GameObject Root => panelRoot != null ? panelRoot : gameObject;

    // 빽도면 "빽도 - 뒤로 1칸", 나머지는 "개 - 앞으로 2칸"
    public void Show(YutResult result, int remainingThrows)
    {
        int steps = result.Steps();
        string direction = steps < 0 ? "뒤로" : "앞으로";

        resultText.text = $"{result.DisplayName()} - {direction} {Mathf.Abs(steps)}칸";
        remainingText.text = $"남은 던지기 기회 : {remainingThrows}회";

        Root.SetActive(true);
    }

    public void Hide()
    {
        Root.SetActive(false);
    }
}