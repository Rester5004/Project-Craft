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
| 에셋의 `m_Script` 를 바꾼 뒤 | ⚠ **이미 로드된 객체의 참조가 죽는다.** 디스크 guid 는 멀쩡한데 `machine == null` 로 읽히고 `ImportAsset(ForceUpdate)` 로도 안 고쳐진다 — `CompilationPipeline.RequestScriptCompilation()` 으로 **도메인 리로드**를 해야 살아난다. 그 전에 `Register All Assets` 를 돌리면 멀쩡한 레시피가 "기계 미지정" 으로 빠진다 |
| 게임뷰 스크린샷 | `manage_camera(action:"screenshot", capture_source:"game_view", output_folder:"Captures")`. **프로젝트 밖 경로 거부.** 비동기라 직전 프레임이 찍힐 수 있으니 `Step()` 후 다시 찍는다. 다 쓰면 `Captures/` 삭제 |
| 월드 스크린샷 | 임시 `Camera` + `RenderTexture` + `Render()` 가 동기라 확실하다 (UI 는 안 나온다) |
| 스프라이트 시트 재슬라이스 | ⚠ **`TextureImporter.spritesheet` 를 쓰면 안 된다.** `SpriteMetaData` 에는 **`spriteID` 필드가 아예 없어서**(name·rect·alignment·pivot·border·customData 뿐) 다시 쓰는 순간 서브 스프라이트의 ID 가 유실되고, Unity 가 이름으로 재연결하다 **충돌한 것만 새 fileID 를 발급**한다 → 그 스프라이트를 가리키던 에셋 참조가 조용히 끊긴다(실측: 55개 중 2개). 반드시 `UnityEditor.U2D.Sprites.SpriteDataProviderFactories` → `ISpriteEditorDataProvider.GetSpriteRects()` 로 **`SpriteRect.rect` 만 고치고 `spriteID` 는 건드리지 않는다**(클래스라 그냥 대입하면 된다). 손대기 전 `.png.meta` 를 백업할 것 — 되돌리면 `ForceUpdate` 재임포트로 참조가 되살아난다 |

### 세이브

**세이브 검증(백업 → 해시 비교 → 복원)은 하지 않는다** — 2026-08-14 사용자 결정.
세이브는 사용자가 직접 관리한다. 검증 중에 사용자가 플레이 모드를 만질 수도 있어
"바이트 동일" 같은 수치는 애초에 신뢰할 수 없고, 그것을 맞추느라 쓰는 시간이 값을 못 한다.

```
C:\Users\c\AppData\LocalLow\DefaultCompany\Project Craft\worldmap.dat
```

여전히 사실이라 알아 둘 것(고쳐야 할 코드 함정이지 절차가 아니다):

- `WorldMap.Load` 는 **예외가 나면 세이브 파일을 옆으로 치운다**(`SafeFile.Quarantine`. 예전엔 `File.Delete` 였다).
- **에디트 모드에서 `WorldMap.Instance` 에 접근하지 말 것.** 싱글톤이 Awake 안 된 오브젝트를 만들어,
  플레이 종료 시 `OnApplicationQuit → Save()` 가 헤더만 쓰고 터져 세이브가 잘린다.
- ⚠ **플레이 중에 스크립트를 저장(재컴파일)하면 그 세션의 저장이 통째로 실패한다**(2026-08-20 실측).
  도메인 리로드가 `bool isLoaded` 는 살려 오지만 `Dictionary` 인 `chunks` 는 못 살려서, `Save` 의
  첫 줄 가드를 통과한 뒤 `chunks.Count` 에서 NRE 가 나고 `SafeFile` 이 그것을 **"기록 실패" 로만**
  알려 준다(`ItemDictionary.EnsureIndex` 가 막는 것과 같은 함정). 지금은 `Save` 가 `chunks == null` 을
  **명시적 에러 로그와 함께** 걸러 낸다 — 원본 파일은 손대지 않으므로 **플레이를 껐다 켜면 복구된다**.
  ⚠ 그 세션에서 지은 것은 사라진다. **월드를 만드는 작업 중에는 스크립트를 저장하지 말 것.**
- 플레이 모드에서 만든 테스트 배치물은 **종료 전에 치운다**(종료 시 자동 저장된다).
  월드를 어지럽히지 않기 위한 정리일 뿐, 개수를 세어 보고할 필요는 없다.

## 3. 폴더 지도

