using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;

namespace ResourceCalculator.Views;

public partial class PasswordDialog : Window
{
    // null лише в безпараметрному конструкторі для дизайнера; у рантаймі завжди задано.
    private readonly AccessService? _access;
    private readonly bool _setupMode;

    public PasswordDialog()
    {
        InitializeComponent();
    }

    public PasswordDialog(AccessService access, Window? owner)
    {
        _access = access;
        // Fail closed: за відсутності settings.json вбудованого пароля більше немає —
        // перший запуск вимагає СТВОРИТИ пароль, а не вгадати дефолт.
        _setupMode = !access.IsPasswordSet;
        InitializeComponent();
        DataContext = this;
        var loc = LocalizationService.Instance;
        if (_setupMode)
        {
            UnlockPanel.IsVisible = false;
            SetupPanel.IsVisible = true;
            ForgotPanel.IsVisible = false;
            TxtTitle.Text = loc["access.setupTitle"];
            TxtSubtitle.Text = loc["access.setupSubtitle"];
            BtnOk.Content = loc["access.create"];
            TxtNewPassword.Focus();
        }
        else
        {
            TxtPassword.Focus();
        }
        // Контакти підтримки: помітні, клікабельні, з копіюванням. Телефон не показуємо.
        Email1Text.Text = AccessService.DevEmail1;
        Email1Text.Tag = AccessService.DevEmail1;
        Email1WriteBtn.Tag = AccessService.DevEmail1;
        Email1CopyBtn.Tag = AccessService.DevEmail1;
        Email2Text.Text = AccessService.DevEmail2;
        Email2Text.Tag = AccessService.DevEmail2;
        Email2WriteBtn.Tag = AccessService.DevEmail2;
        Email2CopyBtn.Tag = AccessService.DevEmail2;
    }

    public string PasswordHint => _access?.GetPasswordHint() ?? "";

    public bool Unlocked { get; private set; }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (_setupMode) TryCreate();
        else TryUnlock();
    }

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_setupMode) TryCreate();
            else TryUnlock();
        }
    }

    private void TryUnlock()
    {
        if (_access is null) return;
        var remaining = _access.LockoutRemaining;
        if (remaining > TimeSpan.Zero)
        {
            ShowError(string.Format(LocalizationService.Instance["access.lockedOut"], (int)remaining.TotalSeconds));
            return;
        }
        // Платформне обмеження: в Avalonia 12 немає штатного PasswordBox —
        // лишається TextBox + PasswordChar. Компенсації: очищення контрола одразу
        // після перевірки та тротлінг спроб на рівні AccessService.
        var pwd = TxtPassword.Text ?? "";
        var ok = _access.Verify(pwd);
        TxtPassword.Clear();
        if (ok)
        {
            Unlocked = true;
            Close(Unlocked);
        }
        else
        {
            var wait = _access.LockoutRemaining;
            ShowError(wait > TimeSpan.Zero
                ? string.Format(LocalizationService.Instance["access.lockedOut"], (int)wait.TotalSeconds)
                : LocalizationService.Instance["access.error"]);
            TxtPassword.Focus();
        }
    }

    private void TryCreate()
    {
        if (_access is null) return;
        var loc = LocalizationService.Instance;
        var p1 = TxtNewPassword.Text ?? "";
        var p2 = TxtConfirmPassword.Text ?? "";
        TxtNewPassword.Clear();
        TxtConfirmPassword.Clear();
        if (p1.Length < AccessService.MinPasswordLength)
        {
            ShowError(loc["access.errorTooShort"]);
            TxtNewPassword.Focus();
            return;
        }
        if (!string.Equals(p1, p2, StringComparison.Ordinal))
        {
            ShowError(loc["access.errorMismatch"]);
            TxtNewPassword.Focus();
            return;
        }
        try
        {
            _access.SetPassword(p1);
            Unlocked = true;
            Close(Unlocked);
        }
        catch (Exception ex)
        {
            ShowError($"{loc["access.changeError"]} {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.IsVisible = true;
    }

    // Клік по адресі — написати листа (поштовий клієнт за замовчуванням).
    private void Email_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as TextBlock)?.Tag is string email && !string.IsNullOrEmpty(email))
            OpenMail(email);
    }

    private void EmailWrite_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string email && !string.IsNullOrEmpty(email))
            OpenMail(email);
    }

    private static void OpenMail(string email)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"mailto:{email}")
            {
                UseShellExecute = true
            });
        }
        catch { }
    }

    // Копіювання адреси в буфер обміну з підтвердженням на кнопці.
    private async void EmailCopy_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string email || string.IsNullOrEmpty(email))
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        try
        {
            await clipboard.SetTextAsync(email);
            var loc = LocalizationService.Instance;
            var original = btn.Content;
            btn.Content = "✓ " + loc["access.copied"];
            await System.Threading.Tasks.Task.Delay(1500);
            btn.Content = original;
        }
        catch { }
    }
}
