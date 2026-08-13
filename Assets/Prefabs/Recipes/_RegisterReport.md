# 딕셔너리 등록 보고서

`Tools/Project Craft/Dictionary/Register All Assets` 가 자동 생성한 파일입니다.

## 아이템

- 에셋 224개 중 224개 등록 대상, 새로 추가 2개

## 블록

- 에셋 60개 중 60개 등록 대상, 새로 추가 0개

## 유체

- 에셋 8개 중 8개 등록 대상, 새로 추가 0개

## 레시피

- 에셋 166개 중 **151개 등록 대상**, 새로 추가 2개
- 제외: 기계 미지정 2개 · 산출물 없음 13개

### ⚠ 재료만 있고 산출물이 없어 제외한 레시피 9개

등록했다면 재료만 먹고 아무것도 만들지 않았을 것들입니다. 산출물을 채운 뒤 다시 실행하세요.

- `Assets/Prefabs/Recipes/Incomplete/factory/nuclear_power.asset` (Machine:NuclearPlant)
- `Assets/Prefabs/Recipes/Incomplete/factory/power_nuclear.asset` (Machine:NuclearPlant)
- `Assets/Prefabs/Recipes/Incomplete/factory/power_thermal_t0.asset` (Machine:ThermalGenerator)
- `Assets/Prefabs/Recipes/Incomplete/factory/power_thermal_t1.asset` (Machine:ThermalGenerator)
- `Assets/Prefabs/Recipes/Incomplete/factory/thermal_power_t0.asset` (Machine:ThermalGenerator)
- `Assets/Prefabs/Recipes/Incomplete/factory/thermal_power_t1.asset` (Machine:ThermalGenerator)
- `Assets/Prefabs/Recipes/Incomplete/factory/transformer.asset` (Machine:Transformer)
- `Assets/Prefabs/Recipes/Incomplete/machine_processing/thermal_gen_coal.asset` (Machine:ThermalGenerator)
- `Assets/Prefabs/Recipes/Incomplete/machine_processing/thermal_gen_lignite.asset` (Machine:ThermalGenerator)

### ⚠ 같은 기계 · 같은 산출물이라 가려지는 레시피 1개

- `Assets/Prefabs/Recipes/Incomplete/factory/mana_from_shard.asset` ← `Assets/Prefabs/Recipes/Incomplete/factory/mana_from_crystal.asset` 가 먼저 잡힘

### 기계별 등록 수

- `Altar` : 11개
- `CoreCrafter` : 70개
- `Extractor0` : 3개
- `Extractor1` : 3개
- `Extractor2` : 3개
- `Furnace` : 11개
- `Machine:AdvancedCrafter` : 1개
- `Machine:AlloySmelter` : 4개
- `Machine:BioIncubator` : 1개
- `Machine:BlastFurnace` : 2개
- `Machine:ChemicalProcessor` : 3개
- `Machine:Compressor` : 11개
- `Machine:Electrolyzer` : 3개
- `Machine:LasorProcessor` : 3개
- `Machine:ManaDissolver` : 2개
- `Machine:OilDrill` : 1개
- `Machine:PrecisionLathe` : 4개
- `Pulverizer` : 12개
- `Pump` : 2개
- `ResourceGenerator` : 1개

씬 저장: `Assets/Scenes/MapTest.unity`
