using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;
using ResourceCalculator.Views;

namespace ResourceCalculator.Dialogs;

public enum MessageBoxButtons { OK, YesNo }
public enum MessageBoxImage { None, Information, Warning, Error }
public enum MessageBoxResult { OK, Yes, No }

public static class MessageBox
{
    public static async Task<MessageBoxResult> Show(Window? owner, string text, string caption, MessageBoxButtons buttons, MessageBoxImage icon)
    {
        var loc = LocalizationService.Instance;
        var tcs = new TaskCompletionSource<MessageBoxResult>();
        var res = Application.Current?.Resources;
        Brush? BrushOf(string key) => res != null && res.TryGetResource(key, null, out var v) ? v as Brush : null;

        var (badgeBg, glyphFg, glyph) = icon switch
        {
            MessageBoxImage.Information => (BrushOf("AccentSoftBg"), BrushOf("AccentBg"), "ⓘ"),
            MessageBoxImage.Warning => (BrushOf("WarningSoftBg"), BrushOf("WarningBg"), "⚠"),
            MessageBoxImage.Error => (BrushOf("DangerSoftBg"), BrushOf("DangerBg"), "✕"),
            _ => ((Brush?)null, BrushOf("TextMuted"), "")
        };

        var dialog = new Window
        {
            Title = caption,
            MinWidth = 420,
            MaxWidth = 540,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            CanResize = false,
            ShowInTaskbar = false,
            Background = BrushOf("WindowBg")
        };

        var panel = new StackPanel { Margin = new Thickness(22) };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        if (!string.IsNullOrEmpty(glyph))
        {
            var badge = new Border
            {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(10),
                Background = badgeBg, Margin = new Thickness(0, 0, 12, 0)
            };
            badge.Child = new TextBlock
            {
                Text = glyph, FontSize = 15, Foreground = glyphFg,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(badge);
        }
        header.Children.Add(new TextBlock
        {
            Text = caption, FontSize = 16, FontWeight = FontWeight.Bold,
            Foreground = BrushOf("TextPrimary"), VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(header);

        panel.Children.Add(new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20),
            FontSize = 13, Foreground = BrushOf("TextPrimary")
        });

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        void AddButton(string label, MessageBoxResult result, bool primary)
        {
            var btn = new Button
            {
                Content = label, MinWidth = 96, Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(16, 7), FontWeight = FontWeight.Bold, FontSize = 13
            };
            if (primary)
            {
                btn.Background = BrushOf("AccentBg");
                btn.Foreground = BrushOf("TextWhite");
                btn.IsDefault = true;
            }
            else
            {
                btn.IsCancel = true;
            }
            btn.Click += (_, __) => { dialog.Close(); tcs.TrySetResult(result); };
            buttonPanel.Children.Add(btn);
        }

        if (buttons == MessageBoxButtons.OK)
        {
            AddButton("OK", MessageBoxResult.OK, primary: true);
        }
        else if (buttons == MessageBoxButtons.YesNo)
        {
            AddButton(loc["dialog.no"], MessageBoxResult.No, primary: false);
            AddButton(loc["dialog.yes"], MessageBoxResult.Yes, primary: true);
        }

        panel.Children.Add(buttonPanel);
        dialog.Content = panel;

        dialog.Closed += (_, __) => tcs.TrySetResult(MessageBoxResult.No);
        if (owner != null) await dialog.ShowDialog<MessageBoxResult>(owner);
        else dialog.Show();

        return await tcs.Task;
    }
}

public class DialogService : IDialogService, IFileSaveService
{
    private readonly AccessService _access;
    private readonly Func<Window?> _owner;

    public DialogService(AccessService access, Func<Window?> owner)
    {
        _access = access;
        _owner = owner;
    }

    public async Task<bool> ConfirmAsync(string message, string title)
    {
        var owner = _owner();
        var result = await MessageBox.Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    public async Task InfoAsync(string message, string title)
    {
        var owner = _owner();
        await MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxImage.Information);
    }

    public async Task ErrorAsync(string message, string title)
    {
        var owner = _owner();
        await MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxImage.Error);
    }

    public async Task<bool> ShowPasswordDialogAsync()
    {
        var owner = _owner();
        if (owner is null) return false;
        var dialog = new PasswordDialog(_access, owner);
        var result = await dialog.ShowDialog<bool>(owner);
        return result;
    }

    public async Task<string?> PickSavePathAsync(string defaultFileName, string filterDescription, string extension)
    {
        var owner = _owner();
        if (owner?.StorageProvider is not { } provider) return null;
        
        var ext = extension.StartsWith(".") ? extension[1..] : extension;
        var choices = new System.Collections.Generic.List<FilePickerFileType>
        {
            new FilePickerFileType(filterDescription) { Patterns = new System.Collections.Generic.List<string> { $"*.{ext}" } }
        };
        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = filterDescription,
            SuggestedFileName = defaultFileName,
            FileTypeChoices = choices
        });
        return file?.Path.LocalPath;
    }
}
