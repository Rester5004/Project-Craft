# Project Craft — 작업 참고

Unity 6 · 2D 탑다운 오픈월드 공장 게임. 대화는 한국어, 주석·문서도 한국어.
**이 파일은 매 세션 자동으로 읽힌다. 여기 있는 것은 다시 조사하지 말 것.**

---

## 1. 작업 규율 (사용자가 정한 것)

- **순차 적용.** 변경·개선은 한 번에 다 넣지 않고 단계로 쪼개, 각 단계마다 검증하고 보고한 뒤 다음으로 간다.
- **플레이어 상호작용 판정은 `PlayerInteraction.cs` 한 곳에서.** 무엇을 클릭했고 무엇을 할지는 여기서 정하고,
  상태 변경은 각 매니저에게 넘긴다.
- 작업 뒤 보고는 **검증 수치를 코드블록으로** 보여 주고, 설계상 짚을 점과 남은 것을 적는다.
- 이 파일은 변경사항이 있을경우 반드시 **갱신**해야한다.

## 2. Unity MCP 함정 (반드시 지킬 것)

| 상황 | 규칙 |
|---|---|
| 새 `.cs` 파일 | **반드시 `create_script`.** Write 로 만들면 어셈블리에 등록되지 않아 컴파일에서 빠진다 |
| 기존 파일 수정 | Edit/Write 로 해도 된다 |
| `execute_code` | 기본 `compiler: codedom` = **C# 6**. 로컬 함수·메서드 본문 `using`·`$"..."` 보간 일부 불가 → `System.Func`/`System.Action` 델리게이트와 정규화된 이름(`UnityEngine.Vector2Int`)을 쓴다 |
| 파일 삭제·`while(true)` | `safety_checks: false` 필요 |
| 백그라운드 에디터 | **프레임이 진행되지 않는다.** `Update`/물리가 안 돈다 → `Tick(dt)` 같은 공개 메서드를 직접 부르거나 `UnityEditor.EditorApplication.Step()`(호출당 1프레임) |
| 스크립트 수정 후 | `refresh_unity(compile: request)` → `read_console(types:["error"])` 로 0 확인 |
| 게임뷰 스크린샷 | `manage_camera(action:"screenshot", capture_source:"game_view", output_folder:"Captures")`. **프로젝트 밖 경로 거부.** 비동기라 직전 프레임이 찍힐 수 있으니 `Step()` 후 다시 찍는다. 다 쓰면 `Captures/` 삭제 |
| 월드 스크린샷 | 임시 `Camera` + `RenderTexture` + `Render()` 가 동기라 확실하다 (UI 는 안 나온다) |

### 세이브를 건드리기 전에

`WorldMap.Load` 는 **예외가 나면 세이브 파일을 지운다**(`File.Delete`). 플레이 모드 검증 전에 항상 백업:

```
C:\Users\c\AppData\LocalLow\DefaultCompany\Project Craft\worldmap.dat
```

- **에디트 모드에서 `WorldMap.Instance` 에 접근하지 말 것.** 싱글톤이 Awake 안 된 오브젝트를 만들어,
  플레이 종료 시 `OnApplicationQuit → Save()` 가 헤더만 쓰고 터져 세이브가 잘린다.
- 플레이 모드에서 만든 테스트 배치물은 **종료 전에 반드시 치운다**(종료 시 자동 저장됨).
  치운 뒤 남은 배치물 수를 세어 확인할 것.

## 3. 폴더 지도

```
Assets/Scripts/
  WorldMap/      WorldMap(청크·세이브) MapGenerator(로드·스폰) TilemapTextureLoader
                 TileAtlas TerrainPalette
                 Pipe{Block은 SO에} Atlas·Cell·Router·NetworkManager·FaceMode·FaceOverlay
                 Editor/ PipeSpriteSlicer PipeAtlasBuilder TileAtlasBuilder
  ScriptableObjects/  Items MainBlock MachineBlock PipeBlock PipeKind WrenchItem Recipe ...
                 Tool/ ToolDefinition ToolItem ToolMaterial ToolPart* ToolRecipe
                 Editor/ PipeSetup PowerSetup FurnaceSetup MachineBlockFiller WrenchSetup
  Machine/       MachineInstance MachineInventory RecipeSolver
  Player/        PlayerInteraction Inventory PlayerSave
  ItemDictionary/ ItemDictionary RecipeDictionary ToolDictionary
                 Editor/ DictionaryRegistrar RecipeJsonImporter RecipeTreeMerger ...
  UI/            UIManager CommandConsole ItemBrowser PowerLinkMode TooltipUI
                 MachineInteraction Inventory*  Slot/ ItemSlot ItemIconView BarTooltip
                 UIFactory/ CraftingTableUI MachineUIElement ...
  InputActionManager.cs   Util/Singleton.cs
Assets/Prefabs/  Blocks/{Machines,Terrain,Pipes} Items/{Placeholder,Resource1,Tools,ToolParts}
                 Recipes/{,Tools,Category,Incomplete} Tools/{Definitions,Materials,PartKinds}
Assets/Asset/    BlockImages ItemImages MachineImages Tiles/Atlas assetPlaceHolder.png
                 Player/Female/  Female.controller + 클립 6개(idle·걷기4·깜빡임)
Assets/Scenes/MapTest.unity   ← 유일한 실사용 씬. Tilemap 이 있는 씬도 여기뿐
```

## 4. 핵심 구조

### 지켜야 할 규약 세 가지 (어기면 조용히 데이터가 샌다)
- **종료·파괴 경로에서는 `Singleton.Instance` 를 쓰지 않는다.** 종료 중엔 **null 을 돌려주도록 설계돼 있어**
  정리 코드가 "할 게 없다"로 오해한다(실제로 인벤토리가 통째로 안 저장됐다). `Singleton.InstanceIfAlive`
  (찾지도 만들지도 않음) 또는 미리 캐시한 참조를 쓴다.
- **개수 클램프의 정본은 `RecipeSolver` 하나뿐이다.** `Inventory.AddItem` 도 `AddPartial` 로 위임한다.
  직접 `stack.count += n` 을 하면 `maxStack` 을 넘겨 **어디서도 못 쪼개는 스택**이 된다.
