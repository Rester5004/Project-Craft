# 도구 에셋 생성 보고서

`Tools/Project Craft/Tool/Generate Tool Assets` 가 자동 생성한 파일입니다.

- 스프라이트 100개 수집(이름 없는 자동 스프라이트 3개 제외)
- 스프라이트 라이브러리 100장 등록
- 재질 16종 (새로 만든 것 16개)
- 부품 종류 3종 (새로 만든 것 3개)
- 부품 아이템 48개 (새로 만든 것 48개)
- 도구 설계도 3종
- 도구 아이템 3개
- 도구 카테고리 생성 (탭 아이콘: iron_pickaxe_head)
- 도구 레시피 3개 (곡괭이 → 망치 → 드라이버 순)

## 중복 플레이스홀더 정리

- 삭제: `Assets/Prefabs/Items/Placeholder/막대.asset` → `wood_rod` 로 대체
- 삭제: `Assets/Prefabs/Items/Placeholder/망치 머리.asset` → `wood_hammer_head` 로 대체
- 삭제: `Assets/Prefabs/Items/Placeholder/철 망치 머리.asset` → `iron_hammer_head` 로 대체
- 삭제: `Assets/Prefabs/Items/Placeholder/돌 망치 머리.asset` → `stone_hammer_head` 로 대체
- 삭제: `Assets/Prefabs/Items/Placeholder/망치.asset` → `tool_hammer` 로 대체
- 삭제: `Assets/Prefabs/Items/Placeholder/철 망치.asset` → `tool_hammer` 로 대체
- 삭제: `Assets/Prefabs/Items/Placeholder/돌 망치.asset` → `tool_hammer` 로 대체
- 삭제: `Assets/Prefabs/Items/Placeholder/드라이버.asset` → `tool_driver` 로 대체

- 참조를 고친 레시피 25개, 삭제한 아이템 8개

### 새 도구 레시피로 대체되어 산출물이 비워진 레시피 7개

- `Assets/Prefabs/Recipes/Incomplete/crafting/hammer.asset`
- `Assets/Prefabs/Recipes/Incomplete/crafting/screwdriver.asset`
- `Assets/Prefabs/Recipes/Incomplete/crafting_table/craft_iron_hammer.asset`
- `Assets/Prefabs/Recipes/Incomplete/crafting_table/craft_stone_hammer.asset`
- `Assets/Prefabs/Recipes/Notion/crafting/driver.asset`
- `Assets/Prefabs/Recipes/Notion/crafting/iron_hammer.asset`
- `Assets/Prefabs/Recipes/Notion/crafting/stone_hammer.asset`

## 딕셔너리 등록

- ItemDictionary 에 아이템 51개 추가(부품 48 + 도구 3 중 새것)
- RecipeDictionary 에 도구 레시피 3개 추가
- ToolDictionary 게임오브젝트를 새로 만들었습니다.
- ToolDictionary 채움 (재질 16 · 종류 3 · 부품 48 · 도구 3)
- 씬 저장: `Assets/Scenes/MapTest.unity`
