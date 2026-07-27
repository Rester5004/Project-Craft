using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 레이아웃에 종속되지 않는 기계 UI 뷰.
/// 자식의 <see cref="MachineUIElement"/> 를 역할별·index 순으로 수집해
/// 기계 설정(<see cref="MachineInstance"/>)에 맞춰 슬롯/바를 바인딩하고 표시한다.
/// 프리팹이 곧 레이아웃이므로 슬롯 개수 상한은 프리팹이 가진 요소 수다.
/// </summary>
public class DefaultMachineUI : MonoBehaviour
{
    private class SlotEntry
    {
        public GameObject go;
        public ItemSlot slot;
    }

    private class BarEntry
    {
        public GameObject go;
        public FillingSlot bar;
    }

    private readonly List<SlotEntry> inputs = new();
    private readonly List<SlotEntry> outputs = new();
    private readonly List<BarEntry> inputGasBars = new();
    private readonly List<BarEntry> outputGasBars = new();
    private BarEntry energyBar;
    private BarEntry progressBar;
    private TMP_Text machineNameText;

    private MachineInventory sharedInventory;            // 인스턴스 없이 열 때 폴백
    private readonly List<ItemSlot> boundSlots = new();  // 현재 바인딩된(활성) 슬롯
    private bool initialized;

    /// <summary>프리팹이 가진 요소 수(레이아웃 상한).</summary>
    public int InputElementCount => inputs.Count;
    public int OutputElementCount => outputs.Count;
    public int InputGasElementCount => inputGasBars.Count;
    public int OutputGasElementCount => outputGasBars.Count;

    /// <summary>패널이 비활성으로 시작할 수 있으므로 Awake 대신 필요 시점에 초기화한다.</summary>
    private void EnsureInitialized()
    {
        if (initialized) return;

        inputs.Clear();
        outputs.Clear();
        inputGasBars.Clear();
        outputGasBars.Clear();
        energyBar = null;
        progressBar = null;
        machineNameText = null;

        List<MachineUIElement> inputEls = new();
        List<MachineUIElement> outputEls = new();
        List<MachineUIElement> inputGasEls = new();
        List<MachineUIElement> outputGasEls = new();
        MachineUIElement energyEl = null;
        MachineUIElement progressEl = null;
        MachineUIElement nameEl = null;

        foreach (MachineUIElement element in GetComponentsInChildren<MachineUIElement>(true))
        {
            switch (element.role)
            {
                case MachineUIRole.InputSlot: inputEls.Add(element); break;
                case MachineUIRole.OutputSlot: outputEls.Add(element); break;
                case MachineUIRole.InputGasBar: inputGasEls.Add(element); break;
                case MachineUIRole.OutputGasBar: outputGasEls.Add(element); break;
                case MachineUIRole.EnergyBar: energyEl = Prefer(energyEl, element); break;
                case MachineUIRole.ProgressBar: progressEl = Prefer(progressEl, element); break;
                case MachineUIRole.MachineName: nameEl = Prefer(nameEl, element); break;
            }
        }

        SortByIndex(inputEls);
        SortByIndex(outputEls);
        SortByIndex(inputGasEls);
        SortByIndex(outputGasEls);

        foreach (MachineUIElement e in inputEls) inputs.Add(MakeSlot(e));
        foreach (MachineUIElement e in outputEls) outputs.Add(MakeSlot(e));
        foreach (MachineUIElement e in inputGasEls) inputGasBars.Add(MakeBar(e));
        foreach (MachineUIElement e in outputGasEls) outputGasBars.Add(MakeBar(e));
        if (energyEl != null) energyBar = MakeBar(energyEl);
        if (progressEl != null) progressBar = MakeBar(progressEl);
        if (nameEl != null)
        {
            machineNameText = nameEl.GetComponent<TMP_Text>();
            if (machineNameText == null)
                Debug.LogError($"[DefaultMachineUI] '{nameEl.name}' 에 TMP_Text 가 없습니다.", nameEl);
        }

        sharedInventory = new MachineInventory(inputs.Count, outputs.Count);
        initialized = true;
    }

    private static MachineUIElement Prefer(MachineUIElement existing, MachineUIElement candidate)
    {
        if (existing == null) return candidate;
        Debug.LogWarning($"[DefaultMachineUI] '{candidate.role}' 역할이 중복입니다. '{existing.name}' 을 사용합니다.", candidate);
        return existing;
    }

    private static void SortByIndex(List<MachineUIElement> list)
        => list.Sort((a, b) => a.index.CompareTo(b.index));

    private static SlotEntry MakeSlot(MachineUIElement element)
    {
        ItemSlot slot = element.GetComponent<ItemSlot>();
        if (slot == null)
            Debug.LogError($"[DefaultMachineUI] '{element.name}' ({element.role}) 에 ItemSlot 이 없습니다.", element);
        return new SlotEntry { go = element.gameObject, slot = slot };
    }

    private static BarEntry MakeBar(MachineUIElement element)
    {
        FillingSlot bar = element.GetComponent<FillingSlot>();
        if (bar == null)
            Debug.LogError($"[DefaultMachineUI] '{element.name}' ({element.role}) 에 FillingSlot 이 없습니다.", element);
        return new BarEntry { go = element.gameObject, bar = bar };
    }

