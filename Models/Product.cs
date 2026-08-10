using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace MiniPos.Models
{
	public partial class Product : ObservableObject
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; } = String.Empty;

		[JsonPropertyName("price")]
		public decimal Price { get; set; }

		[JsonPropertyName("category")]
		public string Category { get; set; } = String.Empty;

		[property: JsonPropertyName("stock")]
		[ObservableProperty]
		private int _stock;
	}
}
