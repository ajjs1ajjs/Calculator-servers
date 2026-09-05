using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;

namespace ResourceCalculator.Avalonia.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly AccessService _access;

    public ChangePasswordDialog(AccessService access)
    {
        _access = access;
        InitializeComponent();
        Opened += (_, _) => Dispatcher.UIThread.Post(() => TxtCurrent.Focus());
        TxtCurrent.KeyDown += OnKeyDown;
        TxtNew.KeyDown += OnKeyDown;
        TxtConfirm.KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TrySave();
    }

    private void BtnSave_Click(object? sender, RoutedEventArgs e) => TrySave();

    private async void TrySave()
    {
        var current = TxtCurrent.Text;
        var newPass = TxtNew.Text;
        var confirm = TxtConfirm.Text;

        if (string.IsNullOrEmpty(newPass) || newPass.Length < 8)
        {
            ShowError("access.errorTooShort");
            return;
        }
        if (newPass != confirm)
        {
            ShowError("access.errorMismatch");
            return;
        }
        if (!_access.ChangePassword(current ?? string.Empty, newPass))
        {
            ShowError("access.errorWrongCurrent");
            return;
        }

        var loc = LocalizationService.Instance;
        await new MessageDialog(loc["access.changedTitle"], loc["access.changed"], confirmButton: false)
            .ShowDialog<bool>(this);
        Close(true);
    }

    private void ShowError(string key)
    {
        TxtError.Text = LocalizationService.Instance[key];
        TxtError.IsVisible = true;
        TxtCurrent.Clear();
        TxtNew.Clear();
        TxtConfirm.Clear();
        TxtCurrent.Focus();
    }
}