    /// <summary>지정한 기계의 설정/인벤토리에 맞춰 슬롯·가스·에너지 UI를 구성하고 패널을 연다.</summary>
    public void Open(MachineInstance instance)
    {
        EnsureInitialized();

        IItemContainer container = instance != null ? (IItemContainer)instance.inventory : sharedInventory;

        // 컨테이너의 실제 입력 칸 수(출력 평면 인덱스의 기준). 화면 클램프와 별개로 유지해야 한다.
        int containerInputCount = instance != null ? instance.InputCount : inputs.Count;
        int visibleInputCount = containerInputCount;
        int visibleOutputCount = instance != null ? instance.OutputCount : outputs.Count;
        int inputGasCount = instance != null ? instance.InputGasCount : 0;
        int outputGasCount = instance != null ? instance.OutputGasCount : 0;
        bool usesEnergy = instance != null && instance.UsesEnergy;

        // 프리팹이 가진 요소 수로 클램프
        if (visibleInputCount > inputs.Count)
        {
            Debug.LogWarning($"[DefaultMachineUI] 입력 {visibleInputCount}칸이 필요하지만 프리팹에는 {inputs.Count}개뿐입니다. 클램프합니다.", this);
            visibleInputCount = inputs.Count;
        }
        if (visibleOutputCount > outputs.Count)
        {
            Debug.LogWarning($"[DefaultMachineUI] 출력 {visibleOutputCount}칸이 필요하지만 프리팹에는 {outputs.Count}개뿐입니다. 클램프합니다.", this);
            visibleOutputCount = outputs.Count;
        }
        if (inputGasCount > inputGasBars.Count)
        {
            Debug.LogWarning($"[DefaultMachineUI] 입력 가스 {inputGasCount}개가 필요하지만 프리팹에는 {inputGasBars.Count}개뿐입니다. 클램프합니다.", this);
            inputGasCount = inputGasBars.Count;
        }
        if (outputGasCount > outputGasBars.Count)
        {
            Debug.LogWarning($"[DefaultMachineUI] 출력 가스 {outputGasCount}개가 필요하지만 프리팹에는 {outputGasBars.Count}개뿐입니다. 클램프합니다.", this);
            outputGasCount = outputGasBars.Count;
        }

        boundSlots.Clear();

        // 입력 슬롯: 컨테이너 평면 인덱스 [0 .. inputCount-1]
        for (int i = 0; i < inputs.Count; i++)
        {
            bool active = i < visibleInputCount;
            if (inputs[i].go != null) inputs[i].go.SetActive(active);
            if (active && inputs[i].slot != null)
            {
                inputs[i].slot.Bind(container, i);
                inputs[i].slot.SetInsertable(true);   // 입력만 드롭 수용
                boundSlots.Add(inputs[i].slot);
            }
        }

        // 출력 슬롯: 컨테이너 평면 인덱스 [containerInputCount .. ]
        for (int j = 0; j < outputs.Count; j++)
        {
            bool active = j < visibleOutputCount;
            if (outputs[j].go != null) outputs[j].go.SetActive(active);
            if (active && outputs[j].slot != null)
            {
                outputs[j].slot.Bind(container, containerInputCount + j);
                outputs[j].slot.SetInsertable(false); // 출력은 드롭 거부
                boundSlots.Add(outputs[j].slot);
            }
        }

        for (int k = 0; k < inputGasBars.Count; k++)
            if (inputGasBars[k].go != null) inputGasBars[k].go.SetActive(k < inputGasCount);
        for (int k = 0; k < outputGasBars.Count; k++)
            if (outputGasBars[k].go != null) outputGasBars[k].go.SetActive(k < outputGasCount);

        if (energyBar != null && energyBar.go != null) energyBar.go.SetActive(usesEnergy);

        if (machineNameText != null)
            machineNameText.text = instance != null ? instance.blockId : "";

        gameObject.SetActive(true);
        RefreshAll();
    }

    /// <summary>폴백 오픈(레이아웃 확인·드래그 회귀 테스트용 공용 저장소).</summary>
    public void Open() => Open(null);

    public void Close() => gameObject.SetActive(false);

    /// <summary>진행도(0~1) 표시.</summary>
    public void SetProgress(float ratio)
    {
        if (progressBar != null && progressBar.bar != null) progressBar.bar.FillAmount = ratio;
    }

    /// <summary>에너지 잔량(0~1) 표시.</summary>
    public void SetEnergy(float ratio)
    {
        if (energyBar != null && energyBar.bar != null) energyBar.bar.FillAmount = ratio;
    }

    /// <summary>입력 가스 잔량(0~1) 표시.</summary>
    public void SetInputGas(int gasIndex, float ratio) => SetBar(inputGasBars, gasIndex, ratio);

    /// <summary>출력 가스 잔량(0~1) 표시.</summary>
    public void SetOutputGas(int gasIndex, float ratio) => SetBar(outputGasBars, gasIndex, ratio);

    private static void SetBar(List<BarEntry> bars, int index, float ratio)
    {
        if (index < 0 || index >= bars.Count) return;
        if (bars[index].bar != null) bars[index].bar.FillAmount = ratio;
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
