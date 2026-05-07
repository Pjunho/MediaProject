# 변경 이력

> 스케줄 작업이 `git log`를 기반으로 자동 갱신할 수 있는 보관용 문서.
> 에이전트 자동 컨텍스트에는 넣지 말고, 이력 확인이 필요할 때만 참조한다.

## 2026-05-07

- 출전 힌트 HUD 개선: `[Space / Click] 전사 출전 (다음: 궁수 +N명)` 형식으로 다음 아군 타입 표시 (`AllyPlacer.cs`, `GameManager.cs`)
- 중복 HP 바 버그 수정: `AllyPlacer.AddHpBar()` 제거, `SharedTypes.cs`의 `HpBar` 클래스 삭제

## 2026-05-06

- `cc37ab7`: 스페이스바/클릭 기반 아군 수동 출전 시스템 구현 (`AllyPlacer.cs`, `GameManager.cs`)
- `2a6e181`: DNF 비트비트 폰트 원형 적용
- `be1f427`: 초기 코인 지급 타이밍 변경. 스타트 버튼이 아니라 스테이지 진입 시 즉시 지급
- `db7275b`: DNF 비트비트 폰트 렌더링 보정
- `48dd327`: `SheetEnemyBase` 공통 기반 클래스 추가 및 스테이지 1~3 적 전원 시트 기반 4방향 애니메이션 적용
- `c0c731e`: DNF 비트비트 UI 폰트 적용