- **세이브 쓰기는 `SafeFile.WriteAtomic`, 읽기 실패는 `SafeFile.Quarantine`.** 원본을 먼저 비우고 쓰면
  중간에 죽었을 때 잘린 파일이 남고, 그걸 지우면 월드가 사라진다. 실패해도 원본은 그대로 둔다.

### 월드 · 세이브
- `WorldMap`(싱글톤) → `Dictionary<Vector2Int, Chunk>`, 청크 16×16. **세이브에서 읽은 청크를 전부 들고 있다.**
- `MapGenerator.LoadedChunks` 는 **렌더 창일 뿐**. 위상 조회는 `WorldMap.GetPlaceableAt`(청크를 만들지 않음)로 한다.
- 타일 ID 는 `"wall:xxx"` / `"floor:xxx"` 접두사. `Chunk.IsWall` / `IsFloor`.
- `PlaceableRecord` = 셀당 배치물 1개(기계·파이프 공용). 인벤토리·연료·전력·링크·파이프 짐·면 상태를 다 들고 있다.
  → 새 셀 상태는 여기 얹으면 청크 수명주기·세이브가 공짜다.
- **세이브 버전** (`SaveVersion`, `MinReadableVersion = 3`)

  | v | 추가된 것 |
  |---|---|
  | 4 | 슬롯별 `ItemInstance` |
  | 5 | 연료 슬롯 + 연소 잔량 |
  | 6 | 필드 드랍 `DropRecord` |
  | 7 | 기계 보유 전력 · 라운드로빈 커서 · 발전기 링크 |
  | 8 | 파이프 운반 중인 짐 `ParcelRecord[]` |
  | 9 | 파이프 네 면 상태 `faceModes`(1바이트) |
  | 10 | 기계 가공 진행도 `progress`(초) |

  `Chunk.Save` 순서: placeable 루프 안에 slots → burn → energy/cursor/links → parcels → faceModes → progress,
  루프 뒤 drops. `Chunk.Load` 는 `if (version >= N)`,
  **참조형은 `else` 로 빈 배열을 넣어야** 이전 세이브에서 NRE 가 안 난다(값형은 기본값이 곧 "없음"이라 불필요).
- ⚠ **`Bind` 에서 `LoadFrom` 이 복원한 값을 다시 0 으로 밀지 말 것.** 전력이 그래서 사라졌고,
  진행도도 같은 자리에서 지워지고 있었다(`progress = 0f` 가 `LoadFrom` 일곱 줄 뒤에 있었다).
  레시피는 저장하지 않고 `Tick` 이 다시 고르며 `craftTime` 으로 잘라 준다 — 그래서 레시피 선택 지점에서도
  `progress` 를 0 으로 밀면 안 된다.

### 아이템 · 딕셔너리
- `Items.itemName` = 세이브 키. **반드시 영어(snake_case)**, `displayName` = **반드시 한글**. 예외 없다.
- **배치 가능한 아이템은 `blockId == itemName == blockName` 이 강제된다** → 아이템 이름을 바꾸면
  블록 이름도 같이 바꿔야 한다(파이프는 `파이프 에셋 설정` 이 `item.itemName` 을 복사해 간다).
  그래서 `GetBlock` 도 `GetItem` 과 **같은 별칭 표**를 폴백으로 본다 — 없으면 이미 놓인 배치물이 통째로 사라진다.
- **한글 이름은 NFC 로 정규화**(`ItemDictionary.NormalizeName`). 에디터 툴에서 한글 이름을 **타이핑하지 말고
  에셋에서 복사**할 것 — NFC/NFD 가 겉보기엔 같아도 딕셔너리 조회가 조용히 실패한다.
- 조회: `GetItem(id)` `GetItemByDisplayName` `FindItem`(둘 다 시도) `AllItems` / `GetBlock` `GetMachineInfo` `GetPipeInfo`
  / 역인덱스 `GetTerrainBlockFor(item)` `GetPipeBlockFor(item)`.
- 에셋을 새로 만들면 **`Tools/Project Craft/Dictionary/Register All Assets`** 를 돌려야 씬 딕셔너리에 등록된다
  (삭제된 에셋이 남긴 빈 칸도 이때 걷어낸다).
- **`MachineAliases`**(에디터) = 옛 기계 이름 → 정본 **표시 이름**. `RecipeTreeMerger` · `MachineBlockFiller` ·
  `RecipeJsonImporter` 가 **같은 표 하나를 본다**(예전엔 세 벌로 갈라져 실제로 어긋났다).
- **`ItemAliases.Resolve` 는 한 단계만 푼다.** 그래서 플레이스홀더를 기계로 승격시킬 때
  `한글 → 옛영문 → Machine:*` 처럼 사슬을 만들면 안 되고, **한글 줄도 최종 이름을 직접 가리켜야** 한다.
- **기계의 `dropItem` 은 비워 둔다.** 기계는 `MapGenerator.DropSelf(record.blockId)` 로 자기 자신을 떨어뜨린다
  (`blockId == itemName` 규약). `dropItem` 은 지형·파이프 전용이다.
- **`ItemAliases`** = 통합돼 사라진 옛 이름 → 정본 `itemName`. **한 표를 세 곳이 함께 본다**:
  `ItemDictionary.GetItem` 폴백(옛 세이브 호환) · `RecipeJsonImporter.ResolveItem`(재임포트 내성) · `ItemMerger`(참조 재작성).
  `itemName` 이 세이브 키라 **이 폴백이 아이템을 지워도 세이브가 안 깨지게 하는 유일한 안전망**이다.
- 중복 정리 흐름: `아이템 중복 조사`(리포트만) → `ItemAliases` 표에 줄 추가 → `중복 아이템 통합` → 다시 조사해 0 확인.

### 기계 · 레시피

#### ⚠ 티어는 두 축이고, **기계에는 티어가 없다**
`tier` 라는 같은 이름이 서로 다른 두 가지를 가리킨다. 섞으면 없는 충돌을 만든다(실제로 그랬다).

