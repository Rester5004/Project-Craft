# 추출기 계열 설정

`Tools/Project Craft/Machines/추출기 계열 설정` 이 자동 생성한 파일입니다.
정본은 `자원과 그 가공방식.canvas` 입니다.

## 기계

| 기계 | ID | 계열 | 등급 | 속도 | 확률 | 상태 |
|---|---|---|---|---|---|---|
| 수동 0-0티어 추출기 | `Machine:Extractor00` | Extractor0 | 0 | ×1 | ×1 | 생성 |
| 0-1티어 추출기 | `Machine:Extractor01` | Extractor0 | 1 | ×1 | ×1 | 생성 |
| 0-2티어 추출기 | `Machine:Extractor02` | Extractor0 | 2 | ×1 | ×1 | 생성 |
| 0-3티어 추출기 | `Machine:Extractor03` | Extractor0 | 3 | ×1 | ×1 | 생성 |
| 1-0티어 추출기 | `Machine:Extractor10` | Extractor1 | 0 | ×1 | ×1 | 생성 |
| 1-1티어 추출기 | `Machine:Extractor11` | Extractor1 | 1 | ×1 | ×1 | 생성 |
| 1-2티어 추출기 | `Machine:Extractor12` | Extractor1 | 2 | ×2 | ×1 | 생성 |
| 1-3티어 추출기 | `Machine:Extractor13` | Extractor1 | 3 | ×4 | ×2 | 생성 |
| 2-0티어 추출기 | `Machine:Extractor20` | Extractor2 | 0 | ×1 | ×1 | 생성 |
| 2-1티어 추출기 | `Machine:Extractor21` | Extractor2 | 1 | ×1 | ×1.5 | 생성 |
| 2-2티어 추출기 | `Machine:Extractor22` | Extractor2 | 2 | ×2 | ×1 | 생성 |
| 2-3티어 추출기 | `Machine:Extractor23` | Extractor2 | 3 | ×4 | ×2 | 생성 |
| 0티어 자원 생성기 | `Machine:ResourceGenerator0` | ResourceGenerator | 0 | ×1 | ×1 | 생성 |
| 0티어 자원 생성기(강화) | `Machine:ResourceGenerator0Plus` | ResourceGenerator | 1 | ×2 | ×1 | 생성 |
| 펌프 | `Machine:Pump` | Pump | 0 | ×1 | ×1 | 생성 |
| 지열 발전기 | `Machine:GeothermalGenerator` | Geothermal | 0 | ×1 | ×1 | 생성 |

## 추출 레시피 재배정

`extraction.json` 은 `terrain` 필드가 붙은 **지형 설치형**이라, 분쇄물을 넣는 캔버스의 추출기와 개념이 다르다.

| 레시피 | 옮긴 곳 |
|---|---|
| `extract_conductor_powder` | `Machine:Extractor00` (수동 0-0티어 추출기) · 1개 |
| `extract_normal` | `Machine:ResourceGenerator0` (0티어 자원 생성기) · 1개 |
| `extract_water` | `Machine:Pump` (펌프) · 1개 |
| `extract_oil` | `Machine:Pump` (펌프) · 1개 |
| `extract_crude_oil` | — 레시피가 없음 |
| `geothermal` | `Machine:GeothermalGenerator` (지열 발전기) · 1개 |
| `extract_ore` | ⚠ **비움** — 1·2티어 자원 생성기가 아직 없음 (1개) |
| `extract_meteorite` | ⚠ **비움** — 1·2티어 자원 생성기가 아직 없음 (1개) |

## 옛 '추출기' 삭제

- `Assets/Prefabs/Blocks/Machines/Extractor.asset` · `Assets/Prefabs/Items/Machines/Extractor.asset` 삭제 (2개)


- 기계 16종 처리(새로 만든 것 16개) · 레시피 5개 이동 · 옛 기계 2개 삭제

이어서 `중복 아이템 통합` 을 돌리면 같은 이름의 플레이스홀더가 흡수됩니다.
