# 아이템 중복 조사

`Tools/Project Craft/Dictionary/아이템 중복 조사` 가 자동 생성한 파일입니다. 아무것도 바꾸지 않습니다.

## 요약

- 아이템 **226개**, 등록된 레시피 155개
  - Items 3개
  - Machines 49개
  - Placeholder 84개
  - Resource1 38개
  - ToolParts 48개
  - Tools 4개

쓰임 표기는 `등록/미완/블록` — 등록된 레시피 / 그 밖의 레시피 / 블록의 dropItem.
`Recipe.importNote` 는 세지 않는다(원문 JSON 이 들어 있어 세면 전부 참조됨으로 나온다).

## 중복 후보

### ① 표시 이름이 같은 짝 (표기 흔들림)

| 키 | 아이템 | 폴더 | 쓰임(등록/미완/블록) |
|---|---|---|---|

### ② 플레이스홀더 ↔ 이미 있는 기계 (이름이 정확히 같음)

| 플레이스홀더 | 겹치는 기계 | 쓰임 |
|---|---|---|

### ③ 이름이 포함 관계인 짝 — ⚠ 오탐 섞임, 사람이 판단할 것

| 플레이스홀더 | 겹칠 수 있는 기계 | 쓰임 |
|---|---|---|

**확실한 중복 0건** (①+②) · 검토 필요 0건 (③)

## 레시피·블록이 참조하지 않는 아이템

⚠ **참조가 없다 ≠ 필요 없다.** 두 가지 이유로 안 잡힌다:

1. **설계에는 있는데 레시피가 아직 안 쓰였다.** 추출기 산출물(철·구리·금…)이 그랬다.
2. **참조가 아니라 이름·딕셔너리로 연결된다.** 기계 배치 아이템은 `blockId == itemName`
   문자열로 이어지고, 도구 부품은 `ToolDictionary` 와 부품 칸 필터가 들고 있다.

그래서 아래 목록에서 **Machines · ToolParts · Tools 는 거의 전부 오탐**이다.
실제로 살펴볼 값어치가 있는 것은 `Placeholder` 와 `Resource1` 뿐이다.

- **Placeholder** 4종: acid_solution, crude_oil, magic_powder, petroleum
- **Resource1** 9종: diamond, energy_crystal, osmium_ingot, raw_osmium_ore, raw_thorium_ore, ruby, sapphire, surface_powder, thorium_ingot
- **Machines** 14종 *(이름으로 연결 — 대부분 오탐)*: CoreCrafter, Machine:Extractor10, Machine:Extractor11, Machine:Extractor12, Machine:Extractor13, Machine:Extractor20, Machine:Extractor21, Machine:Extractor22, Machine:Extractor23, Machine:GeothermalGenerator, Machine:HVElectricFurnace, Machine:NuclearPlant, Machine:OilDrill, Machine:Transformer
- **ToolParts** 43종 *(이름으로 연결 — 대부분 오탐)*: aluminum_hammer_head, aluminum_pickaxe_head, aluminum_rod, copper_hammer_head, copper_pickaxe_head, copper_rod, gold_hammer_head, gold_pickaxe_head, gold_rod, iron_hammer_head, iron_pickaxe_head, lead_hammer_head, lead_pickaxe_head, lead_rod, lithium_hammer_head, lithium_pickaxe_head, lithium_rod, nickel_hammer_head, nickel_pickaxe_head, nickel_rod, osmium_hammer_head, osmium_pickaxe_head, osmium_rod, quartz_hammer_head, quartz_pickaxe_head, quartz_rod, silver_hammer_head, silver_pickaxe_head, silver_rod, thorium_hammer_head, thorium_pickaxe_head, thorium_rod, tin_hammer_head, tin_pickaxe_head, tin_rod, titanium_hammer_head, titanium_pickaxe_head, titanium_rod, uranium_hammer_head, uranium_pickaxe_head, uranium_rod, wood_hammer_head, wood_pickaxe_head