| 축 | 누가 비교하나 | 뜻 |
|---|---|---|
| **해금** | 건설 레시피의 `Recipe.tier` vs `CoreCrafter.tier` (`RecipeDictionary.cs:79,114`) | 코어가 이 티어 이상이어야 조합 목록에 뜬다 |
| **처리** | `MachineBlock.tier` vs 가공 레시피의 `Recipe.tier` (`MachineInstance.cs:633`) | 이 기계가 어느 레시피까지 돌리나 |

- **`MachineBlock.tier` 는 "기계의 티어" 가 아니라 처리 범위다** (`MachineBlock.cs:22` 주석이 정본).
- **`n티어 기계` 라는 것은 없다** — `n티어에서 만들 수 있다` 만 있다. 그래서
  **같은 기계가 n티어에도 m티어에도 있으면 안 된다.** 업그레이드는 `화로 → 전기로 → 고전압 전기로`
  처럼 **이름이 다른 별개 기계**로 표현한다. (이 규칙 위반이라 `2티어 합금 재련기` 와 `정유기` 를 지웠다 —
  용광로는 `합금 재련기 ×1` 을 먹고, 원유 처리는 **증류기** 하나가 한다)
- ⚠ **`Recipe.tier` 한 필드가 두 뜻을 겸한다** — 건설 레시피에선 해금, 가공 레시피에선 처리 요구.
  지금은 가공 레시피가 조합대 목록에 안 떠서 부딪히지 않는다.
- **제련 규칙**: 화로는 **티어와 무관하게 모든 광석을 재련한다. 티타늄만 용광로.**
  (2026-08-07 사용자 결정. `smelt_*` 의 `Recipe.tier` 가 이 규칙과 어긋나 있다 — `TODO.md` §F)

- **레시피 에셋 이름은 만들어지는 것의 이름 하나뿐이다** — `craft_` · `build_` 접두사도, `_2` 꼬리도 붙이지 않는다
  (`hammer` / `craft_hammer` 처럼 갈라져 양쪽 다 반쯤 고장 나 있었다). 기계 건설 레시피는 블록 이름을 따른다
  (`extractor00plus` `extractor01` `extractor02` `extractor03`).
  ⚠ **이름을 바꾸면 `StreamingAssets/Recipes/*.json` 의 `id` 도 같이 바꿔야 한다** —
  `RecipeJsonImporter` 가 에셋 경로를 `Sanitize(id) + ".asset"` 으로 만들기 때문에, 안 맞추면
  `Import JSON Recipes` 가 **옛 이름으로 통째로 되살린다.** 지운 레시피는 JSON 에서도 지워야 같은 이유로 안 살아난다.
- **도구 조립 레시피의 정본은 `ToolRecipe`**(`Prefabs/Recipes/Tools/` 의 `hammer` `driver` `pickaxe`) 다.
  재료가 부품 **종류**라 `inputs` 가 비어 있는 것이 정상 — 조합대가 부품 칸을 따로 띄운다.
  재질을 고정한 옛 조립 레시피 7개는 `ToolAssetGenerator` 가 산출 참조를 끊어 놓은 껍데기였고 **지웠다.**
  부품 레시피(`stick` `hammer_head` `stone_hammerhead` `iron_hammerhead`)는 살아 있는 정상 레시피다.
- `MachineBlock`(SO) → `MachineInstance`(런타임) + `MachineInventory`(input/output/fuel).
- `MachineInstance.ApplyConfig` 조건에 `|| info.fuelSlotCount > 0` 이 있다 — 빼면 발전기가 3/6 으로 폴백한다.
- ⚠ **`SelectRecipe` 는 "지금 만들 수 있는 첫 레시피" 를 고른다** — 우선순위도 플레이어 선택도 없다.
  **같은 재료를 받는 레시피를 한 기계에 둘 이상 두면 목록에서 앞선 것만 영원히 돈다**(실제로 그래서
  분쇄기가 `돌 → 돌` 만 반복했다). 재료가 겹치는 레시피는 서로 다른 기계나 티어로 갈라 둘 것.
- **수동 기계**는 `MachineBlock.manualStepRatio`(0 이면 자동, 0.05 면 20클릭에 1개)로 표현한다.
  `MachineInstance.ManualStep` 이 **`Tick` 을 그대로 재사용**하므로 재료·출력자리·연료·전력 판정이 한 곳에 남는다.
  `AutoProcess=false`(조합대)와는 다르다 — 조합대는 자기 슬롯을 안 쓰고 플레이어 인벤토리로 만든다.
  **수동 기계에 `runningSprite` 를 주지 말 것**: `Update` 가 매 프레임 `SetRunning(false)` 라 한 프레임만 보인다.
- **새 기계를 늘리는 데 에디터 툴은 필요 없다.** 기존 `MachineBlock` 을 복제해
  `recipeGroupId`(레시피 목록 공유) · `tier`(처리 범위) · `uiPrefab`(UI 공유) 세 필드만 맞추고,
  `itemName == blockName` 인 `Items` 를 함께 만든 뒤 `Register All Assets` 를 돌리면 된다.
  화로 3종 · 조합대 2종 · 추출기 12종이 전부 이 방식으로 붙어 있다 —
  **그래서 계열 생성 툴(`ExtractorSetup` 등)은 만들지 않는다.** 표와 에셋이 갈라져 값이 되돌아갈 뿐이다.
- `RecipeSolver` 가 재료 확인·소모·적재를 전담: `CanCraft` `ConsumeInputs` `CanStoreOutputs` `AddItems`
  `CountFreeSpace`(넣어 보지 않고 여유 세기) `CountItem`.
- **`RecipeSolver.AddItems` 는 통지하지 않는다.** 외부에서 슬롯을 건드렸으면
  `inventory.NotifyChanged()` + `instance.Flush()` 를 직접 불러야 UI 갱신·재가공이 걸린다.
