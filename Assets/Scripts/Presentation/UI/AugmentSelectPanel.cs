using System;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentSelectPanel : UIBase
{
    [Header("UI 연결")]
    [SerializeField] private GameObject augumentCardViewPrefab;
    [SerializeField] private Transform augmentCardContainer;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image modalBorder;
    
    private IReadOnlyList<AugmentDefinition> choices;
    private Action<string> onAugmentSelected;
    private readonly List<AugmentCardView> cards = new();
    private int selectionTeam;
    private bool online;
    private bool selectionPending;
    private int selectionVersion;
    private Coroutine selectionRoutine;

    // GameRules가 만든 후보만 받아 표시하는 연결 API.
    // UI가 AugmentPool이나 중복 선택 규칙을 직접 알지 않도록 한다.
    public void Setup(IReadOnlyList<AugmentDefinition> choices, Action<string> onAugmentSelected,
        int selectionTeam = 0, bool online = false)
    {
        CancelPendingSelection();
        this.choices = choices;
        this.onAugmentSelected = onAugmentSelected;
        this.selectionTeam = Mathf.Clamp(selectionTeam, 0, 3);
        this.online = online;
    }

    protected override void OnOpen()
    {
        CancelPendingSelection();
        foreach (Transform child in augmentCardContainer)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        cards.Clear();
        if (titleText != null)
        {
            string[] teamNames = { "빨강", "파랑", "초록", "노랑" };
            titleText.text = online ? "증강을 고르세요" : $"{teamNames[selectionTeam]} 팀, 증강을 고르세요";
            titleText.color = AugmentCardView.TeamColor(selectionTeam, true);
        }
        if (modalBorder != null) modalBorder.color = AugmentCardView.TeamColor(selectionTeam);

        if (choices == null) return;
        foreach (AugmentDefinition choice in choices)
        {
            AugmentCardView card = Instantiate(augumentCardViewPrefab, augmentCardContainer)
                .GetComponent<AugmentCardView>();
            card.Init(choice, OnCardSelected);
            card.SetTeam(selectionTeam);
            cards.Add(card);
        }
    }

    private void OnCardSelected(string id)
    {
        if (selectionPending || !IsOpen) return;
        selectionPending = true;
        foreach (AugmentCardView card in cards)
        {
            card.SetInput(false);
            card.SetSelected(card.AugmentId == id);
        }
        selectionRoutine = StartCoroutine(CompleteSelection(id, selectionVersion));
    }

    private IEnumerator CompleteSelection(string id, int version)
    {
        yield return new WaitForSecondsRealtime(0.3f);
        if (!IsOpen || version != selectionVersion || !selectionPending) yield break;
        Action<string> callback = onAugmentSelected;
        selectionRoutine = null;
        selectionPending = false;
        Close();
        callback?.Invoke(id);
    }

    protected override void OnClose() => CancelPendingSelection();
    private void OnDisable() => CancelPendingSelection();

    private void CancelPendingSelection()
    {
        selectionVersion++;
        selectionPending = false;
        if (selectionRoutine != null) StopCoroutine(selectionRoutine);
        selectionRoutine = null;
    }
}