```
Assets/Scripts/
  WorldMap/      WorldMap(청크·세이브) MapGenerator(로드·스폰) TilemapTextureLoader
                 TileAtlas TerrainPalette
                 Underground{Session Palette World LootTable Portal SceneSetup}
                 Pipe{Block은 SO에} Atlas·Cell·Router·NetworkManager·FaceMode·FaceOverlay
                 StorageNetwork(3티어 저장망 — 탐색·전력·버스)
                 Editor/ PipeSpriteSlicer PipeAtlasBuilder TileAtlasBuilder
  ScriptableObjects/  Items MainBlock MachineBlock CraftingTableBlock PipeBlock PipeKind
                 WrenchItem UpgradeModuleItem FluidDefine Recipe ChanceOutput ...
                 Tool/ ToolDefinition ToolItem ToolMaterial ToolPart* ToolRecipe
                 Editor/ PipeSetup PowerSetup FurnaceSetup MachineBlockFiller WrenchSetup
  Machine/       MachineInstance MachineInventory RecipeSolver ExtractionTable CoreUpgradeTable
  Data/          Item(ItemStack) FluidStack ItemInstance ToolInstance
  Player/        PlayerInteraction Inventory PlayerSave StartingInventory(첫 시작 지급) PlayerFootsteps
  ItemDictionary/ ItemDictionary RecipeDictionary ToolDictionary
                 Editor/ DictionaryRegistrar RecipeJsonImporter RecipeTreeMerger ...
  UI/            UIManager CommandConsole ItemBrowser PowerLinkMode TooltipUI
                 MachineInteraction Inventory*  Slot/ ItemSlot ItemIconView BarTooltip
                 UIFactory/ CraftingTableUI MachineUIElement ...
  Audio/         SfxPlayer(단발 효과음 한 곳)   ※ 발소리는 Player/PlayerFootsteps
  InputActionManager.cs   Util/Singleton.cs
Assets/Prefabs/  Blocks/{Machines,Terrain,Pipes} Items/{Placeholder,Resource1,Tools,ToolParts,Machines}
                 Fluids/  (FluidDefine 8종: water lava crude_oil petroleum acid_solution mana hydrogen oxygen)
                 Recipes/{,Tools,Category,Incomplete} Tools/{Definitions,Materials,PartKinds}
Assets/Asset/    BlockImages ItemImages MachineImages Tiles/Atlas assetPlaceHolder.png
                 Sound/  WalkOnStone WalkOnDirt CraftSound (아래 §사운드)
                 Player/Female/  Female.controller + 클립 6개(idle·걷기4·깜빡임)
Assets/Prefabs/Core/GameRig.prefab   ← 지상·지하 두 씬이 공유하는 공용 rig(아래 §4 참고)
Assets/Scenes/MapTest.unity          ← 시작 씬. 지속 싱글톤 8종이 여기에만 산다
Assets/Scenes/UndergroundScene.unity ← GameRig + UndergroundSceneSetup 둘뿐
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
  | 11 | 기계 유체 탱크(입력·출력) + 파이프가 나르는 유체 짐(`ParcelRecord.fluidId`/`amount`) |
  | 12 | 업그레이드 모듈 칸 + 인스턴스별 티어 `tier`(코어 조합기 업그레이드) |
  | 13 | **바닥 위 오버레이**(지하 입구 · 석유/물 웅덩이) — 청크당 희소 사전 |
  | 14 | **지금 타는 연료의 초당 연소량** `burnRate` — 연료가 정하므로 기계 값으로는 못 되살린다 |

  `Chunk.Save` 순서: placeable 루프 안에 slots → burn(잔량·총량·**연소율**) → energy/cursor/links → parcels → faceModes → progress
  → 유체 탱크 2개 → 업그레이드 슬롯 → tier, 루프 뒤 drops → **오버레이**. `Chunk.Load` 는 `if (version >= N)`,
  **참조형은 `else` 로 빈 배열을 넣어야** 이전 세이브에서 NRE 가 안 난다(값형은 기본값이 곧 "없음"이라 불필요).
- ⚠ **`Bind` 에서 `LoadFrom` 이 복원한 값을 다시 0 으로 밀지 말 것.** 전력이 그래서 사라졌고,
  진행도도 같은 자리에서 지워지고 있었다(`progress = 0f` 가 `LoadFrom` 일곱 줄 뒤에 있었다).
  레시피는 저장하지 않고 `Tick` 이 다시 고르며 `craftTime` 으로 잘라 준다 — 그래서 레시피 선택 지점에서도
  `progress` 를 0 으로 밀면 안 된다.

### 지하맵 (새 씬 · 저장되지 않는 인스턴스 방)

탐지기(`dowsing_rod`)로 땅을 우클릭 → **10%** 로 그 자리에 포탈 → `E` 로 지하 씬으로 **넘어간다**.
7×7 빈 방 중앙에 스폰하고, 바깥은 등급이 정한 벽으로 31×31 까지, 그 밖은 못 캐는 암반이다.
**마력 파편·철 주괴·뼈 가루의 최초 획득처**이자 마력석·운석 수급처다.

⚠ **탐지기의 정본 이름은 `dowsing_rod` 하나다.** 예전에는 만들 수 있는 것이 `dowsing_rod_t0`(별개 아이템)인데
`UndergroundPalette.DowsingTierOf` 는 `dowsing_rod` 만 인정해서 **지하에 영영 못 들어갔고, 그것 하나로
게임이 시작조차 되지 않았다**(2026-08-15 에 `ItemAliases` 로 흡수). `dowsing_rod_t1/t2` 자리는
그 표에 이미 있으니 아이템·레시피만 만들면 1·2등급 방이 열린다.

**탐지기 두 종류.** 표 셋(`DowsingTierOf`·`DiscoveryChanceFor`·`OilPoolChanceFor`)이 전부
`UndergroundPalette` 에 있어 새 탐지기가 생겨도 `PlayerInteraction` 은 손대지 않는다.

| | 등급 | 발견 | 석유 웅덩이 | 소모 |
|---|---|---|---|---|
| `dowsing_rod` 다우징 로드 | 0 | 10% | — | 개수 1 (1회용) |
| `cavity_scanner` 공동 탐색기 | 1 | 25% | 발견 중 **20%** | **내구도 1**(총 30) |

⚠ **공동 탐색기의 내구도는 `Items.initialDurability` + `ToolInstance` 재사용이다** — 새 인스턴스 타입을
만들지 않았다. `materialIds` 가 비면 이름이 그대로고 아이콘도 한 장으로 떨어진다.
붙이는 자리는 **`RecipeSolver.AddItems` 하나**(기계 산출·인벤토리 적재·콘솔 지급이 전부 여기를 지난다)이고,
**`maxStack` 은 1 이어야 한다** — `Items.OnValidate` 가 못박는다(`StorageCellItem` 과 같은 이유).
닳아서 0 이 되면 `stack.Clear()` — `WearMiningTool`·`RecipeSolver.ConsumeTools` 와 **같은 규약**이다.

- **월드를 객체로 나누지 않는다.** `WorldMap` 은 그대로 두고 **청크 생성 델리게이트**(`chunkGenerator`)만
  갈아 끼운다(`EnterEphemeralWorld` / `ReturnToPersistentWorld`). 그래서 `MapGenerator`·`PlayerInteraction`·
  `PipeRouter`·`MachineInstance` 의 **호출부가 한 줄도 안 바뀌고** 채굴·드랍·배치·파이프가 지하에서 그대로 돈다.
- **`WorldMap.IsEphemeral` 이 참이면 `Save()` 가 첫 줄에서 되돌아간다.** 자동 저장·종료 저장·일시정지 저장이
  **한 줄로 함께** 막힌다 — 호출부마다 가드를 두면 언젠가 하나가 빠져 지하 청크가 지상 세이브를 덮어쓴다.
  **세이브 포맷은 그대로다(v12).** 지하는 디스크에 닿지 않는다.
- **월드 교체는 씬을 로드하기 *전에*** 한다(`UndergroundSession.Enter`). `WorldMap` 은 씬을 넘어 살아남으므로
  교체가 따라오고, 새 씬 `MapGenerator.Start` 가 곧바로 `UpdateChunks` 하는 것과 순서를 다투지 않는다.
- ⚠ **`EnterEphemeralWorld` 는 들어가기 전에 지상을 `Save()` 한다.** 돌아올 때 그 파일을 다시 읽으므로
  이 저장을 빼면 마지막 자동 저장 이후의 지상 작업이 통째로 사라진다.
- ⚠ **`PlayerSave` 에 지하 가드 두 개가 있다.** `Load` 는 지하에서 통째로 건너뛰고(좌표를 복원하면 방 밖으로
  튕기고, 인벤토리를 복원하면 살아 있는 것을 옛 디스크 내용으로 덮어쓴다), `Save` 는 좌표만
  `UndergroundSession.SurfaceReturnPosition` 으로 바꿔 쓴다(지하에서 끄면 다음 실행이 허공에서 시작한다).
- 정본 표 둘: **`UndergroundPalette`**(등급 → 벽·바닥, 방/채굴 반지름, 탐지기 → 등급, 발견 확률) ·
  **`UndergroundLootTable`**(보상 행). `ExtractionTable` 과 같은 꼴로 static 이다.
  ⚠ **`iron_ingot` 행을 빼면 0티어가 통째로 막힌다** — 양동이 ← 철판 ← 철 주괴 사슬의 유일한 시작점이다.
- **못 캐는 경계벽은 새 분기가 아니라 `dropItem` 이 빈 블록**(`wall:bedrock`)이다 —
  `WorldMap.IsMineable` 이 이미 `dropItem == null` 을 거른다.
- ⚠ **방은 원점 중심이라 청크 네 장에 걸쳐 있다.** 그래서 물·전리품은 `UndergroundWorld` **생성자에서
  한 번에** 정하고 `Generate` 는 나눠 담기만 한다. 청크마다 굴리면 경계에서 규칙이 갈린다.
- **전리품은 칸마다 굴린다** — 후보 칸에서 표를 위에서부터 훑어 처음 맞은 행 하나만 놓는다(한 칸에 한 종류).
  중앙 3×3 과 물 칸은 후보에서 뺀다(스폰과 동시에 주워지면 안 된다).
- **입구·출구는 배치물이 아니라 바닥 위 오버레이다**(세이브 v13, `overlay:hole0/1/2`).
  등급을 실을 자리가 따로 없어 **id 에 넣는다**(`UndergroundPalette.HoleIdFor`/`TierOfHole`).
  판정은 `PlayerInteraction.TryUseHole` 한 곳 — 지하면 올라오고 지상이면 내려간다.
  ⚠ **지상 입구는 들어가는 순간 지워진다** — 남기면 탐지기 하나로 무한히 드나든다.
  세이브에 남는 뜻은 "찾아 두고 나중에 들어가기" 다. **바닥 타일은 애초에 안 건드리므로 되돌릴 것이 없다.**
  ⚠ 옛 `UndergroundPortal` 은 **삭제했다** — 런타임 오브젝트라 저장되지 않았다.
- 물웅덩이는 이제 **`overlay:water` 오버레이**다(바닥은 방 바닥 그대로). 빈 양동이로 퍼낼 수 있고
  **통행은 막지 않는다**. 옛 `floor:water` 타일도 계속 읽히므로 지운 것은 아니다.
- 디버그: 콘솔 **`/underground <등급>`** 으로 바로 내려가고, 인자 없이 다시 치면 올라온다.

### 씬 구성 — `GameRig` 프리팹 (⚠ 규칙 하나가 전부다)

지상·지하 두 씬이 **같은 `Assets/Prefabs/Core/GameRig.prefab` 한 장**을 놓는다. UI 를 고칠 때 한 곳만 고친다.

> **`PersistAcrossScenes == true` 인 싱글톤은 반드시 씬 루트로 남는다. 프리팹 안에 넣으면 안 된다.**
> `DontDestroyOnLoad` 는 루트 오브젝트에만 듣기 때문이다(자식이면 경고만 내고 아무 일도 안 한다).

| | 무엇 |
|---|---|
| **`GameRig` 안**(씬마다 새로) | Map(Grid+타일맵) · MapGenerator · TilemapTextureLoader · TestPlayer · Main Camera · CinemachineCamera · Global Light 2D · UIs · UIManager · EventSystem · TooltipUI · CommandConsole · PowerLinkMode · ItemBrowser |
| **MapTest 루트로만**(씬을 넘어 산다) | ItemDictionary · RecipeDictionary · ToolDictionary · TileAtlasManager · InputAction · PlayerInventory · TrashCan · TestItemGiver |

- **프리팹은 씬 오브젝트를 참조할 수 없다.** rig 안에서 위 8종을 가리켜야 하면 **`Instance` 로 찾는다**
  (`InventoryUI.trashCan` 이 그래서 직렬화 필드에서 빠졌다).
- ⚠ **거꾸로도 성립한다 — 씬을 넘어 사는 오브젝트는 rig 안의 것을 `Start` 에서 한 번 찾아 두면 안 된다.**
  rig 는 씬과 함께 죽는데 그쪽 `Start` 는 다시 불리지 않아 참조가 **유니티 가짜 null** 로 남는다.
  `InventoryToggle`(PlayerInventory 에 붙어 있다)이 `MachineInteraction` 을 그렇게 잡고 있어서,
  **지하를 한 번 다녀오면 i 키가 기계 UI 를 못 닫았다** — 인벤토리만 닫히고 기계 창이 남아
  `isAnyUIOpen` 이 계속 참이라 **이동까지 잠긴다**(상자·아이템 저장소에서 발견됐지만 기계 47종 전부에 해당).
  고치는 방식은 **캐시가 죽었을 때마다 다시 찾는 프로퍼티**다(`PowerLinkMode.machineInteraction` 과 같은 규약).
- ⚠ **`TooltipUI` 는 `PersistAcrossScenes => false` 다.** `Awake` 에서 패널을 캔버스 아래에 짓는데 캔버스는
  씬과 함께 죽는다 — 살려 두면 싱글톤만 남고 패널이 없어 **툴팁이 영영 안 뜬다**(`Awake` 는 다시 안 불린다).
  `UIManager`·`TilemapTextureLoader` 와 같은 규약이다.
- ⚠ **`Singleton.Awake` 는 `_instance == this` 도 지속 정책을 적용해야 한다.** 누군가 `Awake` 보다 먼저
  `Instance` 를 부르면 게터가 `FindFirstObjectByType` 으로 찾아 `_instance` 에 넣어 두는데 게터는
  `DontDestroyOnLoad` 를 걸지 않는다 — 예전에는 그래서 **`InputActionManager` 가 씬과 함께 죽었다**
  (`PlayerInteraction.OnEnable` 이 먼저 부른다).
- **플레이는 MapTest 에서 시작해야 한다.** UndergroundScene 을 직접 Play 하면 딕셔너리·인벤토리가 없다.

### 아이템 · 딕셔너리
- `Items.itemName` = 세이브 키. **반드시 영어(snake_case)**, `displayName` = **반드시 한글**. 예외 없다.
- ⚠ **`Items` 를 상속하는 클래스는 "타입으로 판정할 것이 있을 때만" 만든다.** 지금 있는 것은
  `WrenchItem`(필드 없음 · 타입이 곧 판정) · `UpgradeModuleItem` · `ToolItem` · `ToolPartItem` ·
  `StorageCellItem` 뿐이고, **기계 아이템은 평범한 `Items` 다.**
  옛 `Machine : Items`(`machineType`·`machineLevel`·`voltageLevel`)는 **아무도 읽지 않는 필드 3개뿐**이라
  2026-08-20 에 지웠다 — 그때까지 **클래스로 아이템을 훑는 도구가 그 7종을 조용히 빠뜨리고 있었다**
  (도달성 조사가 실제로 걸렸다). 새 클래스를 만들면 그런 자리를 전부 찾아 고쳐야 한다.
- **배치 가능한 아이템은 `blockId == itemName == blockName` 이 강제된다** → 아이템 이름을 바꾸면
  블록 이름도 같이 바꿔야 한다(파이프는 `파이프 에셋 설정` 이 `item.itemName` 을 복사해 간다).
  그래서 `GetBlock` 도 `GetItem` 과 **같은 별칭 표**를 폴백으로 본다 — 없으면 이미 놓인 배치물이 통째로 사라진다.
- **한글 이름은 NFC 로 정규화**(`ItemDictionary.NormalizeName`). 에디터 툴에서 한글 이름을 **타이핑하지 말고
  에셋에서 복사**할 것 — NFC/NFD 가 겉보기엔 같아도 딕셔너리 조회가 조용히 실패한다.
- 조회: `GetItem(id)` `GetItemByDisplayName` `FindItem`(둘 다 시도) `AllItems` / `GetBlock` `GetMachineInfo` `GetPipeInfo`
  / 역인덱스 `GetTerrainBlockFor(item)` `GetPipeBlockFor(item)`.
- 에셋을 새로 만들면 **`Tools/Project Craft/Dictionary/Register All Assets`** 를 돌려야 씬 딕셔너리에 등록된다
  (삭제된 에셋이 남긴 빈 칸도 이때 걷어낸다).
- ⚠ **플레이 중 스크립트를 재컴파일하면 도메인 리로드로 색인이 통째로 빈다** — `Dictionary` 필드는 새로
  만들어지는데 `Awake` 는 다시 안 불린다. 그러면 `GetItem`·`GetBlock`·`GetFluid` 가 전부 null 이 되어
  **놓여 있던 기계와 탱크 내용이 사라진 것처럼 읽힌다.** `ItemDictionary.EnsureIndex` 가 조회마다
  이것을 복구한다(`RecipeDictionary.GetRecipesFor` · `ToolDictionary.EnsureIndex` 와 같은 규약) —
  **새 색인을 추가하면 `BuildIndexes` 의 Clear 목록과 `EnsureIndex` 의 stale 판정에 함께 넣을 것.**
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
- **`레드스톤`은 없다 — `전도체`다.** `redstone_crystal`/`redstone_powder` 는 그림까지 같은 완전한 중복이라
  `conductor_crystal`/`conductor_powder` 로 흡수했다(2026-08-08). 둘을 잇던 분쇄 레시피는
  `Prefabs/Recipes/conductor_powder.asset`(전도체 결정 1 → 전도체 가루 2) 하나로 남아 있다.

### 기계 · 레시피

#### ⚠ 티어는 **한 축뿐이다** — "코어 조합대 목록에 언제 나타나는가"
`tier` 는 **해금 시점만** 가리킨다. **기계는 자기 그룹의 레시피를 티어와 무관하게 전부 처리한다** —
분쇄기 한 종류가 돌·마력석·운석을 다 빻고, 화로 한 종류가 모든 광석을 재련한다
(2026-08-15 사용자 결정).

| 누가 비교하나 | 뜻 |
|---|---|
| `Recipe.tier` vs 조합대의 `MachineInstance.Tier` (`RecipeDictionary.cs:79,114` ← `CraftingTableUI.cs:236,294`) | 조합대 목록에 뜨는 조건. **이것이 전부다** |

- ⚠ **일반 기계의 가공 경로에서 `tier` 를 비교하면 안 된다.** 예전에는 `MachineInstance.SelectRecipe` 와
  `PipeRouter.TargetSlots`·`TargetTanks` **세 곳**이 `recipe.tier > machine.Tier` 로 걸렀고, 그래서
  **"2티어 분쇄기가 없어 운석을 못 빻는다" 같은 없는 문제**가 생겼다. 셋 다 지웠다 —
  되살리면 파이프 쪽만 남아 *"기계는 돌릴 수 있는데 파이프가 재료를 안 넣는"* 상태가 된다.
- **`MachineBlock.tier` 는 조합대(와 코어 업그레이드)에서만 뜻을 갖는다.** 일반 기계에서는 아무 뜻이 없다.
  `MachineInstance.Tier` 를 읽는 곳은 `CraftingTableUI` 와 `TryUpgradeTier` 뿐이다.
- **기계를 가르는 것은 `tier` 가 아니라 `recipeGroupId` 다.** `blast_steel`·`blast_titanium` 이
  `Machine:BlastFurnace` 그룹에만 있어서 "티타늄·강철만 용광로" 가 티어 게이트 없이도 성립한다.
  ⚠ 그래서 **같은 그룹의 상위 기계는 `speedMultiplier`·`energyUseRate` 로만 구별된다** —
  값을 안 벌려 두면 "만들 이유가 없는 상위 기계" 가 된다(화로 x1 / 전기로 x2·100 per s / 고전압 전기로 x4·300 per s).
- **`n티어 기계` 라는 것은 없다** — `n티어에서 만들 수 있다` 만 있다. 그래서
  **같은 기계가 n티어에도 m티어에도 있으면 안 된다.** 업그레이드는 `화로 → 전기로 → 고전압 전기로`
  처럼 **이름이 다른 별개 기계**로 표현한다. (이 규칙 위반이라 `2티어 합금 재련기` 와 `정유기` 를 지웠다 —
  용광로는 `합금 재련기 ×1` 을 먹고, 원유 처리는 **증류기** 하나가 한다)
- **`압연기`는 없다 — `압축기`다.** 벽돌 공장 → 압연기 → 압축기로 두 번 통합됐다(2026-08-10 사용자 결정).
  가공 레시피 11개가 전부 `Machine:Compressor` 에 있고 재료가 서로 달라 `SelectRecipe` 함정에 안 걸린다.
  압축기는 **입력 2칸**이어야 한다 — `compress_brick`(모래2 + 물1) 때문이고, UI 프리팹도 그래서 옛
  `RollingMill_UI`(입력2 + 업그레이드2)를 `Compressor_UI` 로 물려받았다.
  ⚠ 옛 이름은 `ItemAliases`(`Machine:RollingMill` → `Machine:Compressor`)와 `MachineAliases`(`압연기`·`벽돌 공장` → `압축기`)가 잇는다.
- **가공 레시피의 `tier` 는 뜻이 없다.** 아무도 안 보므로 값이 무엇이든 동작이 같다 —
  다만 읽는 사람이 "이 기계는 못 돌린다" 로 오해하므로 새로 만들 때는 0 으로 둔다
  (`smelt_lead`·`smelt_nickel`·`smelt_tin`·`smelt_uranium` 을 그래서 1 → 0 으로 정리했다).
- **코어 조합기의 해금 티어는 SO 가 아니라 `PlaceableRecord.tier` 에 산다.**
  `MachineInstance.Tier = max(Info.tier, record.tier)` 라 다른 기계 47종은 record 가 0 이어서 지금까지와 같다.
  SO 를 런타임에 고치면 에디터에서 **에셋이 영구히 바뀌고** 코어가 둘일 때 한쪽만 올릴 수도 없다.
  올리는 재료는 static 정본 표 **`CoreUpgradeTable`**(`ExtractionTable` 과 같은 꼴):
  `마법이 부여된 전도체 가루 → 1` · `마력 칩 → 2` · `공명 칩 → 3`.
  `CraftingTableBlock.acceptsTierUpgrade` 를 켠 코어만 받는다.
  ⚠ **승급 재료는 언제나 "한 티어 아래의 재단" 에 둔다** — 초급(t0) → 중급(t1) → **고급(t2)**.
  코어 자신의 목록에 두면 *그 티어가 되어야 그 티어로 올릴 재료를 만들 수 있어* **영원히 잠긴다**
  (노션 3티어 정본이 공명 칩을 "코어 조합기 3티어" 로 적어 놓았는데, 그대로 만들면 그 함정에 걸린다).
  ⚠ `TargetTier` 는 `NormalizeName` 을 거치지 않는 **정확한 `==` 비교**다(`ExtractionTable` 과 비대칭) —
  `itemName` 을 한 글자라도 다르게 적으면 조용히 -1 이 된다.
  ⚠ **코어를 캤다 다시 놓으면 티어가 0 으로 돌아간다**(레코드가 새로 생긴다) — 의도다.
  재료 칸은 `MachineUIRole.UpgradeSlot`, 누르는 버튼은 **`MachineUIRole.CoreUpgradeButton = 12`** 로
  **둘 다 UI 프리팹에 있다**(팩토리 "요소 추가" 에 버튼이 있다). 조합대 프리팹 한 장을 5종이 나눠 쓰지만
  칸은 `upgradeSlotCount`(재단·고급 조합기는 0)로, 버튼은 `acceptsTierUpgrade` 로 자동으로 꺼진다.
- 조합대 5종의 현재 값: `코어 조합기 0`(업그레이드로 3까지) · `고급 조합기 2` ·
  `초급/중급/고급 재단 0/1/2`. **재단 3종은 `CraftingTableBlock` 이고 `recipeGroupId = "Altar"` 로 목록을 공유**한다.
  고급 조합기·재단은 각자 전용 목록을 가지므로 `recipeGroupId` 를 코어와 합치지 않는다.
- ⚠ **마법 레시피는 재단 전용이다 — 코어 조합기에 같은 것을 두지 않는다**(2026-08-15 사용자 결정).
  예전에는 9개가 코어와 재단 양쪽에 있어 재단을 지을 이유가 없었다. 지금은 코어 쪽을 전부 지웠다:
  **초급 재단(t0)** `물` `용암` `마력 파편 증식`(`magic_shard_duplicate`) `마법이 부여된 전도체 가루`
  `0티어 자원 생성기` **`중급 재단`(`altar_intermediate`)** /
  **중급 재단(t1)** `마력 결정` `마력 칩` `마법부여기` `사과 나무 씨앗` `강화 자원 생성기` /
  **고급 재단(t2)** `공명 칩`(코어 3차 승급 재료. 2026-08-17 에 생겨 고급 재단이 전용 레시피를 갖게 됐다).
  ⚠ **재단을 짓는 레시피만 예외적으로 갈린다** — `초급 재단`(`altar_basic`)은 코어 조합기,
  `중급 재단`(`altar_intermediate`)은 **초급 재단**, `고급 재단`(`advanced_altar`)은 고급 조합기다.
  즉 재단 계열은 **자기 아래 단계에서 짓는다**(코어 → 초급 → 중급).
  ⚠ **`마법이 부여된 전도체 가루`(코어 1차 승급 재료)가 초급 재단에만 있으므로 초급 재단이 승급의 관문**이다 —
  코어 t0 에서 `마력 파편 5 + 돌 2` 로 지을 수 있어 잠기지는 않는다(폐쇄 검증 완료).
  ⚠ **새 마법 레시피를 코어에 붙이지 말 것.** `StreamingAssets/Recipes/crafting.json` 의 `station` 필드는
  임포터가 보지 않아 **적어 둬도 코어에 만들어진다** — 재단 레시피는 `notion_magic.json` 에 넣는다.
- **제련 규칙**: 화로는 **티어와 무관하게 모든 광석을 재련한다. 티타늄·강철만 용광로.**
  (2026-08-07 · 2026-08-10 사용자 결정. **2026-08-15 에 처리 티어 게이트를 지워 코드가 이 규칙과 맞았다** —
  전에는 `smelt_lead`·`smelt_nickel`·`smelt_tin`·`smelt_uranium` 이 tier 1 이라 화로가 거부했다)
  `blast_titanium`(티타늄 조각 4 → 티타늄 주괴) · `blast_steel`(**철 주괴 2 + 석탄 1** → 강철)이
  `Machine:BlastFurnace` 목록의 전부다. ⚠ 둘은 원래 `Furnace` 그룹에 있어 화로·전기로가 뽑고 있었고,
  `blast_steel` 은 **입력이 비어 있어 재료 없이 강철이 나왔다.**

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
- `MachineBlock`(SO) → `MachineInstance`(런타임) + `MachineInventory`(input/output/fuel/upgrade).
- `MachineInstance.ApplyConfig` 조건에 `|| info.fuelSlotCount > 0` 이 있다 — 빼면 발전기가 3/6 으로 폴백한다.
- ⚠ **`MachineBlock` 에 필드를 새로 넣으면 기존 에셋 47개는 `0`/`false` 로 읽힌다** — C# 초기값이 아니다.
  YAML 에 그 줄이 없기 때문이라, 새 필드는 **일괄 스크립트로 값을 써 넣어야** 한다
  (`CoreCrafter` 가 `speedMultiplier` 를 그렇게 잃었고, `upgradeSlotCount = 2` 도 같은 이유로 손으로 채웠다).
- ⚠ **`SelectRecipe` 는 "지금 만들 수 있는 첫 레시피" 를 고른다** — 우선순위도 플레이어 선택도 없다.
  **같은 재료를 받는 레시피를 한 기계에 둘 이상 두면 목록에서 앞선 것만 영원히 돈다**(실제로 그래서
  분쇄기가 `돌 → 돌` 만 반복했다). 재료가 겹치는 레시피는 서로 다른 기계나 티어로 갈라 둘 것.
- **수동 기계**는 `MachineBlock.manualStepRatio`(0 이면 자동, 0.05 면 20클릭에 1개)로 표현한다.
  `MachineInstance.ManualStep` 이 **`Tick` 을 그대로 재사용**하므로 재료·출력자리·연료·전력 판정이 한 곳에 남는다.
  `AutoProcess=false`(조합대)와는 다르다 — 조합대는 자기 슬롯을 안 쓰고 플레이어 인벤토리로 만든다.
  **수동 기계에 `runningSprite` 를 주지 말 것**: `Update` 가 매 프레임 `SetRunning(false)` 라 한 프레임만 보인다.
- **건설 재료의 티어 기본값** (2026-08-12 사용자 결정) — 기계 건설 레시피는 **티어 기본 재료 + 기계별 부품**이다.

  | 해금 티어 | 기본 재료 | 실질 비용 |
  |---|---|---|
  | 0 | `벽돌 ×1` | 모래2 + 물1 |
  | 1 | **`철근 ×4`** | 철판 8 (철근 = 철판 ×2) |
  | 2 | **`철근 콘크리트 ×2`** | 철근 8 + 시멘트 40 |

  ⚠ **콘크리트는 원래 1티어였다.** `철근 ×4 + 시멘트 ×20` 이고 시멘트 ← 석회 ← **뼈 가루**라
  콘크리트 1개가 돌 가루 수천 개다(추출 1%) — 1티어가 통째로 잠겨 있었다. **한 티어 위로 올려**
  2티어 관문으로 삼았고(`reinforced_concrete.tier = 2`), **지하 전리품에 뼈 가루 행을 넣어**
  추출 말고 다른 길을 냈다(2026-08-15). 실측: 등급 0 방 한 판에 뼈 가루 약 10개 = 시멘트 5 →
  **콘크리트 1개에 지하 원정 약 4회 · 2티어 기계 1대에 약 8회.**
  새 기계를 추가할 때 이 표를 따르고, 어기면 그 티어가 조용히 잠긴다.
- **새 기계를 늘리는 데 에디터 툴은 필요 없다.** 기존 `MachineBlock` 을 복제해
  `recipeGroupId`(레시피 목록 공유) · `speedMultiplier`·`energyUseRate`(상위 기계와의 차이) ·
  `uiPrefab`(UI 공유) 세 자리만 맞추고,
  `itemName == blockName` 인 `Items` 를 함께 만든 뒤 `Register All Assets` 를 돌리면 된다.
  화로 3종 · 조합대 2종 · 추출기 12종이 전부 이 방식으로 붙어 있다 —
  **그래서 계열 생성 툴(`ExtractorSetup` 등)은 만들지 않는다.** 표와 에셋이 갈라져 값이 되돌아갈 뿐이다.
#### 여러 칸을 차지하는 기계 (발자국)

`MachineBlock.size` 로 정하고 **기준점은 왼쪽 아래 칸**이다. 지금 6종:
`고급 조합기 5×5` · `고급 재단 3×3` · `코어 조합기 3×3` · `중급 재단 2×2` · `용광로 2×2` · `증류기 1×3`.
세로로 1.1~1.5칸인 그림(화로·기본 재단 등)은 **탑다운 오버행이지 발자국이 아니다** — 1×1 로 둔다.

- **발자국은 저장하지 않는다.** `MachineBlock.Footprint` 에서 파생되고 `WorldMap` 이
  **덮인 칸 → 기준점** 색인(`occupancy`)만 런타임으로 들고 있다. **세이브는 v12 그대로다.**
  ⚠ **`Footprint` 의 `Mathf.Max(1, …)` 클램프를 지우지 말 것** — `size` 가 (0,0) 으로 읽히는 에셋이
  하나라도 생기면 그 기계가 칸을 하나도 안 차지하게 된다. (실측: `size` 를 추가한 뒤 기존 47개 에셋은
  YAML 에 `size:` 줄이 없는데도 C# 초기값 (1,1) 로 읽혔다 — 위쪽 "필드를 새로 넣으면 0 으로 읽힌다"
  경고와 다른 결과이므로, **믿지 말고 클램프로 막아 둔다**.)
- **지렛대는 두 곳뿐이다.** `WorldMap.GetPlaceableAt` 이 덮인 칸에서 **기준점 레코드**를 돌려주고
  (그래서 `PipeRouter` 의 `MachineAt·Connects·StorageAt·FaceAt` 와 배치 판정이 코드 변경 없이 발자국을 안다),
  `MapGenerator` 가 **덮는 칸마다 같은 인스턴스**를 `loadedMachines` 에 넣는다
  (그래서 어느 칸을 우클릭해도 UI 가 열리고 어느 칸을 캐도 회수된다).
- ⚠ **칸으로 기계를 가리키는 자리는 전부 `WorldMap.OriginAt` 으로 정규화한다.** 안 하면 한 기계가
  칸 수만큼 서로 다른 대상으로 세어진다 — 실제로 그럴 뻔한 곳이 넷이다:
  `PipeRouter.AddSink`(도착지) · `PipeNetworkManager` 의 `sourceCell`(자기 급전 가드) ·
  `PowerLinkMode` 의 `AddLink`(전력 몫이 N배) · `MapGenerator.FlushAll`(한 기계를 N번 Flush).
- **쓰기는 `WorldMap.SetPlaceableAt`/`RemovePlaceableAt`(월드 좌표) 로 모았다.** 예전에는 읽기만
  월드 좌표고 쓰기는 청크 로컬이었는데, 발자국은 **청크 경계를 넘을 수 있어** 그 비대칭을 둘 수 없다.
- **콜라이더와 그림자는 프리팹에 저작한다**(2026-08-18 사용자 작업). 기계 47종이 각자 알맞은
  `Collider2D` 와 **`ShadowCaster2D`**(47/47)를 들고 있고, 런타임 보정
  (옛 `MapGenerator.ApplyFootprintCollider`)은 **호출을 껐다**.
  ⚠ **런타임 `AddComponent<ShadowCaster2D>()` 로는 스프라이트 실루엣 그림자를 얻을 수 없다** —
  `Awake` 가 `ShapeProvider` 를 고르는 블록이 `#if UNITY_EDITOR` 안에만 있어(`ShadowCaster2D.cs:321-341`)
  **에디터에서는 실루엣, 빌드에서는 사각형**이 된다. 프리팹에 저작하면 `m_ShadowCastingSource` 가
  직렬화돼 빌드에서도 산다 — 벽 그림자가 코드로 짓는데도 괜찮은 것은 애초에 사각형이기 때문이다.
  ⚠ **`runningSprite` 를 바꿔도 실루엣 그림자는 안 따라간다**(`ShadowShape2DProvider_SpriteRenderer` 의
  갱신 조건이 `drawMode` 변화뿐이다) — 해당 기계 2종은 그림자가 정지 모양으로 남는다.