- `Recipe.tier` = 조합대 티어 요구. `MachineBlock.recipeGroupId` 로 0/1/2티어 화로가 같은 목록을 공유.
- **가동 중 그림**: `MachineBlock.runningSprite` 가 **비어 있으면 그림을 바꾸지 않는다**(47대 중 45대가 그렇다).
  **정지 그림의 정본은 SO 가 아니라 `machinePrefab` 의 `SpriteRenderer`** — `MachineInstance.Bind` 가
  배치 시점에 기억했다가 되돌린다. 두 곳에 두면 언젠가 어긋난다.
  교체는 `SetRunning` 한 곳에서만 하고 **상태가 바뀔 때만** 대입한다(매 프레임 대입하면 배칭이 깨진다).
  가동 판정 = **그 프레임에 실제로 진행됐는가**. 재료가 있어도 연료·전력·출력자리가 없으면 정지고,
  발전기는 **연료를 실제로 태운 프레임**만 가동이라 버퍼가 차면 정지 그림이 된다.

### 추출 체계 (정본 = `자원과 그 가공방식.canvas`, Obsidian Vault 에 있다)

메인자원을 **분쇄기로 1/2/3회 분쇄** → 그 분쇄물을 **추출기**에 넣어 부산물을 확률로 얻는다.
0티어 = **돌** · 1티어 = **마력석** · 2티어 = **운석**. 금속 산출은 `raw_*_ore`(조각), 재련하면 `*_ingot`.

**분쇄 사슬 세 줄.** 분쇄기들은 `recipeGroupId = "Pulverizer"` 로 목록을 공유하므로,
새 분쇄기는 그 값만 주면 사슬이 그대로 붙는다. 사슬 레시피의 `tier` 가 계열 번호다
(그래서 **tier 1 인 전기 분쇄기는 운석 사슬을 못 돌린다** — 2티어 분쇄기가 아직 없다).
분쇄기는 **`Machine:ManualPulverizer`(수동 분쇄기, tier 0, `manualStepRatio 0.05`, 무전력)** 와
**`Machine:ElectricPulverizer`(전기 분쇄기, tier 1)** 둘이다 — 추출기의 `Extractor00`/`Extractor00Plus` 와 같은 꼴.
수동판은 `돌 10 + 크랭크 1`(크랭크가 `돌 ×2`)로 만들 수 있어 **0티어 부트스트랩이 여기서 풀린다.**

| 계열 | 메인자원 | 1회 | 2회 | 3회 |
|---|---|---|---|---|
| 0 | `stone` | `gravel` 자갈 | `sand` 모래 | `stone_powder` 돌 가루 |
| 1 | `manastone` | `manastone_shard` 마력석 조각 | `manastone_dust` 마력석 가루 | `manastone_fine_dust` 미세한 마력석 가루 |
| 2 | `meteorite` | `meteorite_shard` 운석 조각 | `meteorite_dust` 운석 가루 | `meteorite_fine_dust` 운석 미세 가루 |

조각난·부숴진·바스라진 돌덩이는 0계열에 흡수돼 사라졌다(`ItemAliases` 가 분쇄 횟수가 같은 것으로 잇는다).
1계열은 원래 `파쇄 광석 → 광석 알갱이 → 반짝이는 가루` 였는데, **어느 메인자원의 분쇄물인지가 이름에 없어서**
`마력석 조각/가루/미세한 가루` 로 통일했다(운석 사슬과 같은 꼴). 옛 이름은 `ItemAliases` 가 잇는다.
⚠ 사슬 레시피 파일 이름(`crush_ore` · `crush_crushed_ore` · `crush_ore_grains` ·
`extract_crushed_ore` · `extract_ore_grain` · `extract_shiny_powder`)은 **아직 옛 이름 그대로다.**
운석 사슬은 원래 `에너지 결정 → 마력 결정 → 마법 가루` 였는데, **그 둘이 2계열 추출 산출(6%)이라
분쇄가 100% 로 주면 추출기가 쓸모없어져** 중립적인 이름으로 갈아 끼웠다. `magic_powder`(마법 가루)는
그래서 **지금 아무 레시피도 안 쓴다** — 아이템은 남겨 뒀으니 쓸 곳이 정해지면 붙이면 된다.

**추출 산출은 반드시 "가공 전" 형태여야 한다.** 완제품을 바로 주면 그 뒤 기계가 쓸모없어지기 때문이다.
그래서 0계열 상위 등급 둘은 아래처럼 한 단계를 더 거친다 — **표(`ExtractionTable`)와 레시피가 짝이다.**

| 등급 | 추출 산출 | 그다음 |
|---|---|---|
| 0-2 | `sulfur_ore` 유황석 5% | 화학 처리기 `chem_sulfur_powder` → `황 가루` → `chem_acid` → `산성 용액` |
| 0-3 | `turbid_uranium` 탁한 우라늄 1% | 화학 처리기 `chem_uranium`(+산성 용액 ×2) → `우라늄 조각` → 전기로 `smelt_uranium` → `우라늄 주괴` → 압축기 `uranium_concentrate`(×10) → `우라늄 농축물` |

`turbid_uranium` 은 옛 `unrefined_uranium`(미가공 우라늄)을 개명한 것이다 — 그쪽이 획득처가 없어 죽어 있었다.
`uranium_powder`(우라늄 가루)는 `Pulverize_RawUraniumOre` 산출로 남아 있지만 **소비처가 아직 없다.**

- 기계 이름은 **`{메인티어}-{등급}티어 추출기`** 12종(`Machine:Extractor00`~`23`) + `Extractor00Plus`.
- **등급차를 레시피 복제로 표현하지 않는다.** 같은 계열은 `recipeGroupId`(`Extractor0/1/2`)로 목록을 공유하고,
  레시피는 **(계열 × 분쇄단계) 당 한 개, 총 9개**다. 산출물마다 레시피를 쪼개면 자갈을 받는 레시피가
  7개가 되어 `SelectRecipe` 가 **첫 번째만 영원히 돌린다**.

