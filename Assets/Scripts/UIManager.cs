using System.Collections.Generic;
using UnityEngine;

public class UIManager : Singleton<UIManager> //싱글톤 이용
{
    // 현재 열려있는 UI들을 담아둘 리스트
    private List<GameObject> activeUIs = new List<GameObject>();

    // 어떤 UI든 하나라도 열려있다면 true를 반환하는 프로퍼티
    public bool IsAnyUIOpen
    {
        get
        {
            // 혹시 예외 상황으로 파괴되었거나 꺼진 UI가 리스트에 남아있다면 청소
            activeUIs.RemoveAll(ui => ui == null || !ui.activeSelf);
            return activeUIs.Count > 0;
        }
    }

    // UI 열기
    public void OpenUI(GameObject ui)
    {
        if (ui == null) return;

        ui.SetActive(true);

        // 액티브 리스트에 없으면 추가
        if (!activeUIs.Contains(ui))
        {
            activeUIs.Add(ui);
        }
    }

    // UI를 닫기
    public void CloseUI(GameObject ui)
    {
        if (ui == null) return;

        ui.SetActive(false);
        activeUIs.Remove(ui); // 액티브 리스트에서 제거
    }
}