- ⚠ **면 상태는 방향당 하나다.** `faceModes` 는 레코드 하나에 2비트 × 4면이라, 같은 방향으로 늘어선
  여러 칸이 **한 설정을 공유**한다(2×2 의 북쪽 두 면을 따로 지정할 수 없다).
- **설치 미리보기**(`PlacementPreview`, 루트는 아직 `MapGenerator.Start` 가 만든다 — 이관 대상)는 반투명 기계 그림 +
  칸별 초록/빨강이다. ⚠ **판정은 `PlayerInteraction.CanPlaceFootprint` 한 곳만 한다** —
  미리보기가 따로 계산하면 "초록인데 안 놓이는" 상태가 반드시 생긴다. 그래서 `PerformUse` 의 네 분기는
  판정을 갖지 않고 "어떻게 놓는가" 만 남아 있다(칸 조건은 `CanPlaceCell` 이 종류별로 가른다).
- **도달 판정도 발자국 기준**이다(`IsFootprintAdjacent`) — 한 칸이라도 플레이어와 맨해튼 1 이면 된다.
  1×1 이면 옛 `adjacent` 와 결과가 같다.

- `RecipeSolver` 가 재료 확인·소모·적재를 전담: `CanCraft` `ConsumeInputs` `CanStoreOutputs` `AddItems`
  `CountFreeSpace`(넣어 보지 않고 여유 세기) `CountItem`.
- **`RecipeSolver.AddItems` 는 통지하지 않는다.** 외부에서 슬롯을 건드렸으면
  `inventory.NotifyChanged()` + `instance.Flush()` 를 직접 불러야 UI 갱신·재가공이 걸린다.
- `Recipe.tier` = **조합대 해금 요구뿐이다**(일반 기계는 안 본다). 화로 3종은
  `MachineBlock.recipeGroupId` 로 같은 목록을 공유하고 셋 다 전부 처리한다.
- **가동 중 그림**: `MachineBlock.runningSprite` 가 **비어 있으면 그림을 바꾸지 않는다**(47대 중 45대가 그렇다).
  **정지 그림의 정본은 SO 가 아니라 `machinePrefab` 의 `SpriteRenderer`** — `MachineInstance.Bind` 가
  배치 시점에 기억했다가 되돌린다. 두 곳에 두면 언젠가 어긋난다.
  교체는 `SetRunning` 한 곳에서만 하고 **상태가 바뀔 때만** 대입한다(매 프레임 대입하면 배칭이 깨진다).
  가동 판정 = **그 프레임에 실제로 진행됐는가**. 재료가 있어도 연료·전력·출력자리가 없으면 정지고,
  발전기는 **연료를 실제로 태운 프레임**만 가동이라 버퍼가 차면 정지 그림이 된다.
- **완성 시점에 훅이 하나 더 있다** — `boundUI != null` 이면 완성음(`SfxPlayer.PlayCraft`). 아래 §사운드.

#### 발전기의 찌꺼기 산출 (`spentFuelItem`) — 핵발전소

`TickGenerator` 는 **레시피를 아예 보지 않는다**(`Update` 가 `Tick` 앞에서 되돌아간다). 그래서 산출은
레시피가 아니라 **연소 경로에 건 훅** 하나로 낸다 — `MachineBlock.spentFuelItem` 이 비어 있으면
지금까지와 완전히 같으므로 **나머지 발전기 3종은 한 톨도 안 바뀐다**(실측).

- 계약은 `MachineInstance.TryEjectSpentFuel` 하나다: **점화 직전**에 불린다.
  ① 낼 몫이 있으면 출력 칸에 1개 넣고, ② 몫이 없어도 **자리는 미리 확인**한다.
  ⚠ ②를 빼면 자리가 없는데 새 연료에 불을 붙여 **찌꺼기를 빚으로 떠안고**, 그 상태로 기계를 캐면
  그 한 개가 조용히 사라진다(실측으로 이 함정에 걸렸다). ②가 있어야 "산출이 차면 발전이 멈춘다" 가
  **연료를 한 개도 더 안 태우고** 성립한다.
- ⚠ **"아직 안 낸 찌꺼기" 표시는 `burnRemaining == 0 && burnTotal > 0`** 이다. `burnTotal` 을
  다 태운 순간에 0 으로 밀지 않고 남겨 두는 것이 그 표시고, **둘 다 세이브 v5 필드라 새 칸 없이**
  저장·복원을 건너 살아남는다(그래서 이 기능이 **세이브 v12 를 그대로 둔다**).
- `Generator_UI.prefab` 한 장을 발전기 4종이 공유하므로 **출력 칸을 거기 하나 넣어 두면** 되고,
  `outputSlotCount == 0` 인 나머지 셋은 `DefaultMachineUI` 가 알아서 끈다.
- 핵발전소 값: `fuelBurnRate 500` · `maxEnergyAmount 20000` · `핵연료봉.burnEnergy 30000`(= 정확히 60초).

### 유체 (액체·기체를 한 계층으로)

- **액체와 기체를 나누지 않는다.** `FluidDefine`(SO) 하나에 `phase`(Liquid/Gas)만 두고,
  실제로 다른 것은 "어느 파이프가 나르는가"뿐이라 `CarriedBy → PipeKind` 로 해결한다.
  나누면 탱크·`Recipe`·`RecipeSolver`·세이브가 전부 두 벌이 되고 언젠가 한쪽만 고쳐진다.
- **양은 단위 없는 정수다.** 규약으로 **1 양동이 = `FluidDefine.bucketAmount` = 1000**.
  코드에 mL 개념이 없으므로 세분화할 때 **레시피 숫자만** 바꾸면 되고 float 누적 오차도 없다.
- `FluidStack` **하나**가 레시피 항목이자 탱크 한 칸이다(`ItemStack` 과 같은 꼴).
  **탱크 한 칸에는 한 종류만** 담긴다 → 두 종류를 내는 레시피는 출력 탱크가 2칸 이상이어야 한다.
- `RecipeSolver` 의 유체 API 는 아이템 쪽과 **이름·계약이 대칭**이다:
  `CountFluid` `HasFluids` `ConsumeFluids` `CanStoreFluids` `StoreFluids` `AddFluid` `CountFreeFluidSpace`.
  `AddFluid` 도 **통지하지 않는다** — 밖에서 탱크를 만졌으면 `instance.NotifyFluidChanged()` 를 불러야 한다.
- ⚠ **탱크 생성은 `ApplyConfig` 안에서 한다.** `Bind` 가 `LoadFrom` **뒤에** 만들면 복원한 유체를 매번 덮어쓴다
  (옛 `Gas[]` 가 정확히 그 자리에 있었다 — 전력·진행도가 같은 함정에 걸렸던 곳).
- ⚠ **`SelectRecipe` 와 `Tick` 이 같은 `CanRun(Recipe)` 을 봐야 한다.** 고를 때 유체를 안 보면
  "물이 없어 영원히 안 도는" 레시피를 물고 기계가 통째로 잠긴다(첫 후보를 잡으면 더 안 찾는다).
- **양동이 교환은 전용 슬롯 없이 입출력 슬롯으로 한다**(`MachineInstance.ExchangeBuckets`, `Tick` 보다 먼저).
  채워진 양동이 → 입력 탱크 + 빈 그릇이 출력으로 / 빈 그릇 → 출력 탱크를 퍼내 채워진 것이 출력으로.
  **빈 그릇 놓을 자리가 없으면 아무것도 하지 않는다.** 덕분에 파이프로 양동이를 넣는 것도 공짜다.
- **지형 유체 — 빈 그릇을 들고 유체 바닥을 우클릭하면 채워진다**(`PlayerInteraction.TryFillContainer`).
  **바닥은 줄지 않는 무한 원천이다** — 그래서 지하맵의 물이 **물의 유일한 최초 획득처**이고,
  물을 먹는 `compress_brick`·화학 사슬이 여기서 열린다.
  어느 바닥이 어느 유체인지는 **`MainBlock.fluid`(SO 필드) 하나**가 정하고(참조형이라 나머지 지형 7종은
  줄이 없어 자동으로 null = 유체 아님), 그릇 ↔ 내용물의 짝은 `ExchangeBuckets` 와 **같은
  `FluidDefine.emptyItem`/`bucketItem`** 을 본다 — 표가 둘로 갈리지 않는다.
  ⚠ **"이 칸에 무엇이 고여 있나" 의 정본은 `WorldMap.FluidAt(cell)` 하나다** — 오버레이(석유·물 웅덩이)를
  먼저 보고 없으면 바닥 타일을 본다. **빈 그릇으로 퍼는 쪽과 펌프가 같은 함수를 본다** —
  표가 둘로 갈리면 "양동이로는 퍼지는데 펌프는 안 도는" 상태가 생긴다.
  ⚠ **이 분기만 "인접 한 칸" 이 아니라 발밑도 허용한다**(`adjacent || targetCell == playerCell`).
  물은 통행을 막지 않아 그 위에 설 수 있고, 인접만 보면 "물 위에 서 있는데 못 뜨는" 상태가 된다.
  ⚠ **자리가 없으면 빈 그릇을 되돌린다** — 소모가 먼저라 되돌리지 않으면 그릇이 증발한다.
  **붓기(채워진 그릇 → 유체 타일)는 아직 없다** — 놓은 물을 다시 퍼면 무한 증식이라 규칙부터 정해야 한다.
- **펌프는 발밑 유체로 레시피를 고른다.** 규칙 한 줄이 `MachineInstance.CanRun` 에 있다 —
  **입력이 없고 유체만 내는 레시피는 `WorldMap.FluidAt(worldCell)` 이 그 유체일 때만 돈다.**
  그래서 `extract_water`·`extract_oil` 을 **한 그룹(`Pump`)에 둬도 안 겹치고**(무입력 레시피가 둘이면
  `SelectRecipe` 는 원래 첫 것만 고른다), 아무 데나 놓은 펌프는 안 돌며,
  **자원 생성기는 영향을 안 받는다**(그쪽은 아이템을 낸다).
  ⚠ **`ApplyConfig` 의 "미설정" 조건에 유체 탱크를 넣어야 한다** — 펌프는 입출력·연료가 전부 0 이라
  예전엔 3/6 폴백에 걸려 **발밑 판정 이전에 이미 죽어 있었다**(지열 발전기가 `IsGenerator` 로 걸렸던 것과 같다).
  **웅덩이는 줄지 않는다** — 지형 유체라 무한이고, 따로 구현할 것이 없다. 펌프 40/s.
