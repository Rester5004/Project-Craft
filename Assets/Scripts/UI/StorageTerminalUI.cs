using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 저장 터미널 창. 네트워크 전체를 <b>한 화면</b>으로 보여 주고 넣고 뺀다.
///
/// 새로 만든 것이 거의 없다 — 칸은 <see cref="DefaultMachineUI"/> 가 프리팹에서 긁어 온 것을 쓰고,
/// 내용은 <see cref="NetworkContainer"/> 가 대신 답하며, 드래그앤드롭은 <see cref="ItemSlot"/> 그대로다.
/// 여기서 하는 일은 <b>둘을 이어 주고 페이지를 넘기는 것</b>뿐이다.
///
/// 버튼은 <b>방식 ②(서브클래스 + SerializeField)</b> 로 붙였다(<see cref="CraftingTableUI.craftButton"/> 과 같다) —
/// 코드로 만들면 씬에서 위치를 못 옮기고 팩토리 검증기에도 안 잡힌다.
/// </summary>
public class StorageTerminalUI : DefaultMachineUI
{
    [Header("저장 터미널")]
    [SerializeField] private Button prevPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TMP_Text pageLabel;
    [SerializeField] private TMP_Text statusLabel;

    private NetworkContainer container;

    /// <summary>칸 수만큼이 곧 한 페이지다. 프리팹이 가진 입력 요소 수를 그대로 쓴다.</summary>
    private int PageSize => Mathf.Max(1, InputElementCount);

    public override void Open(MachineInstance instance)
    {
        base.Open(instance);

        // 터미널은 자기 칸이 0개라 base 가 입력 칸을 전부 꺼 놓는다. 여기서 네트워크로 갈아 끼운다.
        container = instance != null ? new NetworkContainer(instance.worldCell, PageSize) : null;
        if (container == null) { Refresh(); return; }

        container.OnChanged = Refresh;
        RebindInputs(container, PageSize);

        // ⚠ <b>Open 마다 RemoveAllListeners</b> — 안 하면 터미널을 열 때마다 리스너가 쌓여
        //    한 번 눌렀는데 예전에 열었던 터미널까지 함께 넘어간다.
        if (prevPageButton != null)
        {
            prevPageButton.onClick.RemoveAllListeners();
            prevPageButton.onClick.AddListener(delegate { container.SetPage(container.Page - 1); });
        }
        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveAllListeners();
            nextPageButton.onClick.AddListener(delegate { container.SetPage(container.Page + 1); });
        }

        Refresh();
    }

    public override void Close()
    {
        if (container != null) container.OnChanged = null;
        container = null;
        base.Close();
    }

    /// <summary>
    /// 칸 그림과 라벨을 다시 그린다. <see cref="NetworkContainer.OnChanged"/> 가 이걸 부르므로
    /// 아이템을 옮긴 직후에도 목록이 곧바로 맞는다.
    /// </summary>
    private void Refresh()
    {
        RefreshSlots();

        if (pageLabel != null)
            pageLabel.text = container != null ? $"{container.Page + 1} / {container.PageCount}" : "";

        if (prevPageButton != null) prevPageButton.interactable = container != null && container.Page > 0;
        if (nextPageButton != null) nextPageButton.interactable = container != null && container.Page < container.PageCount - 1;

        if (statusLabel == null) return;

        StorageNetwork network = container != null ? container.Network : null;
        statusLabel.text = network == null ? "케이블에 닿아 있지 않다" : network.StatusText();
    }

    /// <summary>
    /// 터미널은 자기 칸이 0개인데 프리팹에는 칸이 잔뜩 있다 —
    /// 그대로 두면 열 때마다 "칸이 모자란다" 가 아니라 <b>남아돈다</b>는 상태라 경고는 안 나지만,
    /// 조합대처럼 <b>베이스의 칸 관리를 그대로 쓰지 않으므로</b> 경고를 꺼 둔다.
    /// </summary>
    protected override bool WarnOnElementShortage => false;
}
