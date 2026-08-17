using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ray.MemoryManager.Services;

public static class UiVersionService
{
    public static void ApplyReleaseLabels(DependencyObject root)
    {
        Visit(root);
    }

    static void Visit(DependencyObject node)
    {
        if (node is TextBlock text && !string.IsNullOrWhiteSpace(text.Text))
        {
            text.Text = text.Text
                .Replace("v0.9.0-beta.29-dev", UpdateService.CurrentTag, StringComparison.Ordinal)
                .Replace("beta.29 dev", "beta.29", StringComparison.Ordinal);
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++) Visit(VisualTreeHelper.GetChild(node, i));
    }
}
