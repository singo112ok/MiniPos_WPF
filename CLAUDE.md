# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

MiniPos — .NET 8 (`net8.0-windows`) WPF 학습용 POS 모니터링 앱. 단일 프로젝트 솔루션이며 저장소 루트가 곧 프로젝트 루트(`MiniPos.csproj`, `MiniPos.sln`)입니다. MVVM은 `CommunityToolkit.Mvvm` 8.4.2 소스 제너레이터로 구현합니다.

## 빌드 / 실행

```powershell
dotnet build MiniPos.sln          # 빌드
dotnet run --project MiniPos.csproj   # 실행 (WinExe)
```

테스트 프로젝트, 린트 설정, CI가 모두 없습니다. `dotnet test`로 검증할 대상이 존재하지 않으므로, 동작 확인은 앱을 띄워 UI 버튼으로 하는 것이 유일한 방법입니다.

## 아키텍처

### View ↔ ViewModel 연결

DI 컨테이너나 ViewModelLocator가 없습니다. [MainWindow.xaml.cs](MainWindow.xaml.cs)에서 `DataContext = new MainViewModel()`로 직접 꽂습니다. 화면이 하나뿐이라 [ViewModels/MainViewModel.cs](ViewModels/MainViewModel.cs) 한 클래스가 상품 목록·주문 금액·로그·TCP 통신 상태를 전부 들고 있습니다. `MainWindow.xaml`은 프로젝트 루트에 있고, `csproj`에 선언된 `Views\` 폴더는 비어 있습니다.

### 소스 제너레이터 규약 (XAML 바인딩 이름의 출처)

| 선언 | XAML에서 쓰는 이름 |
|---|---|
| `[ObservableProperty] private decimal _totalAmount;` | `{Binding TotalAmount}` |
| `[RelayCommand] private void ClearOrder()` | `{Binding ClearOrderCommand}` |
| `[RelayCommand] private async Task ToggleSimulationAsync()` | `{Binding ToggleSimulationCommand}` (`Async` 접미사 제거됨) |

계산 속성(`SimulationButtonText`, `ServerButtonText`)은 `[NotifyPropertyChangedFor(nameof(...))]`를 원본 bool 필드에 붙여서 갱신시킵니다. 버튼 캡션을 상태에 따라 바꾸는 것도 이 방식이며, XAML 트리거를 쓰지 않습니다.

### 상품 클릭 = 주문 추가

`ListBox`의 `SelectedItem`이 `SelectedProduct`에 양방향 바인딩되어 있고, `partial void OnSelectedProductChanged`가 `AddOrder()`를 호출한 뒤 `SelectedProduct = null`로 되돌립니다. 선택 상태를 남기지 않으므로 같은 상품을 연속 클릭할 수 있습니다. 상품 카드에 별도 버튼/커맨드가 없는 이유입니다.

### 한 프로세스 안의 TCP 서버 + 클라이언트

이 앱은 가상 결제단말기(서버)와 POS(클라이언트)를 **동시에 자기 안에** 갖고 있습니다. `MainViewModel`이 `TcpPosServer`와 `TcpPosClient`를 둘 다 필드로 보유하며, 통신은 `127.0.0.1:9000` 루프백입니다.

- [Services/TcpPosServer.cs](Services/TcpPosServer.cs) — `TcpListener` accept 루프, 접속마다 `Task.Run(HandleClientAsync)`로 분리. 넘겨받은 `TcpClient`의 소유권을 핸들러가 가져가 `using`으로 해제합니다. 승인 지연을 흉내내는 `Task.Delay(1000)`이 들어 있습니다.
- [Services/TcpPosClient.cs](Services/TcpPosClient.cs) — 연결 유지형. `ConnectAsync`는 진입 시 기존 연결을 `DisposeAsync`로 정리합니다.

**프로토콜**: 파이프(`|`) 구분 + **줄 단위**. 서버가 `ReadLineAsync`로 읽으므로 양쪽 모두 개행까지 보내고(`WriteLineAsync`) 한 줄만 읽어야(`ReadLineAsync`) 합니다.

```
요청: PAY_REQ|{금액}
응답: PAY_RES|SUCCESS|{금액}원 승인완료|승인번호:{8자리}
      PAY_RES|FAIL|잘못된 요청 패킷입니다.
```

서버는 응답 후에도 연결을 끊지 않고 루프를 돕니다. 따라서 클라이언트가 EOF를 기다리는 방식(`ReadToEndAsync`)으로 읽으면 영구 대기에 빠집니다. 결제 요청 전에 UI에서 서버를 먼저 가동해야 하며, 안 켜져 있으면 `SocketException`으로 안내 로그가 출력됩니다.

디버깅 주의: `TcpPosServer`의 `catch (Exception ex) { }`와 `catch { break; }`가 예외를 전부 삼킵니다. 통신 문제를 추적할 때는 이 지점에 로그를 임시로 넣어야 원인이 보입니다.

### 로그 표시 규약

`OrderLogs`는 `ObservableCollection<string>`이고 항상 `Insert(0, ...)`로 최신 항목을 위에 쌓습니다. 형식은 `[HH:mm:ss] 이모지 한국어 메시지`이며, 이모지가 상태 종류(🟢 시작 / 🛑 정지 / ⬆️ 송신 / ⬇️ 수신 / ❌ 오류 / 🎉 성공)를 나타냅니다. 새 로그를 추가할 때 이 형식을 맞추세요.

백그라운드 시뮬레이션 루프(`ToggleSimulationAsync`)는 `Task.Delay(3000)` 주기로 돌면서 컬렉션 변경은 `Application.Current.Dispatcher.InvokeAsync`로 UI 스레드에 마샬링합니다. `IsSimulationRunning` 플래그 하나로 시작/정지를 제어합니다.

## 파일 작성 관례

- **인코딩**: 모든 `.cs` / `.xaml`이 **UTF-8 BOM**입니다(예외: `AssemblyInfo.cs`). 한글 주석과 문자열이 많으므로 인코딩을 바꾸면 전부 깨집니다.
- **인덴트**: `.cs`는 탭, `.xaml`은 스페이스 4칸.
- **주석 처리된 이전 버전 코드를 지우지 마세요.** 학습 기록용으로 의도적으로 남겨둔 것입니다 (예: `MainViewModel`의 구버전 `RequestTcpPaymentAsync`, `MainWindow.xaml`의 구버전 가격 `TextBlock`과 테스트 버튼).
- 커밋은 `step1`, `step2`, ... 처럼 학습 단계 단위로 쌓여 있습니다.