using System.Windows;
using System.Windows.Input;
using ResourceCalculator.Services;

namespace ResourceCalculator.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly AccessService _access;

    public ChangePasswordDialog(AccessService access, Window? owner)
    {
        _access = access;
        InitializeComponent();
        Owner = owner;
        Loaded += (_, _) => TxtCurrent.Focus();
    }

    private void Txt_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TrySave();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e) => TrySave();

    private void TrySave()
    {
        var current = TxtCurrent.Password;
        var newPass = TxtNew.Password;
        var confirm = TxtConfirm.Password;

        if (newPass.Length < 8)
        {
            ShowError("access.errorTooShort");
            return;
        }
        if (newPass != confirm)
        {
            ShowError("access.errorMismatch");
            return;
        }
        if (!_access.ChangePassword(current, newPass))
        {
            ShowError("access.errorWrongCurrent");
            return;
        }

        var loc = ResourceCalculator.Localization.LocalizationService.Instance;
        MessageBox.Show(loc["access.changed"], loc["access.changedTitle"],
            MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
        Close();
    }

    private void ShowError(string key)
    {
        TxtError.Text = ResourceCalculator.Localization.LocalizationService.Instance[key];
        TxtError.Visibility = Visibility.Visible;
        TxtCurrent.Clear();
        TxtNew.Clear();
        TxtConfirm.Clear();
        TxtCurrent.Focus();
    }
}
