using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPos.ViewModels
{
    public partial class DiscountViewModel : ObservableObject
    {
		[ObservableProperty]
		private double _selectRate = 0;

		[RelayCommand]
		private void SetRate(string rateStr)
		{
			if (double.TryParse(rateStr, out double rate))
			{
				SelectRate = rate;
			}
		}

		[RelayCommand]
		private async Task Apply(System.Windows.Window window)
		{
			if (window != null)
			{
				window.DialogResult = true;
				window.Close();
			}
		}
		public DiscountViewModel() { }

    }
}
