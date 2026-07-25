using System.Collections.Generic;
using UnityEngine;

public class UIManager : Singleton<UIManager> //싱글톤 이용
{
    private Dictionary<string, GameObject> UIs = new Dictionary<string, GameObject>();
    // 현재 열려 있는 UI 이름 집합(다중 패널을 정확히 추적)
    private readonly HashSet<string> openNames = new HashSet<string>();

    public bool isAnyUIOpen => openNames.Count > 0;
    public int OpenUICount => openNames.Count;

    public void AddUI(GameObject ui, string name)
    {
        UIs[name] = ui;
    }

    public bool IsOpen(string name) => openNames.Contains(name);

    // UI 열기
    public void OpenUI(string name)
    {
        if (!UIs.ContainsKey(name))
        {
            Debug.LogError($"UI '{name}' not found.");
            return;
        }
        UIs[name].SetActive(true);
        openNames.Add(name);
    }

    // UI를 닫기
    public void CloseUI(string name)
    {
        if (!UIs.ContainsKey(name))
        {
            Debug.LogError($"UI '{name}' not found.");
            return;
        }
        UIs[name].SetActive(false);
        openNames.Remove(name);
    }
}
