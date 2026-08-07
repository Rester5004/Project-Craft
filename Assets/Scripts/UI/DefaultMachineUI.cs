using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
    private readonly List<SlotEntry> fuels = new();
    private readonly List<BarEntry> inputGasBars = new();
    private readonly List<BarEntry> outputGasBars = new();
    private BarEntry energyBar;
    private BarEntry fuelBar;
    private BarEntry progressBar;
    private TMP_Text machineNameText;
    private Button manualButton;          // 손으로 돌리는 기계에서만 보이는 "작동" 버튼
    private GameObject manualButtonGO;

    private MachineInventory sharedInventory;            // 인스턴스 없이 열 때 폴백
    private readonly List<ItemSlot> boundSlots = new();  // 현재 바인딩된(활성) 슬롯
    private MachineInstance boundInstance;               // 현재 표시 중인 기계(진행도를 밀어 넣는 주체)
    private bool initialized;
    private Button powerLinkButton;                      // 발전기에서만 보이는 "전력 전송" 버튼

    /// <summary>현재 이 패널이 표시 중인 기계(없으면 null).</summary>
    public MachineInstance BoundInstance => boundInstance;

    /// <summary>프리팹이 가진 요소 수(레이아웃 상한).</summary>
    public int InputElementCount => inputs.Count;
    public int OutputElementCount => outputs.Count;
    public int FuelElementCount => fuels.Count;
    public int InputGasElementCount => inputGasBars.Count;
    public int OutputGasElementCount => outputGasBars.Count;

    /// <summary>패널이 비활성으로 시작할 수 있으므로 Awake 대신 필요 시점에 초기화한다.</summary>
    private void EnsureInitialized()
    {
        if (initialized) return;

        inputs.Clear();
        outputs.Clear();
        fuels.Clear();
        inputGasBars.Clear();
        outputGasBars.Clear();
        energyBar = null;
        fuelBar = null;
        progressBar = null;
        machineNameText = null;
        manualButton = null;
        manualButtonGO = null;

        List<MachineUIElement> inputEls = new();
        List<MachineUIElement> outputEls = new();
        List<MachineUIElement> fuelEls = new();
        List<MachineUIElement> inputGasEls = new();
        List<MachineUIElement> outputGasEls = new();
        MachineUIElement energyEl = null;
        MachineUIElement fuelBarEl = null;
        MachineUIElement progressEl = null;
        MachineUIElement nameEl = null;
        MachineUIElement manualEl = null;

        foreach (MachineUIElement element in GetComponentsInChildren<MachineUIElement>(true))
        {
            switch (element.role)
            {
                case MachineUIRole.InputSlot: inputEls.Add(element); break;
                case MachineUIRole.OutputSlot: outputEls.Add(element); break;
                case MachineUIRole.FuelSlot: fuelEls.Add(element); break;
                case MachineUIRole.InputGasBar: inputGasEls.Add(element); break;
                case MachineUIRole.OutputGasBar: outputGasEls.Add(element); break;
                case MachineUIRole.EnergyBar: energyEl = Prefer(energyEl, element); break;
                case MachineUIRole.FuelBar: fuelBarEl = Prefer(fuelBarEl, element); break;
                case MachineUIRole.ProgressBar: progressEl = Prefer(progressEl, element); break;
                case MachineUIRole.MachineName: nameEl = Prefer(nameEl, element); break;
                case MachineUIRole.ManualButton: manualEl = Prefer(manualEl, element); break;
            }
        }

        SortByIndex(inputEls);
        SortByIndex(outputEls);
        SortByIndex(fuelEls);
        SortByIndex(inputGasEls);
        SortByIndex(outputGasEls);

        foreach (MachineUIElement e in inputEls) inputs.Add(MakeSlot(e));
        foreach (MachineUIElement e in outputEls) outputs.Add(MakeSlot(e));
        foreach (MachineUIElement e in fuelEls) fuels.Add(MakeSlot(e));
        foreach (MachineUIElement e in inputGasEls) inputGasBars.Add(MakeBar(e));
        foreach (MachineUIElement e in outputGasEls) outputGasBars.Add(MakeBar(e));
        if (energyEl != null) energyBar = MakeBar(energyEl);
        if (fuelBarEl != null) fuelBar = MakeBar(fuelBarEl);
        if (progressEl != null) progressBar = MakeBar(progressEl);
        if (nameEl != null)
        {
            machineNameText = nameEl.GetComponent<TMP_Text>();
            if (machineNameText == null)
                Debug.LogError($"[DefaultMachineUI] '{nameEl.name}' 에 TMP_Text 가 없습니다.", nameEl);
        }
        if (manualEl != null)
        {
            manualButtonGO = manualEl.gameObject;
            manualButton = manualEl.GetComponent<Button>();
            if (manualButton == null)
                Debug.LogError($"[DefaultMachineUI] '{manualEl.name}' (ManualButton) 에 Button 이 없습니다.", manualEl);
        }

        sharedInventory = new MachineInventory(inputs.Count, outputs.Count, fuels.Count);
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

    private BarEntry MakeBar(MachineUIElement element)
    {
        FillingSlot bar = element.GetComponent<FillingSlot>();
        if (bar == null)
            Debug.LogError($"[DefaultMachineUI] '{element.name}' ({element.role}) 에 FillingSlot 이 없습니다.", element);

        // 호버 툴팁을 코드로 붙인다(기존 기계 UI 프리팹을 수정하지 않기 위함).
        BarTooltip tooltip = element.GetComponent<BarTooltip>();
        if (tooltip == null) tooltip = element.gameObject.AddComponent<BarTooltip>();
        tooltip.Bind(this);

        return new BarEntry { go = element.gameObject, bar = bar };
    }

    /// <summary>지정한 기계의 설정/인벤토리에 맞춰 슬롯·가스·에너지 UI를 구성하고 패널을 연다.</summary>
    public virtual void Open(MachineInstance instance)
    {
        EnsureInitialized();

        IItemContainer container = instance != null ? (IItemContainer)instance.inventory : sharedInventory;

        // 컨테이너의 실제 입력 칸 수(출력 평면 인덱스의 기준). 화면 클램프와 별개로 유지해야 한다.
        int containerInputCount = instance != null ? instance.InputCount : inputs.Count;
        int containerOutputCount = instance != null ? instance.OutputCount : outputs.Count;
        int visibleInputCount = containerInputCount;
        int visibleOutputCount = containerOutputCount;
        int visibleFuelCount = instance != null ? instance.FuelCount : fuels.Count;
        int inputGasCount = instance != null ? instance.InputGasCount : 0;
        int outputGasCount = instance != null ? instance.OutputGasCount : 0;
        // 발전기는 전력을 쓰지 않지만(isUseEnergy=0) 자기 발전 버퍼는 보여야 한다.
        bool usesEnergy = instance != null && (instance.UsesEnergy || instance.IsGenerator);

        // 프리팹이 가진 요소 수로 클램프
        if (visibleInputCount > inputs.Count)
        {
            WarnShortage("입력", visibleInputCount, inputs.Count);
            visibleInputCount = inputs.Count;
        }
        if (visibleOutputCount > outputs.Count)
        {
            WarnShortage("출력", visibleOutputCount, outputs.Count);
            visibleOutputCount = outputs.Count;
        }
        if (visibleFuelCount > fuels.Count)
        {
            WarnShortage("연료", visibleFuelCount, fuels.Count);
            visibleFuelCount = fuels.Count;
        }
        if (inputGasCount > inputGasBars.Count)
        {
            WarnShortage("입력 가스", inputGasCount, inputGasBars.Count);
            inputGasCount = inputGasBars.Count;
        }
        if (outputGasCount > outputGasBars.Count)
        {
            WarnShortage("출력 가스", outputGasCount, outputGasBars.Count);
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

        // 연료 슬롯: 컨테이너 평면 인덱스 [입력 + 출력 .. ]
        int fuelBase = containerInputCount + containerOutputCount;
        for (int f = 0; f < fuels.Count; f++)
        {
            bool active = f < visibleFuelCount;
            if (fuels[f].go != null) fuels[f].go.SetActive(active);
            if (active && fuels[f].slot != null)
            {
                fuels[f].slot.Bind(container, fuelBase + f);
                fuels[f].slot.SetInsertable(true);
                boundSlots.Add(fuels[f].slot);
            }
        }
        if (fuelBar != null && fuelBar.go != null) fuelBar.go.SetActive(visibleFuelCount > 0);

        for (int k = 0; k < inputGasBars.Count; k++)
            if (inputGasBars[k].go != null) inputGasBars[k].go.SetActive(k < inputGasCount);
        for (int k = 0; k < outputGasBars.Count; k++)
            if (outputGasBars[k].go != null) outputGasBars[k].go.SetActive(k < outputGasCount);

        if (energyBar != null && energyBar.go != null) energyBar.go.SetActive(usesEnergy);

        if (machineNameText != null)
            machineNameText.text = MachineTitle(instance);

        BindPowerLinkButton(instance);
        BindManualButton(instance);

        gameObject.SetActive(true);
        RefreshAll();

        // 진행도 갱신은 기계 인스턴스가 직접 밀어 넣는다.
        if (boundInstance != null && boundInstance != instance) boundInstance.DetachUI(this);
        boundInstance = instance;
        if (instance != null) instance.AttachUI(this);
        else SetProgress(0f);
    }

    /// <summary>
    /// "전력 전송" 버튼을 이 기계에 맞게 붙인다.
    ///
    /// 버튼을 프리팹에 넣지 않고 코드로 만드는 이유: uiPrefab 이 없는 기계들은 <b>기본 패널 한 개를
    /// 돌려 쓰므로</b> 프리팹에 넣으면 전기로에도 버튼이 보인다. 여기서 발전기일 때만 켠다.
    /// </summary>
    private void BindPowerLinkButton(MachineInstance instance)
    {
        bool isGenerator = instance != null && instance.IsGenerator;
        if (isGenerator && powerLinkButton == null) powerLinkButton = BuildPowerLinkButton();
        if (powerLinkButton == null) return;

        powerLinkButton.gameObject.SetActive(isGenerator);

        // 리스너를 지우지 않으면 기계를 열 때마다 쌓여 예전 발전기의 모드가 열린다.
        powerLinkButton.onClick.RemoveAllListeners();
        if (!isGenerator) return;

        MachineInstance captured = instance;
        powerLinkButton.onClick.AddListener(() =>
        {
            if (PowerLinkMode.Instance != null) PowerLinkMode.Instance.Enter(captured);
        });
    }

    /// <summary>
    /// "작동" 버튼을 이 기계에 맞게 붙인다. 손으로 돌리는 기계에서만 보인다.
    ///
    /// 전력 전송 버튼과 달리 <b>프리팹의 요소</b>(<see cref="MachineUIRole.ManualButton"/>)를 쓴다 —
    /// 전력바가 빠진 자리에 버튼을 놓는 등, 위치를 레이아웃에서 잡고 싶기 때문이다.
    /// 버튼이 없는 프리팹(자동 기계용)에서는 아무 일도 하지 않는다.
    /// </summary>
    private void BindManualButton(MachineInstance instance)
    {
        if (manualButtonGO == null) return;

        bool manual = instance != null && instance.Info != null && instance.Info.IsManual;
        manualButtonGO.SetActive(manual);

        if (manualButton == null) return;

        // 리스너를 지우지 않으면 기계를 열 때마다 쌓여, 한 번 눌렀는데 <b>예전에 열었던 기계까지</b> 함께 돈다.
        manualButton.onClick.RemoveAllListeners();
        if (!manual) return;

        MachineInstance captured = instance;
        manualButton.onClick.AddListener(() => captured.ManualStep());
    }

    private Button BuildPowerLinkButton()
    {
        GameObject go = new GameObject("PowerLinkButton", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(200f, 56f);

        Image background = go.AddComponent<Image>();
        background.color = new Color(0.15f, 0.35f, 0.6f, 0.95f);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.layer = go.layer;
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRect = (RectTransform)labelGO.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = "전력 전송";
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        return go.AddComponent<Button>();
    }

    /// <summary>
    /// 프리팹의 요소 수가 기계 설정보다 적을 때 경고할지.
    /// 조합대처럼 베이스의 슬롯을 쓰지 않고 패널이 직접 슬롯을 관리하는 경우에는 끈다.
    /// </summary>
    protected virtual bool WarnOnElementShortage => true;

    private void WarnShortage(string what, int needed, int available)
    {
        if (!WarnOnElementShortage) return;
        Debug.LogWarning($"[DefaultMachineUI] {what} {needed}칸이 필요하지만 프리팹에는 {available}개뿐입니다. 클램프합니다.", this);
    }

    /// <summary>화면에 보일 기계 이름. 블록 정보가 없으면 blockId 로 폴백한다.</summary>
    protected static string MachineTitle(MachineInstance instance)
    {
        if (instance == null) return "";
        return instance.Info != null ? instance.Info.DisplayName : instance.blockId;
    }

    /// <summary>폴백 오픈(레이아웃 확인·드래그 회귀 테스트용 공용 저장소).</summary>
    public void Open() => Open(null);

    public virtual void Close()
    {
        if (boundInstance != null) { boundInstance.DetachUI(this); boundInstance = null; }
        gameObject.SetActive(false);
    }

    /// <summary>기계가 사라질 때(청크 언로드 등) 인스턴스 쪽에서 연결을 끊는다.</summary>
    public void DetachInstance(MachineInstance instance)
    {
        if (boundInstance != instance) return;
        boundInstance = null;
        SetProgress(0f);
    }

    /// <summary>바인딩된 슬롯 뷰를 다시 그린다(기계가 아이템을 생산/소모했을 때 호출).</summary>
    public void RefreshSlots() => RefreshAll();

    // ── 바 호버 툴팁 ───────────────────────────────────────────
    /// <summary>가스/에너지/진행도 바에 표시할 문구. <see cref="BarTooltip"/> 이 호출한다.</summary>
    public string GetBarTooltip(MachineUIRole role, int index)
    {
        switch (role)
        {
            case MachineUIRole.InputGasBar: return GasTooltip(boundInstance != null ? boundInstance.GetInputGas(index) : null);
            case MachineUIRole.OutputGasBar: return GasTooltip(boundInstance != null ? boundInstance.GetOutputGas(index) : null);
            case MachineUIRole.EnergyBar: return EnergyTooltip();
            case MachineUIRole.FuelBar: return FuelTooltip();
            case MachineUIRole.ProgressBar: return ProgressTooltip();
            default: return "";
        }
    }

    private string FuelTooltip()
    {
        if (boundInstance == null) return "";
        if (boundInstance.BurnRemaining <= 0f) return "연료 없음";
        return $"연소 중  {boundInstance.BurnRemaining:N0}";
    }

    private string GasTooltip(Gas gas)
    {
        float max = boundInstance != null ? boundInstance.MaxGas : 0f;
        string name = gas != null && gas.gas != null ? gas.gas.gasName : "비어 있음";
        float amount = gas != null ? gas.amount : 0f;
        return $"{name}  {amount:N0} / {max:N0}";
    }

    private string EnergyTooltip()
    {
        if (boundInstance == null) return "";
        return $"{boundInstance.CurrentEnergy:N0} / {boundInstance.MaxEnergy:N0}";
    }

    private string ProgressTooltip()
    {
        if (boundInstance == null || boundInstance.ActiveRecipe == null) return "대기 중";
        return $"가공 중  {boundInstance.ProgressRatio * 100f:N0}%";
    }

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

    /// <summary>타고 있는 연료의 잔량(0~1) 표시.</summary>
    public void SetFuel(float ratio)
    {
        if (fuelBar != null && fuelBar.bar != null) fuelBar.bar.FillAmount = ratio;
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
