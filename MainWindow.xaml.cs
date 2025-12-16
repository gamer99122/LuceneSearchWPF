using System.Windows;
using LuceneSearchWPFApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LuceneSearchWPFApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // 透過 DI 取得 MainViewModel 實例並設置為 DataContext
            DataContext = App.ServiceProvider.GetRequiredService<MainViewModel>();
        }
    }
}
