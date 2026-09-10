using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;

namespace ResourceCalculator.Avalonia.Views;

public partial class PasswordDialog : Window
{
    private readonly AccessService _access;

    public PasswordDialog(AccessService access)
    {
        _access = access;
        InitializeComponent();
        DataContext = this;
        Opened += (_, _) => Dispatcher.UIThread.Post(() => TxtPassword.Focus());
        TxtPassword.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) TryUnlock();
        };
    }

    public string PasswordHint => _access.GetPasswordHint();

    public bool Unlocked { get; private set; }

    private void BtnOk_Click(object? sender, RoutedEventArgs e) => TryUnlock();

private void TryUnlock()
    {
        if (_access.Verify(TxtPassword.Text ?? string.Empty))
        {
            Unlocked = true;
            Close(true);
        }
        else
        {
            TxtError.IsVisible = true;
            TxtPassword.Clear();
            TxtPassword.Focus();
        }
    }
}