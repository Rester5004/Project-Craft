using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 전력 관련 MachineBlock 값을 일괄로 채우는 에디터 도구.
///
/// 발전기 3종은 아래 표를 그대로 적용하고, 전력을 쓰는 기계는 초당 소비량을
/// 최대 저장량의 10% 로 채운다(코드 폴백 <see cref="MachineBlock.EnergyUseRate"/> 와 같은 값이라
/// 이 도구를 돌리지 않아도 동작은 같다 — 인스펙터에서 값이 보이게 하려는 것이다).
///
/// <b>재실행 안전</b>하다. 이미 값이 들어 있으면 건드리지 않으므로 몇 번을 돌려도 된다.
/// </summary>
public static class PowerSetup
{
    /// <summary>발전기 한 종의 설정. fuelBurnRate 가 곧 초당 발전량이다.</summary>
    private struct GeneratorSpec
    {
        public string blockName;
        public int fuelSlotCount;
        public float fuelBurnRate;     // 초당 발전량
        public float maxEnergyAmount;  // 발전 버퍼
        public int powerRange;         // 전송 거리(칸, 체비셰프)
        public int tier;
    }

    private static readonly GeneratorSpec[] Generators = new GeneratorSpec[]
    {
        new GeneratorSpec { blockName = "Machine:ThermalGenerator", fuelSlotCount = 1, fuelBurnRate = 20f,  maxEnergyAmount = 400f,  powerRange = 8,  tier = 1 },
        new GeneratorSpec { blockName = "Machine:NuclearPlant",     fuelSlotCount = 1, fuelBurnRate = 100f, maxEnergyAmount = 2000f, powerRange = 12, tier = 2 },
    };

    /// <summary>소비 전력이 미설정일 때 최대 저장량에 곱하는 비율.</summary>
    private const float UseRateRatio = 0.1f;

