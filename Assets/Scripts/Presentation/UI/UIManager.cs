
using System.Collections.Generic;
using UnityEngine;
using System;

public class UIManager : MonoBehaviour
{
    [Header("UI 프리팹")]
    [SerializeField] private List<UIBase> prefabs = new List<UIBase>();

    [Header("Root")]
    [SerializeField] private Transform panelRoot;
    [SerializeField] private Transform popupRoot;

    private readonly Dictionary<Type, UIBase> _prefabByType = new Dictionary<Type, UIBase>();
    private readonly Dictionary<Type, UIBase> _instances = new Dictionary<Type, UIBase>();

    private void Awake()
    {
        foreach(var prefab in prefabs)
        {
            if (prefab == null) continue;

            var type = prefab.GetType();
            if (_prefabByType.ContainsKey(type))
            {
                Debug.LogError($"[UIManager] {type.Name} 프리팹 중복 등록");
                continue;
            }
            _prefabByType.Add(type, prefab);
        }
    }

    public T Open<T>() where T : UIBase
    {
        var ui = GetOrCreate<T>();
        ui?.Open();
        return ui;
    }

    public void Close<T>() where T: UIBase
    {
        if (_instances.TryGetValue(typeof(T), out var ui)) ui.Close();
    }

    public T Get<T>() where T : UIBase => GetOrCreate<T>();

    public bool IsOpen<T>() where T : UIBase => _instances.TryGetValue(typeof(T), out var ui) && ui.IsOpen;

    public void CloseAll()
    {
        foreach (var ui in _instances.Values) ui.Close();
    }

    private T GetOrCreate<T>() where T: UIBase
    {
        var type = typeof(T);

        if (_instances.TryGetValue(type, out var cached)) return (T)cached;

        if(!_prefabByType.TryGetValue(type, out var prefab))
        {
            Debug.LogError($"[UIManager] {type.Name} 프리팹 등록X");
            return null;
        }

        var parent = prefab.Layer == UILayer.Popup ? popupRoot : panelRoot;
        var instance = (T)Instantiate(prefab, parent);
        instance.name = type.Name;
        instance.Init();
        instance.gameObject.SetActive(false);

        _instances.Add(type, instance);
        return instance;
    }
}
