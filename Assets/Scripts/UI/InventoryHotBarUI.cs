using UnityEngine;

public class InventoryHotBarUI : MonoBehaviour
{
    private Inventory inventory;   // 보여줄 데이터
    [SerializeField] private InventoryHotBarSlot slotPrefab;    // 만든 슬롯 프리팹
    [SerializeField] private RectTransform UI;  // Grid Layout Group이 붙은 Panel

    [SerializeField] private int hotBarStartIndex = 30; //핫바 시작 인덱스
    [SerializeField] private int hotBarSize = 10; //핫바 크기

    InventoryHotBarSlot[] slots;
    private int selectedSlot = 0;
    public int SelectedInventoryIndex => hotBarStartIndex + selectedSlot;

    void Start()
    { // UIManager를 통해 열기
        inventory = Inventory.Instance;
        slots = new InventoryHotBarSlot[hotBarSize];
        for (int i = hotBarStartIndex; i < hotBarStartIndex + hotBarSize; i++)
        {
            var s = Instantiate(slotPrefab, UI);
            s.index = i;
            slots[i - hotBarStartIndex] = s;
        }
        inventory.OnChanged += Refresh; // 데이터 바뀌면 자동으로 다시 그림
        Refresh();
        slots[selectedSlot].SetSelected(true); // 기본으로 1번 키 슬롯이 선택된 상태로 시작
        inventory.SetSeclectedSlotIndex(selectedSlot); // 인벤토리에도 선택된 슬롯 인덱스 전달
    }

    void Refresh() { foreach (var s in slots) s.Refresh(); }

    void OnEnable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnHotbarSlotSelected += HandleHotbarSlotSelected;
    }

    void OnDisable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnHotbarSlotSelected -= HandleHotbarSlotSelected;
    }

    private void HandleHotbarSlotSelected(int slot)
    {
        if (slot < 0 || slot >= slots.Length) return;
        slots[selectedSlot].SetSelected(false);
        selectedSlot = slot;
        slots[selectedSlot].SetSelected(true);
        inventory.SetSeclectedSlotIndex(slot);
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= Refresh;
    }
    void Update()
    {
        if(UIManager.Instance.isAnyUIOpen)
        {
            UI.gameObject.SetActive(false);
        }
        else
        {
            UI.gameObject.SetActive(true);
        }
    }
}
