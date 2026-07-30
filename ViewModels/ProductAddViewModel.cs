using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MiniPos.Models;

namespace MiniPos.ViewModels
{
	public partial class ProductAddViewModel : ObservableObject
	{
		[ObservableProperty]
		private string _inputName = string.Empty;

		[ObservableProperty]
		private decimal _inputPrice = 0;

		[ObservableProperty]
		private string _selectedCategory = "Coffee";

		public List<String> Categories { get; } = new() { "Coffee", "Dessert", "Beverage" };

		public ProductAddViewModel()
		{

		}

	}
}
