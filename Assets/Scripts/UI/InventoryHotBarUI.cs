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
            s.Bind(inventory, i);
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
        InputActionManager input = InputActionManager.InstanceIfAlive;   // 종료 중엔 Instance 가 null 이다
        if (input != null) input.OnHotbarSlotSelected -= HandleHotbarSlotSelected;
    }

    /// <summary>
    /// 전체 인벤토리 인덱스(0~39)로 핫바 칸을 고른다. 핫바 범위 밖이면 아무것도 하지 않는다.
    ///
    /// 인벤토리 창의 슬롯 클릭이 이리로 들어온다. 예전에는 그쪽에서 <c>Inventory.SelectSlot</c> 을
    /// 직접 불렀는데, 그 함수는 받은 값을 그대로 쓰는 반면 핫바는 +30 규약이라
    /// <b>핫바 하이라이트는 1번인데 실제로는 5번 칸의 아이템이 소모되는</b> 어긋남이 났다.
    /// "지금 든 것" 의 정본은 핫바 하나뿐이어야 한다.
    /// </summary>
    public void SelectByInventoryIndex(int inventoryIndex)
    {
        int slot = inventoryIndex - hotBarStartIndex;
        if (slot < 0 || slot >= hotBarSize) return;
        HandleHotbarSlotSelected(slot);
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
