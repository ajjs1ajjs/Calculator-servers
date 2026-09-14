using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace ResourceCalculator.Views;

public partial class ResultsTabControl : UserControl
{
    private void RootScroll_PreviewMouseWheel(object? sender, PointerWheelEventArgs e)
    {
        var inner = FindScrollableAncestor(e.Source as Visual, e.Delta);
        if (inner != null && inner != RootScroll)
            return;

        if (RootScroll.Extent.Height > RootScroll.Viewport.Height)
        {
            RootScroll.Offset = RootScroll.Offset.WithY(
                Math.Max(0, Math.Min(RootScroll.Extent.Height - RootScroll.Viewport.Height,
                    RootScroll.Offset.Y - e.Delta.Y)));
        }
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollableAncestor(Visual? from, Vector delta)
    {
        for (var node = from; node != null; node = node.Parent as Visual)
        {
            if (node is ScrollViewer sv)
            {
                var maxOffset = sv.Extent.Height - sv.Viewport.Height;
                if (maxOffset <= 0) continue;
                bool canUp = delta.Y > 0 && sv.Offset.Y > 0;
                bool canDown = delta.Y < 0 && sv.Offset.Y < maxOffset;
                if (canUp || canDown) return sv;
            }
        }
        return null;
    }

    public ResultsTabControl()
    {
        InitializeComponent();
    }
}
