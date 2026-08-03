using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPos.Models
{
	public partial class OrderItem : ObservableObject
	{
		public Product ProductInfo { get; set; }

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(SubTotal))]
		private int _quantity = 1;

		public decimal SubTotal => ProductInfo.Price * Quantity;
	}
}
