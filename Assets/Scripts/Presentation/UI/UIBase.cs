using System;
using UnityEngine;

public enum UILayer
{
    Panel,
    Popup,
}
public class UIBase : MonoBehaviour
{
    [SerializeField] private UILayer layer = UILayer.Panel;

    public UILayer Layer => layer;
    public bool IsOpen => gameObject.activeSelf;

    public event Action<UIBase> Closed;

    public virtual void Init() { }

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        OnOpen();
    }

    public void Close()
    {
        if (!IsOpen) return;
        OnClose();
        gameObject.SetActive(false);
        Closed?.Invoke(this);
    }

    protected virtual void OnOpen() { }

    protected virtual void OnClose() { }
}
