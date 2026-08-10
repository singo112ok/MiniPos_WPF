using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Windows;
using MiniPos.Services;
using MiniPos.ViewModels;
using MiniPos.Views;

namespace MiniPos
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Services { get; }
        public App()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IRestApiService, RestApiService>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<MainWindow>();

            Services = services.BuildServiceProvider();

        }


		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

			var mainWindow = Services.GetRequiredService<MainWindow>();

            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();

            mainWindow.Show();			
		}
    }

}