## 승격 대기 — 정본이 없는 플레이스홀더

| 아이템 | 쓰임 | 아이콘 | 배치 가능 |
|---|---|---|---|
| `mana_shard` | 21/0/0 | 있음 | 아니오 |
| `stone` | 19/0/1 | 있음 | 예 |
| `iron_plate` | 18/0/0 | 있음 | 아니오 |
| `conductor_powder` | 16/0/0 | 있음 | 아니오 |
| `rebar` | 14/0/0 | 있음 | 아니오 |
| `reinforced_concrete` | 11/0/0 | 있음 | 아니오 |
| `computer_chip` | 9/0/0 | 있음 | 아니오 |
| `copper_plate` | 6/0/0 | 있음 | 아니오 |
| `sand` | 6/0/0 | 있음 | 아니오 |
| `crank` | 5/0/0 | 있음 | 아니오 |
| `water` | 5/0/0 | 있음 | 아니오 |
| `brick` | 5/0/0 | 있음 | 아니오 |
| `gravel` | 4/0/0 | 있음 | 아니오 |
| `glass` | 4/0/0 | 있음 | 아니오 |
| `silver_plate` | 3/0/0 | 있음 | 아니오 |
| `manastone` | 2/0/1 | 있음 | 예 |
| `manastone_dust` | 3/0/0 | 있음 | 아니오 |
| `manastone_shard` | 3/0/0 | 있음 | 아니오 |
| `meteorite` | 2/0/1 | 있음 | 아니오 |
| `invar_plate` | 3/0/0 | 있음 | 아니오 |
| `invar` | 3/0/0 | 있음 | 아니오 |
| `gold_plate` | 3/0/0 | 있음 | 아니오 |
| `meteorite_dust` | 3/0/0 | 있음 | 아니오 |
| `bronze` | 3/0/0 | 있음 | 아니오 |
| `machine_upgrade_module` | 3/0/0 | 있음 | 아니오 |
| `reinforced_alloy` | 3/0/0 | 있음 | 아니오 |
| `bucket` | 3/0/0 | 있음 | 아니오 |
| `silicon_plate` | 3/0/0 | 있음 | 아니오 |
| `special_alloy` | 3/0/0 | 있음 | 아니오 |
| `meteorite_shard` | 3/0/0 | 있음 | 아니오 |
| `nuclear_fuel_rod` | 0/2/0 | 있음 | 아니오 |
| `tree_seed` | 2/0/0 | 있음 | 아니오 |
| `manastone_fine_dust` | 2/0/0 | 있음 | 아니오 |
| `silicon` | 2/0/0 | 있음 | 아니오 |
| `metal_plate` | 2/0/0 | 있음 | 아니오 |
| `solid_pipe` | 1/0/1 | 있음 | 예 |
| `steel` | 2/0/0 | 있음 | 아니오 |
| `stone_blade` | 2/0/0 | 있음 | 아니오 |
| `mana_chip` | 2/0/0 | 있음 | 아니오 |
| `stone_powder` | 2/0/0 | 있음 | 아니오 |
| `sulfur_powder` | 2/0/0 | 있음 | 아니오 |
| `meteorite_fine_dust` | 2/0/0 | 있음 | 아니오 |
| `liquid_pipe` | 1/0/1 | 있음 | 예 |
| `gas_pipe` | 1/0/1 | 있음 | 예 |
| `cement` | 2/0/0 | 있음 | 아니오 |
| `dirt` | 2/0/0 | 있음 | 아니오 |
| `apple_tree_seed` | 2/0/0 | 있음 | 아니오 |
| `enchanted_conductor_powder` | 2/0/0 | 있음 | 아니오 |
| `bronze_plate` | 2/0/0 | 있음 | 아니오 |
| `glass_pipe` | 2/0/0 | 있음 | 아니오 |
| `iron_blade` | 2/0/0 | 있음 | 아니오 |
| `blade` | 2/0/0 | 있음 | 아니오 |
| `item_pipe` | 1/0/1 | 있음 | 예 |
| `lime` | 2/0/0 | 있음 | 아니오 |
| `any_coal_lignite` | 0/1/0 | 있음 | 아니오 |
| `sulfur_ore` | 1/0/0 | 있음 | 아니오 |
| `turbid_uranium` | 1/0/0 | 있음 | 아니오 |
| `any_coal_lignite_oil` | 0/1/0 | 있음 | 아니오 |
| `stone_knife` | 1/0/0 | 있음 | 아니오 |
| `any_coal_lignite_oil_2` | 0/1/0 | 있음 | 아니오 |
| `upgrade_efficiency` | 1/0/0 | 있음 | 아니오 |
| `upgrade_speed` | 1/0/0 | 있음 | 아니오 |
| `auto_crafter` | 1/0/0 | 있음 | 아니오 |
| `bearing` | 1/0/0 | 있음 | 아니오 |
| `any_coal_lignite_2` | 0/1/0 | 있음 | 아니오 |
| `uranium_concentrate` | 1/0/0 | 있음 | 아니오 |
| `conductor_crystal` | 1/0/0 | 있음 | 아니오 |
| `low_voltage_power` | 0/1/0 | 있음 | 아니오 |
| `dowsing_rod` | 1/0/0 | 있음 | 아니오 |
| `dowsing_rod_t0` | 1/0/0 | 있음 | 아니오 |
| `propeller` | 1/0/0 | 있음 | 아니오 |
| `motor` | 1/0/0 | 있음 | 아니오 |
| `glass_container` | 1/0/0 | 있음 | 아니오 |
| `iron_knife` | 1/0/0 | 있음 | 아니오 |
| `metal_ingot` | 1/0/0 | 있음 | 아니오 |
| `metal` | 1/0/0 | 있음 | 아니오 |
| `knife` | 1/0/0 | 있음 | 아니오 |
| `lava` | 1/0/0 | 있음 | 아니오 |
| `cavity_scanner` | 1/0/0 | 있음 | 아니오 |
| `acid_pipe` | 1/0/0 | 있음 | 아니오 |

