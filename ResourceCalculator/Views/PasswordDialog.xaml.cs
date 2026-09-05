using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;

namespace ResourceCalculator.Views;

public partial class PasswordDialog : Window
{
    private readonly AccessService _access;

    public PasswordDialog(AccessService access, Window? owner)
    {
        _access = access;
        InitializeComponent();
        Owner = owner;
        DataContext = this;
        TxtPassword.Focus();
        Loaded += (_, _) => TxtPassword.Focus();
    }

    public string DevContacts => AccessService.DevContacts;

    public bool Unlocked { get; private set; }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => TryUnlock();

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryUnlock();
    }

    // Відновлення доступу: генерує новий пароль, зберігає його й відкриває поштовий клієнт
    // (mailto:) із контактами розробника та новим паролем у листі.
    private void BtnRegenerate_Click(object sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Instance;
        var confirm = MessageBox.Show(
            loc["access.regenerateConfirm"],
            loc["access.regenerateTitle"],
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var newPassword = _access.RegeneratePassword();

        // Відкриваємо поштовий клієнт на адреси розробника з новим паролем у темі/тілі.
        var subject = Uri.EscapeDataString($"[ResourceCalculator] Новий пароль доступу до матриці");
        var body = Uri.EscapeDataString(
            $"Новий пароль для розблокування матриці: {newPassword}\n\n" +
            "Застосуйте його при наступному запуску.\n\n-- Resource Calculator");
        var mailto = $"mailto:{AccessService.DevEmail1}?subject={subject}&body={body}";
        try
        {
            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
        }
        catch { /* email client may be unavailable — show the password directly */ }

        var shown = MessageBox.Show(
            string.Format(loc["access.regenerated"], newPassword) + "\n\n" + AccessService.DevContacts,
            loc["access.regenerateTitle"],
            MessageBoxButton.OK, MessageBoxImage.Information);

        // Після перегенерації пароль відомий — розблоковуємо.
        Unlocked = true;
        DialogResult = true;
        Close();
    }

    private void TryUnlock()
    {
        if (_access.Verify(TxtPassword.Password))
        {
            Unlocked = true;
            DialogResult = true;
            Close();
        }
        else
        {
            TxtError.Visibility = Visibility.Visible;
            TxtPassword.Clear();
            TxtPassword.Focus();
        }
    }
}
