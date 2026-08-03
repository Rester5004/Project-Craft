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

  `Chunk.Save` 순서: placeable 루프 안에 slots → burn → energy/cursor/links → parcels → faceModes, 루프 뒤 drops.
  `Chunk.Load` 는 `if (version >= N)`, **참조형은 `else` 로 빈 배열을 넣어야** 이전 세이브에서 NRE 가 안 난다.

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
- **`ItemAliases`** = 통합돼 사라진 옛 이름 → 정본 `itemName`. **한 표를 세 곳이 함께 본다**:
  `ItemDictionary.GetItem` 폴백(옛 세이브 호환) · `RecipeJsonImporter.ResolveItem`(재임포트 내성) · `ItemMerger`(참조 재작성).
  `itemName` 이 세이브 키라 **이 폴백이 아이템을 지워도 세이브가 안 깨지게 하는 유일한 안전망**이다.
- 중복 정리 흐름: `아이템 중복 조사`(리포트만) → `ItemAliases` 표에 줄 추가 → `중복 아이템 통합` → 다시 조사해 0 확인.

### 기계 · 레시피
- `MachineBlock`(SO) → `MachineInstance`(런타임) + `MachineInventory`(input/output/fuel).
- `MachineInstance.ApplyConfig` 조건에 `|| info.fuelSlotCount > 0` 이 있다 — 빼면 발전기가 3/6 으로 폴백한다.
- `RecipeSolver` 가 재료 확인·소모·적재를 전담: `CanCraft` `ConsumeInputs` `CanStoreOutputs` `AddItems`
  `CountFreeSpace`(넣어 보지 않고 여유 세기) `CountItem`.
- **`RecipeSolver.AddItems` 는 통지하지 않는다.** 외부에서 슬롯을 건드렸으면
  `inventory.NotifyChanged()` + `instance.Flush()` 를 직접 불러야 UI 갱신·재가공이 걸린다.
- `Recipe.tier` = 조합대 티어 요구. `MachineBlock.recipeGroupId` 로 0/1/2티어 화로가 같은 목록을 공유.

### 추출 체계 (정본 = `자원과 그 가공방식.canvas`)
메인자원을 **분쇄기로 1/2/3회 분쇄** → 그 분쇄물을 **추출기**에 넣어 부산물을 확률로 얻는다.
0티어 = **돌** · 1티어 = **마력석** · 2티어 = **운석**. 금속 산출은 `raw_*_ore`(조각), 재련하면 `*_ingot`.
- 기계 이름은 **`{메인티어}-{등급}티어 추출기`** 12종(`Machine:Extractor00`~`23`).
- **등급차를 레시피 복제로 표현하지 않는다.** 같은 계열은 `recipeGroupId`(`Extractor0/1/2`)로 목록을 공유하고,
  `tier` 가 "등급 N 은 0~N 의 산출을 모두 가진다" 를 그대로 구현하며,
  속도·확률차는 `MachineBlock.speedMultiplier` / `chanceMultiplier` 가 낸다. **확률 산출 동작은 아직 미구현.**
- **지형에 설치해 메인자원·유체를 뽑는 것은 추출기가 아니다** — `자원 생성기`·`펌프`·`지열 발전기` 쪽이다
  (`extraction.json` 의 `terrain` 필드가 그 표시). 입력 0 / 출력 1 로 둬야 3/6 폴백에 안 걸린다.
- 생성: `Tools/Project Craft/Machines/추출기 계열 설정` → 이어서 `중복 아이템 통합`(같은 이름 플레이스홀더 흡수).

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
Tools/Project Craft/Machines/추출기 계열 설정
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
- **확률 산출 미구현** — `speedMultiplier`/`chanceMultiplier` 는 필드만 있고 `MachineInstance` 가 아직 안 본다.
  캔버스의 추출 레시피 36종과 마력석·운석의 1/2/3회 분쇄 아이템 6종도 아직 없다.
- `extract_ore`·`extract_meteorite` 는 **`machine` 이 비어 있다** — 1·2티어 자원 생성기가 아직 없어서다.
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
- `MachineAliases` 의 `{수동 분쇄기 → 전기 분쇄기}` 는 **임시** — 수동 분쇄기가 정식 기계가 되면 그 줄을 지운다.