#### 확률 산출 — 표가 정본이다
- 레시피는 `chanceOutputs`(`ChanceOutput`)에 **그 레시피에서의 가장 낮은 확률**만 갖는다. **등급을 적지 않는다.**
- **어느 기계가 무엇을 얼마나 얻는지는 `ExtractionTable`(static) 한 곳**이 정한다.
  표에는 **"그 등급에서 처음 열리는 산출물"만** 적고 상위 등급이 하위 줄을 물려받는다
  (= 캔버스의 "N티어 추출기는 0~N-1 의 결과물을 모두 가짐"). 표에 없으면 배수 0 = **못 얻는다**.
  배수 예외는 따로 적는다(지금은 `전도체 결정 @ 1-1 이상 = ×2` 하나).
- 최종 확률 = `chance × ExtractionTable.Multiplier × chanceMultiplier`, **항목마다 독립 굴림**
  (한 번에 여러 개가 나올 수도, 아무것도 안 나올 수도 있다).
- **출력이 9칸인 이유**: `RecipeSolver.CanStoreOutputs` 가 **나올 수 있는 산출물 전부**의 자리를 확인해야
  진행한다(한 레시피의 후보가 최대 7종). 굴린 뒤 자리가 없어 버리는 일도, 자리가 날 때까지 다시 굴리는
  편법도 이래야 없다. 대신 **출력이 차면 재료를 먹지 않고 그냥 멈춘다.**
- `speedMultiplier` 는 **`MachineInstance.EffectiveCraftTime` 한 곳에서만** 나눈다
  (진행 비교·진행률·수동 한 걸음이 다 이걸 본다). 수동 클릭 수는 배수와 무관하다.
- ⚠ `DictionaryRegistrar.HasAnyOutput` 은 **확률 산출도 산출로 친다.** 여기서 빠뜨리면 확률 전용 레시피가
  "재료만 먹는 위험한 레시피"로 걸러져 딕셔너리에 등록되지 않고, 기계가 영원히 논다(실제로 한 번 걸렸다).
- **전부 입력 1 / 출력 9 로 통일**돼 있고 UI 도 `Prefabs/ui/Machines/Extractor01_UI.prefab` **한 장을 공유**한다
  (`uiPrefab`). 등급이 올라도 산출 종류만 늘 뿐 칸 수는 그대로라, 등급마다 UI 를 만들 이유가 없다.
- **0-0티어만 두 갈래다** — `Extractor00`(수동, "수동 0-0티어 추출기") · `Extractor00Plus`(전기 자동, "0-0티어 추출기").
  수동은 `MachineBlock.manualStepRatio = 0.05`(20클릭에 1개)이고 UI 는 전력바 대신 작동 버튼을 둔
  `Extractor00_UI.prefab` 을 혼자 쓴다. 나머지 12종은 전기 전용 + 공유 UI.
- **지형에 설치해 메인자원·유체를 뽑는 것은 추출기가 아니다** — `자원 생성기`·`펌프`·`지열 발전기` 쪽이다
  (`extraction.json` 의 `terrain` 필드가 그 표시). 입력 0 / 출력 1 로 둬야 3/6 폴백에 안 걸린다.
  **위의 9칸·공유 UI 는 여기엔 적용하지 않는다.**
- ⚠ 생성 툴 `ExtractorSetup`(`추출기 계열 설정`) 은 **삭제했다.** 12종이 이미 다 있는데 `Spec` 표가
  `outputSlotCount` 를 조건 없이 덮어써, 손으로 맞춘 값을 되돌리는 함정이었다(실제로 표는 1, 에셋은 9였다).
  이제 **에셋이 유일한 정본**이고 설계 정본은 `자원과 그 가공방식.canvas` 다.

### 전력
- `MachineBlock`: `isGenerator` `powerRange` `energyUseRate`. 발전 = 연료 연소.
- `MachineInstance`: `TickGenerator` → `Distribute()`(라운드로빈, 꽉 찬 곳 건너뜀, 죽은 링크 정리) → `ConsumeEnergy`.
- `PowerLinkMode` = 전체화면 전송 설정 모드(빨강 미연결 / 초록 연결 / 파랑 발전기). 오버레이 타일맵을 런타임 생성.
- 값 채우기: `Tools/Project Craft/Machines/전력 기본값 채우기`.

### 파이프 (아이템만 실제 운반. 유체·기체는 배치·오토타일·세이브까지만)
- `PipeBlock : BlockBase` — **`MainBlock` 을 상속하면 안 된다**(지형 배치 경로로 새서 조용히 실패).
  `kind` `tier` `secondsPerCell` `throughput` `atlas` `tint`.
- `PipeNetworkManager` 하나가 **로드된 파이프 전부를 대신 그리고 대신 돌본다**(칸마다 MonoBehaviour 금지).
  `MapGenerator.Start()` 가 런타임 생성 → 씬 파일을 건드리지 않는다.
- 연결 마스크 N=1 E=2 S=4 W=8. **저장하지 않고 매번 계산**(`PipeRouter.ConnectionMask`).
- 경로는 **다익스트라**(칸마다 `secondsPerCell` 이 달라 가중치 그래프). `FindSinks` 는 도착 후보 **전부**를
  시간 순으로 캐시하고, `routeVersion != TopologyVersion` 일 때만 다시 찾는다.
- **면 상태를 바꾸면 반드시 `MarkTopologyDirty`.** 안 하면 "설정은 바뀌었는데 물건은 옛길로 간다".
- `PipeRouter.TargetSlots` 가 **레시피를 근거로** 받을 슬롯을 고른다 — 없으면 화로가 자기 산출물을 도로 먹는다.
- 짐(`ParcelRecord`)은 출발 파이프의 레코드에 실리고 **남은 시간(초)** 으로 저장한다. 도착지가 없으면
  **필드에 쏟지 않고 들고 기다린다**. 회수는 파이프를 캘 때만.
- 값 채우기: `Tools/Project Craft/Pipes/파이프 에셋 설정`.

### 렌치 (파이프 연결면)
- `WrenchItem : Items` — 필드 없음. **타입으로 판정**하려고 만든 클래스(문자열 비교 금지).
- 면 상태 `PipeFaceMode` : `Default=0 Insert=1 Extract=2 Cut=3`, 2비트 × 4면 = `PlaceableRecord.faceModes` 1바이트.
  읽고 쓰기는 `PipeRouter.FaceOf` / `SetFace` / `FaceAt` / `Opposite` **만** 쓴다.
