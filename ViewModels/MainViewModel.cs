using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniPos.Models;
using System.Collections.ObjectModel;

namespace MiniPos.ViewModels
{
	public partial class MainViewModel : ObservableObject
	{
		[ObservableProperty]
		private string _title = "미니 POS 주문 모니터링 시스템 v1.0";

		[ObservableProperty]
		private decimal _totalAmount = 0;

		public ObservableCollection<Product> Products { get; } = new();
		public ObservableCollection<string> OrderLogs { get; } = new();

		public MainViewModel()
		{
			LoadInitialProducts();
		}

		private void LoadInitialProducts()
		{
			Products.Add(new Product { Id = 1, Name = "아메리카노", Price = 4500, Category = "Coffee" });
			Products.Add(new Product { Id = 2, Name = "카페라떼", Price = 5000, Category = "Coffee" });
			Products.Add(new Product { Id = 3, Name = "치즈케이크", Price = 6500, Category = "Dessert" });
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
	}
}
