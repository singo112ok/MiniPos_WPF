using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniPos.Models;
using MiniPos.Services;
using MiniPos.Views;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Runtime.Serialization.DataContracts;
using System.ComponentModel;
using System.Windows.Data;


namespace MiniPos.ViewModels
{
	public partial class MainViewModel : ObservableObject
	{
		private readonly RestApiService _restService = new();

		public ObservableCollection<ApiPost> ApiPosts { get; } = new();

		public ICollectionView ProductsView { get; }

		private readonly TcpPosClient _tcpClient = new();
		private readonly TcpPosServer _tcpServer = new();

		private readonly SerialReceiptPrinter _printer = new();

		[ObservableProperty]
		private string _currentCategoryFileter = "All";

		[ObservableProperty]
		private bool _isApiLoading = false;

		[ObservableProperty]
		private string _comPortName = "COM1";

		[ObservableProperty]
		private string _title = "미니 POS 주문 모니터링 시스템 v1.0";

		[ObservableProperty]
		private decimal _totalAmount = 0;

		[ObservableProperty]
		private Product? _selectedProduct;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(SimulationButtonText))]
		private bool _isSimulationRunning = false;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(ServerButtonText))]
		private bool _isServerRunning = false;

		public string SimulationButtonText => IsSimulationRunning ? "⏹️ 실시간 유입 정지" : "▶️ 실시간 유입 시작 (3초 간격)";

		public string ServerButtonText => IsServerRunning ? "🛑 가상 결제단말기 (서버) 정지" : "🟢 가상 결제단말기 (서버) 가동";

		partial void OnCurrentCategoryFileterChanged(string value)
		{
			ProductsView.Refresh();
		}

		partial void OnSelectedProductChanged(Product? value)
		{
			if (value == null) return;

			AddOrder(value);

			SelectedProduct = null;
		}


		public ObservableCollection<Product> Products { get; } = new();
		public ObservableCollection<string> OrderLogs { get; } = new();

		public MainViewModel()
		{
			LoadInitialProducts();

			ProductsView = CollectionViewSource.GetDefaultView(Products);
			ProductsView.Filter = FilterProducts;
		}

		private bool FilterProducts(object obj)
		{
			if (obj is Product product)
			{
				if(CurrentCategoryFileter == "All")
					return true;

				return product.Category == CurrentCategoryFileter;
			}
			return false;
		}

		[RelayCommand]
		private void SetCategoryFilter(string category)
		{
			CurrentCategoryFileter = category;
		}

		private void LoadInitialProducts()
		{
			Products.Add(new Product { Id = 1, Name = "아메리카노", Price = 4500, Category = "Coffee" });
			Products.Add(new Product { Id = 2, Name = "카페라떼", Price = 5000, Category = "Coffee" });
			Products.Add(new Product { Id = 3, Name = "치즈케이크", Price = 6500, Category = "Dessert" });
		}

		[RelayCommand]
		private async Task AddProduct()
		{
			var addProductViewModel = new ProductAddViewModel();

			var addProductWindow = new ProductAddWindow
			{
				DataContext = addProductViewModel,
				Owner = Application.Current.MainWindow
			};

			if (addProductWindow.ShowDialog() == true)
			{
				if (string.IsNullOrWhiteSpace(addProductViewModel.InputName))
				{
					MessageBox.Show("상품명을 입력해주세요.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				int newId = Products.Count > 0 ? Products.Max(p => p.Id) + 1 : 1;

				Products.Add(new Product
				{
					Id = newId,
					Name = addProductViewModel.InputName,
					Price = addProductViewModel.InputPrice,
					Category = addProductViewModel.SelectedCategory
				});

				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🆕 신규 상품 등록: {addProductViewModel.InputName}");

			}
		}

		[RelayCommand]
		private async Task FetchApiDataAsync()
		{
			try
			{
				IsApiLoading = true;
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🌐 공개 REST API(jsonplaceholder) 호출 중...");

				List<ApiPost>? result = await _restService.GetPostsAsync();

				if (result != null)
				{
					ApiPosts.Clear();

					foreach (var post in result)
					{
						ApiPosts.Add(post);
					}

					OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🟢 REST API 로드 성공! 총 {result.Count}개의 게시물을 가져왔습니다.");
				}
			}
			catch (Exception ex)
			{
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ❌ API 통신 오류: {ex.Message}");
				MessageBox.Show($"API 통신 에러!\n{ex.Message}", "REST API Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				IsApiLoading = false;
			}
		}


		[RelayCommand]
		private void AddOrder(Product? selectedProduct)
		{
			if (selectedProduct == null) return;

			TotalAmount += selectedProduct.Price;
			OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {selectedProduct.Name} 주문 (+{selectedProduct.Price:N0}원)");
		}

		[RelayCommand]
		private void ClearOrder()
		{
			TotalAmount = 0;
			OrderLogs.Clear();
			OrderLogs.Add($"[{DateTime.Now:HH:mm:ss}] 주문 내역이 초기화되었습니다");
		}

		[RelayCommand]
		private async Task ToggleSimulationAsync()
		{
			IsSimulationRunning = !IsSimulationRunning;

			if(!IsSimulationRunning)
			{
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ⚠️ 실시간 자동 주문 시뮬레이션이 정지되었습니다.");
				return;
			}

			OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🟢 백그라운드 실시간 주문 유입이 시작됩니다...");

			while (IsSimulationRunning)
			{
				await Task.Delay(3000);

				if (!IsSimulationRunning) break;

				Product randomProduct = await Task.Run(() =>
				{
					int randomIndex = new Random().Next(Products.Count);
					return Products[randomIndex];
				});

				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
					AddOrder(randomProduct);
				});
			}
		}

		//[RelayCommand]
		//private async Task RequestTcpPaymentAsync()
		//{
		//	try
		//	{
		//		OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🌐 결제 서버 연결 시도 중...");

		//		if (!_tcpClient.IsConnected)
		//		{
		//			await _tcpClient.ConnectAsync("127.0.0.1", 9000);
		//		}

		//		string packet = $"PAY_REQ|총결제금액|{TotalAmount}";
		//		OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ⬆️ 전송: {packet}");

		//		string? response = await _tcpClient.SendCommandAsync(packet);

		//		OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ⬇️ 응답 수신: {response}");
		//	}
		//	catch (SocketException ex)
		//	{
		//		OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ❌ 통신 오류: 서버가 켜져 있는지 확인하세요. ({ex.Message})");
		//	}
		//	catch (Exception ex)
		//	{
		//		OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ❌ 예외 발생: {ex.Message}");
		//	}
		//}

		[RelayCommand]
		private void ToggleTcpServer()
		{
			if(IsServerRunning)
			{
				_tcpServer.Stop();
				IsServerRunning = false;
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🛑 가상 TCP 결제 단말기 서버가 정지되었습니다.");
			}
			else
			{
				_tcpServer.Start(9000); // 9000번 포트 열기
				IsServerRunning = true;
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🟢 가상 TCP 결제 단말기 서버 가동 (Port: 9000)");
			}
		}

		[RelayCommand]
		private async Task RequestTcpPaymentAsync()
		{
			if (TotalAmount == 0)
			{
				MessageBox.Show("주문할 상품을 먼저 선택해주세요!", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			try
			{
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🌐 결제 단말기(Port:9000)로 승인 요청 중...");

				if (!_tcpClient.IsConnected)
				{
					await _tcpClient.ConnectAsync("127.0.0.1", 9000);
				}

				string reqPacket = $"PAY_REQ|{TotalAmount}";
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ⬆️ TCP 전송: {reqPacket}");

				string? resPacket = await _tcpClient.SendCommandAsync(reqPacket);

				if (resPacket != null)
				{
					OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ⬇️ TCP 수신: {resPacket}");

					if (reqPacket.StartsWith("PAY_RES|SUCCESS"))
					{
						OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🎉 신용카드 결제가 정상적으로 완료되었습니다!");
						TotalAmount = 0;
					}
				}
			}
			catch (SocketException)
			{
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ❌ 연결 실패! 먼저 좌측 하단의 [🟢 가상 결제단말기 가동] 버튼을 눌러 서버를 켜주세요.");
			} catch (Exception ex) {
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ❌ 통신 오류: {ex.Message}");
			}
		}

		[RelayCommand]
		private async Task PrintReceiptAsync()
		{
			if (Products.Count == 0 || TotalAmount == 0)
			{
				MessageBox.Show("출력할 주문 내역이 없습니다!", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			try
			{
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🖨️ 영수증 출력 중... ({ComPortName})");

				_printer.Open(ComPortName, 9600);

				await _printer.PrintReceiptAsync(Products, TotalAmount);

				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🎉 영수증 출력 및 용지 컷팅 완료!");
			}
			catch (Exception ex)
			{
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ❌ 프린터 오류: {ex.Message}");
				MessageBox.Show($"시리얼 프린터 연결 실패!\n{ex.Message}\n\n하드웨어가 없다면 COM 포트 번호나 가상 시리얼 포트(COM0COM)를 확인하세요.",
								"프린터 에러", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				_printer.Close();
			}
		}
	}
}