- 우클릭: 파이프-파이프 면 → `Cut ↔ Default`(양쪽 레코드에 미러) / 파이프-기계 면 → `Default → Insert → Extract → Default`.
  기계 칸의 그쪽 절반을 눌러도 통한다. 면 선택은 `PlayerInteraction.NearestFace`(사각지대 없음).
- **끊김 판정은 양쪽 레코드를 다 본다.** 파이프를 캘 때 `MapGenerator.ClearMirroredCuts` 로 이웃 표시를 지워
  "끊김은 살아 있는 파이프 두 칸 사이에만 있다" 를 유지한다.
- 표시: `PipeFaceOverlay` 가 `SpriteRenderer` 풀로 파랑/빨강 막대(한 칸에 두 면이 칠해질 수 있어 타일맵 불가).
  **인접 기계가 실제로 있을 때만 그린다**(설정 자체는 기계를 캐도 남긴다).

### 도구 (커스텀 조합)
`ToolDefinition`(부품 칸 = 그림 레이어) + `ToolPartItem`(재질×종류) + `ToolItem`(완성품, maxStack 1)
+ 스택마다 붙는 `ToolInstance`(재질·내구도). 레시피는 `requiredTools` 로 요구하고 **소모가 아니라 내구도 차감**.
생성: `Tools/Project Craft/Tool/Generate Tool Assets`.

### UI
- `UIManager` 가 이름으로 패널을 켜고 끈다(`AddUI` → `OpenUI`/`CloseUI`, `isAnyUIOpen`).
  **`AddUI` 를 열 때마다 다시 부른다** — 등록이 빠지면 `OpenUI` 가 조용히 실패해 영구히 못 연다.
- 런타임 UI 구성이 규약(`CommandConsole` `PowerLinkMode` `ItemBrowser`) — 씬 파일을 건드리지 않기 위해.
- **기계 UI 프리팹은 여러 기계가 나눠 쓴다** — `DefaultMachineUI` 가 자식의 `MachineUIElement` 를 역할별로
  긁어모아 **남는 칸은 끄고 모자라면 경고 후 클램프**하므로, N칸짜리 한 장이 N칸 이하 전부를 감당한다
  (전력바도 `isUseEnergy` 가 아니면 자동으로 꺼진다). 실제 공유: `Furnace_UI` 3종 · `Generator_UI` 2종 ·
  `CraftingTable_UI` 2종 · `Extractor01_UI` 12종. 프리팹 이름이 사용자 중 하나만 가리키는 것은 정상이다
  (`MachineUIFactoryWindow` 가 `{기계 이름}_UI.prefab` 로 저장하므로 **이름을 바꾸면 다음 저장 때 파일이 하나 더 생긴다**).
- ⚠ `MachineUIHost.Resolve` 의 캐시 키는 프리팹이 아니라 **`blockId`** 다 — 한 프리팹을 12종이 공유해도
  인스턴스는 12개 생긴다(열어 볼 때만 지연 생성).
- 버튼을 붙이는 방법은 셋이다 — ① **코드 생성**(`BuildPowerLinkButton`. 공유 기본 패널에 프리팹을 못 고칠 때) ·
  ② **서브클래스 + `SerializeField`**(`CraftingTableUI.craftButton`) · ③ **`MachineUIElement` 역할**
  (`ManualButton`. 프리팹에서 위치를 잡고 싶을 때). 어느 쪽이든 **`Open` 마다 `onClick.RemoveAllListeners()`** —
  안 하면 기계를 열 때마다 리스너가 쌓여 한 번 눌렀는데 예전에 열었던 기계까지 함께 돈다.
- **비활성 오브젝트는 레이아웃 재계산이 통째로 무시된다.** 켠 다음에 짓고 `LayoutRebuilder.ForceRebuildLayoutImmediate`.
- 툴팁은 `TooltipUI.Show(Func<string>)` 로 넘겨야 실시간 갱신된다(문자열을 넘기면 고정).
- 아이콘은 `ItemIconView.Apply` — 도구는 자루+머리를 겹쳐 그린다.
- 한글 폰트: `Assets/TextMesh Pro/Fonts/Maplestory Bold SDF.asset`, `Tools/Project Craft/Font/Apply Korean Font To All`.

### 입력 (`InputActionManager`, 코드로 만든 액션맵)
`WASD` 이동 · 좌클릭 채굴(홀드) · 우클릭 Use · `E` 상호작용 · `I` 인벤토리 · `1~0` 핫바 ·
`Enter` 콘솔 · `P` 아이템 목록. 텍스트 입력 중엔 `SetPlayerInputEnabled(false)`.
**입력을 끄는 UI 는 자기 토글 키로 못 닫는다** — 콘솔은 ESC, 아이템 목록은 입력을 끄지 않는다.

### 애니메이션 (플레이어 · `Assets/Asset/Player/Female/`)
- **이동·상태 전이에 `Any State` 를 쓰지 않는다.** Any State 전환에는 `Has Exit Time` 이 **아예 없어**
  "클립이 끝날 때까지 기다려"를 표현할 수 없고, `Can Transition To Self` 와 겹치면 조건이 참인 동안
  같은 상태로 계속 재진입해 **클립이 앞부분만 맴돈다**(눈 깜빡임이 재생되지 않던 원인).
  Any State 는 피격·사망처럼 **어디서든 끼어드는 것** 전용이다.
- 방향은 **블렌드 트리 한 상태**로 둔다 — 방향을 바꿔도 같은 상태 안에서 섞이므로 클립이 재시작되지 않고,
  대각선도 조건 분기 없이 처리된다.

```
Idle(FemaleNormal) ──[blink]──▶ Blink ──[Exit Time 1.0]──▶ Idle
   │  ▲                                  (Blink 클립은 루프 금지)
   └──[moving]──▶ Walk = BlendTree(SimpleDirectional2D, moveX·moveY)
                    Leftward(-1,0) Rightward(1,0) Forward(0,1) Backward(0,-1)
```

