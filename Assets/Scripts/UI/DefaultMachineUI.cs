using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DefaultMachineUI : MonoBehaviour
{
    [Header("InputSlots")]
    [SerializeField] private GameObject inputSlot1;
    [SerializeField] private GameObject inputSlot2;
    [SerializeField] private GameObject inputSlot3;

    [Header("ProgressBar")]
    [SerializeField] private GameObject progressBar;

    [Header("OutputSlots")]
    [SerializeField] private GameObject outputSlot1;
    [SerializeField] private GameObject outputSlot2;
    [SerializeField] private GameObject outputSlot3;
    [SerializeField] private GameObject outputSlot4;
    [SerializeField] private GameObject outputSlot5;
    [SerializeField] private GameObject outputSlot6;

    [Header("GasBar")]
    [SerializeField] private GameObject gasBar1;
    [SerializeField] private GameObject gasBar2;

    [Header("EnergyBar")]
    [SerializeField] private GameObject energyBar;

    [Header("MachineName")]
    [SerializeField] private TMP_Text machineName;

    private GameObject[] inputSlotObjs;
    private GameObject[] outputSlotObjs;
    private ItemSlot[] inputSlotComps;
    private ItemSlot[] outputSlotComps;

    private MachineInventory sharedInventory;              // 인스턴스 없이 열 때 폴백
    private readonly List<ItemSlot> boundSlots = new();    // 현재 바인딩된(활성) 슬롯
    private bool initialized;

    private void EnsureInitialized()
    {
        if (initialized) return;

        inputSlotObjs = new[] { inputSlot1, inputSlot2, inputSlot3 };
        outputSlotObjs = new[] { outputSlot1, outputSlot2, outputSlot3, outputSlot4, outputSlot5, outputSlot6 };

        inputSlotComps = GatherSlots(inputSlotObjs);
        outputSlotComps = GatherSlots(outputSlotObjs);

        sharedInventory = new MachineInventory(inputSlotObjs.Length, outputSlotObjs.Length);
        if (UIManager.Instance != null)
            UIManager.Instance.AddUI(gameObject, "Machine");

        initialized = true;
    }

    private static ItemSlot[] GatherSlots(GameObject[] objs)
    {
        ItemSlot[] comps = new ItemSlot[objs.Length];
        for (int i = 0; i < objs.Length; i++)
        {
            if (objs[i] == null) { Debug.LogWarning($"[DefaultMachineUI] slot 참조 {i} 가 비어 있습니다."); continue; }
            comps[i] = objs[i].GetComponent<ItemSlot>();
            if (comps[i] == null) Debug.LogError($"[DefaultMachineUI] '{objs[i].name}' 에 ItemSlot 이 없습니다.");
        }
        return comps;
    }

    /// <summary>지정한 기계의 설정/인벤토리에 맞춰 슬롯·가스·에너지 UI를 구성하고 패널을 연다.</summary>
    public void Open(MachineInstance instance)
    {
        EnsureInitialized();

        IItemContainer container = instance != null ? (IItemContainer)instance.inventory : sharedInventory;
        int inputCount, outputCount, gasCount;
        bool usesEnergy;
        if (instance != null)
        {
            inputCount = instance.InputCount;
            outputCount = instance.OutputCount;
            gasCount = instance.GasCount;
            usesEnergy = instance.UsesEnergy;
        }
        else
        {
            inputCount = inputSlotObjs.Length;
            outputCount = outputSlotObjs.Length;
            gasCount = 0;
            usesEnergy = false;
        }

        // 물리 슬롯 GameObject 상한으로 클램프
        if (inputCount > inputSlotObjs.Length)
        {
            Debug.LogWarning($"[DefaultMachineUI] inputCount {inputCount} > 물리 입력 슬롯 {inputSlotObjs.Length}. 클램프함.");
            inputCount = inputSlotObjs.Length;
        }
        if (outputCount > outputSlotObjs.Length)
        {
            Debug.LogWarning($"[DefaultMachineUI] outputCount {outputCount} > 물리 출력 슬롯 {outputSlotObjs.Length}. 클램프함.");
            outputCount = outputSlotObjs.Length;
        }

        boundSlots.Clear();

        // 입력 슬롯: index [0 .. inputCount-1]
        for (int i = 0; i < inputSlotObjs.Length; i++)
        {
            bool active = i < inputCount;
            if (inputSlotObjs[i] != null) inputSlotObjs[i].SetActive(active);
            if (active && inputSlotComps[i] != null)
            {
                inputSlotComps[i].Bind(container, i);
                inputSlotComps[i].SetInsertable(true);   // 입력만 드롭 수용
                boundSlots.Add(inputSlotComps[i]);
            }
        }

        // 출력 슬롯: index [inputCount .. inputCount+outputCount-1]
        for (int j = 0; j < outputSlotObjs.Length; j++)
        {
            bool active = j < outputCount;
            if (outputSlotObjs[j] != null) outputSlotObjs[j].SetActive(active);
            if (active && outputSlotComps[j] != null)
            {
                outputSlotComps[j].Bind(container, inputCount + j);
                outputSlotComps[j].SetInsertable(false); // 출력은 드롭 거부
                boundSlots.Add(outputSlotComps[j]);
            }
        }

        // 가스/에너지 바 표시
        if (gasBar1 != null) gasBar1.SetActive(gasCount >= 1);
        if (gasBar2 != null) gasBar2.SetActive(gasCount >= 2);
        if (energyBar != null) energyBar.SetActive(usesEnergy);

        if (machineName != null)
            machineName.text = instance != null ? instance.blockId : "";

        if (UIManager.Instance != null) UIManager.Instance.OpenUI("Machine");
        else gameObject.SetActive(true);

        RefreshAll();
    }

    /// <summary>폴백 오픈(드래그 회귀 테스트용 공용 저장소).</summary>
    public void Open() => Open(null);

    public void Close()
    {
        if (UIManager.Instance != null) UIManager.Instance.CloseUI("Machine");
        else gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        foreach (ItemSlot slot in boundSlots)
            if (slot != null) slot.Refresh();
    }

    private void OnEnable()
    {
        if (initialized) RefreshAll();
    }
}