- **탱크는 유체를 다루는 것이 정체성이고 지금 유체 레시피가 있는 기계만** 준다
  (전기 분해기 1/2 · 화학 처리기 2/1 · 마나 용해기 0/1 · 펌프 0/1).
  화로·압축기·조합대·재단은 탱크 없이 **`물`(채워진 양동이) 아이템**을 그대로 먹는다.
  레시피가 없는 기계에 탱크를 주면 값이 안 채워지는 빈 바가 남는다(옛 `Gas[]` 뼈대가 그랬다).
- 파이프는 아이템과 **같은 짐 방식**이다. `ParcelRecord.fluidId/amount` 로 판별만 하므로
  `DeliverAll`·다익스트라·라운드로빈·렌치 면 규칙이 전부 그대로 재사용된다.
  `PipeRouter.TargetTanks` 도 `TargetSlots` 처럼 **레시피를 근거로** 거른다(안 그러면 자기 산출물을 도로 먹는다).
  ⚠ **파이프를 캐면 안에 있던 유체는 사라진다** — 필드에 유체를 떨어뜨릴 수단이 없다(의도).
- **유체는 그림 대신 단색으로 그린다.** 색은 `FluidDefine` 이 아니라 **`FluidColors`(static 정본 표)** 가
  `fluidId → Color` 로 갖는다. 그래서 `MachineInstance.PushFluids` 는 UI 에 **색이 아니라 유체 이름**을
  넘기고(`SetInputFluid(index, ratio, fluidId)`), 무슨 색인지는 `FluidColors.Of` 한 곳만 안다 —
  색을 넘겨받게 두면 부르는 쪽마다 색을 정하게 되어 언젠가 서로 달라진다.
  표에 없는 이름·빈 탱크는 **회색(`Unknown`)** 이라 새 유체를 넣고 줄을 안 적으면 화면에서 티가 난다.
  칠하는 것은 `FillingSlot.FillColor`(채움 이미지만) — 전력·연료·진행도 바는 건드리지 않는다.

### 업그레이드 모듈

- `UpgradeModuleItem : Items`(`WrenchItem` 과 같은 **타입 판정**) — `kind`(Speed/Efficiency)와
  `valuePerUnit` 을 **에셋에 둔다**. 밸런스는 반드시 여러 번 바뀌므로 코드 상수로 두면 정본이 흐려진다.
- **소모되지 않는다.** 칸에 든 **개수**만큼 효과가 붙으므로 상한은 `maxStack`(현재 8)이 정한다.
- 배수는 **캐시하지 않고 소비 시점에 곱한다.** `energyUseRate`·`fuelBurnRate` 는 `ApplyConfig` 가 `Bind`
  시점에 복사해 둔 값이라, 거기 곱하면 창을 닫았다 열기 전까지 옛 값이 쓰인다.

  | 값 | 자리 |
  |---|---|
  | 속도 | `EffectiveCraftTime` 의 `speed` 에 `× SpeedFactor` |
  | 전력 | `ConsumeEnergy` 의 `need` 에 `× EfficiencyFactor` |
  | 연료 | `BurnFuel` 의 `want` 에 `× EfficiencyFactor` (발전기는 `× SpeedFactor`) |
  | 발전 | `TickGenerator` 에서 `burned / EfficiencyFactor` |

- ⚠ **발전기는 방향이 반대다.** 태운 양 = 발전량이라 소비를 줄이면 출력이 *준다*.
  효율은 **산출 쪽**(같은 연료로 더 많은 전력), 속도는 **연소 속도**(총 에너지 그대로, 초당 출력↑)에 건다.
- 평면 인덱스는 **`[입력][출력][연료][업그레이드]`** — 새 구간은 언제나 맨 뒤에 붙인다
  (앞에 끼우면 기존 UI 프리팹의 바인딩이 통째로 어긋난다).
- `MapGenerator.RemoveMachineAt` 이 업그레이드 칸도 `DropSlots` 한다 — 빼면 기계를 캘 때 모듈이 증발한다.

### 추출 체계 (정본 = `자원과 그 가공방식.canvas`, Obsidian Vault 에 있다)

메인자원을 **분쇄기로 1/2/3회 분쇄** → 그 분쇄물을 **추출기**에 넣어 부산물을 확률로 얻는다.
0티어 = **돌** · 1티어 = **마력석** · 2티어 = **운석**. 금속 산출은 `raw_*_ore`(조각), 재련하면 `*_ingot`.

**분쇄 사슬 세 줄.** 분쇄기들은 `recipeGroupId = "Pulverizer"` 로 목록을 공유하므로,
새 분쇄기는 그 값만 주면 사슬이 그대로 붙는다. 사슬 레시피의 `tier` 는 계열 번호를 적어 둔 것일 뿐
**아무도 안 본다** — **분쇄기는 한 종류이고 모든 계열을 빻는다**(2026-08-15 사용자 결정.
전에는 처리 티어 게이트 때문에 전기 분쇄기가 운석 사슬을 못 돌렸다).
분쇄기는 **`Machine:ManualPulverizer`(수동 분쇄기, `manualStepRatio 0.05`, 무전력)** 와
**`Machine:ElectricPulverizer`(전기 분쇄기, 전력식)** 둘인데, 갈리는 것은 티어가 아니라 **전력 유무**다 —
추출기의 `Extractor00`/`Extractor00Plus` 와 같은 꼴이다.
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
- ⚠ **`chanceOutputs` 를 보는 곳은 `inputs`/`outputs` 를 보는 곳과 반드시 짝이어야 한다.**
  `DictionaryRegistrar.HasAnyOutput` 은 확률 산출도 산출로 친다 — 빠뜨리면 확률 전용 레시피가
  "재료만 먹는 위험한 레시피"로 걸러져 등록되지 않고 기계가 영원히 논다(실제로 한 번 걸렸다).
  `ItemMerger.CollectReferenced`·`RewriteRecipes` 도 같은 규약이다 — 빠뜨리면 **확률로만 나오는 아이템 7종**
  (`conductor_crystal` `diamond` `energy_crystal` `ruby` `sapphire` `raw_osmium_ore` `raw_thorium_ore`)이
  "아무도 안 쓴다"로 판정돼 통합 때 지워지고, 레시피에 `{fileID: 0}` 줄만 남는다.
  단 확률 줄은 **합치지 않는다** — 줄마다 `chance` 가 달라 개수를 더하면 한쪽 확률이 사라진다.
- **전부 입력 1 / 출력 9 로 통일**돼 있고 UI 도 `Prefabs/ui/Machines/Extractor01_UI.prefab` **한 장을 공유**한다
  (`uiPrefab`). 등급이 올라도 산출 종류만 늘 뿐 칸 수는 그대로라, 등급마다 UI 를 만들 이유가 없다.
- **0-0티어만 두 갈래다** — `Extractor00`(수동, "수동 0-0티어 추출기") · `Extractor00Plus`(전기 자동, "0-0티어 추출기").
  수동은 `MachineBlock.manualStepRatio = 0.05`(20클릭에 1개)이고 UI 는 전력바 대신 작동 버튼을 둔
  `Extractor00_UI.prefab` 을 혼자 쓴다. 나머지 12종은 전기 전용 + 공유 UI.
- **지형에 설치해 메인자원·유체를 뽑는 것은 추출기가 아니다** — `자원 생성기`·`펌프`·`지열 발전기` 쪽이다
  (`extraction.json` 의 `terrain` 필드가 그 표시). 입력 0 / 출력 1 로 둬야 3/6 폴백에 안 걸린다.
  **위의 9칸·공유 UI 는 여기엔 적용하지 않는다.**

#### 자원 생성기 (입력 없이 1초에 1개)

**입력이 하나도 없는 레시피**를 도는 기계 4종. 티어가 산출을 정한다 —
`ResourceGenerator0` 돌 · `0Plus`(강화, `speedMultiplier 2`) 돌 · `ResourceGenerator1` 마력석 ·
`ResourceGenerator2` 운석. 레시피는 `Recipes/Incomplete/extraction/` 의
`extract_normal`·`extract_ore`·`extract_meteorite`(전부 `craftTime 1`)다.

- ⚠ **무입력 레시피의 게이트는 `MachineInstance.CanRun` 하나다. `RecipeSolver.HasInputs` 를 고치면 안 된다.**
  그쪽은 "입력도 유체도 없으면 false" 인데, **조합대의 도구 부품 레시피 6개**
  (`rod` `blade` `plate` `hammer_head` `pickaxe_head` `knife`)도 재료를 **재질 칸**에서 받느라
  `inputs` 가 비어 있고 `CraftingTableUI` 가 `RecipeSolver.CanCraft` 를 **직접 부른다** —
  열어 주면 **부품을 재료 없이 무한히 찍게 된다.** 그래서 예외는
  **런타임 `inputSlotCount == 0` 인 기계**에서만 연다(자원 생성기의 정의 그 자체다).
  SO 값이 아니라 `ApplyConfig` 가 채운 값을 보므로 **3/6 폴백에 걸린 기계는 자동으로 제외된다.**
- ⚠ **그룹을 반드시 티어별로 가른다**(`ResourceGenerator0/1/2`). 입력이 없으면 `CanRun` 이 **항상 참**이라
  한 그룹에 둘 이상 두면 `SelectRecipe` 가 **첫 것만 영원히 고른다** — 셋 다 돌이 나온다.
  강화형은 같은 돌 레시피를 쓰므로 `ResourceGenerator0` 를 공유한다.
- **전부 무전력**(2026-08-19 사용자 결정): `isUseEnergy 0` · `energyUseRate 0` · **`maxEnergyAmount 0`** 셋을
  함께 넣는다. ⚠ `MachineBlock.EnergyUseRate` 가 `energyUseRate > 0 ? … : maxEnergyAmount * 0.1f` 라
  버퍼만 남겨 두면 **100/s 가 살아 있다**(전기 화로와 같다).
- 속도는 **업그레이드 모듈**로만 올린다 — `upgrade_speed` 0.25/개 × 8 × 2칸 → `SpeedFactor` 최대 5 =
  **초당 5개**(강화형은 ×2 라 10개). 실측으로 확인했다.
- UI 는 `ResourceGenerator0_UI.prefab` **한 장을 4종이 공유**한다(`BlastFurnace_UI` 에서 입력 2칸만 뺀 것).
  전력바는 남겨 뒀지만 `isUseEnergy` 가 꺼져 있어 자동으로 안 보인다.
- ⚠ **1·2티어 건설 레시피가 없다**(2026-08-19 보류) — 아이템 목록(`P`)·콘솔로만 얻는다.

- ⚠ 생성 툴 `ExtractorSetup`(`추출기 계열 설정`) 은 **삭제했다.** 12종이 이미 다 있는데 `Spec` 표가
  `outputSlotCount` 를 조건 없이 덮어써, 손으로 맞춘 값을 되돌리는 함정이었다(실제로 표는 1, 에셋은 9였다).
  이제 **에셋이 유일한 정본**이고 설계 정본은 `자원과 그 가공방식.canvas` 다.

### 연료 (`FuelTable` 이 정본)

**어느 기계가 무엇을 태우고, 그것이 초당 얼마로 몇 초 타는지**를 `Assets/Scripts/Machine/FuelTable.cs`
static 표 하나가 정한다(`ExtractionTable`·`UndergroundLootTable` 과 같은 꼴 — 값이 문자열·숫자뿐이라
에셋을 가리킬 필요가 없다). 한 표가 넷을 함께 답한다: **허용 목록**(행이 있는가) · **초당 발전량**(`rate`) ·
**발전 시간**(`seconds`) · **총 에너지**(둘의 곱).

| 기계 | 연료 | 1단위 | 초당 | 지속 |
|---|---|---|---|---|
| 핵발전소 | `nuclear_fuel_rod` | 1개 | 3000 | 60s |
| 화력 발전기 | `coal` / `brown_coal` | 1개 | 200 | 20s / 10s |
| 가스 발전기 | `crude_oil` / `gasoline` | **1000(유체)** | 300 / 600 | 20s / 20s |

⚠ **위 숫자를 에셋에서 고치면 아무 일도 일어나지 않는다**(2026-08-20 에 실제로 그랬다).
`ThermalGenerator.fuelBurnRate` 를 20 → 200 으로, `NuclearPlant` 를 500 → 3000 으로 바꿔 놓았는데
표가 이기므로 발전량은 그대로였다 — **표를 고쳐야** 바뀐다. 반대로 **표에 행이 없는 기계**
(화로·지열)는 에셋 값이 정본이다.

- ⚠ **표에 그 기계의 행이 하나도 없으면 지금까지와 완전히 같이 돈다** — 연료 판정은 `Items.IsFuel`,
  총량은 `Items.burnEnergy`, 초당은 `MachineBlock.fuelBurnRate`. 그래서 **화로·지열은 한 줄도 안 바뀐다**
  (화로는 그래서 여전히 핵연료봉도 받는다 — 막으려면 표에 두 줄만 더하면 되고 코드는 안 바뀐다).
- ⚠ **표가 있는 기계에서는 표가 이긴다.** 에셋의 `fuelBurnRate` 는 그 기계에서 더 이상 안 쓰인다 —
  화력·핵의 값을 바꾸려면 **에셋이 아니라 표**를 고쳐야 한다.
- **화이트리스트의 정본은 `MachineInstance.AcceptsFuel` 하나**이고 <b>두 곳이 그것을 함께 본다</b>:
  손으로 넣는 쪽(`IItemContainer.AcceptsItem` → `MachineInventory.acceptOverride` → `ItemSlot.Accepts`)과
  파이프(`PipeRouter.TargetSlots`). 갈라지면 "손으로는 들어가는데 파이프로는 안 들어가는" 상태가 된다.
  ⚠ `AcceptsItem` 은 `SlotCapacity` 와 **같은 이유로 저장소에 묻는다** — 슬롯 UI 가 저장소 종류를 보고
  분기하면 규칙이 흩어진다. 그래서 **UI 프리팹을 한 장도 안 고치고** 기존 연료 칸 전부에 적용됐다.
- **발전기의 세 갈래를 표가 가른다**(`TickGenerator`):
  `BurnsFluidFuel`(표에 유체 행) → 입력 탱크에서 점화 · `UsesFuel`(연료 칸) → 칸에서 점화 · 그 외 → 무연료(지열).
  ⚠ **가스 발전기는 연료 칸이 0 이다** — `BurnsFluidFuel` 판정이 없으면 곧바로 "무연료 = 무한 발전"으로 떨어진다.
- ⚠ **지금 타는 연료의 연소율은 세이브에 남긴다**(v14 `burnRate`). 남은 에너지만으로는 석탄인지 갈탄인지
  알 수 없어 로드 뒤 엉뚱한 속도로 탄다. **0 이 "모름"** 이고 그때 `ActiveBurnRate` 가 기계 값으로 떨어진다
  (= v13 세이브가 예전 그대로 이어 탄다).
- **가스 발전기는 파이프 전용이다** — 입출력 아이템 칸이 0 이라 `ExchangeBuckets`(양동이 교환)가 안 걸린다.
  양동이로도 넣게 하려면 입력·출력 칸을 1씩 주면 된다.

### 전력
- `MachineBlock`: `isGenerator` `powerRange` `energyUseRate`. 발전 = 연료 연소(위 §연료).
- `MachineInstance`: `TickGenerator` → `Distribute()`(라운드로빈, 꽉 찬 곳 건너뜀, 죽은 링크 정리) → `ConsumeEnergy`.
- `PowerLinkMode` = 전체화면 전송 설정 모드(빨강 미연결 / 초록 연결 / 파랑 발전기). 오버레이 타일맵을 런타임 생성.
  - **이동 키로 화면을 민다**(발전 범위 8~12칸 > 화면 세로 8칸이라 한 번에 다 안 보인다).
    `CinemachineCamera.Follow` 를 **null 로 떼고 vcam 트랜스폼을 직접 옮긴다** — 따라갈 임시 오브젝트를
    만들면 수명 관리가 하나 더 늘고, 몸통(`OrbitalFollow`)은 대상이 없으면 아무 일도 안 해 그 자리에 선다.
    ⚠ **`Exit` 에서 `Follow` 를 반드시 되돌린다** — 안 그러면 카메라가 플레이어를 영영 안 따라간다.
  - ⚠ **WASD 를 직접 읽지 않는다.** 액션맵은 끄되 **`Move` 액션만 도로 켠다**(`GetAction("Move").Enable()`) —
    직접 읽으면 키 재설정(`StartRebind`)이 이 모드에서만 안 먹는다. 플레이어는 `isAnyUIOpen` 때문에
    어차피 안 움직인다. 화면은 발전기 중심 정사각형(`powerRange + panMargin`)에 가둔다 —
    더 나가면 청크가 안 불려 빈 화면이 된다.
