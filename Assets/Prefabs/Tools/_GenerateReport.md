# 도구 에셋 생성 보고서

`Tools/Project Craft/Tool/Generate Tool Assets` 가 자동 생성한 파일입니다.

- 스프라이트 120개 수집(이름 없는 자동 스프라이트 3개 제외)
- 스프라이트 라이브러리 120장 등록
- 재질 16종 (새로 만든 것 0개 · 재료 아이템을 채운 것 0개)
- 부품 종류 5종 (새로 만든 것 0개)
- 부품 아이템 80개 (새로 만든 것 12개)
  - 다른 폴더에 이미 있어 그대로 쓴 것 4개: iron_plate, copper_plate, gold_plate, silver_plate
  - ⚠ 스프라이트를 못 찾은 부품 2개: wood_plate, stone_plate
- 도구 설계도 4종
- 도구 아이템 4개
- 도구 레시피 4개 (곡괭이 → 망치 → 드라이버 순)

## 중복 플레이스홀더 정리

- 정리할 대상이 없습니다(이미 삭제됨).

## 딕셔너리 등록

- ItemDictionary 에 아이템 12개 추가(부품 80 + 도구 4 중 새것)
- RecipeDictionary 에 도구 레시피 0개 추가
- ToolDictionary 채움 (재질 16 · 종류 5 · 부품 80 · 도구 4)
- 씬 저장: `Assets/Scenes/MapTest.unity`
