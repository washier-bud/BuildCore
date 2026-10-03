using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BuildCore
{
    public sealed partial class MainWindow
    {
        private void RefreshLibrarySelectionUi()
        {
            int selectedCount = _selectedLibraryTweaks.Count;

            OptimizationSelectedCountText.Text =
                $"{selectedCount} SELECTED";

            ClearAllOptimizationsButton.IsEnabled =
                selectedCount > 0;

            ReviewSelectedOptimizationsButton.IsEnabled =
                selectedCount > 0;
        }

        private void SelectAllOptimizationsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            foreach (UIElement child in OptimizationLibraryPanel.Children)
            {
                if (child is not FrameworkElement groupCard ||
                    groupCard.Visibility != Visibility.Visible)
                {
                    continue;
                }

                foreach (ToggleSwitch toggle in FindToggles(groupCard))
                {
                    toggle.IsOn = true;

                    if (toggle.Tag is OptimizationLibraryToggleContext context)
                    {
                        _selectedLibraryTweaks.Add(context.Tweak.Id);
                    }
                }
            }

            RefreshLibrarySelectionUi();
        }

        private void ClearAllOptimizationsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _selectedLibraryTweaks.Clear();

            foreach (UIElement child in OptimizationLibraryPanel.Children)
            {
                if (child is FrameworkElement groupCard)
                {
                    foreach (ToggleSwitch toggle in FindToggles(groupCard))
                    {
                        toggle.IsOn = false;
                    }
                }
            }

            RefreshLibrarySelectionUi();
        }

        private async void ReviewSelectedOptimizationsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RefreshLibrarySelectionUi();

            List<OptimizationTweakDefinition> selected =
                OptimizationLibrary.Groups
                    .SelectMany(group => group.Tweaks)
                    .Where(tweak => _selectedLibraryTweaks.Contains(tweak.Id))
                    .ToList();

            if (selected.Count == 0)
            {
                return;
            }

            var panel = new StackPanel
            {
                Spacing = 10
            };

            panel.Children.Add(new TextBlock
            {
                Text = "Review the selected controls before the application stage.",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });

            foreach (OptimizationTweakDefinition tweak in selected)
            {
                panel.Children.Add(new TextBlock
                {
                    Text =
                        $"{tweak.Title}  •  {tweak.Risk.ToString().ToUpperInvariant()}" +
                        (tweak.RequiresReboot ? "  •  REBOOT" : "") +
                        (tweak.RollbackSupported ? "  •  ROLLBACK READY" : ""),
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.White),
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                });

                panel.Children.Add(new TextBlock
                {
                    Text = tweak.Description,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray),
                    FontSize = 9,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var dialog = new ContentDialog
            {
                Title = $"REVIEW — {selected.Count} SELECTED",
                Content = new ScrollViewer
                {
                    Content = panel,
                    MaxHeight = 420
                },
                CloseButtonText = "CLOSE",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private static IEnumerable<ToggleSwitch> FindToggles(
            DependencyObject root)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(root);

            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child =
                    VisualTreeHelper.GetChild(root, index);

                if (child is ToggleSwitch toggle)
                {
                    yield return toggle;
                }

                foreach (ToggleSwitch nested in FindToggles(child))
                {
                    yield return nested;
                }
            }
        }
    }
}
