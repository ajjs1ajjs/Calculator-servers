using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ResourceCalculator.Avalonia.Views;

public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message, bool confirmButton, bool isError = false)
    {
        InitializeComponent();
        TxtTitle.Text = title;
        TxtMessage.Text = message;
        BtnOk.IsVisible = !confirmButton;
        BtnYes.IsVisible = confirmButton;
        BtnNo.IsVisible = confirmButton;
        if (isError) TxtTitle.Foreground = this.FindResource("DangerBg") as IBrush ?? TxtTitle.Foreground;
    }

    private void BtnYes_Click(object? sender, RoutedEventArgs e) => Close(true);
    private void BtnNo_Click(object? sender, RoutedEventArgs e) => Close(false);
    private void BtnOk_Click(object? sender, RoutedEventArgs e) => Close(true);
}