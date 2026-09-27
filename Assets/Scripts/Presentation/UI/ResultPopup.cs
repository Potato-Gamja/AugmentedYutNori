using System;
using TMPro;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class ResultPopup : MonoBehaviour
{
    [Header("UI연결")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Image winnerColorImage;
    [SerializeField] private Button restartButton;

    public event Action RestartClicked;

    public bool IsShowing => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (restartButton != null) restartButton.onClick.AddListener(HandleRestart);
        Hide();
    }

    private void OnDestroy()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(HandleRestart);
    }

    public void Show(string winnername, Color winnerColor)
    {
        if (winnerText != null) winnerText.text = string.Format("{0} win", winnername);
        if (winnerColorImage != null) winnerColorImage.color = winnerColor;
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void HandleRestart()
    {
        Hide();

        if (RestartClicked != null) RestartClicked.Invoke();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

}
