using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    private Inventory inventory;   // 보여줄 데이터
    public InventorySlot slotPrefab;     // 만든 슬롯 프리팹
    public RectTransform UIslot;  // Grid Layout Group이 붙은 Panel
    public RectTransform UI;      // 전체 인벤토리 UI
    public ItemSlot trashSlot; // TrashCanInventory 슬롯 프리팹

    /// <summary>
    /// 쓰레기통 데이터. <b>씬 참조로 두면 안 된다</b> — 이 UI 는 두 씬이 공유하는
    /// <c>GameRig</c> 프리팹 안에 살고, <see cref="TrashCanInventory"/> 는 씬을 넘어 살아남아야 해서
    /// 프리팹 바깥의 씬 루트에 남는다. 프리팹은 씬 오브젝트를 참조할 수 없으므로 싱글톤으로 찾는다.
    /// </summary>
    private TrashCanInventory trashCan;

    InventorySlot[] slots;

    void Start()
    {
        UIManager.Instance.AddUI(UI.gameObject,"Inventory"); // UIManager를 통해 열기
        UI.gameObject.SetActive(true); // 슬롯 Awake()가 정상 실행되도록 초기화 동안만 활성화
        inventory = Inventory.Instance;
        trashCan = TrashCanInventory.Instance;
        slots = new InventorySlot[inventory.size];
        for (int i = 0; i < inventory.size; i++)
        {
            var s = Instantiate(slotPrefab, UIslot);
            s.Bind(inventory, i);
            slots[i] = s;
        }
        trashSlot.Bind(trashCan, 0); // TrashCanInventory 슬롯 바인딩
        inventory.OnChanged += Refresh; // 데이터 바뀌면 자동으로 다시 그림
        trashCan.OnChanged += RefreshTrashCan; // 쓰레기통 내용 바뀌면 자동으로 다시 그림
        Refresh();
        UI.gameObject.SetActive(false);
    }

    void Refresh() { foreach (var s in slots) s.Refresh(); }
    void RefreshTrashCan() { trashSlot.Refresh(); }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= Refresh;
        if (trashCan != null) trashCan.OnChanged -= RefreshTrashCan;
    }
}