- 값 채우기: `Tools/Project Craft/Machines/전력 기본값 채우기`.

### 파이프 (아이템 · 액체 · 기체 전부 운반한다)
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
  **필드에 쏟지 않고 들고 기다린다**. 회수는 파이프를 캘 때만(단 **유체 짐은 버린다** — 떨어뜨릴 수단이 없다).
- 유체는 `TryExtractFluid`/`DeliverFluid` 가 맡고 나머지는 아이템 경로와 완전히 같다.
  ⚠ 유체 파이프의 `throughput` 은 **한 번에 싣는 양**이라 1000(=1양동이) 단위다. 1 로 두면 물 한 통에 1000번 걸린다.
- 값 채우기: `Tools/Project Craft/Pipes/파이프 에셋 설정`.

#### ⚠ 공장을 지을 때 반드시 아는 세 가지 (2026-08-20 시연 공장에서 전부 걸렸다)

- **파이프는 출발 기계의 "첫 비어 있지 않은 칸" 하나만 본다**(`TryExtract` 가 그 자리에서 `break`).
  그 아이템을 아무도 안 받으면 **뒤 칸은 영영 안 나간다** — 짐을 싣지 않으므로 교착은 아니지만
  라인이 통째로 선다. 그래서 **한 상자에 두 종류를 넣고 번갈아 나가길 기대하면 안 된다.**
- **여러 칸 기계는 발자국 전체가 파이프에 닿는다.** 2×2 용광로의 `(-28,8)` 칸이 한 칸 위를 지나던
  다른 라인의 파이프에 잡혀 **강철이 엉뚱한 상자로 샜다.** 옆을 지나는 파이프는 그쪽 면을
  **`Cut` 으로 막는다**(렌치와 같은 규약).
- **입력이 여러 칸인 기계는 칸을 미리 선점해 둔다.** `RecipeSolver.AddItems` 는 빈 칸을 앞에서부터
  채우므로, 재료 A 가 먼저 몰리면 **두 칸을 다 A 가 먹어** B 가 영영 못 들어간다(용광로 = 철+석탄,
  합금 재련기 = 구리+주석). 각 칸에 1개씩 미리 넣어 두면 그 뒤로는 종류가 고정된다.

**저장 네트워크는 이 셋을 전부 피한다** — `PullFrom` 은 **모든 출력 칸을 훑고**, `PushTo` 는
`PipeRouter.TargetSlots` 로 **그 기계가 받는 것만** 민다. 즉 **망이 곧 분류기**다(광석 7종을 받아
화로에 알아서 나눠 넣는다). 대신 **한 기계가 여러 레시피를 받으면 엉뚱한 재료가 입력칸을 채울 수 있다**
(압축기가 `compress_brick` 때문에 모래를 받는다) — 망에 물릴 기계는 그 점을 보고 고른다.

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

### 저장소 (상자 · 아이템 저장소)

`StorageBlock : MachineBlock` **한 클래스에 에셋 둘**. 차이는 숫자 세 개뿐이다.

| | `Machine:Chest` 상자 | `Machine:ItemStorage` 아이템 저장소 |
|---|---|---|
| `storageSlotCount` | 40 | 1 |
| `baseCapacity` | 0 = 아이템의 `maxStack` | **1024** |
| `capacityPerUpgrade` | 0 | **1024** (효율 모듈 1개당, 칸 1개 × maxStack 8 → 최대 **9216**) |

- **저장 칸은 새 구간이 아니라 `inputSlots` 다.** 5번째 구간을 만들면 `PlaceableRecord`·`Chunk.Save/Load`
  (세이브 버전)·`MapGenerator.DropSlots` 가 전부 따라 늘어난다. 입력 구간에 얹으면
  **세이브 변경 0 · 드랍 처리 0 · 평면 인덱스 변경 0**. `ApplyConfig` 가 `inputSlotCount` 를
  `storageSlotCount` 로 덮어쓰므로 에셋에서 두 값을 맞춰 둘 필요가 없다.
  `AutoProcess = false` 라 `Tick` 이 그 칸을 재료로 볼 일이 없다(`AllowsZeroSlots` 도 켜야 3/6 폴백을 안 탄다).
- ⚠ **개수 클램프의 정본은 이제 `IItemContainer.SlotCapacity(index, item)` 다.**
  `RecipeSolver.MaxStackOf` 는 그 기본값일 뿐이고, **`item.maxStack` 을 직접 읽으면 안 된다**
  (`ItemSlot.OnDrop` 이 그러고 있어서 1칸 저장소가 64개에서 막혔다). `RecipeSolver.AddItems`·
  `CountFreeSpace` 는 선택 인자 `perSlotCap`(0 = maxStack)으로 같은 값을 받는다 —
  **둘에 다른 값을 주면** "자리가 있다고 해서 보냈는데 안 들어가는" 짐이 파이프에 영원히 남는다.
- **최대치는 캐시하지 않는다.** `MachineInventory.capacityOverride` 에 숫자가 아니라 **함수**를 꽂는다
  (`MachineInstance.SlotCapacityFor`) — 모듈 개수로 런타임에 바뀌기 때문. `SpeedFactor` 와 같은 규약이다.
  효율 모듈의 `valuePerUnit`(0.10)은 **쓰지 않는다**. 그건 전력·연료 절감률이라 개수와 단위가 다르다.
- **모듈을 빼서 최대치가 줄어도 내용을 버리지 않는다.** 넘치는 동안 더 못 넣게만 한다.
- ⚠ **아이템 저장소는 칸이 하나라 한 종류만 담는다.** 그래서 **재료 공급용으로는 상자보다 낫고**
  (9,216개 = 상자 40칸의 3.6배), **여러 종류가 들어오는 산출 자리에는 못 쓴다**
  (두 번째 종류가 통째로 거부되어 파이프가 선다). 2026-08-20 시연 공장에서 공급 상자만 바꾼 이유다.
- **한 파이프 칸이 여러 저장소에서 번갈아 꺼낼 수 있다** — `TryExtract` 가 이웃을 N→E→S→W 로 훑기 때문이라,
  같은 재료의 저장소를 두 칸에 붙이면 용량이 그대로 두 배가 된다(상한 9,216 을 넘기는 유일한 방법).
- **아이템 저장소는 개체 데이터(`ItemInstance`)를 받지 않는다** — 한 칸에 수천 개인데 인스턴스는 하나뿐이라
  그것이 사라질 때 전부가 사라진다. 상자는 칸이 40개라 그대로 받는다.
  판정은 `MachineInstance.AcceptsInstanceItems` 하나, 막는 자리는 셋(`StorageSlotUI.Accepts` ·
  `PipeNetworkManager.TryDeliver`·`Retarget`).
- ⚠ **파이프는 `Insert`/`Extract` 면으로만 저장소를 건드린다. `Default` 면은 아무 일도 하지 않는다.**
  일반 기계는 입력칸·출력칸이 방향을 정해 주지만 저장소는 한 칸이 둘을 겸해서, Default 를 양방향으로 두면
  **상자 두 개를 이으면 아이템이 영원히 왕복한다.** 규칙은 `PipeRouter.CanInsertInto`/`CanExtractFrom`
  **한 짝**에 있고, 경로 탐색은 레코드만 보는 `StorageAt` 으로, 배달·추출은 살아 있는 인스턴스로 같은 함수를 부른다.
  꺼낼 칸은 `PipeRouter.SourceSlots`(일반=출력칸 / 저장소=저장칸).
- **UI 역할은 `MachineUIRole.StorageSlot = 11`** 이고 `DefaultMachineUI` 가 **`InputSlot` 과 같은 목록**에 담는다
  (저장 칸이 입력 구간에 살기 때문). ⚠ **한 프리팹에 둘을 섞으면 인덱스가 겹친다** — 오류로 잡는다.
  빌딩블록은 `Prefabs/UI/Machine/StorageSlot.prefab`(`StorageSlotUI`), 팩토리 "요소 추가" 에 버튼이 있고
  `CreateNewLayout` 은 저장 블록이면 **10칸씩 줄바꿈**해 놓는다(40칸을 한 줄로 놓으면 화면 밖으로 나간다).

### 저장 네트워크 (3티어 · AE2 의 ME 같은 것)

**데이터 케이블로 이은 컨트롤러 1 + 드라이브 N + 터미널 N.** 셀에 담고 터미널로 꺼낸다.
**새로 만든 개념이 거의 없다** — 연결은 `PipeKind.Data`, 서브넷 분리는 렌치 `Cut`,
입출력 버스는 렌치 `Insert`/`Extract`, 한계는 **전력**이다(노션의 "채널 제한 대신 전력").

- **저장하지 않는다.** 구성·용량은 파생 상태고 `PipeNetworkManager.TopologyVersion` 이 바뀔 때만
  다시 계산한다(경로 캐시와 같은 규약). 셀 내용만 `StorageCellInstance : ItemInstance` 로 스택에 붙는다
  → **세이브 v12 그대로.** `ItemInstanceSerializer` 가 어셈블리를 훑어 자동 등록한다.
- ⚠ **전력은 캐시하면 안 된다.** 위상은 케이블·렌치를 건드릴 때만 바뀌는데 **전원은 매 프레임 바뀐다** —
  `StorageNetwork.Status` 와 `Cells()` 는 `IsPowered` 를 **물어볼 때마다** 다시 본다.
  Build 시점에 굳혔더니 컨트롤러가 먼저 켜지며 망을 캐시해 **드라이브가 영영 0/1** 이었다(실측).
- **`StorageNetworkBlock : MachineBlock`** + `role`(Controller/Drive/Terminal). 기계를 물려받는 것은
  프리팹 스폰·발자국·콜라이더·채굴·세이브·전력 링크를 공짜로 얻기 위해서다(`LightBlock` 과 같은 이유).
  ⚠ **blockId 문자열로 판정하지 말 것** — 이름을 바꾸는 순간 조용히 망에서 빠진다.
  `IsAlwaysOn = true` 라 조명과 같은 상시 소비 경로를 타고, **`IsRunning` 이 곧 "전기가 들어와 있는가"** 다.
  값: 컨트롤러 40/s · 드라이브 20/s(셀 6칸) · 터미널 5/s.
- **컨트롤러는 네트워크당 하나.** 0개면 `NoController`, 2개 이상이면 **그 망이 통째로 멈춘다.**
- **버스는 케이블 쪽 면**이다 — 별도의 버스 기계가 없다. `Insert` = 망 → 기계, `Extract` = 기계 → 망,
  **`Default` 는 아무 일도 안 한다**(저장 블록에서 상자 두 개가 왕복하던 것과 같은 규칙).
- **버스를 돌리는 것은 컨트롤러뿐이다**(`MachineInstance.Update` 의 상시 소비 분기 →
  `StorageNetwork.PumpBuses`). 망당 컨트롤러가 하나라 **프레임당 정확히 한 번** 돈다 —
  케이블이나 드라이브에서 돌리면 개수만큼 빨라져 밸런스가 배치 모양에 좌우된다.
  ⚠ **예산(`BusRate` 8개/초)은 버스마다 따로 준다.** 하나를 나눠 쓰면 목록에서 앞선 버스가 다 써
  뒤쪽이 영영 굶는다(실측: Extract 가 독차지해 Insert 가 한 개도 못 옮겼다).
- ⚠ **넣고 빼는 규칙의 정본은 `StorageNetwork.Insert`/`Remove` 하나다.** 터미널도 버스도 이것을 부른다 —
  두 벌로 갈리면 "터미널로는 들어가는데 파이프로는 안 들어가는" 상태가 된다.
  **이미 그 종류를 담은 셀부터** 채운다(빈 셀을 먼저 쓰면 종류 자리가 쪼개진다).
- **셀의 한도는 인스턴스가 아니라 `StorageCellItem`(에셋)** 에 있다 — 밸런스가 바뀌면 이미 만든 셀만
  옛 한도를 들고 다니면 안 되기 때문(`UpgradeModuleItem.valuePerUnit` 과 같은 규약).
  `1k 8종/4,096` · `4k 16종/16,384` · `16k 32종/65,536` · `64k 64종/262,144`, **`maxStack` 은 1**
  (`OnValidate` 로 못박았다 — 64로 두면 내용이 다른 셀 둘이 합쳐지며 한쪽이 사라진다).
  **종류 한도가 개수 한도보다 먼저 걸린다.**
- **터미널 UI 는 `NetworkContainer : IItemContainer`** 를 물려 `ItemSlot` 드래그앤드롭을 그대로 쓴다.
  ⚠ **그 칸은 진짜 저장소가 아니라 사본(view)** 이다 — `OnDrop` 이 사본을 고치면 끝에 부르는
  `NotifyChanged` 에서 **스냅샷과 비교해 셀에 되쓴다.** 교환(칸의 아이템 자체가 바뀜)도 다뤄야 하고,
  `SlotCapacity` 가 실제 여유보다 크면 "넣었다는데 셀에 없는" 아이템이 생긴다(그때 `LogError` 가 뜬다).
  프리팹은 `Chest_UI` 를 복사한 `StorageTerminal_UI`(40칸 = 한 페이지) 고, 페이지 버튼은
  **방식 ②(서브클래스 + `SerializeField`)** 다. 입력 칸 재바인딩은 `DefaultMachineUI.RebindInputs`.
- ⚠ **개체 데이터가 붙은 것은 버스로 안 들어간다**(도구·저장 셀). 셀은 `itemName` 만 세므로
  재질·내구도·내용이 통째로 사라진다.
- ⚠ **데이터 케이블 그림은 아이템 파이프 밴드를 임시로 공유한다**(`pipes.png` 가 3밴드로 꽉 찼다).
  전용 시트가 오면 `PipeSetup.AtlasNameFor` 한 줄과 에셋의 `atlas` 참조만 바꾼다.

### 도구 (커스텀 조합)
`ToolDefinition`(부품 칸 = 그림 레이어) + `ToolPartItem`(재질×종류) + `ToolItem`(완성품, maxStack 1)
+ 스택마다 붙는 `ToolInstance`(재질·내구도). 레시피는 `requiredTools` 로 요구하고 **소모가 아니라 내구도 차감**.
생성: `Tools/Project Craft/Tool/Generate Tool Assets`.

- **부품도 재질 슬롯으로 만든다** — `ToolPartRecipe : Recipe`(**종류 하나당 레시피 한 개**).
  조합대에서 재질 칸에 재료를 올리면 **그 재질의 부품**이 나온다(돌 → 돌 곡괭이 머리).
  재질마다 레시피를 복제하면 종류 5 × 재질 16 = **80개**가 되고 재질이 늘 때마다 5개씩 또 는다.
  지금 종류 5: `rod ×1` · `hammer_head ×2` · `pickaxe_head ×2` · `blade ×2` · `plate ×1`.
- **칼도 도구다**(2026-08-15). `칼` = `ToolDefinition`(자루 `rod` + 날 `blade`), 자루 그림은 망치·곡괭이와
  공유하고 날만 `{material}_blade` 로 갈아 끼운다. 옛 `돌 칼`·`철 칼`·`칼날` 아이템 6종은 지웠다
  (`ItemAliases` 가 잇는다). ⚠ **`stone_blade`·`iron_blade` 는 이름이 그대로 살아남았다** — 부품이 되면서
  타입만 바뀌었을 뿐이라 별칭이 필요 없다.
- ⚠ **`판`은 부품 구조를 빌렸을 뿐 자원이다.** 재질마다 하나라는 *구조*가 같아 `ToolPartItem(kind=plate)`
  으로 만들었지만, 도구에 꽂는 물건이 아니라 **온갖 레시피가 먹는 중간재**다(철판 하나를 20개가 쓴다).
  그래서 `RecipeCategoryAssigner` 가 **`kind == "plate"` 만 따로 자원 탭으로** 보낸다.
  `bronze`·`invar`·`silicon`·`metal` 판은 대응 `ToolMaterial` 이 없어 일반 `Items` 로 남았고 **압축기 전용**이다.
  ⚠ `stone_plate`·`wood_plate` 는 **그림이 없다**(돌 판은 만들 수는 있다).
- ⚠ **기존 아이템의 타입을 바꿀 때는 `m_Script` 만 바꿔 guid 를 지킨다.** 판 4종
  (`iron`·`copper`·`gold`·`silver`)을 부품으로 승격시킬 때 그랬다 — 지우고 다시 만들면
  **레시피 참조 44건이 통째로 끊긴다.**
  바꾼 뒤에는 `CompilationPipeline.RequestScriptCompilation()` 으로 **도메인 리로드를 먼저** 하고
  그다음에 `Register All Assets` 를 돌린다(§2 의 함정).
  기계 아이템 7종을 레거시 `Machine : Items` 에서 되돌릴 때도 같은 방식이었다(2026-08-20, `TODO.md §Z`).
