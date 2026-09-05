using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ResourceCalculator.Avalonia.Views;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Services;

namespace ResourceCalculator.Avalonia.Dialogs;

// Avalonia-реалізація діалогів, вибору файлу та перемикання теми (Linux/macOS/Windows).
public class AvaloniaDialogService : IDialogService, IFileSaveService
{
    private readonly AccessService _access;
    private readonly Func<Window?> _owner;

    public AvaloniaDialogService(AccessService access, Func<Window?> owner)
    {
        _access = access;
        _owner = owner;
    }

    public async Task<bool> ConfirmAsync(string message, string title)
    {
        var owner = _owner();
        if (owner is null) return false;
        var dialog = new MessageDialog(title, message, confirmButton: true);
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task InfoAsync(string message, string title)
    {
        var owner = _owner();
        if (owner is null) return;
        var dialog = new MessageDialog(title, message, confirmButton: false);
        await dialog.ShowDialog<bool>(owner);
    }

    public async Task ErrorAsync(string message, string title)
    {
        var owner = _owner();
        if (owner is null) return;
        var dialog = new MessageDialog(title, message, confirmButton: false, isError: true);
        await dialog.ShowDialog<bool>(owner);
    }

    public async Task<bool> ShowPasswordDialogAsync()
    {
        var owner = _owner();
        if (owner is null) return false;
        var dialog = new PasswordDialog(_access);
        await dialog.ShowDialog<bool>(owner);
        return dialog.Unlocked;
    }

    public async Task ShowChangePasswordDialogAsync()
    {
        var owner = _owner();
        if (owner is null) return;
        await new ChangePasswordDialog(_access).ShowDialog<bool>(owner);
    }

    public async Task<string?> PickSavePathAsync(string defaultFileName, string filterDescription, string extension)
    {
        var owner = _owner();
        if (owner is null) return null;
        var storage = owner.StorageProvider;
        var result = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = defaultFileName,
            DefaultExtension = extension.TrimStart('.'),
            FileTypeChoices = new[]
            {
                new FilePickerFileType(filterDescription) { Patterns = new[] { $"*{extension}" } }
            }
        });
        return result?.TryGetLocalPath();
    }
}

public class AvaloniaThemeService : IThemeService
{
    public bool IsDark => Themes.ThemeService.IsDark;
    public void SetDark(bool dark) => Themes.ThemeService.SetDark(dark);
}