# Recipes (레시피 데이터)

출처: 노션 "기획 백로그" (README.md의 아이템 기획서 링크)를 JSON으로 정리한 것.
런타임에서 `Application.streamingAssetsPath + "/Recipes/*.json"` 로 로드할 수 있다.

## 파일 구성
| 파일 | 대상 | 설명 |
|------|------|------|
| `crushing.json` | 분쇄기 | 직렬 누적 가공. 주 산출물(100%) + 등급별 부산물(일반 10% / 고급 5% / 희귀 1%). 0~2티어. |
| `smelting.json` | 용광로 | 금속 조각 → 주괴, 뼈 가루 → 석회. 대체로 조각 ×4 → 주괴 ×1. |
| `crafting_table.json` | 가공대 | 수동 제작. 전력 불필요. 일부는 도구(망치) 내구도 소모. |
| `factory.json` | 전용 공장 | 압연기/철근공장/벽돌공장/유리/파이프/감별기/화학처리기/전기분해기/수전해기/정유기/발전소/변압기 등. 레시피별 `machine` 필드로 구분. |
| `extraction.json` | 추출기 | 지형 기반 자동 추출(투입 자원 없음). |

## 공통 스키마
각 파일: `{ recipeType, description, ..., recipes: [ ... ] }`

레시피 객체 필드:
- `id`: 고유 식별자
- `machine`: 생산 기계/공장 (파일 상단 공통이면 생략될 수 있음)
- `tier`: 티어 (해당 시)
- `power`: `"저전압"` / `"고전압"` (해당 시)
- `input` / `inputs`: 입력. 항목당 `{ item, qty, fluid? }`. `input`은 단일 문자열 축약형(분쇄기).
- `output` / `outputs`: 출력.
  - 단일 확정 출력은 `output`(문자열).
  - 다중/확률 출력은 `outputs: [{ item, qty?, chance?, role?, grade?, fluid?, type? }]`.
  - `role: "main"` = 주 산출물(chance 1.0). `grade`: `common`/`uncommon`/`rare`.
  - `type: "power"` = 전력 산출(아이템 아님).
- `tool`: 필요 도구 (가공대).
- `note`: 비고. 기획서의 "(미정)" 항목은 값 `null` + `note`로 표기.

## 주의
- 아이템 식별자는 기획서의 **한글 이름**을 그대로 사용한다(예: `철 조각`, `철 주괴`).
  게임 코드의 `Items.itemName`/`blockName` 규약과 매핑이 필요할 수 있다.
- 기획서에 "(미정)"으로 남은 수량/부산물/일부 레시피는 `null` + `note`로 보존했다.
