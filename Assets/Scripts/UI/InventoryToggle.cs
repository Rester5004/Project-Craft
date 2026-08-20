using UnityEngine;

// 인벤토리 패널 자체가 비활성 상태로 시작할 수 있으므로,
// 항상 활성화되어 있는 별도의 오브젝트(플레이어 등)에 붙여서 토글 키를 감지합니다.
public class InventoryToggle : MonoBehaviour
{
    private MachineInteraction machineInteraction;

    /// <summary>
    /// 기계 UI 를 닫아 줄 상대. <b>캐시가 죽어 있으면 그때마다 다시 찾는다.</b>
    ///
    /// ⚠ 이 컴포넌트는 씬을 넘어 사는 <c>PlayerInventory</c> 에 붙어 있는데
    /// <see cref="MachineInteraction"/> 은 <c>GameRig</c> 안, 즉 <b>씬과 함께 죽는다</b>.
    /// 예전에는 <c>Start</c> 에서 한 번만 찾아 뒀고 그 <c>Start</c> 는 다시 불리지 않으므로,
    /// 지하를 한 번 다녀오면 참조가 유니티 가짜 null 이 되어 "기계 UI 가 열려 있다" 를 놓쳤다.
    /// 그러면 i 키가 인벤토리만 닫고 <b>기계 창은 영영 안 닫히며</b>, <c>isAnyUIOpen</c> 이
    /// 계속 참이라 이동까지 잠긴다(상자·아이템 저장소에서 처음 발견됐지만 기계 전종에 해당한다).
    /// <see cref="PowerLinkMode"/> 가 같은 참조를 다루는 방식과 같은 규약이다.
    /// </summary>
    private MachineInteraction Machine
    {
        get
        {
            if (machineInteraction == null)
                machineInteraction = FindAnyObjectByType<MachineInteraction>(FindObjectsInactive.Include);
            return machineInteraction;
        }
    }

    void OnEnable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnToggleInventoryPerformed += Toggle;
    }

    void OnDisable()
    {
        InputActionManager input = InputActionManager.InstanceIfAlive;   // 종료 중엔 Instance 가 null 이다
        if (input != null) input.OnToggleInventoryPerformed -= Toggle;
    }

    private void Toggle()
    {
        if (UIManager.Instance == null)
            return;

        // 기계 UI가 열려 있으면 i 키는 기계 뷰(기계+인벤토리)를 닫는다.
        MachineInteraction machine = Machine;
        if (machine != null && machine.IsOpen)
        {
            machine.CloseView();
            return;
        }

        // 그 외에는 인벤토리 패널만 토글(실제 열림 상태 기준으로 판단해 desync 방지).
        if (UIManager.Instance.IsOpen("Inventory"))
            UIManager.Instance.CloseUI("Inventory");
        else
            UIManager.Instance.OpenUI("Inventory");
    }
}
