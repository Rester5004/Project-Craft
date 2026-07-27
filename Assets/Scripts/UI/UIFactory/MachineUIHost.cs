using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기계별 UI 패널을 관리하는 호스트. UIManager 에 "Machine" 으로 등록되는 루트이며,
/// blockId 에 맞는 패널(<see cref="MachineBlock.uiPrefab"/>)을 1회 인스턴스화해 캐시하고 전환한다.
/// uiPrefab 이 없는 기계는 <see cref="defaultPanel"/> 로 폴백한다.
/// 이 오브젝트는 씬에서 활성 상태로 시작해야 하며(Awake 등록), 패널들은 비활성으로 둔다.
/// </summary>
public class MachineUIHost : MonoBehaviour
{
    [Tooltip("uiPrefab 이 지정되지 않은 기계가 사용할 기본 패널")]
    [SerializeField] private DefaultMachineUI defaultPanel;

    [Tooltip("생성된 패널이 배치될 부모. 비우면 이 오브젝트")]
    [SerializeField] private Transform panelParent;

    private readonly Dictionary<string, DefaultMachineUI> cache = new();
    private DefaultMachineUI current;

    public DefaultMachineUI CurrentPanel => current;

    private void Awake()
    {
        if (panelParent == null) panelParent = transform;
        if (UIManager.Instance != null)
            UIManager.Instance.AddUI(gameObject, "Machine");
    }

    /// <summary>기계에 맞는 패널을 열고 바인딩한다.</summary>
    public void Open(MachineInstance instance)
    {
        if (instance == null) return;

        DefaultMachineUI panel = Resolve(instance.blockId);
        if (panel == null)
        {
            Debug.LogError("[MachineUIHost] 사용할 수 있는 기계 UI 패널이 없습니다.", this);
            return;
        }

        if (current != null && current != panel) current.Close();
        current = panel;

        if (UIManager.Instance != null) UIManager.Instance.OpenUI("Machine");
        else gameObject.SetActive(true);

        panel.Open(instance);
    }

    public void Close()
    {
        if (current != null) current.Close();
        current = null;

        if (UIManager.Instance != null) UIManager.Instance.CloseUI("Machine");
        else gameObject.SetActive(false);
    }

    /// <summary>blockId 에 대응하는 패널을 반환한다(최초 1회 인스턴스화 후 캐시).</summary>
    private DefaultMachineUI Resolve(string blockId)
    {
        if (!string.IsNullOrEmpty(blockId) && cache.TryGetValue(blockId, out DefaultMachineUI cached) && cached != null)
            return cached;

        MachineBlock info = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetMachineInfo(blockId) : null;
        GameObject prefab = info != null ? info.uiPrefab : null;

        if (prefab == null)
            return defaultPanel; // 폴백은 캐시하지 않는다(딕셔너리가 늦게 채워져도 다음 오픈에서 회복되도록)

        GameObject go = Instantiate(prefab, panelParent);
        go.name = prefab.name;
        DefaultMachineUI spawned = go.GetComponent<DefaultMachineUI>();
        if (spawned == null)
        {
            Debug.LogError($"[MachineUIHost] '{prefab.name}' 에 DefaultMachineUI 가 없어 기본 패널로 폴백합니다.", prefab);
            Destroy(go);
            return defaultPanel;
        }

        go.SetActive(false);
        if (!string.IsNullOrEmpty(blockId)) cache[blockId] = spawned;
        return spawned;
    }
}