- ⚠ **생성기의 존재 검사는 `itemName` 전역이다.** 예전에는 자기 폴더(`PartItemFolder`)만 봐서,
  다른 폴더에 있던 `iron_plate` 를 **하나 더 만들었다**(세이브 키가 겹친다).
- **목록에 뜨는 이름의 정본은 `Recipe.ListName`** 이다. `outputs[0]` 은 아이콘용 **견본**이라
  그대로 보여 주면 "돌 망치 머리" 로 읽혀 돌 것만 나오는 것처럼 보인다 —
  `ToolPartRecipe` 는 `kind`, `ToolRecipe` 는 `tool` 의 이름으로 덮는다.
- **재료 아이템 ↔ 재질의 정본은 `ToolMaterial.sourceItem` 하나다**(`iron → iron_ingot` · `stone → stone` ·
  `quartz → quartz_crystal`). 이름 규칙으로 추측하면 `iron_ingot` 과 `raw_iron_ore` 를 구별하지 못한다.
  ⚠ **`나무`는 비어 있다** — 게임에 나무 아이템이 없어서 나무 부품은 만들 수 없다(시작 도구는 돌이다).
  `ToolAssetGenerator` 가 **비어 있을 때만** 채우므로 손으로 바꾼 값은 보존된다.
- ⚠ **`Generate Tool Assets` 는 옛 이름 `Craft_Pickaxe` 를 다시 만들던 버그가 있었다** — 정본이
  `pickaxe`·`hammer`·`driver` 로 개명된 뒤에도 생성 경로가 그대로여서, 돌릴 때마다 같은 도구의 레시피가
  하나 더 생겨 서로를 가렸다. 지금은 정본 이름으로 만들고 옛 이름을 지운다.

- **채굴 도구 판정은 `ToolDefinition.canMineBlocks`**(곡괭이만 true). 곡괭이는 망치·드라이버와
  `ToolItem` 하나를 공유하므로 `WrenchItem` 처럼 **타입으로는 구분되지 않는다** — 에셋 데이터가 정본이라
  문자열 비교도 씬 참조도 없다(재료 티어별 제한도 나중에 같은 자리에 필드로 붙는다).
- **곡괭이는 벽에만 요구한다.** 기계·파이프까지 막으면 곡괭이가 부러졌을 때 이미 지은 공장을 못 뜯어 갇힌다.
  벽 한 칸당 내구도 1, 0 이 되면 `stack.Clear()` — `RecipeSolver.ConsumeTools` 와 **같은 규약**이라
  도구가 없어지는 방식이 한 가지뿐이다.
- ⚠ 손에 든 것을 볼 때는 `Inventory.GetSelectedStack()` 을 쓴다. `GetSelectedItem()` 은 선택 칸이 없을 때
  (`ConsumeSelectedItem` 이 -1 로 만든다) **예외를 던진다**.

### UI
- `UIManager` 가 이름으로 패널을 켜고 끈다(`AddUI` → `OpenUI`/`CloseUI`, `isAnyUIOpen`).
  **`AddUI` 를 열 때마다 다시 부른다** — 등록이 빠지면 `OpenUI` 가 조용히 실패해 영구히 못 연다.
- ⚠ **오브젝트는 에디터(프리팹)에서 저작하는 것이 기본이다.** 코드로 `new GameObject(...)` 하는 것은
  **개수·모양이 월드 데이터나 플레이어 행동에 달렸을 때만** 쓴다(청크별 그림자 사각형 · 배치된 기계 ·
  풀링되는 미리보기 칸처럼). 항상 정확히 하나인 것은 프리팹에 놓고 참조는 `[SerializeField]` 로 명시한다.
  static 홀더를 두면 "누가 언제 채우나" 라는 초기화 순서 문제가 그대로 남는다.
  - **이유는 아래 §UI 버튼 ①과 같다** — 코드로 만든 것은 씬에서 위치·크기를 못 옮기고 인스펙터에 안 보인다.
  - **"씬 파일을 건드리지 않으려고" 라는 옛 명분은 성립하지 않는다.** 두 씬이 `GameRig` 한 장을 쓰고
    PrefabInstance override 가 transform·이름뿐이라, **프리팹만 고치면 두 씬에 그대로 전파된다**(실측).
  - **이관 대상 레거시**(아직 코드로 짓는다 · `TODO.md §M-3`): `CommandConsole` · `ItemBrowser` ·
    `TooltipUI` · `PowerLinkMode` 패널 · `DefaultMachineUI.BuildPowerLinkButton` ·
    `PipeNetworkManager` 의 `Pipes` 타일맵 · `PowerLinkMode` 의 오버레이 타일맵 ·
    `PlacementPreview` 루트 · `MapGenerator` 의 `Placeables`/`Drops` 컨테이너.
    `TooltipUI` 가 `PersistAcrossScenes => false` 를 강제당하는 것도 코드로 지어서 생긴 문제다.
- **기계 UI 프리팹은 여러 기계가 나눠 쓴다** — `DefaultMachineUI` 가 자식의 `MachineUIElement` 를 역할별로
  긁어모아 **남는 칸은 끄고 모자라면 경고 후 클램프**하므로, N칸짜리 한 장이 N칸 이하 전부를 감당한다
  (전력바도 `isUseEnergy` 가 아니면 자동으로 꺼진다). **업그레이드 칸과 유체 바만은 조용히 클램프**한다 —
  기존 프리팹 11장에 아직 요소가 없어서, 경고를 내면 기계를 열 때마다 11종 × 매번 로그가 쏟아진다.
  실제 공유: `Furnace_UI` 3종 · `Generator_UI` 2종 ·
  `CraftingTable_UI` 5종(조합대 2 + 재단 3) · `Extractor01_UI` 12종. 프리팹 이름이 사용자 중 하나만 가리키는 것은 정상이다
  (`MachineUIFactoryWindow` 가 `{기계 이름}_UI.prefab` 로 저장하므로 **이름을 바꾸면 다음 저장 때 파일이 하나 더 생긴다**).
- ⚠ `MachineUIHost.Resolve` 의 캐시 키는 프리팹이 아니라 **`blockId`** 다 — 한 프리팹을 12종이 공유해도
  인스턴스는 12개 생긴다(열어 볼 때만 지연 생성).
- 버튼을 붙이는 방법은 셋이다 — ① **코드 생성**(`BuildPowerLinkButton`. 공유 기본 패널에 프리팹을 못 고칠 때) ·
  ② **서브클래스 + `SerializeField`**(`CraftingTableUI.craftButton`) · ③ **`MachineUIElement` 역할**
  (`ManualButton` · `CoreUpgradeButton`. 프리팹에서 위치를 잡고 싶을 때). 어느 쪽이든
  **`Open` 마다 `onClick.RemoveAllListeners()`** — 안 하면 기계를 열 때마다 리스너가 쌓여
  한 번 눌렀는데 예전에 열었던 기계까지 함께 돈다.
  ⚠ **①은 되도록 쓰지 않는다.** 코드로 만든 것은 씬에서 위치·크기를 못 옮기고 팩토리 검증기에도 안 잡힌다 —
  코어 업그레이드 칸·버튼이 그래서 ①에서 ③으로 옮겨졌다(`BuildCoreUpgradeUI` 삭제).
  **한 프리팹을 여러 기계가 나눠 써도 ③이 맞다** — 안 쓰는 기계에서는 베이스가 알아서 꺼 준다
  (칸은 `upgradeSlotCount`, 버튼은 `CraftingTableUI` 가 `acceptsTierUpgrade` 로).
- **비활성 오브젝트는 레이아웃 재계산이 통째로 무시된다.** 켠 다음에 짓고 `LayoutRebuilder.ForceRebuildLayoutImmediate`.
- 툴팁은 `TooltipUI.Show(Func<string>)` 로 넘겨야 실시간 갱신된다(문자열을 넘기면 고정).
- **"무엇을 몇 장으로 그리는가" 의 정본은 `ItemIconLayers.Collect` 하나다.** 우선순위는
  ① 개체 데이터(도구의 자루+머리) → ② 채워진 그릇(빈 그릇 그림 + `Items.fluidOverlay` 를 `FluidColors` 색으로)
  → ③ `item.Icon` 한 장. **`ItemIconView`(UI 슬롯)와 `DroppedItem`(필드 드랍)이 이것 하나를 함께 본다** —
  예전엔 폴백 네 줄을 각자 복사해 갖고 있어, 규칙을 하나만 고치면 "슬롯에선 파란 물인데 바닥에 떨어뜨리면 회색"
  이 됐다. `ItemIconView` 는 이제 **어떻게 그리는가만** 안다(겹침 Image 생성·재사용).
  ⚠ **오버레이 그림은 유체가 아니라 "빈 그릇" 아이템이 갖는다**(`bucket.fluidOverlay`). 같은 물이라도
  양동이와 유리 용기는 다르게 그려져야 하는데, `FluidDefine` 에 두면 그릇이 늘 때마다 유체 8개를 다 고쳐야 한다.
  **반드시 흰색 마스크로 그릴 것** — 색은 `FluidColors.Of(fluidId)` 곱셈이 정한다. 오버레이가 없는 그릇
  (유리 용기)은 **조용히 ③으로 떨어진다**(경고를 내면 그 아이템을 볼 때마다 로그가 쏟아진다).
- 채워진 그릇의 `displayName` 규약은 **`{빈 그릇}({유체})`** — `양동이(물)` `유리 용기(산성 용액)`.
  `itemName`(세이브 키)은 그대로고, 옛 한글 이름은 `ItemAliases`(`물 → water` 등)가 이미 잇는다.
- 한글 폰트: `Assets/TextMesh Pro/Fonts/Maplestory Bold SDF.asset`, `Tools/Project Craft/Font/Apply Korean Font To All`.

### 입력 (`InputActionManager`, 코드로 만든 액션맵)
`WASD` 이동 · 좌클릭 채굴(홀드) · 우클릭 Use · `E` 상호작용 · `I` 인벤토리 · `1~0` 핫바 ·
`Enter` 콘솔 · `P` 아이템 목록. 텍스트 입력 중엔 `SetPlayerInputEnabled(false)`.
**입력을 끄는 UI 는 자기 토글 키로 못 닫는다** — 콘솔은 ESC, 아이템 목록은 입력을 끄지 않는다.

- ⚠ **`EventSystem.IsPointerOverGameObject()` 를 입력 콜백 안에서 부르면 안 된다.** 콜백은 UI 레이캐스트보다
  먼저 도는 구간이라 **지난 프레임 값**이 돌아오고(Unity 가 경고를 낸다), 이번 프레임에 뜬 패널 위에서 누른
  우클릭이 월드로 샌다. 그래서 `PlayerInteraction` 의 우클릭은 **콜백에서 `usePending` 만 세우고
  판정은 `Update` 로 미룬다**(`isPointerOverUI` 를 갱신한 직후 `PerformUse`).
  ⚠ `PowerLinkMode.IsActive` 조기 return 분기에서 **`usePending` 을 반드시 지운다** — 안 지우면
  전송 모드를 끄는 순간 묵은 클릭이 한 번 터진다.
- **슬롯 드래그: 좌클릭 = 전량 · 우클릭 = 올림 절반.** 씬의 UI 모듈이 `InputSystemUIInputModule` 이라
  우클릭에도 드래그·드롭 이벤트가 오고 `eventData.button` 으로 갈린다. 월드 우클릭(배치)과는
  `PlayerInteraction` 의 `IsPointerOverGameObject()` 가드가 이미 갈라 준다.
- ⚠ **`ItemSlot.draggedAmount` 는 `draggedFrom` 보다 오래 살아남으면 안 된다.** 지우는 자리는 넷
  (`OnBeginDrag` 의 조기 return · `OnDrop` 의 끝 · `OnEndDrag` · `OnDisable`) — 남겨 두면
  **다음 드래그가 옛 개수를 물고 간다.**
- **데이터는 `OnDrop` 에서만 움직인다.** 절반 집기는 "지금 쪼개기" 가 아니라 요청량을 기억해 두는 것이라,
  "우클릭 절반" 과 "1칸 저장소에서 maxStack 만큼만 꺼내기" 가 **같은 분기 하나**로 처리된다.
  일부만 옮길 때는 **교환하지 않는다**(든 것보다 많이 돌아오는 교환은 뜻이 성립하지 않는다).
- **커서를 따라가는 그림은 슬롯의 아이콘이 아니라 별도의 고스트**(`ItemSlot.ghostIcon`, static 하나를 돌려 쓴다).
  예전에는 `iconImage`·`countText` 를 캔버스로 **옮겨** 썼는데, 그러면 드래그 도중 슬롯이 통째로 비어 보여
  **절반만 집었을 때 남은 절반이 안 보였다.** 이제 `Refresh` 가 `count - draggedAmount` 를 그리므로
  슬롯에 남는 몫이 계속 보인다(전량을 집으면 빈 칸으로 보인다).
  - 고스트는 **`rootCanvas` 의 맨 뒤 + `raycastTarget = false`** — 켜 두면 고스트가 드롭을 가로챈다.
  - **아이콘·숫자의 자리는 드래그를 시작한 슬롯에서 매번 베낀다**(`CopyPlacement`). 숫자 위치가
    프리팹마다 달라서(`slot` 은 `(-5, -32.1)`, `MachineSlot`·`hotBarSlot` 은 `(26.6, -14.5)`)
    좌표를 코드에 박으면 어느 한쪽에서 반드시 어긋난다. 앵커·부모 계층도 제각각이라
    `anchoredPosition` 을 그대로 베끼지 않고 **월드 좌표 차이 ÷ 루트 캔버스 배율**로 잰다.
  - 옮기지 않게 되면서 `pendingRestore`·`RestoreDragVisuals`·`OnEnable` 복구 경로가 **통째로 사라졌다**
    (비활성화 중엔 `SetParent` 가 거부돼 `OnEnable` 까지 미뤄야 했던 그 코드다).
  - ⚠ 검증할 때: **에디트 모드에서는 `OnDisable` 이 안 불린다**(`ExecuteAlways` 가 없어서).
    "창을 닫으면 드래그가 정리되는가" 는 플레이 모드이거나 직접 호출해야 확인된다.

### 애니메이션 (플레이어 · `Assets/Asset/Player/Female/`)
- **이동·상태 전이에 `Any State` 를 쓰지 않는다.** Any State 전환에는 `Has Exit Time` 이 **아예 없어**
  "클립이 끝날 때까지 기다려"를 표현할 수 없고, `Can Transition To Self` 와 겹치면 조건이 참인 동안
  같은 상태로 계속 재진입해 **클립이 앞부분만 맴돈다**(눈 깜빡임이 재생되지 않던 원인).
  Any State 는 피격·사망처럼 **어디서든 끼어드는 것** 전용이다.
- 방향은 **블렌드 트리 한 상태**로 둔다 — 방향을 바꿔도 같은 상태 안에서 섞이므로 클립이 재시작되지 않고,
  대각선도 조건 분기 없이 처리된다.

