# 화로 구성 보고서

`Tools/Project Craft/Machines/Setup Furnace And Move Smelting` 가 자동 생성한 파일입니다.

- 연료 설정: 갈탄 = 200 Energy
- 연료 설정: 석탄 = 400 Energy
- UI 프리팹 생성: `Assets/Prefabs/UI/Machines/Furnace_UI.prefab` (연료 칸 + 연료 바 추가)
- 월드 프리팹 생성: `Assets/Prefabs/Blocks/Machines/Furnace.prefab`
- 블록 생성: `Machine:Furnace` (화로, 티어 0, 연료 1칸)
- 아이템 생성: `Machine:Furnace` (화로)
- 월드 프리팹 생성: `Assets/Prefabs/Blocks/Machines/ElectricFurnace.prefab`
- 블록 생성: `Machine:ElectricFurnace` (전기로, 티어 1, 전력)
- 아이템 생성: `Machine:ElectricFurnace` (전기로)
- 월드 프리팹 생성: `Assets/Prefabs/Blocks/Machines/HVElectricFurnace.prefab`
- 블록 생성: `Machine:HVElectricFurnace` (고전압 전기로, 티어 2, 전력)
- 아이템 생성: `Machine:HVElectricFurnace` (고전압 전기로)

## 제련 레시피 이동

- 화로에 연결한 제련 레시피 11개 (새로 연결 11개)
  - 티어 0 · smelt_copper → 구리 주괴
  - 티어 0 · smelt_glass → 유리
  - 티어 0 · smelt_gold → 금 주괴
  - 티어 0 · smelt_iron → 철 주괴
  - 티어 0 · smelt_lime → 석회
  - 티어 0 · smelt_silicon → 실리콘
  - 티어 0 · smelt_silver → 은 주괴
  - 티어 1 · smelt_lead → 납 주괴
  - 티어 1 · smelt_nickel → 니켈 주괴
  - 티어 1 · smelt_tin → 주석 주괴
  - 티어 1 · smelt_titanium → 티타늄 주괴

## 합금 재련기 정리

- 삭제(화로의 제련 레시피와 중복): `Assets/Prefabs/Recipes/Smelt_CopperIngot.asset`
- 삭제(화로의 제련 레시피와 중복): `Assets/Prefabs/Recipes/Smelt_IronIngot.asset`
- 합금 재련기에 남은 레시피 4개, 그중 등록 대상(Notion) 2개
  - 티어 1 · alloy_bronze → 청동
  - 티어 1 · alloy_invar → 인바(invar)

## 딕셔너리 등록

- ItemDictionary: 아이템 3개 · 블록 3개 추가
- RecipeDictionary: 레시피 13개 추가 (제련 11 + 합금 2 중 새것)
- RecipeDictionary 의 빈 칸 2개 제거
- 씬 저장: `Assets/Scenes/MapTest.unity`