合 80종. `Assets/Prefabs/Items/Placeholder` 에는 이미 정본인 것(돌·마력석·파이프)도 섞여 있으니 폴더로 판단하지 말 것.

## 설계와 어긋난 곳 (사람 판단 필요)

- `MachineAliases` 의 `{ 용광로 → 화로 }` — 설계 JSON 은
  "화로와 별개인 상위 제련 기계(강철·티타늄)" 라고 적고 있다. 이미 합쳐진 상태다.
- `MachineAliases` 의 `{ 수동 분쇄기 → 전기 분쇄기 }` — 수동 분쇄기는 별개 기계로
  두기로 했지만 아직 MachineBlock 이 없어 임시로 보내고 있다. 정식 기계가 되면 그 줄을 지운다.
- `extract_conductor_powder` 는 지형 '모래' 산출인데, 캔버스에서는
  수동 0-0티어 추출기의 3회 분쇄 산출이다.
- `extraction.json` 7개는 `terrain` 필드가 붙은 **지형 설치형**이라,
  분쇄물을 넣는 캔버스의 추출기와 개념이 다르다.

## 다음 작업으로 미룬 것

- 칼 계열(칼·돌 칼·철 칼·칼날·돌 칼날·철 칼날)의 도구 체계 흡수
- 마력석·운석의 1/2/3회 분쇄 아이템 6종 (돌은 조각난·부숴진·바스라진이 이미 있다)
- 캔버스의 추출 레시피 36종과 확률 산출 동작
- 1·2티어 자원 생성기
- 2티어(운석) 지형 — `TerrainPalette` 는 아직 stage1(돌)·stage2(마력석) 뿐이다
