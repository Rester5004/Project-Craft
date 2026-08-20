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
    private readonly List<SlotEntry> upgrades = new();
    private readonly List<BarEntry> inputFluidBars = new();
    private readonly List<BarEntry> outputFluidBars = new();
    private BarEntry energyBar;
    private BarEntry fuelBar;
    private BarEntry progressBar;
    private TMP_Text machineNameText;
    private Button manualButton;          // 손으로 돌리는 기계에서만 보이는 "작동" 버튼
    private GameObject manualButtonGO;
    private Button coreUpgradeButton;     // 코어 조합기에서만 보이는 "티어 업그레이드" 버튼
    private GameObject coreUpgradeButtonGO;
    private TMP_Text coreUpgradeLabel;

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
    public int UpgradeElementCount => upgrades.Count;
    public int InputFluidElementCount => inputFluidBars.Count;
    public int OutputFluidElementCount => outputFluidBars.Count;

    // ── 코어 업그레이드 (CraftingTableUI 가 쓴다) ────────────────
    // 칸·버튼 모두 <b>프리팹에 있는 요소</b>다. 예전에는 코드로 만들었는데, 그러면 위치·크기를
    // 씬에서 못 옮기고 팩토리 검증기도 볼 수 없었다. 코어가 아닌 조합대에서는 Open 이 꺼 준다.

    /// <summary>첫 업그레이드 칸(프리팹에 없으면 null). 코어 조합기의 티어 재료 자리다.</summary>
    protected ItemSlot FirstUpgradeSlot => upgrades.Count > 0 ? upgrades[0].slot : null;

    protected Button CoreUpgradeButton => coreUpgradeButton;
    protected GameObject CoreUpgradeButtonObject => coreUpgradeButtonGO;
    protected TMP_Text CoreUpgradeLabel => coreUpgradeLabel;

    /// <summary>패널이 비활성으로 시작할 수 있으므로 Awake 대신 필요 시점에 초기화한다.</summary>
    private void EnsureInitialized()
    {
        if (initialized) return;

        inputs.Clear();
        outputs.Clear();
        fuels.Clear();
        upgrades.Clear();
        inputFluidBars.Clear();
        outputFluidBars.Clear();
        energyBar = null;
        fuelBar = null;
        progressBar = null;
        machineNameText = null;
        manualButton = null;
        manualButtonGO = null;
        coreUpgradeButton = null;
        coreUpgradeButtonGO = null;
        coreUpgradeLabel = null;

        List<MachineUIElement> inputEls = new();
        List<MachineUIElement> outputEls = new();
        List<MachineUIElement> fuelEls = new();
        List<MachineUIElement> upgradeEls = new();
        List<MachineUIElement> inputFluidEls = new();
        List<MachineUIElement> outputFluidEls = new();
        MachineUIElement energyEl = null;
        MachineUIElement fuelBarEl = null;
        MachineUIElement progressEl = null;
        MachineUIElement nameEl = null;
        MachineUIElement manualEl = null;
        MachineUIElement coreUpgradeEl = null;
        bool hasPlainInput = false;
        bool hasStorage = false;

        foreach (MachineUIElement element in GetComponentsInChildren<MachineUIElement>(true))
        {
            if (element.role == MachineUIRole.InputSlot) hasPlainInput = true;
            if (element.role == MachineUIRole.StorageSlot) hasStorage = true;
            switch (element.role)
            {
                // 저장 칸은 새 구간이 아니라 입력 구간에 산다(StorageBlock 주석 참조) — 같은 목록에 담는다.
                case MachineUIRole.InputSlot:
                case MachineUIRole.StorageSlot: inputEls.Add(element); break;
                case MachineUIRole.OutputSlot: outputEls.Add(element); break;
                case MachineUIRole.FuelSlot: fuelEls.Add(element); break;
                case MachineUIRole.UpgradeSlot: upgradeEls.Add(element); break;
                case MachineUIRole.InputFluidBar: inputFluidEls.Add(element); break;
                case MachineUIRole.OutputFluidBar: outputFluidEls.Add(element); break;
                case MachineUIRole.EnergyBar: energyEl = Prefer(energyEl, element); break;
                case MachineUIRole.FuelBar: fuelBarEl = Prefer(fuelBarEl, element); break;
                case MachineUIRole.ProgressBar: progressEl = Prefer(progressEl, element); break;
                case MachineUIRole.MachineName: nameEl = Prefer(nameEl, element); break;
                case MachineUIRole.ManualButton: manualEl = Prefer(manualEl, element); break;
                case MachineUIRole.CoreUpgradeButton: coreUpgradeEl = Prefer(coreUpgradeEl, element); break;
            }
        }

        // 둘은 같은 평면 인덱스를 노린다 — 섞여 있으면 index 가 겹쳐 두 칸이 같은 스택을 그린다.
        if (hasPlainInput && hasStorage)
            Debug.LogError($"[DefaultMachineUI] '{name}' 에 InputSlot 과 StorageSlot 이 섞여 있습니다. " +
                           "저장 칸은 입력 구간에 살아 인덱스가 겹칩니다 — 한 종류만 쓰세요.", this);

        SortByIndex(inputEls);
        SortByIndex(outputEls);
        SortByIndex(fuelEls);
        SortByIndex(upgradeEls);
        SortByIndex(inputFluidEls);
        SortByIndex(outputFluidEls);

        foreach (MachineUIElement e in inputEls) inputs.Add(MakeSlot(e));
        foreach (MachineUIElement e in outputEls) outputs.Add(MakeSlot(e));
        foreach (MachineUIElement e in fuelEls) fuels.Add(MakeSlot(e));
        foreach (MachineUIElement e in upgradeEls) upgrades.Add(MakeSlot(e));
        foreach (MachineUIElement e in inputFluidEls) inputFluidBars.Add(MakeBar(e));
        foreach (MachineUIElement e in outputFluidEls) outputFluidBars.Add(MakeBar(e));
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
        if (coreUpgradeEl != null)
        {
            coreUpgradeButtonGO = coreUpgradeEl.gameObject;
            coreUpgradeButton = coreUpgradeEl.GetComponent<Button>();
            // 라벨은 자식에서 찾는다 — 버튼과 글자를 따로 배선하게 하면 한쪽만 빠뜨린다.
            coreUpgradeLabel = coreUpgradeEl.GetComponentInChildren<TMP_Text>(true);
            if (coreUpgradeButton == null)
                Debug.LogError($"[DefaultMachineUI] '{coreUpgradeEl.name}' (CoreUpgradeButton) 에 Button 이 없습니다.", coreUpgradeEl);
        }

        sharedInventory = new MachineInventory(inputs.Count, outputs.Count, fuels.Count, upgrades.Count);
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

    /// <summary>지정한 기계의 설정/인벤토리에 맞춰 슬롯·유체·에너지 UI를 구성하고 패널을 연다.</summary>
    public virtual void Open(MachineInstance instance)
    {
        EnsureInitialized();

        IItemContainer container = instance != null ? (IItemContainer)instance.inventory : sharedInventory;

        // 컨테이너의 실제 입력 칸 수(출력 평면 인덱스의 기준). 화면 클램프와 별개로 유지해야 한다.
        int containerInputCount = instance != null ? instance.InputCount : inputs.Count;
        int containerOutputCount = instance != null ? instance.OutputCount : outputs.Count;
        int visibleInputCount = containerInputCount;
        int visibleOutputCount = containerOutputCount;
        int containerFuelCount = instance != null ? instance.FuelCount : fuels.Count;
        int visibleFuelCount = containerFuelCount;
        int visibleUpgradeCount = instance != null ? instance.UpgradeCount : upgrades.Count;
        int inputFluidCount = instance != null ? instance.InputTankCount : 0;
        int outputFluidCount = instance != null ? instance.OutputTankCount : 0;
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
        // 업그레이드 칸과 유체 바는 <b>조용히</b> 클램프한다. 기존 UI 프리팹 11장에 아직 요소가 없어서,
        // 경고를 내면 기계를 열 때마다 11종 × 매번 로그가 쏟아진다(그러면 진짜 경고가 묻힌다).
        if (visibleUpgradeCount > upgrades.Count) visibleUpgradeCount = upgrades.Count;
        if (inputFluidCount > inputFluidBars.Count) inputFluidCount = inputFluidBars.Count;
        if (outputFluidCount > outputFluidBars.Count) outputFluidCount = outputFluidBars.Count;

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
        // 연료 칸이 없어도 유체를 태우는 기계(가스 발전기)는 잔량 바가 보여야 한다.
        bool showFuelBar = visibleFuelCount > 0 || (instance != null && instance.BurnsFuel);
        if (fuelBar != null && fuelBar.go != null) fuelBar.go.SetActive(showFuelBar);

        // 업그레이드 슬롯: 컨테이너 평면 인덱스 [입력 + 출력 + 연료 .. ]
        int upgradeBase = containerInputCount + containerOutputCount + containerFuelCount;
        for (int u = 0; u < upgrades.Count; u++)
        {
            bool active = u < visibleUpgradeCount;
            if (upgrades[u].go != null) upgrades[u].go.SetActive(active);
            if (active && upgrades[u].slot != null)
            {
                upgrades[u].slot.Bind(container, upgradeBase + u);
                upgrades[u].slot.SetInsertable(true);
                boundSlots.Add(upgrades[u].slot);
            }
        }

        for (int k = 0; k < inputFluidBars.Count; k++)
            if (inputFluidBars[k].go != null) inputFluidBars[k].go.SetActive(k < inputFluidCount);
        for (int k = 0; k < outputFluidBars.Count; k++)
            if (outputFluidBars[k].go != null) outputFluidBars[k].go.SetActive(k < outputFluidCount);

        if (energyBar != null && energyBar.go != null) energyBar.go.SetActive(usesEnergy);

        if (machineNameText != null)
            machineNameText.text = MachineTitle(instance);

        BindPowerLinkButton(instance);
        BindManualButton(instance);

        // 코어 업그레이드는 조합대 하나가 쓰는 것이라 기본은 꺼 둔다.
        // 조합대 패널을 5종이 나눠 쓰므로, 켜는 것은 CraftingTableUI 가 코어일 때만 한다.
        if (coreUpgradeButtonGO != null) coreUpgradeButtonGO.SetActive(false);

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

    /// <summary>
    /// 입력 칸을 <b>기계 인벤토리가 아닌 다른 컨테이너</b>에 다시 붙인다.
    ///
    /// 저장 터미널이 쓴다 — 터미널은 자기 칸이 0개고 보여 줄 것은 <b>네트워크 전체</b>라,
    /// <see cref="Open"/> 이 끝난 뒤 <see cref="NetworkContainer"/> 로 갈아 끼운다.
    /// (<see cref="MachineUIRole.StorageSlot"/> 요소도 입력 구간에 담기므로 상자 프리팹을 그대로 쓸 수 있다.)
    ///
    /// ⚠ <b>base.Open 뒤에 불러야 한다</b> — 그 안에서 칸을 끄고 다시 바인딩하기 때문이다.
    /// </summary>
    protected void RebindInputs(IItemContainer container, int count)
    {
        if (container == null) return;

        for (int i = 0; i < inputs.Count; i++)
        {
            bool active = i < count;
            if (inputs[i].go != null) inputs[i].go.SetActive(active);
            if (!active || inputs[i].slot == null) continue;

            inputs[i].slot.Bind(container, i);
            inputs[i].slot.SetInsertable(true);
            if (!boundSlots.Contains(inputs[i].slot)) boundSlots.Add(inputs[i].slot);
        }
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
    /// <summary>유체·에너지·진행도 바에 표시할 문구. <see cref="BarTooltip"/> 이 호출한다.</summary>
    public string GetBarTooltip(MachineUIRole role, int index)
    {
        switch (role)
        {
            case MachineUIRole.InputFluidBar: return FluidTooltip(boundInstance != null ? boundInstance.GetInputTank(index) : null);
            case MachineUIRole.OutputFluidBar: return FluidTooltip(boundInstance != null ? boundInstance.GetOutputTank(index) : null);
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

    private string FluidTooltip(FluidStack tank)
    {
        int max = boundInstance != null ? boundInstance.MaxFluid : 0;
        string name = tank != null && tank.fluid != null ? tank.fluid.DisplayName : "비어 있음";
        int amount = tank != null ? tank.amount : 0;
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

    /// <summary>
    /// 입력 탱크 잔량(0~1)과 담긴 유체를 표시한다.
    ///
    /// <b>색이 아니라 유체 이름을 받는다</b> — 그림이 없어도 색으로 구분되게 하는 것이 목적이고,
    /// 무슨 색으로 그릴지는 <see cref="FluidColors"/> 한 곳만 안다. 색을 넘겨받으면
    /// 부르는 쪽마다 색을 정하게 되어 언젠가 서로 달라진다.
    /// </summary>
    public void SetInputFluid(int index, float ratio, string fluidId)
        => SetFluidBar(inputFluidBars, index, ratio, fluidId);

    /// <summary>출력 탱크 잔량(0~1)과 담긴 유체를 표시한다.</summary>
    public void SetOutputFluid(int index, float ratio, string fluidId)
        => SetFluidBar(outputFluidBars, index, ratio, fluidId);

    private static void SetFluidBar(List<BarEntry> bars, int index, float ratio, string fluidId)
    {
        if (index < 0 || index >= bars.Count) return;
        FillingSlot bar = bars[index].bar;
        if (bar == null) return;

        bar.FillAmount = ratio;
        bar.FillColor = FluidColors.Of(fluidId);   // 빈 탱크·모르는 유체는 회색
    }

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
