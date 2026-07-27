using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.UIFactory.EditorTools
{
    /// <summary>MachineUIElement 인스펙터: 역할/인덱스 + 필수 컴포넌트 경고를 함께 표시한다.</summary>
    [CustomEditor(typeof(MachineUIElement))]
    [CanEditMultipleObjects]
    public class MachineUIElementEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1) return;

            MachineUIElement element = (MachineUIElement)target;
            switch (element.role)
            {
                case MachineUIRole.InputSlot:
                case MachineUIRole.OutputSlot:
                    if (element.GetComponent<ItemSlot>() == null)
                        EditorGUILayout.HelpBox("이 역할에는 ItemSlot 컴포넌트가 필요합니다.", MessageType.Error);
                    break;
                case MachineUIRole.ProgressBar:
                case MachineUIRole.EnergyBar:
                case MachineUIRole.InputGasBar:
                case MachineUIRole.OutputGasBar:
                    if (element.GetComponent<FillingSlot>() == null)
                        EditorGUILayout.HelpBox("이 역할에는 FillingSlot 컴포넌트가 필요합니다.", MessageType.Error);
                    break;
                case MachineUIRole.MachineName:
                    if (element.GetComponent<TMPro.TMP_Text>() == null)
                        EditorGUILayout.HelpBox("이 역할에는 TMP_Text 컴포넌트가 필요합니다.", MessageType.Error);
                    break;
            }

            DefaultMachineUI owner = element.GetComponentInParent<DefaultMachineUI>(true);
            if (owner == null)
            {
                EditorGUILayout.HelpBox("상위에 DefaultMachineUI 가 없습니다. 런타임에 바인딩되지 않습니다.", MessageType.Warning);
                return;
            }

            List<MachineUIElement> sameRole = new();
            foreach (MachineUIElement e in owner.GetComponentsInChildren<MachineUIElement>(true))
                if (e.role == element.role) sameRole.Add(e);

            foreach (MachineUIElement e in sameRole)
                if (e != element && e.index == element.index)
                {
                    EditorGUILayout.HelpBox($"같은 역할의 '{e.name}' 과 index({element.index})가 중복입니다.", MessageType.Error);
                    break;
                }
        }
    }
}
