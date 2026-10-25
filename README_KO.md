# Scenario Part Clipboard (RimWorld 1.7)

## 기능
- 시나리오 편집 모드에서 각 항목 제목줄의 바닐라 휴지통 왼쪽에 Copy, Paste, Duplicate 아이콘 버튼을 표시합니다.
- Copy는 선택 항목을 임시 클립보드에 저장하고, Paste와 Duplicate는 해당 항목 바로 아래에 새 항목을 삽입합니다.
- 드래그 핸들을 잡고 다른 항목 제목줄 위/아래 절반에 놓으면 순서를 이동합니다. 바닐라의 재정렬 가능 여부를 따릅니다.
- 복제할 때 리스트, 배열, 딕셔너리 컨테이너는 별도로 만들어 원본과 복제본의 컨테이너가 공유되지 않도록 합니다. 컨테이너 안의 사용자 정의 객체는 얕은 복사이므로 일부 모드 항목은 내부 객체를 공유할 수 있습니다.
- Harmony와 RimWorld 설치 폴더의 어셈블리를 참조하며, 게임 플레이 모드에는 별도로 의존하지 않습니다.

## 빌드
1. RimWorld 설치 폴더 내 `Mods\ScenarioPartClipboard\Build.bat`을 실행합니다.
2. 스크립트가 `Assembly-CSharp.dll`, `UnityStubs.dll`, Harmony의 `0Harmony.dll`을 찾아 Release 빌드를 실행합니다.
3. 성공하면 `Assemblies\ScenarioPartClipboard.dll`이 생성됩니다.

게임이 실행 중이면 현재 로드된 모드 DLL을 덮어쓸 수 없으므로, 게임을 종료한 뒤 빌드하세요.

Harmony가 표준 위치에 없다면 `Source\ScenarioPartClipboard.csproj`를 직접 빌드하고 `/p:HarmonyPath="Harmony DLL 전체 경로"`를 지정하세요.

## 설치
이 ZIP을 풀어 `ScenarioPartClipboard` 폴더를 `RimWorld\Mods\`에 배치하고 `Build.bat`을 실행한 뒤 게임에서 모드를 활성화하세요.

## 테스트 체크리스트
- 여러 기본 시나리오 항목에서 아이콘 Copy, Paste, Duplicate 동작 확인
- 핸들을 드래그해 위/아래 항목으로 이동하고 바닐라 재정렬 제한이 적용되는지 확인
- 복제 후 원본과 리스트/배열 값이 독립적으로 동작하는지 확인
- 모드가 추가한 ScenPart에서 버튼 표시 및 복제 확인
- 빈 클립보드에서 Paste를 눌렀을 때 안내 메시지 표시 확인
- 개발자 모드 로그에서 Harmony 패치 예외 확인
