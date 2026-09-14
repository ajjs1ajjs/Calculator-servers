using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;

namespace ResourceCalculator.Views;

public partial class PasswordDialog : Window
{
    // null лише в безпараметрному конструкторі для дизайнера; у рантаймі завжди задано.
    private readonly AccessService? _access;

    public PasswordDialog()
    {
        InitializeComponent();
    }

    public PasswordDialog(AccessService access, Window? owner)
    {
        _access = access;
        InitializeComponent();
        DataContext = this;
        TxtPassword.Focus();
    }

    public string PasswordHint => _access?.GetPasswordHint() ?? "";

    public bool Unlocked { get; private set; }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => TryUnlock();

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryUnlock();
    }

    private void TryUnlock()
    {
        if (_access?.Verify(TxtPassword.Text ?? "") == true)
        {
            Unlocked = true;
            Close(Unlocked);
        }
        else
        {
            TxtError.IsVisible = true;
            TxtPassword.Clear();
            TxtPassword.Focus();
        }
    }
}
