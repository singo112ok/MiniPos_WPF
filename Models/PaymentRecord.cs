using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MiniPos.Models
{
    public class PaymentRecord
    {
        [JsonPropertyName("sale_date")]
        public string SaleDateTime { get; set; } = string.Empty;

        [JsonPropertyName("bill_no")]
        public string BillNo { get; set; } = string.Empty;

        [JsonPropertyName("total_sale_amt")]
        public decimal TotalSaleAmt { get; set; }

        [JsonPropertyName("total_dc_amt")]
        public decimal TotalDcAmt { get; set; }

        [JsonPropertyName("items")]
        public List<OrderItem> Items { get; set; } = new();
    }
}