```
Idle = BlendTree(SimpleDirectional2D, moveX·moveY) ──[blink]──▶ Blink ──[Exit Time 1.0]──▶ Idle
   │  ▲   Leftward(-1,0) Backward(0,-1) Forward(0,1) Rightward(1,0)   (Blink 클립은 루프 금지)
   └──[moving]──▶ Walk = BlendTree(SimpleDirectional2D, moveX·moveY)   ← 좌표가 Idle 과 같아야 한다
                    Leftward(-1,0) Backward(0,-1) Forward(0,1) Rightward(1,0)
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
- **`Facing` 이 방향의 정본이다**(`PlayerAnimation.Facing`, **월드 방향**). 입력이 0 이 되어도
  `moveX/moveY` 에 **0 을 쓰지 않고 마지막 방향을 남긴다** — Idle 도 블렌드 트리라 0 을 쓰면
  네 클립이 같은 무게로 섞여 어느 쪽도 아닌 포즈가 된다. 플레이어 빛도 같은 값을 본다
  (`PlayerLightAim`) — 두 곳에서 따로 방향을 정하면 "스프라이트는 왼쪽인데 빛은 오른쪽" 이 된다.
  ⚠ **애니메이터에 넣을 때만 `moveY = -Facing.y` 로 뒤집는다.** 빛은 월드 방향을 그대로 쓴다.
- ⚠ **방향별 Idle 클립 3장은 걷기 클립의 2프레임을 빌린 임시 포즈다**
  (`FemaleIdle{Backward,Leftward,Rightward}`). 옆·뒤 정지 그림이 스프라이트 시트에 아예 없다 —
  `Female_normal1~5` 는 정면 하나뿐이다. 전용 아트가 오면 **이 3장만 갈아 끼우면** 컨트롤러·코드는 그대로다.
- ⚠ **Blink 트리거는 정면일 때만 쏜다**(`facing.y < -0.5f`). 깜빡임 그림이 정면 것뿐인데
  `FemaleBlink.anim` 이 빈 클립이고 상태가 Write Defaults 라, 옆·뒤로 서 있다가 Blink 로 넘어가면
  **스프라이트가 프리팹 기본 그림으로 튄다.** 정면 Idle 은 `FemaleNormal` 클립 자체가 5프레임으로
  깜빡이므로 트리거가 없어도 손해가 없다(= Blink 상태는 사실상 죽어 있다).

### 카메라 · 픽셀
- 전 스프라이트가 **PPU 32 · Point 필터 · 압축 없음**으로 통일돼 있다. **새 아트도 반드시 맞춘다** —
  하나만 어긋나도 그 오브젝트만 흐려진다.
- **물리로 움직이는 오브젝트는 `Rigidbody2D.Interpolate` 를 켠다.** `FixedUpdate`(50Hz)와 화면 주사율이
  어긋나 생기는 저더는 눈에 "흐리다"로 보인다. 플레이어는 `MovePosition` 을 쓰므로 특히 필요하다.
- 카메라는 `orthographic size 4`(세로 8유닛 = 원화 256px) · `UIs` 캔버스는 **`Screen Space - Overlay`**.
- ⚠ **UI 배율은 화면 세로에만 맞춘다** — `CanvasScaler` = `Scale With Screen Size` · 기준 `2560×1440` ·
  **`Match Width Or Height` · `Match = 1`(Height)**. 카메라가 **세로 8유닛 고정**이라 월드 배율이
  `화면 세로 ÷ 8` 인데, UI 가 가로도 보면(= `Expand` 나 `Match 0.5`) **가로가 좁은 화면에서만
  UI 가 월드보다 작아진다.** 실측(2026-08-20, UI:월드 비):

  | | 1920×1080 | 1920×1200 | 2256×1504 | 2560×1080 |
  |---|---|---|---|---|
  | `Match 0.5`(옛 지상) | 5.556 | 5.270 | 5.103 | 6.415 |
  | `Expand`(옛 지하) | 5.556 | 5.000 | 4.688 | 5.556 |
  | **`Match 1`(지금)** | **5.556** | **5.556** | **5.556** | **5.556** |

  ⚠ **정본은 `GameRig` 프리팹 하나다.** 예전에는 MapTest 가 `m_ScreenMatchMode` 를 오버라이드해
  **지상과 지하의 UI 크기가 서로 달랐다** — 오버라이드는 걷어 냈다.
  ⚠ 세로에 맞추면 캔버스 **가로**가 비율에 따라 변한다(기준 단위로 16:9=2560 · 16:10=2304 · 3:2=2160 ·
  4:3=1920 · 21:9=3413). **그래서 앵커가 곧 규칙이다** — 아래 항목.

- ⚠ **한 덩어리로 보여야 하는 것은 같은 기준점(앵커)에 매단다.** 캔버스 가로가 비율마다 변하므로,
  서로 다른 모서리에 매달린 요소는 **화면 비율이 바뀌는 순간 사이 간격이 벌어진다.**
  2026-08-20 에 실제로 셋이 어긋나 있었다(전부 `GameRig` 프리팹에서 고쳤다):

  | | 예전 | 지금 |
  |---|---|---|
  | `trashCanSlot` | 패널 **왼쪽 아래** 기준 `x=+2422` | **오른쪽 아래** 기준 `(-138, 550)` |
  | `trashCanText` | 패널 **한가운데** 기준 `x=+1144` | **오른쪽 아래** 기준 `(-136, 461)` |
  | `InventoryHotBar` | 좌우 스트레치(가로 = 캔버스−869) | **아래 한가운데** · 고정 `1690×148` |
  | `MachinePanel` | 좌우 스트레치(가로 = 캔버스−600) | **한가운데** · 고정 `1960×854` |

  ⚠ **스트레치 패널 안에 절대 좌표로 놓인 자식**이 제일 위험하다 — 부모 폭이 변하면 자식이 통째로 밀린다
  (핫바 슬롯 10칸과 기계 UI 내용물이 그랬다). 늘어나야 하는 것(배경 띠·목록)만 스트레치로 두고,
  **칸이 늘어선 것은 고정 크기 + 한 점 앵커**로 둔다.
  실측(1366×768 ~ 2560×1080 여섯 해상도): 쓰레기통은 오른쪽 끝에서 138 · 글씨는 슬롯에서 (2,−89) ·
  핫바 1번은 화면 중앙에서 −767 · 기계 패널 폭 1960 · 인벤 1번은 왼쪽에서 100 — **전부 고정**.
  **보이는 월드의 가로 범위가 비율마다 달라지는 것은 그대로다** — 그걸 고정하려면 레터박스가 필요한데,
  그 대가는 아래 픽셀 퍼펙트 항목에 적어 두었다.

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

### 조명 (URP 2D · 세계 전체가 동굴이다)

**메인 맵도 설정상 지하**라 낮/밤이 없고 지상·지하를 밝기로 가르지 않는다. 전역 광원을 어둡게 깔고
플레이어·횃불·전등이 그것을 밀어낸다.

- **배선은 원래부터 다 되어 있었다** — 2D Renderer 활성, 스프라이트 **전부 `Sprite-Lit-Default`**,
  `GameRig` 에 Global Light 2D. 흰색 intensity 1 이라 아무 효과가 없었을 뿐이다.
  그래서 **머티리얼 마이그레이션이 필요 없었다.**
- **밝기의 정본은 `LightingPalette`**(static 표, `ExtractionTable`·`FluidColors` 와 같은 꼴).
  `MapLighting`(**GameRig 프리팹에 저작**, `Global Light 2D` 오브젝트에 붙어 있다)이 그 값을
  Global Light 와 **플레이어 빛 두 개**(`TestPlayer/PlayerLight`·`PlayerAura`)에 넣는다 —
  ⚠ **씬에 값을 넣지 않는다.** 두 씬이 어긋나고 팔레트와 정본이 갈라진다.
  ⚠ Global Light 는 **찾아서 값만 바꾼다**(새로 만들면 두 개가 겹쳐 두 배로 밝아진다).
- ⚠ **월드 UI 오버레이는 조명을 받으면 안 된다.** 정렬 레이어가 `Default` 하나뿐이라 order 로는 못 뺀다 →
  **`OverlayMaterial`(공유 Unlit) 을 지정**한다. 대상 넷: 커서 윤곽선 · 파이프 면 막대 ·
  PowerLink 오버레이 · 설치 미리보기. 정렬 레이어를 나누는 쪽은 **모든 조명마다** Target Sorting Layers 를
  맞춰야 해서 훨씬 비싸다. 지정하지 않으면 `Renderer2DData.m_DefaultMaterialType = Lit` 때문에
  자동으로 Lit 이 되어 어두운 곳에서 안내 표시가 통째로 사라진다.
- **조명 배치물은 `LightBlock : MachineBlock`**. 기계를 물려받는 것은 순전히 공짜로 얻는 것 때문이다
  (프리팹 스폰·발자국·콜라이더·채굴·세이브·청크 수명주기·전력 링크). 새 배치물 *종류*를 만들면
  분기를 여섯 곳에 더해야 한다. **빛 값의 정본은 SO** 고 `LightEmitter` 가 프리팹의 `Light2D` 에 베껴 넣는다.
- ⚠ **`MachineBlock.IsAlwaysOn` 이 없으면 전등은 영원히 안 켜진다.** `Tick` 은 `activeRecipe == null` 이면
  첫 줄에서 되돌아가 `ConsumeEnergy` 에 닿지 않는다("놀고 있는 기계는 전기를 먹지 않는다"가 의도된 규칙) —
  상시 소비는 `Update` 의 **AutoProcess 조기 return 앞** 분기가 따로 맡는다.
  `OpensUI` 는 슬롯 0개짜리가 빈 패널을 띄우지 않게 한다.
- **플레이어 빛은 둘이다** — 보는 쪽으로 나가는 **원뿔 빔**(`PlayerLight`)과 발밑을 덮는
  작은 **원형 오라**(`PlayerAura`). 빔 하나만 두면 옆·뒤가 환경광(0.18)만 남아 방향 감각을 잃고,
  원형 하나만 두면 어디를 보든 똑같이 밝아 탐험에 방향이 안 생긴다.
  ⚠ **콘의 중심은 광원의 로컬 +Y 다**(URP 규약 — `Light2DLookupTexture.cs:104` 가 각을 `Vector2.down`
  기준으로 굽고 `Light2D.shader:193` 이 그것을 뒤집어 쓴다). 그래서 `PlayerLightAim` 이
  `Atan2(f.y, f.x) - 90°` 를 쓴다. **-90 을 빼먹으면 보는 쪽의 왼쪽 90도를 비춘다.**
  ⚠ **각도·회전을 씬이나 프리팹에서 정본으로 삼지 않는다.** 넓이·밝기는 `LightingPalette`(`Beam*`/`Aura*`),
  방향은 `PlayerLightAim` 하나다 — 실제로 콘 설정이 **MapTest 오버라이드로만 있어 지하에서는 원형**이던 적이
  있다. 프리팹의 각도·세기는 에디트 모드 미리보기일 뿐 `MapLighting.Apply()` 가 Start 에서 덮는다.
  ⚠ **`normalMapQuality` 만은 프리팹에 저작해야 한다** — 읽기 전용 프로퍼티다(`Light2D.cs:334`).
  빔 1(Fast) · 오라 2(Disabled: 부드러운 채움광이라 노멀맵으로 갈릴 방향이 없다).
- **벽 그림자 = `ChunkShadowCasters`**(**GameRig 프리팹의 `Map` 자식으로 저작**. 청크별 사각형만 런타임).
  청크마다 벽 칸을 **그리디 메싱**해 사각형으로 묶는다 —
  세계가 대부분 꽉 찬 돌이라 **통째로 벽인 청크는 사각형 1개**다(실측: 25청크 6400칸 → 캐스터 29개).
  ⚠ **캐스터 오브젝트에 `SpriteRenderer` 를 붙이면 안 된다.** `ShadowCaster2D.Awake` 는
  `shapePath` 가 비어 있으면(= `AddComponent` 로 갓 만든 것은 언제나 비어 있다)
  **스프라이트에서 모양을 뽑는 provider 를 먼저 고르는데**, 그 provider 는 `drawMode` 가 Simple 이면
  스프라이트 메시를 쓰고 1×1 스프라이트로는 **정점 0개짜리 빈 그림자 메시**가 나온다 —
  캐스터는 29개 다 서 있는데 **빛이 벽을 전혀 안 막는다**(실측으로 이 함정에 걸렸다).
  게다가 그 경로는 `#if UNITY_EDITOR` 안에만 있어 **에디터와 빌드가 다르게 동작**한다.
  ⚠ 렌더러가 없으면 provider 가 안 잡혀 `ShapeEditor` 로 떨어지고 `Awake` 가 `Bounds(position, one)` 에서
  **로컬 단위 사각형**을 만든다. 그래서 **배율 1 로 만들어 모양을 굳힌 뒤 `localScale = (w,h,1)` 로 늘린다**
  (순서를 바꾸면 배율만큼 나눠져 결국 1×1 그림자가 된다). 덕분에 **풀에서 꺼내 쓸 때 위치·크기만 바꾸면 된다.**
  청크 부모의 `CompositeShadowCaster2D` 는 이음매를 없앤다.
  검증은 캐스터 개수가 아니라 **`ShadowCaster2D.mesh.vertexCount > 0`** 로 한다.
  훅 셋: `RenderChunk`(생성) · `UnLoadChunk`(반납) · `RefreshTile`(그 청크만 재생성).

#### 노멀맵 (기계에만 · Secondary Texture)

기계 시트 8장이 `_NormalMap` **Secondary Texture** 를 갖고 있어, 점광원 근처에서 면이 갈려 입체감이 난다.

- ⚠ **`NormalMapQuality` 는 `Disabled = 2` · `Fast = 1` · `Accurate = 0` 이다**(`Light2D.cs:69`).
  숫자가 클수록 좋아 보이지만 **2 가 꺼짐**이다 — 예전에 프로젝트의 Light2D 4개가 전부 2 라
  노멀맵을 붙여도 아무 효과가 없었다. 지금 1(Fast)인 것: 플레이어 빔 · 전등 · 횃불.
- **전역광(LightType 4)에는 효과가 없다** — 위치가 없어 갈릴 방향이 없다. 그래서 2(Disabled) 그대로 둔다.
  즉 **입체감은 플레이어·조명 근처에서만** 보이고 맨 어둠은 평평하다(의도).
- ⚠ **시트를 재슬라이스하지 않는 유일한 길이 Secondary Texture 다.**
  `TextureImporter.secondarySpriteTextures` 는 **텍스처 단위** 설정이라 `SpriteRect.spriteID` 를
  건드리지 않는다(§2 의 재슬라이스 함정과 정면으로 어긋나지 않는다). 이름은 정확히 **`_NormalMap`**
  이어야 한다 — `Sprite-Lit-Default.shader:7` 의 프로퍼티 이름이다.
- 생성 툴 **`Tools/Project Craft/Art/기계 노멀맵 생성`** · 배선 **`… /기계 노멀맵 배선`**
  (`Editor/NormalMapGenerator.cs`). 원본 PNG 바이트를 직접 읽어 굽기 때문에 **원본의 `isReadable` 을
  건드리지 않는다.** 알파 경계에서 기울기가 폭발하지 않게 마스크 밖 이웃은 자기 픽셀로 클램프한다
  (안 하면 모든 기계에 흰 테두리가 생긴다).
- 노멀 텍스처 임포트 6개 값은 반드시: `NormalMap` · **`convertToNormalmap = false`**(이미 노멀로 구웠다) ·
  `sRGB off` · `Point` · 무압축 · `mipmap off`.
- **손그림으로 바꿀 때는 `Normals/{이름}_n.png` 를 덮어쓰기만 하면 된다** — 배선은 그대로다.

### 사운드 (지금 3종 · `Assets/Asset/Sound/`)

**재생기를 둘로 나눈다 — 루프 하나, 단발 하나.** 클립의 성질이 달라서지 계층을 나누려는 것이 아니다.

| | 무엇 | 어디 | 클립 |
|---|---|---|---|
| **`PlayerFootsteps`** | 걷는 동안 자기 `AudioSource` 를 `loop` 로 켜고 끈다 | `GameRig/TestPlayer` | `WalkOnStone` `WalkOnDirt` |
| **`SfxPlayer`** | 이름 붙은 단발음(`PlayOneShot`). 지금은 `craftSound` 하나 | `GameRig/SfxPlayer` | `CraftSound` |

- ⚠ **걷기 클립은 한 걸음이 아니라 여러 걸음이 든 루프다**(실측: WalkOnStone 2.54초에 8걸음,
  WalkOnDirt 2.10초에 6걸음, 사이는 무음). 걸음마다 `PlayOneShot` 하면 **8배로 겹친다** —
  그래서 발소리만 루프 재생이다. 한 걸음짜리 클립으로 바꾸는 날에는
  `PlayerFootsteps` 의 재생 규칙만 갈아 끼우면 되고 아래 정본 둘은 그대로다.
- **어느 바닥이 어떤 소리인지는 `MainBlock.footstepSound` 하나가 정한다**(`MainBlock.fluid` 와 같은 자리).
  static 표로 둘 수 없는 이유는 표에서 `AudioClip` **에셋을 가리킬 수 없어서**다(`FluidColors` 가
  static 인 것은 값이 `Color` 라서). 참조형이라 **소리를 안 준 지형 10종은 자동으로 null = 무음**이다.
  지금 값이 든 것은 둘뿐: `floor:stage1`(돌 바닥) → `WalkOnStone` · `floor:dirt`(흙) → `WalkOnDirt`.
  `UndergroundPalette.FloorIdFor` 가 **같은 두 id** 를 쓰므로 지하방도 공짜로 따라온다.
- **"이 칸의 발소리" 의 정본은 `WorldMap.FootstepAt(cell)` 하나다.**
  ⚠ **`FluidAt` 과 한 군데가 일부러 다르다 — 오버레이가 있으면 거기서 끝난다.** 유체는
  "오버레이에 유체가 없으면 밑의 바닥을 본다" 가 맞지만, 발소리에 같은 폴백을 두면
  **물웅덩이를 밟을 때 흙 소리가 난다.** 웅덩이·지하 입구는 바닥을 덮고 있으니 소리도 그쪽이 정한다
  (2026-08-19 사용자 결정: 물 위는 무음). 첨벙 소리가 생기면 `OverlayWater` 에셋에 클립만 꽂으면 된다.
