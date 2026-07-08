using System.Collections.Generic;
using UnityEngine;

public class UIManager : Singleton<UIManager> //싱글톤 이용
{
    private Dictionary<string, GameObject> UIs = new Dictionary<string, GameObject>();
    public bool isAnyUIOpen = false;
    public int OpenUICount = 0;

    public void AddUI(GameObject ui,string name)
    {
        UIs[name] = ui;
    }
    // UI 열기
    public void OpenUI(string name)
    {
        if (!UIs.ContainsKey(name))
        {
            Debug.LogError($"UI '{name}' not found.");
            return;
        }
        UIs[name].SetActive(true);
        isAnyUIOpen = true;
        OpenUICount++;
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
        isAnyUIOpen = false;
        OpenUICount--;
    }
}