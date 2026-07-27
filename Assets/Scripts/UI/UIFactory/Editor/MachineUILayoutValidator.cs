using System.Collections.Generic;
using UnityEngine;

namespace ProjectCraft.UIFactory.EditorTools
{
    public enum IssueLevel { Warning, Error }

    public class LayoutIssue
    {
        public IssueLevel level;
        public string message;
        public Object context;   // 클릭 시 선택할 대상
    }

    /// <summary>
    /// 기계 UI 레이아웃(루트 하위의 MachineUIElement 구성)을 검사한다.
    /// 제작 창과 커스텀 인스펙터가 함께 사용한다.
    /// </summary>
    public static class MachineUILayoutValidator
    {
        public static List<LayoutIssue> Validate(GameObject root, MachineBlock target)
        {
            List<LayoutIssue> issues = new();
            if (root == null)
            {
                issues.Add(new LayoutIssue { level = IssueLevel.Error, message = "레이아웃 루트가 없습니다." });
                return issues;
            }

            if (root.GetComponent<DefaultMachineUI>() == null)
                issues.Add(new LayoutIssue
                {
                    level = IssueLevel.Error,
                    message = "루트에 DefaultMachineUI 컴포넌트가 없습니다. 런타임에 열리지 않습니다.",
                    context = root
                });

            MachineUIElement[] elements = root.GetComponentsInChildren<MachineUIElement>(true);

            Dictionary<MachineUIRole, List<MachineUIElement>> byRole = new();
            foreach (MachineUIElement e in elements)
            {
                if (!byRole.TryGetValue(e.role, out List<MachineUIElement> list))
                    byRole[e.role] = list = new List<MachineUIElement>();
                list.Add(e);
            }

            // 필수 컴포넌트 확인
            foreach (MachineUIElement e in elements)
            {
                switch (e.role)
                {
                    case MachineUIRole.InputSlot:
                    case MachineUIRole.OutputSlot:
                        if (e.GetComponent<ItemSlot>() == null)
                            issues.Add(Err($"'{e.name}' ({e.role}) 에 ItemSlot 컴포넌트가 없습니다.", e));
                        break;
                    case MachineUIRole.ProgressBar:
                    case MachineUIRole.EnergyBar:
                    case MachineUIRole.InputGasBar:
                    case MachineUIRole.OutputGasBar:
                        if (e.GetComponent<FillingSlot>() == null)
                            issues.Add(Err($"'{e.name}' ({e.role}) 에 FillingSlot 컴포넌트가 없습니다.", e));
                        break;
                    case MachineUIRole.MachineName:
                        if (e.GetComponent<TMPro.TMP_Text>() == null)
                            issues.Add(Err($"'{e.name}' (MachineName) 에 TMP_Text 컴포넌트가 없습니다.", e));
                        break;
                }
            }

            // index 중복/누락 (여러 개를 쓰는 역할만)
            CheckIndices(byRole, MachineUIRole.InputSlot, issues);
            CheckIndices(byRole, MachineUIRole.OutputSlot, issues);
            CheckIndices(byRole, MachineUIRole.InputGasBar, issues);
            CheckIndices(byRole, MachineUIRole.OutputGasBar, issues);

            // 단일 역할 중복
            CheckSingle(byRole, MachineUIRole.EnergyBar, issues);
            CheckSingle(byRole, MachineUIRole.ProgressBar, issues);
            CheckSingle(byRole, MachineUIRole.MachineName, issues);

            // MachineBlock 설정과 개수 비교
            if (target != null)
            {
                CompareCount(issues, "입력 슬롯", Count(byRole, MachineUIRole.InputSlot), target.inputSlotCount);
                CompareCount(issues, "출력 슬롯", Count(byRole, MachineUIRole.OutputSlot), target.outputSlotCount);
                CompareCount(issues, "입력 가스 바", Count(byRole, MachineUIRole.InputGasBar), target.inputGasSlotCount);
                CompareCount(issues, "출력 가스 바", Count(byRole, MachineUIRole.OutputGasBar), target.outputGasSlotCount);

                bool hasEnergy = Count(byRole, MachineUIRole.EnergyBar) > 0;
                if (target.isUseEnergy && !hasEnergy)
                    issues.Add(Warn("이 기계는 에너지를 사용하지만 EnergyBar 요소가 없습니다."));
                if (!target.isUseEnergy && hasEnergy)
                    issues.Add(Warn("이 기계는 에너지를 사용하지 않지만 EnergyBar 요소가 있습니다(런타임에 숨겨짐)."));
            }

            return issues;
        }

        private static int Count(Dictionary<MachineUIRole, List<MachineUIElement>> byRole, MachineUIRole role)
            => byRole.TryGetValue(role, out List<MachineUIElement> list) ? list.Count : 0;

        private static void CompareCount(List<LayoutIssue> issues, string label, int actual, int expected)
        {
            if (actual < expected)
                issues.Add(Warn($"{label}이 {expected}개 필요하지만 {actual}개뿐입니다(런타임에 클램프됨)."));
            else if (actual > expected)
                issues.Add(Warn($"{label}이 {expected}개 필요한데 {actual}개 배치되어 있습니다(초과분은 숨겨짐)."));
        }

        private static void CheckIndices(Dictionary<MachineUIRole, List<MachineUIElement>> byRole, MachineUIRole role, List<LayoutIssue> issues)
        {
            if (!byRole.TryGetValue(role, out List<MachineUIElement> list) || list.Count == 0) return;

            HashSet<int> seen = new();
            foreach (MachineUIElement e in list)
            {
                if (e.index < 0)
                    issues.Add(Err($"'{e.name}' ({role}) 의 index 가 음수입니다.", e));
                else if (!seen.Add(e.index))
                    issues.Add(Err($"'{e.name}' ({role}) 의 index {e.index} 가 중복입니다.", e));
            }

            for (int i = 0; i < list.Count; i++)
                if (!seen.Contains(i))
                    issues.Add(Warn($"{role} 의 index {i} 가 비어 있습니다(0부터 연속이어야 합니다)."));
        }

        private static void CheckSingle(Dictionary<MachineUIRole, List<MachineUIElement>> byRole, MachineUIRole role, List<LayoutIssue> issues)
        {
            if (byRole.TryGetValue(role, out List<MachineUIElement> list) && list.Count > 1)
                issues.Add(Warn($"{role} 요소가 {list.Count}개입니다. 첫 번째만 사용됩니다.", list[1]));
        }

        private static LayoutIssue Err(string msg, Object ctx = null)
            => new() { level = IssueLevel.Error, message = msg, context = ctx };

        private static LayoutIssue Warn(string msg, Object ctx = null)
            => new() { level = IssueLevel.Warning, message = msg, context = ctx };
    }
}
