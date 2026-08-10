# Recipes (레시피 데이터)

런타임에서 `Application.streamingAssetsPath + "/Recipes/*.json"` 로 로드할 수 있다.

출처가 두 가지이며 **서로 다른 기획 이터레이션**이다. 아래 "출처별 구분"을 먼저 확인할 것.

## 파일 구성

### A. Obsidian `레시피.md` 기반 (최신 기획)
| 파일 | 대상 | 설명 |
|------|------|------|
| `crafting.json` | 조합 | 도구/기계/마법 아이템 제작. 0~2티어. `tools`(내구도 소모), `station`(초급/중급 재단) 포함. |
| `machine_processing.json` | 기계 가공 | 수동 분쇄기·화로·압축기·합금 재련기·전기 분해기·유기물 배양기·레이저 가공기·마나 용해기·화력 발전기(연료) 등. |

### B. 노션 "기획 백로그" 기반 (이전 기획)
| 파일 | 대상 | 설명 |
|------|------|------|
| `crushing.json` | 분쇄기 | 주 산출물(100%) + 등급별 부산물(일반 10% / 고급 5% / 희귀 1%). |
| `smelting.json` | 용광로 | 금속 조각 ×4 → 주괴, 뼈 가루 → 석회. |
| `crafting_table.json` | 가공대 | 수동 제작. 전력 불필요. |
| `factory.json` | 전용 공장 | 압축기(옛 압연기·벽돌 공장)/화학처리기/발전소/변압기 등. |
| `extraction.json` | 추출기 | 지형 기반 자동 추출. |

> ⚠️ A와 B는 아이템 체계가 다르다(예: B의 `철 조각→철 주괴` vs A의 `마력 파편`·`재단`·`크랭크`·`드라이버` 체계).
> 실제 게임에 반영할 때는 어느 쪽을 정본으로 삼을지 정하고 나머지는 정리하는 것을 권장한다.

## 공통 스키마
각 파일: `{ recipeType, source?, description, globalNotes?, recipes: [ ... ] }`

레시피 객체 필드:
- `id`: 고유 식별자
- `machine`: 생산 기계/공장 (파일 상단 공통이면 생략될 수 있음)
- `tier`: 티어, `manual`: 수동(무전력) 여부
- `station`: 조합 장소 (예: `초급 재단`, `중급 재단`)
- `power`: `"저전압"` / `"고전압"` (B 파일들)
- `input` / `inputs`: 입력. 항목당 `{ item, qty, fluid?, unit? }`. `qty: null` = 원문 수량 미표기.
- `output` / `outputQty` / `outputs`: 출력.
  - 단일 확정 출력은 `output`(문자열) + `outputQty`.
  - 다중/확률 출력은 `outputs: [{ item, qty?, chance?, role?, grade?, fluid?, type?, amount? }]`.
  - `type: "power"` = 전력 산출(아이템 아님). `chance` = 산출 확률(0~1).
- `tools`: 조합 시 **내구도만 소모**하고 재료로 소모되지 않는 도구 배열 (A 파일). B 파일은 단수 `tool`.
- `category`: `magic` / `pipe` / `fuel` 등 분류
- `note`: 비고

## 주의
- 아이템 식별자는 기획서의 **한글 이름**을 그대로 사용한다(예: `철판`, `마력 파편`).
  게임 코드의 `Items.itemName`/`blockName` 규약과 매핑이 필요하다.
- **조합법이 미정인 항목은 `inputs: []` + `note: "조합법 미정"`** 으로 엔트리만 남겼다(2티어 대부분).
- 수량이 표기되지 않은 재료는 1개로 간주했고, 불명확한 경우 `qty: null` + `note`로 남겼다.