    [MenuItem("Tools/Project Craft/Machines/전력 기본값 채우기")]
    public static void FillAll()
    {
        Dictionary<string, MachineBlock> byName = new Dictionary<string, MachineBlock>();
        foreach (string guid in AssetDatabase.FindAssets("t:MachineBlock"))
        {
            MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(AssetDatabase.GUIDToAssetPath(guid));
            if (block == null || string.IsNullOrEmpty(block.blockName)) continue;
            byName[block.blockName] = block;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("# 전력 기본값 채우기");
        report.AppendLine();

        int changed = 0;
        changed += ApplyGenerators(byName, report);
        changed += ApplyConsumers(byName, report);
        changed += ApplyGeneratorUI(byName, report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine($"바뀐 에셋 {changed}개 / 검사한 MachineBlock {byName.Count}개");
        Debug.Log(report.ToString());
    }

    private static int ApplyGenerators(Dictionary<string, MachineBlock> byName, StringBuilder report)
    {
        report.AppendLine("## 발전기");
        int changed = 0;

        foreach (GeneratorSpec spec in Generators)
        {
            if (!byName.TryGetValue(spec.blockName, out MachineBlock block))
            {
                report.AppendLine($"- ⚠ `{spec.blockName}` 을 찾지 못했습니다.");
                continue;
            }

            List<string> edits = new List<string>();
            Set(ref block.isGenerator, true, "isGenerator", edits);
            Set(ref block.fuelSlotCount, spec.fuelSlotCount, "fuelSlotCount", edits);
            Set(ref block.fuelBurnRate, spec.fuelBurnRate, "fuelBurnRate(=초당 발전량)", edits);
            Set(ref block.maxEnergyAmount, spec.maxEnergyAmount, "maxEnergyAmount", edits);
            Set(ref block.powerRange, spec.powerRange, "powerRange", edits);
            Set(ref block.tier, spec.tier, "tier", edits);

            // 발전기는 레시피를 처리하지 않으므로 입출력 칸이 필요 없다.
            Set(ref block.inputSlotCount, 0, "inputSlotCount", edits);
            Set(ref block.outputSlotCount, 0, "outputSlotCount", edits);

            // 스스로 만든 전력을 다시 소비하지는 않는다.
            Set(ref block.isUseEnergy, false, "isUseEnergy", edits);

            if (edits.Count == 0) { report.AppendLine($"- `{block.blockName}` — 이미 설정됨"); continue; }

            EditorUtility.SetDirty(block);
            changed++;
            report.AppendLine($"- **{block.DisplayName}** (`{block.blockName}`) — {string.Join(", ", edits)}");
        }

        return changed;
    }

    private static int ApplyConsumers(Dictionary<string, MachineBlock> byName, StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("## 소비 기계 (energyUseRate = maxEnergyAmount × 10%)");
        int changed = 0;

        foreach (KeyValuePair<string, MachineBlock> pair in byName)
        {
            MachineBlock block = pair.Value;
            if (block.isGenerator || !block.isUseEnergy || block.maxEnergyAmount <= 0f) continue;
            if (block.energyUseRate > 0f) continue;   // 손으로 조정한 값은 덮어쓰지 않는다

            block.energyUseRate = block.maxEnergyAmount * UseRateRatio;
            EditorUtility.SetDirty(block);
            changed++;
            report.AppendLine($"- {block.DisplayName} (`{block.blockName}`) — {block.energyUseRate:N0}/s");
        }

        if (changed == 0) report.AppendLine("- 채울 것 없음(전부 이미 설정됨)");
        return changed;
    }

    // ── 발전기 전용 UI 프리팹 ────────────────────────────────────────
    private const string SourceUIPrefab = "Assets/Prefabs/UI/Machines/Furnace_UI.prefab";
    private const string GeneratorUIPrefab = "Assets/Prefabs/UI/Machines/Generator_UI.prefab";

    /// <summary>
    /// 발전기 UI 를 만들고 연결한다.
    ///
    /// <b>이게 없으면 발전기에 연료를 넣을 수 없다.</b> uiPrefab 이 없는 기계는 씬의 기본 패널로 폴백하는데
    /// 그 패널에는 연료 칸(FuelSlot)이 아예 없기 때문이다.
    /// 화로 UI 는 연료 칸·연료 바·에너지 바를 이미 갖고 있으므로 복제해서 필요 없는 요소만 걷어낸다.
    /// </summary>
    private static int ApplyGeneratorUI(Dictionary<string, MachineBlock> byName, StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("## 발전기 UI");

        GameObject ui = EnsureGeneratorUI(report);
        if (ui == null) return 0;

        int changed = 0;
        foreach (GeneratorSpec spec in Generators)
        {
            if (!byName.TryGetValue(spec.blockName, out MachineBlock block)) continue;
            if (block.uiPrefab == ui) { report.AppendLine($"- `{block.blockName}` — 이미 연결됨"); continue; }

            block.uiPrefab = ui;
            EditorUtility.SetDirty(block);
            changed++;
            report.AppendLine($"- **{block.DisplayName}** — uiPrefab 연결");
        }
        return changed;
    }

    private static GameObject EnsureGeneratorUI(StringBuilder report)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratorUIPrefab);
        if (existing != null) { report.AppendLine($"- `{GeneratorUIPrefab}` — 이미 있음"); return existing; }

        if (!AssetDatabase.CopyAsset(SourceUIPrefab, GeneratorUIPrefab))
        {
            report.AppendLine($"- ⚠ 복제 실패: {SourceUIPrefab} → {GeneratorUIPrefab}");
            return null;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(GeneratorUIPrefab);
        try
        {
            contents.name = "Generator_UI";

            // 발전기는 레시피를 처리하지 않으므로 입력·출력·진행도 요소가 필요 없다.
            List<GameObject> doomed = new List<GameObject>();
            foreach (MachineUIElement element in contents.GetComponentsInChildren<MachineUIElement>(true))
            {
                if (element.role == MachineUIRole.InputSlot
                    || element.role == MachineUIRole.OutputSlot
                    || element.role == MachineUIRole.ProgressBar)
                    doomed.Add(element.gameObject);
            }
            foreach (GameObject go in doomed) Object.DestroyImmediate(go);

            // 남은 요소를 가운데로 모은다.
            Move(contents, MachineUIRole.FuelSlot, new Vector2(-260f, 20f));
            Move(contents, MachineUIRole.FuelBar, new Vector2(-60f, 20f));
            Move(contents, MachineUIRole.EnergyBar, new Vector2(160f, 20f));

            PrefabUtility.SaveAsPrefabAsset(contents, GeneratorUIPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        report.AppendLine($"- UI 프리팹 생성: `{GeneratorUIPrefab}` (입력·출력·진행도 제거, 연료 칸/바 + 에너지 바 유지)");
        return AssetDatabase.LoadAssetAtPath<GameObject>(GeneratorUIPrefab);
    }

    private static void Move(GameObject root, MachineUIRole role, Vector2 position)
    {
        foreach (MachineUIElement element in root.GetComponentsInChildren<MachineUIElement>(true))
        {
            if (element.role != role) continue;
            RectTransform rect = element.transform as RectTransform;
            if (rect != null) rect.anchoredPosition = position;
            return;
        }
    }

    // ── 값이 다를 때만 쓰고 무엇을 바꿨는지 기록한다 ──────────────────
    private static void Set(ref bool field, bool value, string name, List<string> edits)
    {
        if (field == value) return;
        edits.Add($"{name} {field}→{value}");
        field = value;
    }

    private static void Set(ref int field, int value, string name, List<string> edits)
    {
        if (field == value) return;
        edits.Add($"{name} {field}→{value}");
        field = value;
    }

    private static void Set(ref float field, float value, string name, List<string> edits)
    {
        if (Mathf.Approximately(field, value)) return;
        edits.Add($"{name} {field:N0}→{value:N0}");
        field = value;
    }
}
