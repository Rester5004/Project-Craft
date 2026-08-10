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
        public const string FluidBarPrefab = BuildingBlockFolder + "/GasBar.prefab";
        public const string NameTextPrefab = BuildingBlockFolder + "/MachineNameText.prefab";

        /// <summary>손으로 돌리는 기계의 "작동" 버튼.</summary>
        public const string ManualButtonPrefab = BuildingBlockFolder + "/ManualButton.prefab";

        /// <summary>조합대 목록의 레시피 칸(MachineSlot 아트에서 파생, 드래그 없는 클릭 전용).</summary>
        public const string CraftRecipeSlotPrefab = BuildingBlockFolder + "/CraftRecipeSlot.prefab";

        /// <summary>조합대의 도구 부품 칸(MachineSlot 아트에서 파생, 종류 제한이 있는 드래그 슬롯).</summary>
        public const string ToolPartSlotPrefab = BuildingBlockFolder + "/ToolPartSlot.prefab";

        /// <summary>업그레이드 모듈 칸(MachineSlot 아트에서 파생, UpgradeModuleItem 만 받는다).</summary>
        public const string UpgradeSlotPrefab = BuildingBlockFolder + "/UpgradeSlot.prefab";

        /// <summary>저장 블록(상자·아이템 저장소)의 칸(MachineSlot 아트에서 파생).</summary>
        public const string StorageSlotPrefab = BuildingBlockFolder + "/StorageSlot.prefab";

        /// <summary>코어 조합기의 "티어 업그레이드" 버튼(ManualButton 아트에서 파생).</summary>
        public const string CoreUpgradeButtonPrefab = BuildingBlockFolder + "/CoreUpgradeButton.prefab";

        /// <summary>조합대 UI 산출물.</summary>
        public const string CraftingTableUIPrefab = OutputFolder + "/CraftingTable_UI.prefab";

        public static string PrefabPathFor(MachineUIRole role) => role switch
        {
            MachineUIRole.InputSlot => SlotPrefab,
            MachineUIRole.OutputSlot => SlotPrefab,
            MachineUIRole.FuelSlot => SlotPrefab,
            MachineUIRole.UpgradeSlot => UpgradeSlotPrefab,
            MachineUIRole.StorageSlot => StorageSlotPrefab,
            MachineUIRole.ProgressBar => ProgressBarPrefab,
            MachineUIRole.EnergyBar => EnergyBarPrefab,
            MachineUIRole.FuelBar => EnergyBarPrefab,   // 잔량 바 아트를 그대로 쓴다
            MachineUIRole.InputFluidBar => FluidBarPrefab,
            MachineUIRole.OutputFluidBar => FluidBarPrefab,
            MachineUIRole.MachineName => NameTextPrefab,
            MachineUIRole.ManualButton => ManualButtonPrefab,
            MachineUIRole.CoreUpgradeButton => CoreUpgradeButtonPrefab,
            _ => null
        };
    }
}
