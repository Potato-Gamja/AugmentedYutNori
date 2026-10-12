using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class ResultPopup : UIBase
{
    [Header("UI연결")]
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Image winnerColorImage;
    [SerializeField] private Button restartButton;

    public event Action RestartClicked;

    public override void Init()
    {
        restartButton.onClick.AddListener(HandleRestart);
    }

    protected override void OnOpen()
    {
        SoundManager.Play(SoundId.ResultPopup);
    }

    public ResultPopup SetWinner(string winnername, Color winnerColor)
    {
        if (winnerText != null) winnerText.text = string.Format("{0} win", winnername);
        if (winnerColorImage != null) winnerColorImage.color = winnerColor;
        return this;
    }

    private void HandleRestart()
    {
        Close();

        if (RestartClicked != null) RestartClicked.Invoke();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

}