- **파라미터 이름은 `PlayerAnimation.cs` 와 `Female.controller` 양쪽이 정본.** 한쪽만 바꾸면
  `Animator` 가 경고 없이 조용히 아무 일도 하지 않는다. 현재: `moveX` `moveY`(Float) `moving`(Bool) `blink`(Trigger).
- **블렌드 트리는 Float 만 받는다.** 예전엔 Int `x`/`y` 를 `(int)` 로 잘라 넣었는데, `2DVector` 컴포짓이
  대각선을 정규화해 `0.707` 을 주므로 **잘리면 0** 이 되어 대각선으로 걸을 때 idle 이 나왔다.
- 부호 규약: `moveY = -input.y` — **S(아래)=정면(Forward), W(위)=뒷모습(Backward)**.
- 스프라이트 애니메이션이라 크로스페이드가 의미 없어 **전이 duration 은 전부 0**.
- ⚠ `FemaleWalkBackward` 만 1.1833초로 나머지 셋(0.5167초)과 다르다. 블렌드 트리는 자식을 정규화 시간으로
  함께 재생하므로 섞이는 동안 속도가 어긋나 보인다 — 신경 쓰이면 길이를 맞춘다.
- ⚠ `FemaleBlink.anim` 은 **빈 클립**이다(프레임 미작성). 컨트롤러 배선만 되어 있다.

### 카메라 · 픽셀
- 전 스프라이트가 **PPU 32 · Point 필터 · 압축 없음**으로 통일돼 있다. **새 아트도 반드시 맞춘다** —
  하나만 어긋나도 그 오브젝트만 흐려진다.
- **물리로 움직이는 오브젝트는 `Rigidbody2D.Interpolate` 를 켠다.** `FixedUpdate`(50Hz)와 화면 주사율이
  어긋나 생기는 저더는 눈에 "흐리다"로 보인다. 플레이어는 `MovePosition` 을 쓰므로 특히 필요하다.
- 카메라는 `orthographic size 4`(세로 8유닛 = 원화 256px) · `UIs` 캔버스는 **`Screen Space - Overlay`**.

#### ⚠ 픽셀 퍼펙트를 다시 시도한다면 (한 번 넣었다가 되돌린 이력 있음)
`PixelPerfectCamera` 를 달면 배율은 정수로 깔끔해지지만 **화면 구성이 통째로 흔들린다.** 실측한 것:
- 배율을 정수로 맞추는 순간 **보이는 범위가 해상도마다 달라진다.** 원래 범위(256px)를 고정하려면
  Crop Frame(레터박스)이 필요하고, 그러면 검은 띠가 생긴다. **"정확한 범위 + 정수 배율 + 여백 없음" 은
  동시에 못 가진다** — 256 이 나누어떨어지는 화면 세로는 1024·1280·1536·2048뿐이라 1080·1440·2160 어디에도 없다.
- 레터박스를 켜면 **Overlay 캔버스가 카메라 뷰포트를 무시**해 UI 가 검은 띠 위로 밀려난다.
  `Screen Space - Camera` 로 바꾸면 캔버스 크기(레퍼런스 단위)가 달라져 **UI 배치가 또 어긋난다** — 실제로 그랬다.
- 함께 필요한 것: `CinemachineCamera` 에 `CinemachinePixelPerfect` 확장(없으면 둘이 `orthographicSize` 를
  서로 덮어써 떨린다) · 캔버스 `sortingOrder` 를 스프라이트(0~6)보다 위로 · `ItemSlot.OnDrag` 의
  화면→월드 좌표 변환(Overlay 에서만 둘이 같다).

  → **UI 레이아웃을 먼저 해상도 독립적으로 정리한 뒤에** 손대는 것이 맞다.

### 정렬 순서 (한 레이어, sortingOrder)
`0` Blocks/Floor · `1` FloorTexture · `2` 기계·파이프 · `3` 플레이어 · `3+i` 드랍 ·
`4` 벽 윗면 · `5` 아웃라인·파이프 면 막대 · `6` PowerLink 오버레이

## 5. 에디터 메뉴 (전부 재실행 안전, 대화상자 없음)

```
Tools/Project Craft/Dictionary/Register All Assets      ← 에셋 만들면 이거 먼저
Tools/Project Craft/Dictionary/아이템 중복 조사 · 중복 아이템 통합
Tools/Project Craft/Machines/전력 기본값 채우기
Tools/Project Craft/Machines/Fill Missing Machine Blocks
Tools/Project Craft/Pipes/파이프 에셋 설정
Tools/Project Craft/Tool/Generate Tool Assets · 렌치 에셋 설정
Tools/Project Craft/Recipes/Import JSON Recipes · Assign Recipe Categories · Merge ...
Tools/Project Craft/Font/Apply Korean Font To All
Tools/Tiles/Build Tile Atlas · Slice Pipe Sheet · Build Pipe Atlas   (슬라이스 → 빌드 순서)
```

새 에디터 툴을 쓸 때도 이 규약을 따른다: **이미 있는 에셋은 값만 갱신**(손으로 다듬은 값 보존),
`Debug.Log` 로 마크다운 보고서, 끝에 `Register All Assets` 실행.

## 6. 코딩 규약

- SO 는 **한 파일 한 클래스** (아니면 `m_Script: {fileID: 0}` 로 에셋이 안 열린다).
- 파일명 = 클래스명 유지(에셋의 `m_Script` 참조).
- 주석은 한국어로, **"무엇"이 아니라 "왜"** 를 적는다. 함정은 `<b>`로 강조하고 어기면 무슨 일이 나는지 쓴다.
- 파생 상태는 저장하지 않고 매번 계산한다(연결 마스크 등).
- `Tile.transform` 회전을 쓰면 `TileFlags.LockTransform` 필수(기본은 `LockColor` 뿐).

## 7. 알려진 잠복 버그 · 미구현 (별건, 손대기 전 확인)

