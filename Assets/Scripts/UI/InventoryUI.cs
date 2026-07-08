using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    private Inventory inventory;   // 보여줄 데이터
    public InventorySlot slotPrefab;     // 만든 슬롯 프리팹
    public RectTransform UI;  // Grid Layout Group이 붙은 Panel

    InventorySlot[] slots;

    void Start()
    {
        UIManager.Instance.AddUI(UI.gameObject,"Inventory"); // UIManager를 통해 열기
        UI.gameObject.SetActive(true); // 슬롯 Awake()가 정상 실행되도록 초기화 동안만 활성화
        inventory = Inventory.Instance;
        slots = new InventorySlot[inventory.size];
        for (int i = 0; i < inventory.size; i++)
        {
            var s = Instantiate(slotPrefab, UI);
            s.index = i;
            slots[i] = s;
        }
        inventory.OnChanged += Refresh; // 데이터 바뀌면 자동으로 다시 그림
        Refresh();
        UI.gameObject.SetActive(false);
    }

    void Refresh() { foreach (var s in slots) s.Refresh(); }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= Refresh;
    }
}