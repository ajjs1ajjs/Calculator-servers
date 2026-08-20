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

    public string DevContacts => AccessService.DevContacts;

    public bool Unlocked { get; private set; }

    private void BtnOk_Click(object? sender, RoutedEventArgs e) => TryUnlock();

    // Відновлення доступу: генерує новий пароль, зберігає його й відкриває поштовий клієнт
    // (mailto:) із контактами розробника та новим паролем у листі.
    private async void BtnRegenerate_Click(object? sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Instance;
        var confirm = await new MessageDialog(loc["access.regenerateTitle"], loc["access.regenerateConfirm"], confirmButton: true)
            .ShowDialog<bool>(this);
        if (!confirm) return;

        var newPassword = _access.RegeneratePassword();

        var subject = Uri.EscapeDataString("[ResourceCalculator] Новий пароль доступу до матриці");
        var body = Uri.EscapeDataString(
            $"Новий пароль для розблокування матриці: {newPassword}\n\n" +
            "Застосуйте його при наступному запуску.\n\n-- Resource Calculator");
        var mailto = $"mailto:{AccessService.DevEmail1}?subject={subject}&body={body}";
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(mailto) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch { /* email client may be unavailable — show the password directly */ }

        await new MessageDialog(loc["access.regenerateTitle"],
                string.Format(loc["access.regenerated"], newPassword) + "\n\n" + AccessService.DevContacts,
                confirmButton: false)
            .ShowDialog<bool>(this);

        // Після перегенерації пароль відомий — розблоковуємо.
        Unlocked = true;
        Close(true);
    }

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