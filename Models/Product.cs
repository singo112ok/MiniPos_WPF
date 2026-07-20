using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPos.Models
{
	public class Product
	{
		public int Id { get; set; }
		public string Name { get; set; } = String.Empty;
		public decimal Price { get; set; }
		public string Category { get; set; } = String.Empty;
	}
}
