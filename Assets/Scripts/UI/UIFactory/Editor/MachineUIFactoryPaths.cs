namespace ProjectCraft.UIFactory.EditorTools
{
    /// <summary>제작 도구가 사용하는 고정 경로 모음.</summary>
    public static class MachineUIFactoryPaths
    {
        public const string FactoryScene = "Assets/Scenes/MachineUIFactory.unity";

        public const string BuildingBlockFolder = "Assets/Prefabs/UI/Machine";
        public const string OutputFolder = "Assets/Prefabs/UI/Machines";

        /// <summary>새 레이아웃의 기본 배경(기존 MachinePanel 의 패널 이미지에서 추출).</summary>
        public const string PanelBasePrefab = BuildingBlockFolder + "/MachinePanelBase.prefab";

        public const string SlotPrefab = BuildingBlockFolder + "/MachineSlot.prefab";
        public const string ProgressBarPrefab = BuildingBlockFolder + "/ProgressBar.prefab";
        public const string EnergyBarPrefab = BuildingBlockFolder + "/EnergyBar.prefab";
        public const string GasBarPrefab = BuildingBlockFolder + "/GasBar.prefab";
        public const string NameTextPrefab = BuildingBlockFolder + "/MachineNameText.prefab";

        /// <summary>조합대 목록의 레시피 칸(MachineSlot 아트에서 파생, 드래그 없는 클릭 전용).</summary>
        public const string CraftRecipeSlotPrefab = BuildingBlockFolder + "/CraftRecipeSlot.prefab";

        /// <summary>조합대 UI 산출물.</summary>
        public const string CraftingTableUIPrefab = OutputFolder + "/CraftingTable_UI.prefab";

        public static string PrefabPathFor(MachineUIRole role) => role switch
        {
            MachineUIRole.InputSlot => SlotPrefab,
            MachineUIRole.OutputSlot => SlotPrefab,
            MachineUIRole.ProgressBar => ProgressBarPrefab,
            MachineUIRole.EnergyBar => EnergyBarPrefab,
            MachineUIRole.InputGasBar => GasBarPrefab,
            MachineUIRole.OutputGasBar => GasBarPrefab,
            MachineUIRole.MachineName => NameTextPrefab,
            _ => null
        };
    }
}
