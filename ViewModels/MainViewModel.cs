using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniPos.Models;
using MiniPos.Services;
using MiniPos.Views;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.DataContracts;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;


namespace MiniPos.ViewModels
{
	public partial class MainViewModel : ObservableObject
	{
		private readonly ConcurrentQueue<PaymentRecord> _paymentQueue = new ConcurrentQueue<PaymentRecord>();

		public ObservableCollection<OrderItem> CartItems { get; } = new();

		private readonly IRestApiService _restService;

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
		private double _currentDiscountRate = 0;

		[ObservableProperty]
		private decimal _discountAmount = 0;

		[ObservableProperty]		
		private string _searchName = "";

		[ObservableProperty]
		private double _payProgress = 0;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanPay))]
		private bool _isProcessing = false;

		// 카드 클릭을 AddOrderCommand로 직접 처리하도록 변경 (ListBox 선택 상태 미사용)
		//[ObservableProperty]
		//private Product? _selectedProduct;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(SimulationButtonText))]
		private bool _isSimulationRunning = false;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(ServerButtonText))]
		private bool _isServerRunning = false;

		private readonly JsonSerializerOptions _logJsonOpts = new()
		{
			WriteIndented = true,
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};


		public string SimulationButtonText => IsSimulationRunning ? "⏹️ 실시간 유입 정지" : "▶️ 실시간 유입 시작 (3초 간격)";

		public string ServerButtonText => IsServerRunning ? "🛑 가상 결제단말기 (서버) 정지" : "🟢 가상 결제단말기 (서버) 가동";

		public bool CanPay => !IsProcessing;

		partial void OnCurrentCategoryFileterChanged(string value)
		{
			ProductsView.Refresh();
		}

		partial void OnSearchNameChanged(string value)
		{
			ProductsView.Refresh();
		}

		// 선택 → 주문 추가 → null 되돌리기 방식은 ListBox에 null이 반영되지 않아 폐기
		//partial void OnSelectedProductChanged(Product? value)
		//{
		//	if (value == null) return;
		//
		//	AddOrder(value);
		//
		//	SelectedProduct = null;
		//}


		public ObservableCollection<Product> Products { get; } = new();
		public List<OrderItem> Items { get; set; } = new();
		public ObservableCollection<string> OrderLogs { get; } = new();

		public MainViewModel(IRestApiService restService)
		{
			_restService = restService;

			LoadInitialProducts();

			ProductsView = CollectionViewSource.GetDefaultView(Products);
			ProductsView.Filter = FilterProducts;

			_ = StartBackgroundApiWorkerAsync();
		}

		private async Task StartBackgroundApiWorkerAsync()
		{
			while(true)
			{
				if(_paymentQueue.TryDequeue(out PaymentRecord pendingPayment))
				{
					await Task.Delay(2000);

					string jsonPayload = JsonSerializer.Serialize(pendingPayment, _logJsonOpts);

					await Application.Current.Dispatcher.InvokeAsync(() =>
					{
						ApiPosts.Insert(0, new ApiPost
						{
							Id = ApiPosts.Count + 1,
							UserId = 999,
							Title = $"[결제 자동 전송 성공] {pendingPayment.SaleDateTime}",
							Body = jsonPayload
						});
						OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🌐 백그라운드 결제 데이터 전송 완료! (REST API 탭 확인)");
					});
				}
				else
				{
					await Task.Delay(500);
				}
			}
		}

		private bool FilterProducts(object obj)
		{
			if (obj is Product product)
			{
				//if(CurrentCategoryFileter == "All")
				//	return true;

				//if(string.IsNullOrEmpty(SearchName))
				//	return true;

				return (product.Category == CurrentCategoryFileter || CurrentCategoryFileter == "All")
					&& (product.Name.Contains(SearchName, StringComparison.OrdinalIgnoreCase)
						|| string.IsNullOrEmpty(SearchName) );
			}
			return false;
		}

		[RelayCommand]
		private async Task Payment()
		{
			IsProcessing = true;

			await Task.Delay(1000);
			PayProgress = 50;
			await Task.Delay(1000);
			PayProgress = 100;

			PaymentRecord paymentRecord = new PaymentRecord();
			paymentRecord.TotalSaleAmt = TotalAmount;
			paymentRecord.TotalDcAmt = DiscountAmount;
			paymentRecord.SaleDateTime = $"{DateTime.Now:HH:mm:ss}";

			//test woo
			foreach (var item in CartItems)
			{
				paymentRecord.Items.Add(item);
			}

			string json = JsonSerializer.Serialize(paymentRecord, _logJsonOpts);
			OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🧾 결제 기록\n{json}");

			_paymentQueue.Enqueue(paymentRecord);

			CartItems.Clear();
			CurrentDiscountRate = 0;
			UpdateTotalAmount();

			IsProcessing = false;
		}


		[RelayCommand]
		private void SetCategoryFilter(string category)
		{
			CurrentCategoryFileter = category;
		}

		private void LoadInitialProducts()
		{
			string path = Path.Combine(Environment.CurrentDirectory, "products.json");
			
			if(!File.Exists(path))
			{
				Products.Add(new Product { Id = 1, Name = "아메리카노", Price = 4500, Category = "Coffee", Stock = 3 });
				Products.Add(new Product { Id = 2, Name = "카페라떼", Price = 5000, Category = "Coffee", Stock = 3 });
				Products.Add(new Product { Id = 3, Name = "딸기주물럭", Price = 5000, Category = "Beverage", Stock = 5 });
				Products.Add(new Product { Id = 4, Name = "치즈케이크", Price = 6500, Category = "Dessert", Stock = 4 });

				SaveProducsToFile();
			}
			else
			{
				try
				{
					string jsonText = File.ReadAllText(path);
					List<Product?>? jsonProduct = JsonSerializer.Deserialize<List<Product?>?>(jsonText);
					if (jsonProduct is null)
						return;
					foreach (var product in jsonProduct)
					{
						if (product is null)
							continue;
						Products.Add(product);
					}
				}
				catch(Exception ex)
				{
					OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ❌ 상품 파일 읽기 실패: {ex.Message}");
				}
			}
		}

		private void SaveProducsToFile()
		{
			string path = Path.Combine(Environment.CurrentDirectory, "products.json");
			string json = JsonSerializer.Serialize(Products, _logJsonOpts);
			File.WriteAllText(path, json);
		}

		[RelayCommand]
		private async Task EditProduct(Product productToEdit)
		{
			if (productToEdit == null) return;

			var editViewModel = new ProductAddViewModel
			{
				InputName = productToEdit.Name,
				InputPrice = productToEdit.Price,
				SelectedCategory = productToEdit.Category
			};

			var addProductWindow = new ProductAddWindow
			{
				DataContext = editViewModel,
				Owner = Application.Current.MainWindow
			};

			if(addProductWindow.ShowDialog() == true)
			{
				productToEdit.Name = editViewModel.InputName;
				productToEdit.Price = editViewModel.InputPrice;
				productToEdit.Category = editViewModel.SelectedCategory;

				ProductsView.Refresh();

				SaveProducsToFile();
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ✏️ 상품 수정됨: {productToEdit.Name}");
			}
		}

		[RelayCommand]
		private async Task Discount()
		{
			if (CartItems.Sum(x => x.SubTotal) == 0)
				return;

			var discountViewModel = new DiscountViewModel();

			var discountWindow = new DiscountWindow
			{
				DataContext = discountViewModel,
				Owner = Application.Current.MainWindow
			};

			if(discountWindow.ShowDialog() == true)
			{
				CurrentDiscountRate = discountViewModel.SelectRate;
				UpdateTotalAmount();

				if(CurrentDiscountRate > 0)
				{
					OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 💸 {CurrentDiscountRate * 100}% 할인 적용");
				}
			}


		}

		[RelayCommand]
		private async Task ResetProduct()
		{
			string path = Path.Combine(Environment.CurrentDirectory, "products.json");
			File.Delete(path);
			Products.Clear();

			Products.Add(new Product { Id = 1, Name = "아메리카노", Price = 4500, Category = "Coffee", Stock = 3 });
			Products.Add(new Product { Id = 2, Name = "카페라떼", Price = 5000, Category = "Coffee", Stock = 3 });
			Products.Add(new Product { Id = 3, Name = "딸기주물럭", Price = 5000, Category = "Beverage", Stock = 5 });
			Products.Add(new Product { Id = 4, Name = "치즈케이크", Price = 6500, Category = "Dessert", Stock = 4 });

			SaveProducsToFile();
		}

		[RelayCommand]
		private async Task DelProduct(Product productToRemove)
		{
			if (productToRemove == null) return;

			var result = System.Windows.MessageBox.Show(
				$"'{productToRemove.Name}' 상품을 삭제하시겠습니까?",
				"상품 삭제 확인",
				System.Windows.MessageBoxButton.YesNo,
				System.Windows.MessageBoxImage.Question);

			if(result == System.Windows.MessageBoxResult.Yes)
			{
				Products.Remove(productToRemove);

				ProductsView.Refresh();

				SaveProducsToFile();
				OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🗑️ 상품 삭제됨: {productToRemove.Name}");
			}
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
					Category = addProductViewModel.SelectedCategory,
					Stock = 5
				});

				SaveProducsToFile();
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

			if( selectedProduct.Stock <= 0 ) return;

			var existingItem = CartItems.FirstOrDefault(x=> x.ProductInfo.Id == selectedProduct.Id);
			if (existingItem != null) 
			{
				existingItem.Quantity++;
			}
			else
			{
				CartItems.Add(new OrderItem {  ProductInfo =selectedProduct });
			}
			selectedProduct.Stock--;
			UpdateTotalAmount();
			
			//TotalAmount += selectedProduct.Price;
			//OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {selectedProduct.Name} 주문 (+{selectedProduct.Price:N0}원)");
		}

		//[RelayCommand]
		//private void ClearOrder()
		//{
		//	TotalAmount = 0;
		//	OrderLogs.Clear();
		//	OrderLogs.Add($"[{DateTime.Now:HH:mm:ss}] 주문 내역이 초기화되었습니다");
		//}

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

					if (resPacket.StartsWith("PAY_RES|SUCCESS"))
					{
						OrderLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🎉 신용카드 결제가 정상적으로 완료되었습니다!");
						Payment();
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

		[RelayCommand]
		private void IncreaseQuantity(OrderItem item)
		{
			if(item != null)
			{
				if (item.ProductInfo.Stock <= 0)
					return;

				item.Quantity++;
				item.ProductInfo.Stock--;
				UpdateTotalAmount();
			}
		}

		[RelayCommand]
		private void DecreaseQuantity(OrderItem item)
		{
			if (item != null)
			{
				if( item.Quantity > 1)
				{
					item.Quantity--;
				}
				else
				{
					CartItems.Remove(item);
				}
				item.ProductInfo.Stock++;
				UpdateTotalAmount();
			}
		}

		[RelayCommand]
		private void ClearOrder()
		{
			foreach(var item in CartItems)
			{
				item.ProductInfo.Stock += item.Quantity;
			}

			CartItems.Clear();
			UpdateTotalAmount();
		}


		private void UpdateTotalAmount()
		{
			decimal subTotal = CartItems.Sum(x => x.SubTotal);

			DiscountAmount = subTotal * (decimal)CurrentDiscountRate;

			TotalAmount = subTotal - DiscountAmount;
		}
	}
}