- ⚠ **`SfxPlayer` 를 부를 때 `Singleton.Instance` 를 쓰면 안 된다.** 게터는 못 찾으면 빈 `GameObject`
  를 만들어 버려 **클립도 `AudioSource` 도 없는 유령 재생기**가 서고, 진짜 재생기가 나타나면
  그쪽이 중복으로 지워진다. 정적 진입점 `SfxPlayer.PlayCraft()` 가 **`InstanceIfAlive`** 를 쓴다.
- **조합음은 성공했을 때만 운다.** `CraftingTableUI` 의 제작 세 갈래(`Craft`·`CraftTool`·`CraftToolPart`)가
  전부 **`bool` 을 돌려주고** 재생은 `CraftSelected` **한 곳**이 한다 — 셋 다 "재료 부족·자리 없음" 이면
  조용히 되돌아가므로, 버튼 `onClick` 에 소리를 걸면 **아무것도 안 만들어졌는데 울린다.**
- **기계 완성음의 게이트는 `MachineInstance.boundUI` 하나다**(완성 블록, 재료 소모 직후).
  "이 기계의 창이 지금 떠 있는가" 의 정본이 그것이라 화면 밖 공장 수십 대가 함께 울지 않는다.
  수동 기계도 `ManualStep → Tick` 으로 같은 자리를 지난다.
  ⚠ **최소 간격은 두지 않았다**(2026-08-19 사용자 결정) — 자원 생성기 UI 를 열어 두면
  초당 1번, 속도 모듈을 채운 강화형이면 **초당 10번** 겹친다. 거슬리면 `SfxPlayer.PlayCraft` 에
  한 줄만 넣으면 되고 **호출부는 안 바뀐다**(그래서 재생을 한 곳에 모았다).
- `AudioSource` 는 **`spatialBlend = 0`(2D) · `playOnAwake = false` 로 프리팹에 저작**한다 —
  임포터의 `3D` 플래그는 새 소스의 기본값일 뿐이라 여기서 못박아야 어긋나지 않는다.
  `AudioListener` 는 `GameRig` 의 Main Camera 에 있다.
- 임포트: **`preloadAudioData = true`**(끄면 Vorbis 를 첫 재생 때 풀어 눈에 띄게 늦다) ·
  DecompressOnLoad · Vorbis. 원본이 88.2kHz 인 두 개도 **Unity 가 48kHz 로 디코드**하므로
  샘플레이트를 따로 손댈 것이 없다(실측).
- **볼륨은 두 컴포넌트의 직렬화 필드뿐이다.** 설정 UI·마스터 볼륨·믹서는 없다 — 소리가 셋뿐이라
  지금 만들면 쓰이지 않는 뼈대만 남는다.

### 공용 표시 에셋 (`Assets/Asset/Common/`)

크기를 `localScale` 로 내는 표시물(설치 미리보기 · 파이프 면 막대 · 벽 그림자 · 전력 오버레이)이 함께 쓴다.
예전에는 네 곳이 똑같은 흰 점을 각자 코드로 만들고 있었다.

| 에셋 | 쓰는 곳 |
|---|---|
| `White1x1.png` | 미리보기 칸 · 파이프 면 막대 (⚠ **벽 그림자는 안 쓴다** — 위 조명 항목의 provider 함정) |
| `White1x1Tile.asset` | `PowerLinkMode` 오버레이(타일맵이라 `Sprite` 가 아니라 `Tile` 이 필요하다) |
| `OverlayUnlit.mat` | 위 셋 + 커서 윤곽선. **조명을 받지 않게** 한다 |

- ⚠ **`White1x1.png` 의 PPU 는 1 이다. 프로젝트 전역 규칙(PPU 32)의 의도적 예외다.**
  크기를 전부 `localScale` 로 내기 때문에 `1픽셀 = 1유닛` 이어야 한다 —
  32 로 "고치면" **파이프 면 막대·설치 미리보기·벽 그림자가 한꺼번에 1/32 로 쪼그라든다.**
- ⚠ **`Shader.Find` 로 머티리얼을 만들지 않는다.** 빌드에서 셰이더가 스트립되면 **런타임에만 조용히 깨진다.**
  실제 `.mat` 에셋을 참조하면 셰이더가 반드시 빌드에 포함된다.
- 참조는 전부 `[SerializeField]` 다 — `MapGenerator`(미리보기·파이프로 넘겨 준다) · `PowerLinkMode` ·
  `ChunkShadowCasters`. 커서 윤곽선은 **`OutLine` 타일맵에 머티리얼을 직접 지정**했다
  (그 자리만 `Awake` 라 코드로 넣으면 초기화 순서에 기대게 된다).

### 정렬 순서 (한 레이어, sortingOrder)

**2026-08-13 에 배율이 `(옛값 + 10) × 10` 으로 바뀌었다.** 사이에 열 칸씩 여유를 두려는 것이므로
**새 값은 반드시 10 단위로** 넣는다. 옛 한 자릿수 값(0~9)을 그대로 쓰면 바닥(100)보다 아래라
**화면에서 통째로 사라진다** — 실제로 파이프 면 막대(5)와 PowerLink 오버레이(6)가 그렇게 안 보이고 있었다.

| 값 | 무엇 | 어디가 정본 |
|---|---|---|
| `100` | Blocks · Floor | 씬(GameRig) |
| `110` | FloorTexture | 씬 |
| `115` | **FloorOverlay** — 바닥 위에 겹치는 것(지하 입구 · 웅덩이) | 씬(GameRig) |
| `120` | 기계 · 파이프 · 작물 · WallBottomTexture | 코드(`MapGenerator:319` `PipeNetworkManager` `CropInstance`) + 씬 |
| `130`(+i) | 플레이어 · 필드 드랍(레이어마다 +1) | 씬 · `DroppedItem` |
| `140` | 벽 윗면 | 씬 |
| `150` | 아웃라인 · PlaceableObjects · 파이프 면 막대 | 씬 · `PipeFaceOverlay` |
| `160` | PowerLink 오버레이 | `PowerLinkMode` |
| `170` / `180` | 설치 미리보기 칸(초록·빨강) / 기계 그림 | `PlacementPreview` |

## 5. 에디터 메뉴 (전부 재실행 안전, 대화상자 없음)

```
Tools/Project Craft/Dictionary/Register All Assets      ← 에셋 만들면 이거 먼저
Tools/Project Craft/Dictionary/아이템 중복 조사 · 중복 아이템 통합
Tools/Project Craft/Machines/전력 기본값 채우기
Tools/Project Craft/Machines/Fill Missing Machine Blocks
Tools/Project Craft/Pipes/파이프 에셋 설정
Tools/Project Craft/Tool/Generate Tool Assets · 렌치 에셋 설정
Tools/Project Craft/Recipes/Import JSON Recipes · Assign Recipe Categories · Merge ...
```

⚠ **`Assign Recipe Categories` 는 카테고리의 유일한 정본이라 돌릴 때마다 전부 다시 계산한다** —
**손으로 고친 카테고리는 덮인다.** 예전에는 "이미 있으면 건너뛴다" 였고, 그래서 JSON 임포터가 붙인 옛 값이
영영 남아 **기계 12개·파이프 6개가 '자원' 탭에 있었다**(2026-08-15 에 뒤집었다).
규칙: `Machine:*` → 기계 · `kind == plate` → 자원 · `ToolItem`/`ToolPartItem`/`WrenchItem` → 도구 ·
`placeable` → 블록 · 나머지 → 자원. 규칙으로 표현 안 되는 것만 `Overrides` 표에 적는다(지금 9줄).

```
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
- **개수 상한은 `IItemContainer.SlotCapacity` 에 묻는다.** `item.maxStack` 을 직접 읽지 말 것 —
  아이템 저장소가 그 상한을 무시하므로, 직접 읽는 자리가 생기는 즉시 거기서만 64개에서 막힌다.
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
- 산성/유리 파이프 미구현(아이템만 있고 `PipeBlock` 에셋이 없다).
- ~~펌프가 3/6 폴백에 걸려 안 돈다~~ → **2026-08-19 해결**(위 §유체 의 펌프 항목).
  석유 웅덩이는 **공동 탐색기**로만 나온다 — 발견 25% 중 20%.
- ~~증류기 레시피가 0개~~ → **2026-08-20 에 `원유 1000 → 가솔린 500`(craftTime 2)을 넣었다.**
  옛 `petroleum`(석유)은 산출처도 소비처도 없는 죽은 유체였어서 **`gasoline`(가솔린)으로 개명**했다
  (에셋 guid 유지 · `ItemAliases` 에 `석유`·`petroleum` 두 줄). 가솔린은 가스 발전기 연료다.
- (옛 서술) **증류기는 탱크가 없는 게 아니라 레시피가 0개다** — 에셋은 이미 `입력 탱크 1 / 출력 탱크 5 / 1000` 이고
  `Distiller_UI` 에 유체 바도 있다(2026-08-17 실측. "탱크가 없다" 는 옛 서술이 틀렸다).
  `원유 → 석유·가스` 레시피와 가스 아이템이 없어서 빈 바만 보인다.
  ~~발전기 산출 훅~~ → **2026-08-17 에 `spentFuelItem` 으로 붙였다**(위 §발전기의 찌꺼기 산출).
  핵발전소는 고체 연료라 **탱크가 필요 없다.**
- **`chem_acid` 는 산성 용액을 출력 탱크에 내는데 `chem_uranium` 은 입력 탱크에서 먹는다** —
  같은 기계 안에서 이어지지 않아 파이프로 되돌리거나 유리 용기로 퍼 옮겨야 한다(설계 확인 필요).
- **마나의 수량 스케일이 다르다** — `레시피.md` 정본이 `마나 100 = 마력결정 1` 이라 물(1000/양동이)과 자릿수가 어긋난다.
- ~~고급 재단 전용 레시피가 0개~~ → **2026-08-17 에 `공명 칩` 하나가 생겼다**(중급 11 → 고급 12).
  코어 3차 승급 재료라 고급 재단이 곧 3티어의 관문이다. 그 밖의 2티어 마법 레시피는 아직 없다.
- ⚠ **3티어 전체가 `conductor_crystal`(전도체 결정) 하나에 걸려 있다.** 유일한 획득처가
  `extract_shiny_powder`(1-0티어 추출기, 5%)인데 **1계열 추출기 4종에 건설 레시피가 없다.**
  분쇄 사슬(`manastone_fine_dust` 까지)은 전부 도달 가능하므로 **`Machine:Extractor10` 건설 레시피
  하나만 생기면** 공명 결정 → 공명 칩 → 코어 3차 승급 → 3티어 콘텐츠 전부가 함께 열린다.
  ⚠ 그래서 **핵발전소(건설 t3)도 실제로는 아직 못 짓는다** — 사슬(연료봉 제작·재처리)은 t2 에서도 돈다.
- ~~**0-2 등급이 자기 자신에 걸린 순환 교착**~~ → **2026-08-19 해결.** `Machine:Extractor02` 재료에
  `nickel_ingot` 이 있는데 니켈이 `ExtractionTable` 상 0-2 에서야 열려 *0-2 추출기를 지으려면
  0-2 추출기가 있어야 하는* 상태였다(그 뒤로 정밀 세공기·업그레이드 모듈·용광로·강철·티타늄·
  인바·합금·핵 사슬이 통째로 매달려 있었다). **두 걸음으로 끊었다:**
  ① `laser_processor` 재료 `invar_plate` → `iron_plate`(정밀 세공기가 대신 `invar_plate ×50` 을 받는다) —
  레이저 가공기가 고리에서 빠져 **공동 탐색기**가 열렸다.
  ② `UndergroundLootTable` 에 **`nickel_ingot`(minTier 1)** 추가 — 공동 탐색기로 여는 1등급 방에서 나온다.
  실측: 아이템 260개 중 만들 수 있는 것이 **113 → 190** 이 됐다.
  ⚠ **니켈 경로는 이 두 개에 함께 걸려 있다** — 레이저 가공기 재료를 되돌리거나 전리품 행을 빼면
  교착이 그대로 돌아온다.
- ~~`강화 합금` 아이템이 없다~~ → **2026-08-12 에 만들었다**(합금 재련기 · 인바5+청동5+철5 → 5).
- ~~2계열 분쇄물을 못 만든다~~ → **2026-08-15 에 처리 티어 게이트를 지워 분쇄기가 운석도 빻는다**(실측).
  ⚠ 다만 **운석 원석 자체를 못 얻는다** — 등급 2 지하방이 유일한 획득처인데 `dowsing_rod_t2` 가 없다.
- ~~마력 파편·철 주괴의 최초 획득처가 없다~~ → **지하맵 전리품으로 풀렸다**(`UndergroundLootTable`).
  수동 0-0티어 추출기 · 물·용암(`양동이 + 마력파편2`) · 0티어 마법이 여기서 열린다.
  ⚠ 표에서 `mana_shard`·`iron_ingot` 행을 빼면 그대로 다시 막힌다.
- **지하맵 아트가 임시다** — `wall:meteorite` 는 마력석 아틀라스를, `wall:bedrock` 은 돌 아틀라스를
  그대로 쓴다(암반이 돌과 똑같이 보인다). `floor:water` 는 32×32 단색 파랑 한 장이다.
- `uranium_powder`(우라늄 가루)를 쓰는 레시피가 없다 — 0-3 추출 산출이 `turbid_uranium` 으로 바뀌면서
  `Pulverize_RawUraniumOre` 의 산출로만 남았다.
- `magic_powder`(마법 가루)를 **쓰는 레시피가 하나도 없다** — 운석 사슬 재편으로 자리를 잃었다(아이템은 남겨 뒀다).
- `energy_crystal`·`magic_crystal` 은 이제 2계열 추출로만 나온다.
  (마법 레시피가 2026-08-15 부터 **재단 전용**이 된 것은 위 §기계·레시피 의 조합대 항목을 볼 것.)
- ~~`extract_ore`·`extract_meteorite` 는 `machine` 이 비어 있다~~ → **2026-08-19 해결**.
  1·2티어 자원 생성기를 만들어 꽂았다(위 §자원 생성기). **마력석·운석의 최초 획득처**가 이것이다 —
  전에는 지하 등급 1·2 방뿐인데 `dowsing_rod_t1/t2` 가 없어 아예 닿을 수 없었다.
  ⚠ 다만 **1·2티어 자원 생성기의 건설 레시피가 아직 없다.**
- ~~지열 발전기가 발전을 못 한다~~ → **2026-08-15 해결**. 막던 것이 셋이었다:
  `MachineBlock.IsGenerator` 의 `fuelSlotCount > 0` · `MachineInstance.ApplyConfig` 의 "미설정" 폴백
  (입출력·연료가 전부 0 이라 세 조건에 안 걸려 `isGenerator` 가 지워졌다 — 지금은 `|| info.IsGenerator` 를 함께 본다) ·
  에셋의 `isGenerator = false`. **연료 없는 발전기의 발전량은 `fuelBurnRate` 를 그대로 쓴다**(연료식도
  "태운 양 = 발전량" 이라 필드를 새로 만들면 같은 뜻이 두 곳으로 갈린다). 지열은 12/s.
- ⚠ **변압기는 지을 수는 있는데 아무 일도 하지 않는다** — 전압 2풀이 미구현이라 가공 레시피가 0개다
  (옛 `transformer`·`power_transformer` 는 산출이 없는 죽은 에셋이라 지웠고, 그 이름은 이제 건설 레시피다).
- 전력 밸런스(2026-08-20 사용자 상향): 발전 **화력 200/s · 지열 12/s · 가스 300(원유)~600(가솔린)/s · 핵 3000/s**
  vs 소비 전기로 100/s · 고전압 전기로 300/s · 증류기 100/s · 용광로 500/s · 추출기 100/s(폴백).
  화력 한 대가 전기로 두 대를 감당하고, **지열만 이 사다리에서 혼자 떨어져 있다**(12/s).
  ⚠ 발전기의 초당 값은 **`FuelTable`** 이 정한다 — 에셋의 `fuelBurnRate` 는 표에 없는 기계(지열)만 쓴다.
- 렌치·파이프 아이콘이 전부 `assetPlaceHolder`. 파이프 레시피가 아직 `Recipes/Incomplete` 안에 있음.
- **`강화 자원 생성기`·`화학 처리기` 는 월드에 놓으면 `No Asset` 로 보인다** — 프리팹에 스프라이트가 없다
  (2026-08-20 시연 공장에서 7대가 그렇게 떴다). 아이템 아이콘과 달리 이쪽은 **배치물 프리팹 쪽** 문제다.
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