- ⚠ **`Import JSON Recipes` 는 의도적으로 지운 것을 되살린다.** JSON 에 있는데 에셋이 없으면 무조건 만들기
  때문에, `Merge Duplicate Recipes` 가 흡수해 지운 레시피와 `Generate Tool Assets` 가 치운 플레이스홀더가
  통째로 부활한다(실측: 레시피 61개 + `막대`). **함부로 돌리지 말 것.** 돌렸으면 `git status` 로 확인하고
  `git clean` 으로 되돌린다. 근본 해결은 "일부러 지웠음" 장부가 필요하다.
- `Recipe.importNote` 에 **원문 JSON 이 통째로** 들어 있다. 참조를 문자열로 세면 모든 아이템이 "쓰임"으로
  잡히므로, 참조는 **객체로** 세야 한다(`ItemAudit` 이 그렇게 한다).
- `WorldMap.Save` 는 `OnBeforeSave → FlushAll` 로 **살아 있는 기계 인벤토리를 레코드에 덮어쓴다.**
  레코드를 손으로 고쳐 저장 테스트를 하면 그 편집이 지워진다 — 옛 레코드 테스트는 `MachineInstance.LoadFrom` 을
  직접 불러야 한다.

- `StreamingAssets/DefaultWorldmap.dat` 은 매직 도입 이전 포맷이라 매번 로드 실패 → 이제 `.corrupt` 로 치워지고 새 월드 생성.
- 유체·기체 운반 미구현(`GasDefine` 에셋이 0개). 산성/유리 파이프 미구현.
- **2계열 분쇄물은 게임 안에서 못 만든다** — 운석 사슬 레시피가 tier 2 인데 분쇄기는 `ManualPulverizer`(tier 0) ·
  `ElectricPulverizer`(tier 1) 둘뿐이다. 2티어 분쇄기가 생기면 풀린다. 0·1계열은 정상.
- **마력 파편의 최초 획득처가 없다**(있는 것은 증식 `마력파편1 + 구리주괴10 → 2` 뿐). 그래서
  **수동 0-0티어 추출기(`돌10 + 크랭크 + 마력파편`)를 아직 만들 수 없고**, 0티어 마법 전체가 막혀 있다.
  분쇄기 경로(`돌10 + 크랭크`)는 마력 파편을 안 쓰므로 정상 작동한다. → 추가 예정(사용자 결정).
- `uranium_powder`(우라늄 가루)를 쓰는 레시피가 없다 — 0-3 추출 산출이 `turbid_uranium` 으로 바뀌면서
  `Pulverize_RawUraniumOre` 의 산출로만 남았다.
- `magic_powder`(마법 가루)를 **쓰는 레시피가 하나도 없다** — 운석 사슬 재편으로 자리를 잃었다(아이템은 남겨 뒀다).
- `energy_crystal`·`magic_crystal` 은 이제 2계열 추출로만 나온다(마력 결정은 제단·CoreCrafter 경로도 있다).
- `extract_ore`·`extract_meteorite` 는 **`machine` 이 비어 있다** — 1·2티어 자원 생성기가 아직 없어서다.
  (지형 설치형이라 추출기 9종과는 별개다.)
- 지열 발전기는 연료 없이 발전해야 하는데 `IsGenerator` 가 `fuelSlotCount > 0` 을 요구해 **발전을 못 한다**.
- 전력 밸런스: 화력 발전기 20/s vs 전기 화로 100/s.
- 렌치·파이프 아이콘이 전부 `assetPlaceHolder`. 파이프 레시피가 아직 `Recipes/Incomplete` 안에 있음.
- 아이템 목록(P)에 검색창 없음 — 223개라 있으면 좋지만 P 키가 글자로 먹히는 문제를 같이 풀어야 한다.

### 결함 조사에서 확인했으나 **일부러 남겨 둔 것** (동작 규칙부터 정해야 함)
- **완성 프레임의 잉여 시간을 버린다** (`MachineInstance.cs` `progress = 0`, `burnRemaining -= want`).
  프레임 드랍이 잦을수록 가공이 느려지고 연료 효율이 프레임레이트에 따라 달라진다.
- **중계기 전력이 고인다** — `PowerLinkMode` 는 `powerRange > 0 && !isGenerator` 기계를 연결 후보로 칠하지만
  `MachineInstance.Update` 는 `isGenerator` 일 때만 `Distribute()` 를 부른다. 받기만 하고 못 보낸다.
- **`Info == null` 이면 `AutoProcess` 가 true 로 취급**돼 조합대가 버튼 없이 부품을 먹는다(딕셔너리 미등록 시).
- **성능**: `RefreshAllTileTextures` 가 청크 경계마다 로드된 전 영역을 다시 그린다(2-11 수정으로 청크 16→25개라
  더 무거워졌다). `TooltipUI` 가 매 프레임 `ForceRebuildLayoutImmediate` + 문자열 할당.
  `PipeRouter` 가 칸마다 `Singleton.Instance` 의 lock 을 잡는다(탐색 1회에 ~8,700회).
- `ItemDictionary` 의 주 색인은 `NormalizeName` 을 안 거친다(폴백인 `ItemAliases` 는 거친다) — 에셋이 NFD 면 조회 실패.
- `ToolDictionary` 의 네 조회 함수가 실패 시 **로그 없이 null**. `EnsureIndex` 가 `materials` 만 보고 복구 판단.
- ⚠ **에디트 모드에서는 `ItemDictionary`·`RecipeDictionary` 의 색인이 비어 있다**(`Awake` 가 안 돌아서).
  그래서 `GetItem`/`GetMachineInfo` 가 전부 null 이고 `MachineInstance.Bind` 도 `Info == null` 이 된다.
  **기계 동작을 검증하려면 플레이 모드에서** 임시 `GameObject` + `MachineInstance.Bind(new PlaceableRecord(), cell)`
  로 돌린다 — `Bind` 는 `ItemDictionary` 만 보므로 **월드에 아무것도 배치되지 않아 세이브가 안 바뀐다.**
  `Tick` 은 private 이라 리플렉션으로 부른다(수동 기계는 공개 `ManualStep` 이 있다).
