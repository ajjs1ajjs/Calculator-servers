using System.Windows;
using AIResourceCalculator.ViewModels;

namespace AIResourceCalculator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
