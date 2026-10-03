using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BuildCore
{
    public sealed partial class MainWindow
    {
        private async void ApplySelectedOptimizationsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            List<OptimizationTweakDefinition> selected =
                OptimizationLibrary.Groups
                    .SelectMany(group => group.Tweaks)
                    .Where(tweak => _selectedLibraryTweaks.Contains(tweak.Id))
                    .ToList();

            if (selected.Count == 0)
            {
                RefreshLibrarySelectionUi();
                return;
            }

            var panel = new StackPanel { Spacing = 8 };

            panel.Children.Add(new TextBlock
            {
                Text = "BuildCore will create a snapshot before applying any supported changes. Tweaks without a verified handler will be skipped.",
                TextWrapping = TextWrapping.Wrap
            });

            foreach (OptimizationTweakDefinition tweak in selected)
            {
                OptimizationTweakHandler? handler =
                    OptimizationTweakHandlerRegistry.Find(tweak.Id);

                panel.Children.Add(new TextBlock
                {
                    Text =
                        $"{tweak.Title}  •  " +
                        (handler == null
                            ? "NO HANDLER"
                            : handler.CanApply
                                ? "APPLY READY"
                                : "REVIEW ONLY"),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var dialog = new ContentDialog
            {
                Title = $"CONFIRM — {selected.Count} SELECTED",
                Content = new ScrollViewer
                {
                    Content = panel,
                    MaxHeight = 420
                },
                PrimaryButtonText = "CREATE SNAPSHOT & APPLY",
                CloseButtonText = "CANCEL",
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            ApplySelectedOptimizationsButton.IsEnabled = false;

            try
            {
                SelectedOptimizationApplyResult result =
                    SelectedOptimizationApplyService.Apply(
                        _selectedLibraryTweaks);

                var resultPanel = new StackPanel { Spacing = 7 };

                resultPanel.Children.Add(new TextBlock
                {
                    Text = $"Snapshot: {result.SnapshotId}",
                    TextWrapping = TextWrapping.Wrap
                });

                foreach (SelectedOptimizationApplyItem item in result.Items)
                {
                    resultPanel.Children.Add(new TextBlock
                    {
                        Text = $"{item.Title}: {item.Status}",
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                var resultDialog = new ContentDialog
                {
                    Title = "OPTIMIZATION RESULTS",
                    Content = new ScrollViewer
                    {
                        Content = resultPanel,
                        MaxHeight = 420
                    },
                    CloseButtonText = "CLOSE",
                    XamlRoot = Content.XamlRoot
                };

                await resultDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "OPTIMIZATION APPLY FAILED",
                    Content = new TextBlock
                    {
                        Text = ex.Message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = "CLOSE",
                    XamlRoot = Content.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
            finally
            {
                ApplySelectedOptimizationsButton.IsEnabled = true;
                RefreshLibrarySelectionUi();
            }
        }
    }
}
