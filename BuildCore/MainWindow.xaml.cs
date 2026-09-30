using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BuildCore
{
    [System.Runtime.Versioning.SupportedOSPlatform(
        "windows10.0.17763.0")]
    public sealed partial class MainWindow : Window
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly PerformanceService _performanceService;

        private readonly PerformanceHistory _performanceHistory;

        private readonly HardwareMonitorService
            _hardwareMonitorService;

        private readonly BenchmarkService
            _benchmarkService;

        private readonly OptimizationBenchmarkService
            _optimizationBenchmarkService;

        // ============================================================
        // TIMERS
        // ============================================================

        private readonly DispatcherQueueTimer
            _performanceTimer;

        private readonly DispatcherQueueTimer
            _benchmarkUiTimer;

        private DateTime _benchmarkStartedAt;

        private bool _benchmarkRunning;

        private bool _optimizationTestRunning;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public MainWindow()
        {
            this.InitializeComponent();

            _performanceService =
                new PerformanceService();

            _performanceHistory =
                new PerformanceHistory();

            _hardwareMonitorService =
                new HardwareMonitorService();

            _benchmarkService =
                new BenchmarkService(
                    _performanceService,
                    _hardwareMonitorService);

            _optimizationBenchmarkService =
    new OptimizationBenchmarkService(
        new ReliableBenchmarkService(_benchmarkService));

            LoadSystemInformation();

            LoadWindowsSystemInformation();

            _performanceTimer =
                DispatcherQueue.CreateTimer();

            _performanceTimer.Interval =
                TimeSpan.FromSeconds(1);

            _performanceTimer.Tick +=
                UpdatePerformance;

            _performanceTimer.Start();

            _benchmarkUiTimer =
                DispatcherQueue.CreateTimer();

            _benchmarkUiTimer.Interval =
                TimeSpan.FromMilliseconds(100);

            _benchmarkUiTimer.Tick +=
                UpdateBenchmarkProgress;

            ShowDashboard();

            LoadSnapshotInformation();
        }

        // ============================================================
        // SYSTEM INFORMATION
        // ============================================================

        private void LoadSystemInformation()
        {
            SystemInfo info =
                SystemInfoService.GetSystemInfo();

            CpuText.Text =
                info.Cpu;

            GpuText.Text =
                info.Gpu;

            RamText.Text =
                info.Ram;

            StorageText.Text =
                info.Storage;
        }

        private void LoadWindowsSystemInformation()
        {
            WindowsSystemData data =
                WindowsSystemService.Scan();

            WindowsVersionText.Text =
                data.WindowsVersion;

            WindowsBuildText.Text =
                $"Build {data.WindowsBuild}";

            PowerPlanText.Text =
                data.PowerPlan;

            GameModeText.Text =
                data.GameModeEnabled
                    ? "Enabled"
                    : "Disabled";

            HagsText.Text =
                data.HagsEnabled
                    ? "Enabled"
                    : "Disabled";

            MemoryIntegrityText.Text =
                data.MemoryIntegrityEnabled
                    ? "Enabled"
                    : "Disabled";

            DefenderText.Text =
                data.DefenderEnabled
                    ? "Enabled"
                    : "Disabled";

            GameBarText.Text =
                data.XboxGameBarEnabled
                    ? "Enabled"
                    : "Disabled";

            WindowsUpdateText.Text =
                data.WindowsUpdateStatus;

            ProcessorText.Text =
                $"{data.ProcessorCount} cores / " +
                $"{data.LogicalProcessorCount} threads";
        }

        // ============================================================
        // WINDOWS SCAN
        // ============================================================

        private void ScanWindowsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoadWindowsSystemInformation();

            WindowsScanStatusText.Text =
                "SCAN COMPLETE";

            RunOptimizationAnalysis();
        }

        // ============================================================
        // OPTIMIZATION ANALYSIS
        // ============================================================

        private List<OptimizationRecommendation>
            AnalyzeSystem()
        {
            WindowsSystemData data =
                WindowsSystemService.Scan();

            return OptimizationAnalyzer.Analyze(
                data);
        }

        private void RunOptimizationAnalysis()
        {
            try
            {
                var recommendations =
                    AnalyzeSystem();

                OptimizationCountText.Text =
                    recommendations.Count.ToString();

                OptimizationStatusText.Text =
                    "Analysis complete";

                OptimizationSummaryText.Text =
                    $"BuildCore found " +
                    $"{recommendations.Count} system checks.";

                RecommendationsPanel.Children.Clear();

                foreach (var recommendation
                    in recommendations)
                {
                    AddRecommendationCard(
                        recommendation);
                }

                Debug.WriteLine(
                    "================================");

                Debug.WriteLine(
                    "BUILDCORE OPTIMIZATION ANALYSIS");

                Debug.WriteLine(
                    "================================");

                foreach (var recommendation
                    in recommendations)
                {
                    Debug.WriteLine(
                        $"[{recommendation.Category}] " +
                        $"{recommendation.Title}");

                    Debug.WriteLine(
                        $"Current: " +
                        $"{recommendation.CurrentValue}");

                    Debug.WriteLine(
                        $"Recommended: " +
                        $"{recommendation.RecommendedValue}");

                    Debug.WriteLine(
                        $"Risk: " +
                        $"{recommendation.Risk}");

                    Debug.WriteLine(
                        $"Impact: " +
                        $"{recommendation.Impact}");

                    Debug.WriteLine(
                        $"Can Apply: " +
                        $"{recommendation.CanApply}");

                    Debug.WriteLine(
                        $"Can Test: " +
                        $"{recommendation.CanTest}");

                    Debug.WriteLine(
                        $"Requires Reboot: " +
                        $"{recommendation.RequiresReboot}");

                    Debug.WriteLine(
                        $"Rollback Supported: " +
                        $"{recommendation.RollbackSupported}");

                    Debug.WriteLine(
                        $"Test Type: " +
                        $"{recommendation.TestType}");

                    Debug.WriteLine(
                        $"Action Status: " +
                        $"{recommendation.ActionStatus}");

                    Debug.WriteLine(
                        "--------------------------------");
                }
            }
            catch (Exception ex)
            {
                OptimizationStatusText.Text =
                    "Analysis failed";

                OptimizationSummaryText.Text =
                    "BuildCore could not complete " +
                    "the system analysis.";

                Debug.WriteLine(
                    "BUILDCORE ANALYSIS ERROR");

                Debug.WriteLine(
                    ex.ToString());
            }
        }

        // ============================================================
        // RECOMMENDATION CARDS
        // ============================================================

        private void AddRecommendationCard(
            OptimizationRecommendation recommendation)
        {
            var border =
                new Border
                {
                    Background =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Transparent),

                    CornerRadius =
                        new CornerRadius(12),

                    Padding =
                        new Thickness(20),

                    Margin =
                        new Thickness(0, 0, 0, 10)
                };

            var panel =
                new StackPanel();

            var topRow =
                new Grid();

            topRow.ColumnDefinitions.Add(
                new ColumnDefinition());

            topRow.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(180)
                });

            var titlePanel =
                new StackPanel();

            var title =
                new TextBlock
                {
                    Text =
                        GetCategoryIcon(
                            recommendation.Category)
                        + "  "
                        + recommendation.Title,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.White),

                    FontSize = 15,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold
                };

            var category =
                new TextBlock
                {
                    Text =
                        recommendation.Category
                            .ToString()
                            .ToUpperInvariant(),

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),

                    FontSize = 9,

                    Margin =
                        new Thickness(0, 5, 0, 0)
                };

            titlePanel.Children.Add(title);

            titlePanel.Children.Add(category);

            Grid.SetColumn(
                titlePanel,
                0);

            topRow.Children.Add(
                titlePanel);

            var riskPanel =
                new StackPanel
                {
                    HorizontalAlignment =
                        HorizontalAlignment.Right
                };

            var risk =
                new TextBlock
                {
                    Text =
                        $"RISK  " +
                        $"{recommendation.Risk.ToString().ToUpperInvariant()}",

                    Foreground =
                        GetRiskBrush(
                            recommendation.Risk),

                    FontSize = 9,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold
                };

            var impact =
                new TextBlock
                {
                    Text =
                        $"IMPACT  " +
                        $"{recommendation.Impact.ToString().ToUpperInvariant()}",

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),

                    FontSize = 9,

                    Margin =
                        new Thickness(0, 5, 0, 0)
                };

            riskPanel.Children.Add(risk);

            riskPanel.Children.Add(impact);

            Grid.SetColumn(
                riskPanel,
                1);

            topRow.Children.Add(
                riskPanel);

            panel.Children.Add(
                topRow);

            var values =
                new Grid
                {
                    Margin =
                        new Thickness(0, 16, 0, 0)
                };

            values.ColumnDefinitions.Add(
                new ColumnDefinition());

            values.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(40)
                });

            values.ColumnDefinitions.Add(
                new ColumnDefinition());

            var currentPanel =
                CreateValuePanel(
                    "CURRENT",
                    recommendation.CurrentValue);

            Grid.SetColumn(
                currentPanel,
                0);

            values.Children.Add(
                currentPanel);

            var arrow =
                new TextBlock
                {
                    Text = "→",

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),

                    FontSize = 18,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            Grid.SetColumn(
                arrow,
                1);

            values.Children.Add(
                arrow);

            var recommendedPanel =
                CreateValuePanel(
                    "RECOMMENDED",
                    recommendation.RecommendedValue);

            Grid.SetColumn(
                recommendedPanel,
                2);

            values.Children.Add(
                recommendedPanel);

            panel.Children.Add(values);

            var description =
                new TextBlock
                {
                    Text =
                        recommendation.Description,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGray),

                    FontSize = 11,

                    TextWrapping =
                        TextWrapping.Wrap,

                    Margin =
                        new Thickness(0, 16, 0, 0)
                };

            panel.Children.Add(
                description);

            var reason =
                new TextBlock
                {
                    Text =
                        "WHY: " +
                        recommendation.Reason,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),

                    FontSize = 10,

                    TextWrapping =
                        TextWrapping.Wrap,

                    Margin =
                        new Thickness(0, 7, 0, 0)
                };

            panel.Children.Add(
                reason);

            // ========================================================
            // PHASE 1.11C ACTION ROW
            // ========================================================

            var actionRow =
                new Grid
                {
                    Margin =
                        new Thickness(0, 18, 0, 0)
                };

            actionRow.ColumnDefinitions.Add(
                new ColumnDefinition());

            actionRow.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(300)
                });

            var status =
                new TextBlock
                {
                    Text =
                        recommendation.ActionStatus,

                    Foreground =
                        GetActionStatusBrush(
                            recommendation),

                    FontSize = 9,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    TextWrapping =
                        TextWrapping.Wrap
                };

            Grid.SetColumn(
                status,
                0);

            actionRow.Children.Add(
                status);

            // ========================================================
            // TEST BUTTON
            // ========================================================

            if (recommendation.CanTest)
            {
                var buttonPanel =
                    new StackPanel
                    {
                        Orientation =
                            Orientation.Horizontal,

                        HorizontalAlignment =
                            HorizontalAlignment.Right,

                        Spacing = 8
                    };

                var testButton =
                    new Button
                    {
                        Content =
                            recommendation.RequiresReboot
                                ? "TEST • REBOOT"
                                : "TEST",

                        Padding =
                            new Thickness(12, 8, 12, 8),

                        Tag =
                            new TestButtonContext
                            {
                                Recommendation =
                                    recommendation,

                                StatusText =
                                    status
                            }
                    };

                testButton.Click +=
                    RunOptimizationTestButton_Click;

                buttonPanel.Children.Add(
                    testButton);

                // ====================================================
                // APPLY BUTTON
                // ====================================================

                if (recommendation.CanApply)
                {
                    var applyButton =
                        new Button
                        {
                            Content =
                                "APPLY",

                            Padding =
                                new Thickness(12, 8, 12, 8),

                            Tag =
                                new ApplyButtonContext
                                {
                                    Recommendation =
                                        recommendation,

                                    StatusText =
                                        status
                                }
                        };

                    applyButton.Click +=
                        ApplyOptimizationButton_Click;

                    buttonPanel.Children.Add(
                        applyButton);
                }

                Grid.SetColumn(
                    buttonPanel,
                    1);

                actionRow.Children.Add(
                    buttonPanel);
            }
            else if (recommendation.CanApply)
            {
                var buttonPanel =
                    new StackPanel
                    {
                        Orientation =
                            Orientation.Horizontal,

                        HorizontalAlignment =
                            HorizontalAlignment.Right,

                        Spacing = 8
                    };

                var applyButton =
                    new Button
                    {
                        Content =
                            "APPLY",

                        Padding =
                            new Thickness(12, 8, 12, 8),

                        Tag =
                            new ApplyButtonContext
                            {
                                Recommendation =
                                    recommendation,

                                StatusText =
                                    status
                            }
                    };

                applyButton.Click +=
                    ApplyOptimizationButton_Click;

                buttonPanel.Children.Add(
                    applyButton);

                Grid.SetColumn(
                    buttonPanel,
                    1);

                actionRow.Children.Add(
                    buttonPanel);
            }

            panel.Children.Add(
                actionRow);

            // ========================================================
            // EXPERIMENTAL TEST INFORMATION
            // ========================================================

            if (recommendation.CanTest &&
                !string.IsNullOrWhiteSpace(
                    recommendation.TestDescription))
            {
                var testInfo =
                    new TextBlock
                    {
                        Text =
                            "TEST INFO: " +
                            recommendation.TestDescription,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gray),

                        FontSize = 9,

                        TextWrapping =
                            TextWrapping.Wrap,

                        Margin =
                            new Thickness(0, 9, 0, 0)
                    };

                panel.Children.Add(
                    testInfo);
            }

            border.Child =
                panel;

            RecommendationsPanel.Children.Add(
                border);
        }

        private class ApplyButtonContext
        {
            public OptimizationRecommendation
                Recommendation
            { get; set; } =
                new OptimizationRecommendation();

            public TextBlock StatusText { get; set; } =
                new TextBlock();
        }

        private class TestButtonContext
        {
            public OptimizationRecommendation
                Recommendation
            { get; set; } =
                new OptimizationRecommendation();

            public TextBlock StatusText { get; set; } =
                new TextBlock();
        }

        private class OptimizationTestButtonContext
        {
            public OptimizationBenchmarkResult Test { get; set; } =
                new OptimizationBenchmarkResult();

            public ContentDialog ParentDialog { get; set; } =
                null!;
        }

        // ============================================================
        // CONTROLLED OPTIMIZATION TEST
        // ============================================================

        private async void RunOptimizationTestButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_optimizationTestRunning)
                return;

            if (sender is not Button button)
                return;

            if (button.Tag is not TestButtonContext context)
                return;

            OptimizationRecommendation recommendation =
                context.Recommendation;

            TextBlock statusText =
                context.StatusText;

            // ========================================================
            // REBOOT-BASED EXPERIMENTS
            // ========================================================

            if (recommendation.RequiresReboot)
            {
                OptimizationResultsBorder.Visibility =
                    Visibility.Visible;

                OptimizationResultsPanel.Children.Clear();

                OptimizationResultsTitleText.Text =
                    recommendation.Title;

                OptimizationResultsStatusText.Text =
                    "REBOOT-BASED TEST NOT READY";

                OptimizationResultsStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold);

                OptimizationResultsSummaryText.Text =
                    "This optimization requires a Windows restart. " +
                    "BuildCore has not yet enabled the reboot-persistent " +
                    "experiment workflow, so no system setting was changed.";

                statusText.Text =
                    "REBOOT TEST COMING IN 1.11D";

                statusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold);

                Debug.WriteLine(
                    "BUILDCORE EXPERIMENT BLOCKED");

                Debug.WriteLine(
                    $"Optimization '{recommendation.Title}' " +
                    "requires a reboot-persistent test workflow.");

                await ShowRebootTestNotReadyDialog(
                    recommendation);

                return;
            }

            if (_optimizationTestRunning)
                return;

            _optimizationTestRunning =
                true;

            button.IsEnabled =
                false;

            OptimizationResultsBorder.Visibility =
                Visibility.Visible;

            OptimizationResultsTitleText.Text =
                recommendation.Title;

            OptimizationResultsStatusText.Text =
                "STARTING CONTROLLED TEST...";

            OptimizationResultsStatusText.Foreground =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gold);

            OptimizationResultsPanel.Children.Clear();

            OptimizationResultsSummaryText.Text =
                "BuildCore is measuring the system before and after this change.";

            statusText.Text =
                "RUNNING CONTROLLED TEST";

            statusText.Foreground =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gold);

            button.Content =
                "TESTING...";

            OptimizationStatusText.Text =
                "Controlled optimization test running";

            OptimizationSummaryText.Text =
                $"Testing {recommendation.Title}...";

            try
            {
                OptimizationBenchmarkResult result =
                    await _optimizationBenchmarkService.RunAsync(
                        recommendation,
                        5,
                        100,
                        1500);

                DisplayOptimizationBenchmarkResult(
                    result);

                if (result.IsSuccessful)
                {
                    statusText.Text =
                        "TEST COMPLETE";

                    statusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGreen);

                    button.Content =
                        "TEST AGAIN";

                    OptimizationStatusText.Text =
                        "Controlled test complete";

                    OptimizationSummaryText.Text =
                        "BuildCore completed a before/after telemetry comparison.";
                }
                else
                {
                    statusText.Text =
                        "TEST FAILED";

                    statusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.OrangeRed);

                    button.Content =
                        "TEST AGAIN";

                    OptimizationStatusText.Text =
                        "Controlled test failed";

                    OptimizationSummaryText.Text =
                        result.Status;
                }
            }
            catch (Exception ex)
            {
                button.Content =
                    "TEST AGAIN";

                statusText.Text =
                    "TEST FAILED";

                statusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.OrangeRed);

                OptimizationStatusText.Text =
                    "Controlled test failed";

                OptimizationSummaryText.Text =
                    "BuildCore could not complete the controlled test.";

                OptimizationResultsBorder.Visibility =
                    Visibility.Visible;

                OptimizationResultsTitleText.Text =
                    recommendation.Title;

                OptimizationResultsStatusText.Text =
                    "TEST FAILED";

                OptimizationResultsStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.OrangeRed);

                OptimizationResultsSummaryText.Text =
                    ex.Message;

                Debug.WriteLine(
                    "BUILDCORE CONTROLLED TEST UI ERROR");

                Debug.WriteLine(
                    ex.ToString());
            }
            finally
            {
                _optimizationTestRunning =
                    false;

                button.IsEnabled =
                    true;
            }
        }

        private async Task ShowRebootTestNotReadyDialog(
            OptimizationRecommendation recommendation)
        {
            var dialog =
                new ContentDialog
                {
                    Title =
                        "Experimental Test",

                    Content =
                        new TextBlock
                        {
                            Text =
                                $"{recommendation.Title} requires " +
                                "a Windows restart before its new state " +
                                "can be measured.\n\n" +
                                "BuildCore has not changed the setting " +
                                "and will not restart your PC during " +
                                "Phase 1.11C.\n\n" +
                                "The reboot-persistent experiment workflow " +
                                "will be added in Phase 1.11D.",

                            TextWrapping =
                                TextWrapping.Wrap
                        },

                    CloseButtonText =
                        "OK",

                    XamlRoot =
                        ((FrameworkElement)
                            this.Content).XamlRoot
                };

            await dialog.ShowAsync();
        }

        private void DisplayOptimizationBenchmarkResult(
            OptimizationBenchmarkResult result)
        {
            OptimizationResultsBorder.Visibility =
                Visibility.Visible;

            OptimizationResultsPanel.Children.Clear();

            OptimizationResultsTitleText.Text =
                string.IsNullOrWhiteSpace(
                    result.OptimizationTitle)
                    ? "Optimization Test"
                    : result.OptimizationTitle;

            if (!result.IsSuccessful)
            {
                OptimizationResultsStatusText.Text =
                    result.Status;

                OptimizationResultsStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.OrangeRed);

                OptimizationResultsSummaryText.Text =
                    OptimizationResultsFormatter
                        .BuildStatus(result);

                return;
            }

            BenchmarkComparison comparison =
                result.Comparison!;

            OptimizationResultsStatusText.Text =
                "TELEMETRY COMPARISON COMPLETE";

            OptimizationResultsStatusText.Foreground =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGreen);

            OptimizationResultsSummaryText.Text =
                comparison.Summary;

            AddOptimizationResultRow(
                "CPU average",
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.CpuAverageBefore),
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.CpuAverageAfter),
                OptimizationResultsFormatter
                    .FormatPercentageDelta(
                        comparison.CpuAverageDelta));

            AddOptimizationResultRow(
                "CPU peak",
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.CpuPeakBefore),
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.CpuPeakAfter),
                OptimizationResultsFormatter
                    .FormatPercentageDelta(
                        comparison.CpuPeakDelta));

            AddOptimizationResultRow(
                "RAM average",
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.RamAverageBefore),
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.RamAverageAfter),
                OptimizationResultsFormatter
                    .FormatPercentageDelta(
                        comparison.RamAverageDelta));

            AddOptimizationResultRow(
                "RAM peak",
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.RamPeakBefore),
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.RamPeakAfter),
                OptimizationResultsFormatter
                    .FormatPercentageDelta(
                        comparison.RamPeakDelta));

            AddOptimizationResultRow(
                "GPU average",
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.GpuAverageBefore),
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.GpuAverageAfter),
                OptimizationResultsFormatter
                    .FormatPercentageDelta(
                        comparison.GpuAverageDelta));

            AddOptimizationResultRow(
                "GPU peak",
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.GpuPeakBefore),
                OptimizationResultsFormatter
                    .FormatPercentage(
                        comparison.GpuPeakAfter),
                OptimizationResultsFormatter
                    .FormatPercentageDelta(
                        comparison.GpuPeakDelta));

            AddOptimizationResultRow(
                "GPU clock",
                OptimizationResultsFormatter
                    .FormatMegahertz(
                        comparison.GpuClockBeforeMHz),
                OptimizationResultsFormatter
                    .FormatMegahertz(
                        comparison.GpuClockAfterMHz),
                OptimizationResultsFormatter
                    .FormatMegahertzDelta(
                        comparison.GpuClockDeltaMHz));

            AddOptimizationResultRow(
                "GPU temperature",
                OptimizationResultsFormatter
                    .FormatTemperature(
                        comparison.GpuTemperatureBeforeC),
                OptimizationResultsFormatter
                    .FormatTemperature(
                        comparison.GpuTemperatureAfterC),
                OptimizationResultsFormatter
                    .FormatTemperatureDelta(
                        comparison.GpuTemperatureDeltaC));

            AddOptimizationResultRow(
                "VRAM average",
                OptimizationResultsFormatter
                    .FormatGigabytes(
                        comparison.VramAverageBeforeGB),
                OptimizationResultsFormatter
                    .FormatGigabytes(
                        comparison.VramAverageAfterGB),
                OptimizationResultsFormatter
                    .FormatGigabyteDelta(
                        comparison.VramAverageDeltaGB));

            AddOptimizationResultRow(
                "VRAM peak",
                OptimizationResultsFormatter
                    .FormatGigabytes(
                        comparison.VramPeakBeforeGB),
                OptimizationResultsFormatter
                    .FormatGigabytes(
                        comparison.VramPeakAfterGB),
                OptimizationResultsFormatter
                    .FormatGigabyteDelta(
                        comparison.VramPeakDeltaGB));

            AddOptimizationResultRow(
                "Benchmark duration",
                OptimizationResultsFormatter
                    .FormatDuration(
                        comparison.BaselineDurationSeconds),
                OptimizationResultsFormatter
                    .FormatDuration(
                        comparison.AfterDurationSeconds),
                OptimizationResultsFormatter
                    .FormatDuration(
                        comparison.DurationDeltaSeconds));
        }

        private void AddOptimizationResultRow(
            string metric,
            string before,
            string after,
            string change)
        {
            var border =
                new Border
                {
                    Padding =
                        new Thickness(14, 10, 14, 10),

                    Margin =
                        new Thickness(0, 1, 0, 0),

                    Background =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Transparent)
                };

            var grid =
                new Grid();

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(2, GridUnitType.Star)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(1, GridUnitType.Star)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(1, GridUnitType.Star)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(1, GridUnitType.Star)
                });

            var metricText =
                new TextBlock
                {
                    Text =
                        metric,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGray),

                    FontSize = 10
                };

            var beforeText =
                new TextBlock
                {
                    Text =
                        before,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.White),

                    FontSize = 10,

                    HorizontalAlignment =
                        HorizontalAlignment.Right
                };

            var afterText =
                new TextBlock
                {
                    Text =
                        after,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.White),

                    FontSize = 10,

                    HorizontalAlignment =
                        HorizontalAlignment.Right
                };

            var changeText =
                new TextBlock
                {
                    Text =
                        change,

                    Foreground =
                        GetChangeBrush(change),

                    FontSize = 10,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,

                    HorizontalAlignment =
                        HorizontalAlignment.Right
                };

            Grid.SetColumn(
                metricText,
                0);

            Grid.SetColumn(
                beforeText,
                1);

            Grid.SetColumn(
                afterText,
                2);

            Grid.SetColumn(
                changeText,
                3);

            grid.Children.Add(metricText);
            grid.Children.Add(beforeText);
            grid.Children.Add(afterText);
            grid.Children.Add(changeText);

            border.Child =
                grid;

            OptimizationResultsPanel.Children.Add(
                border);
        }

        private Microsoft.UI.Xaml.Media.Brush
            GetChangeBrush(
                string change)
        {
            if (string.IsNullOrWhiteSpace(change))
            {
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray);
            }

            if (change.StartsWith("+"))
            {
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gold);
            }

            if (change.StartsWith("-"))
            {
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGreen);
            }

            return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray);
        }

        // ============================================================
        // ACTION STATUS COLORS
        // ============================================================

        private Microsoft.UI.Xaml.Media.Brush
            GetActionStatusBrush(
                OptimizationRecommendation recommendation)
        {
            if (recommendation.CanTest)
            {
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gold);
            }

            if (recommendation.CanApply)
            {
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGreen);
            }

            if (recommendation.RecommendedValue.Equals(
                "No change",
                StringComparison.OrdinalIgnoreCase))
            {
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGreen);
            }

            return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray);
        }

        // ============================================================
        // APPLY OPTIMIZATION
        // ============================================================

        private void ApplyOptimizationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not ApplyButtonContext context)
                return;

            OptimizationRecommendation recommendation =
                context.Recommendation;

            TextBlock statusText =
                context.StatusText;

            button.IsEnabled =
                false;

            button.Content =
                "SNAPSHOTTING...";

            statusText.Text =
                "CREATING SAFETY SNAPSHOT";

            statusText.Foreground =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gold);

            try
            {
                BuildCoreSnapshot snapshot =
                    SnapshotService.CreateSnapshot();

                if (snapshot == null ||
                    string.IsNullOrWhiteSpace(
                        snapshot.Id))
                {
                    throw new InvalidOperationException(
                        "BuildCore could not create " +
                        "a valid safety snapshot.");
                }

                bool snapshotValid =
                    SnapshotService.ValidateSnapshot(
                        snapshot.Id);

                if (!snapshotValid)
                {
                    throw new InvalidOperationException(
                        "BuildCore created a snapshot " +
                        "but could not validate it.");
                }

                LoadSnapshotInformation();

                OptimizationTransaction transaction =
                    OptimizationTransactionService
                        .CreateTransaction(
                            snapshot.Id,
                            recommendation);

                button.Content =
                    "APPLYING...";

                statusText.Text =
                    "APPLYING OPTIMIZATION";

                statusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold);

                OptimizationApplyResult result =
                    OptimizationApplyService.Apply(
                        recommendation);

                OptimizationTransactionService
                    .CompleteTransaction(
                        transaction,
                        result);

                if (result.Success &&
                    result.Verified)
                {
                    button.Content =
                        "APPLIED ✓";

                    statusText.Text =
                        result.Message;

                    statusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGreen);

                    OptimizationStatusText.Text =
                        "Optimization applied successfully";

                    OptimizationSummaryText.Text =
                        result.Message;

                    LoadSystemInformation();

                    LoadWindowsSystemInformation();

                    LoadSnapshotInformation();

                    RunOptimizationAnalysis();
                }
                else
                {
                    button.Content =
                        "APPLY FAILED";

                    button.IsEnabled =
                        true;

                    statusText.Text =
                        result.Message;

                    statusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.OrangeRed);

                    OptimizationStatusText.Text =
                        "Optimization failed";

                    OptimizationSummaryText.Text =
                        result.Message;
                }
            }
            catch (Exception ex)
            {
                button.Content =
                    "APPLY FAILED";

                button.IsEnabled =
                    true;

                statusText.Text =
                    "No change was made.";

                statusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.OrangeRed);

                OptimizationStatusText.Text =
                    "Optimization blocked";

                OptimizationSummaryText.Text =
                    "BuildCore stopped the operation because " +
                    "the safety snapshot could not be confirmed.";

                Debug.WriteLine(
                    "BUILDCORE SAFETY BLOCK");

                Debug.WriteLine(
                    ex.ToString());
            }
        }

        // ============================================================
        // VALUE PANEL
        // ============================================================

        private StackPanel CreateValuePanel(
            string label,
            string value)
        {
            var panel =
                new StackPanel();

            var labelText =
                new TextBlock
                {
                    Text = label,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),

                    FontSize = 8
                };

            var valueText =
                new TextBlock
                {
                    Text = value,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.White),

                    FontSize = 12,

                    TextWrapping =
                        TextWrapping.Wrap,

                    Margin =
                        new Thickness(0, 4, 0, 0)
                };

            panel.Children.Add(labelText);

            panel.Children.Add(valueText);

            return panel;
        }

        // ============================================================
        // CATEGORY ICONS
        // ============================================================

        private string GetCategoryIcon(
            OptimizationCategory category)
        {
            return category switch
            {
                OptimizationCategory.Power => "⚡",
                OptimizationCategory.Gaming => "🎮",
                OptimizationCategory.Graphics => "🖥",
                OptimizationCategory.Security => "🛡",
                OptimizationCategory.Windows => "▣",
                OptimizationCategory.Network => "◉",
                OptimizationCategory.Background => "◆",
                _ => "•"
            };
        }

        // ============================================================
        // RISK COLORS
        // ============================================================

        private Microsoft.UI.Xaml.Media.Brush
            GetRiskBrush(
                OptimizationRisk risk)
        {
            return risk switch
            {
                OptimizationRisk.None =>
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGreen),

                OptimizationRisk.Low =>
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGreen),

                OptimizationRisk.Medium =>
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold),

                OptimizationRisk.High =>
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.OrangeRed),

                _ =>
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray)
            };
        }

        // ============================================================
        // ANALYZE BUTTON
        // ============================================================

        private void AnalyzeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RunOptimizationAnalysis();
        }

        // ============================================================
        // SNAPSHOT SYSTEM
        // ============================================================

        private void LoadSnapshotInformation()
        {
            try
            {
                var snapshots =
                    SnapshotService.GetSnapshots();

                SnapshotCountText.Text =
                    $"{snapshots.Count} SNAPSHOTS";

                SnapshotLocationText.Text =
                    $"Snapshot storage: " +
                    $"{SnapshotService.GetSnapshotDirectory()}";

                if (snapshots.Count > 0)
                {
                    var latest =
                        snapshots[0];

                    SnapshotStatusText.Text =
                        $"Last snapshot: " +
                        $"{latest.CreatedAt:yyyy-MM-dd HH:mm:ss}";

                    SnapshotStatusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGreen);
                }
                else
                {
                    SnapshotStatusText.Text =
                        "No snapshot created yet.";

                    SnapshotStatusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Snapshot information error: {ex}");

                SnapshotCountText.Text =
                    "0 SNAPSHOTS";

                SnapshotStatusText.Text =
                    "Unable to read snapshot storage.";

                SnapshotLocationText.Text =
                    "Snapshot storage unavailable.";
            }
        }

        private void CreateSnapshotButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                BuildCoreSnapshot snapshot =
                    SnapshotService.CreateSnapshot();

                LoadSnapshotInformation();

                SnapshotStatusText.Text =
                    "Snapshot created successfully.";

                SnapshotStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGreen);

                Debug.WriteLine(
                    $"BUILDCORE SNAPSHOT CREATED: " +
                    $"{snapshot.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "BUILDCORE SNAPSHOT ERROR");

                Debug.WriteLine(
                    ex.ToString());

                SnapshotStatusText.Text =
                    "Snapshot creation failed.";

                SnapshotStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.OrangeRed);
            }
        }

        // ============================================================
        // HISTORY
        // ============================================================

        private async void HistoryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await ShowHistoryDialog();
        }

        private async Task ShowHistoryDialog()
        {
            try
            {
                var transactions =
                    OptimizationTransactionService
                        .GetTransactions();

                var tests =
                    OptimizationTestStorageService
                        .GetTests();

                var root =
                    new StackPanel
                    {
                        Spacing = 12
                    };

                // ====================================================
                // HEADER
                // ====================================================

                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "BUILDCORE HISTORY",

                        FontSize = 20,

                        FontWeight =
                            Microsoft.UI.Text.FontWeights.SemiBold,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.White)
                    });

                // ====================================================
                // OPTIMIZATION TRANSACTIONS
                // ====================================================

                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "OPTIMIZATION CHANGES",

                        FontSize = 11,

                        FontWeight =
                            Microsoft.UI.Text.FontWeights.SemiBold,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gold),

                        Margin =
                            new Thickness(0, 8, 0, 0)
                    });

                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            $"{transactions.Count} transaction(s) recorded",

                        FontSize = 10,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gray)
                    });

                if (transactions.Count == 0)
                {
                    root.Children.Add(
                        new TextBlock
                        {
                            Text =
                                "No optimization transactions " +
                                "have been recorded yet.",

                            FontSize = 12,

                            Foreground =
                                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                    Microsoft.UI.Colors.LightGray),

                            TextWrapping =
                                TextWrapping.Wrap
                        });
                }
                else
                {
                    foreach (
                        OptimizationTransaction transaction
                        in transactions)
                    {
                        root.Children.Add(
                            CreateHistoryCard(
                                transaction));
                    }
                }

                // ====================================================
                // OPTIMIZATION TESTS
                // ====================================================

                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "CONTROLLED OPTIMIZATION TESTS",

                        FontSize = 11,

                        FontWeight =
                            Microsoft.UI.Text.FontWeights.SemiBold,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gold),

                        Margin =
                            new Thickness(0, 18, 0, 0)
                    });

                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            $"{tests.Count} controlled test(s) recorded",

                        FontSize = 10,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gray)
                    });

                if (tests.Count == 0)
                {
                    root.Children.Add(
                        new TextBlock
                        {
                            Text =
                                "No controlled optimization tests " +
                                "have been recorded yet.",

                            FontSize = 12,

                            Foreground =
                                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                    Microsoft.UI.Colors.LightGray),

                            TextWrapping =
                                TextWrapping.Wrap
                        });
                }
                else
                {
                    foreach (
                        OptimizationBenchmarkResult test
                        in tests)
                    {
                        root.Children.Add(
                            CreateOptimizationTestHistoryCard(
                                test));
                    }
                }

                // ====================================================
                // DIALOG
                // ====================================================

                var dialog =
                    new ContentDialog
                    {
                        Title =
                            "BuildCore History",

                        Content =
                            new ScrollViewer
                            {
                                Content = root,

                                MaxHeight = 650,

                                VerticalScrollBarVisibility =
                                    ScrollBarVisibility.Auto
                            },

                        CloseButtonText =
                            "CLOSE",

                        XamlRoot =
                            ((FrameworkElement)
                                this.Content).XamlRoot
                    };

                // Attach the dialog to test buttons after creation.
                foreach (
                    UIElement child
                    in root.Children)
                {
                    AttachHistoryDialogToTestButtons(
                        child,
                        dialog);
                }

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "BUILDCORE HISTORY ERROR");

                Debug.WriteLine(
                    ex.ToString());
            }
        }

        private void AttachHistoryDialogToTestButtons(
            UIElement element,
            ContentDialog dialog)
        {
            if (element is Border border &&
                border.Child is StackPanel panel)
            {
                foreach (
                    UIElement child
                    in panel.Children)
                {
                    if (child is Button button &&
                        button.Tag is OptimizationTestButtonContext)
                    {
                        var context =
                            (OptimizationTestButtonContext)
                                button.Tag;

                        context.ParentDialog =
                            dialog;
                    }
                }
            }
        }

        private Border CreateHistoryCard(
            OptimizationTransaction transaction)
        {
            var panel =
                new StackPanel
                {
                    Spacing = 6
                };

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        GetCategoryIcon(
                            transaction.Category)
                        + "  "
                        + transaction.OptimizationTitle,

                    FontSize = 14,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.White)
                });

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        transaction.CreatedAt
                            .ToString(
                                "yyyy-MM-dd HH:mm:ss"),

                    FontSize = 9,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray)
                });

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        $"{transaction.BeforeValue}  →  " +
                        $"{transaction.TargetValue}",

                    FontSize = 11,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGray),

                    TextWrapping =
                        TextWrapping.Wrap
                });

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        transaction.IsSuccessful
                            ? "✓ APPLIED & VERIFIED"
                            : "✕ APPLY FAILED",

                    FontSize = 9,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,

                    Foreground =
                        transaction.IsSuccessful
                            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.LightGreen)
                            : new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.OrangeRed)
                });

            if (transaction.IsRestored)
            {
                panel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "↩ RESTORED & VERIFIED",

                        FontSize = 9,

                        FontWeight =
                            Microsoft.UI.Text.FontWeights.SemiBold,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.LightGreen)
                    });
            }
            else if (transaction.RestoreAvailable)
            {
                var restoreButton =
                    new Button
                    {
                        Content =
                            "RESTORE",

                        HorizontalAlignment =
                            HorizontalAlignment.Left,

                        Padding =
                            new Thickness(12, 7, 12, 7),

                        Tag =
                            transaction
                    };

                restoreButton.Click +=
                    RestoreTransactionButton_Click;

                panel.Children.Add(
                    restoreButton);
            }
            else
            {
                panel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "RESTORE UNAVAILABLE",

                        FontSize = 9,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gray)
                    });
            }

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Snapshot: " +
                        $"{transaction.SnapshotId}",

                    FontSize = 8,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),

                    TextWrapping =
                        TextWrapping.Wrap
                });

            return new Border
            {
                Padding =
                    new Thickness(14),

                Margin =
                    new Thickness(0, 0, 0, 10),

                CornerRadius =
                    new CornerRadius(10),

                Background =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Transparent),

                BorderBrush =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.DimGray),

                BorderThickness =
                    new Thickness(1),

                Child =
                    panel
            };
        }

        // ============================================================
        // OPTIMIZATION TEST HISTORY CARD
        // ============================================================

        private Border CreateOptimizationTestHistoryCard(
            OptimizationBenchmarkResult test)
        {
            bool successful =
                test.IsSuccessful &&
                test.Comparison != null;

            var panel =
                new StackPanel
                {
                    Spacing = 7
                };

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        "◈  " +
                        (string.IsNullOrWhiteSpace(
                            test.OptimizationTitle)
                            ? "Optimization Test"
                            : test.OptimizationTitle),

                    FontSize = 14,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.White)
                });

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        test.StartedAt.ToString(
                            "yyyy-MM-dd HH:mm:ss"),

                    FontSize = 9,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray)
                });

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        successful
                            ? "✓ CONTROLLED TEST COMPLETE"
                            : $"✕ {test.Status.ToUpperInvariant()}",

                    FontSize = 9,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,

                    Foreground =
                        successful
                            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.LightGreen)
                            : new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.OrangeRed)
                });

            if (successful &&
                test.Comparison != null)
            {
                BenchmarkComparison comparison =
                    test.Comparison;

                panel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            $"CPU {OptimizationResultsFormatter.FormatPercentageDelta(comparison.CpuAverageDelta)}   •   " +
                            $"GPU {OptimizationResultsFormatter.FormatPercentageDelta(comparison.GpuAverageDelta)}   •   " +
                            $"GPU Temp {OptimizationResultsFormatter.FormatTemperatureDelta(comparison.GpuTemperatureDeltaC)}",

                        FontSize = 10,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.LightGray),

                        TextWrapping =
                            TextWrapping.Wrap
                    });
            }

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Snapshot: {test.SnapshotId}",

                    FontSize = 8,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),

                    TextWrapping =
                        TextWrapping.Wrap
                });

            var buttonRow =
                new StackPanel
                {
                    Orientation =
                        Orientation.Horizontal,

                    Spacing = 8,

                    Margin =
                        new Thickness(0, 5, 0, 0)
                };

            var viewButton =
                new Button
                {
                    Content =
                        "VIEW TEST",

                    Padding =
                        new Thickness(12, 7, 12, 7),

                    Tag =
                        new OptimizationTestButtonContext
                        {
                            Test =
                                test
                        }
                };

            viewButton.Click +=
                ViewOptimizationTestButton_Click;

            buttonRow.Children.Add(
                viewButton);

            var deleteButton =
                new Button
                {
                    Content =
                        "DELETE",

                    Padding =
                        new Thickness(12, 7, 12, 7),

                    Tag =
                        test
                };

            deleteButton.Click +=
                DeleteOptimizationTestButton_Click;

            buttonRow.Children.Add(
                deleteButton);

            panel.Children.Add(
                buttonRow);

            return new Border
            {
                Padding =
                    new Thickness(14),

                Margin =
                    new Thickness(0, 0, 0, 10),

                CornerRadius =
                    new CornerRadius(10),

                Background =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Transparent),

                BorderBrush =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.DimGray),

                BorderThickness =
                    new Thickness(1),

                Child =
                    panel
            };
        }

        // ============================================================
        // VIEW OPTIMIZATION TEST
        // ============================================================

        private async void ViewOptimizationTestButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not
                OptimizationTestButtonContext context)
                return;

            OptimizationBenchmarkResult test =
                context.Test;

            ContentDialog parentDialog =
                context.ParentDialog;

            parentDialog.Hide();

            await Task.Delay(50);

            await ShowOptimizationTestDialog(
                test);
        }

        private async Task ShowOptimizationTestDialog(
            OptimizationBenchmarkResult test)
        {
            var root =
                new StackPanel
                {
                    Spacing = 10
                };

            root.Children.Add(
                new TextBlock
                {
                    Text =
                        string.IsNullOrWhiteSpace(
                            test.OptimizationTitle)
                            ? "Optimization Test"
                            : test.OptimizationTitle,

                    FontSize = 18,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.White)
                });

            root.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Test ID: {test.TestId}",

                    FontSize = 8,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),

                    TextWrapping =
                        TextWrapping.Wrap
                });

            root.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Started: {test.StartedAt:yyyy-MM-dd HH:mm:ss}",

                    FontSize = 9,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray)
                });

            root.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Completed: {test.CompletedAt:yyyy-MM-dd HH:mm:ss}",

                    FontSize = 9,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray)
                });

            bool successful =
                test.IsSuccessful &&
                test.Comparison != null;

            root.Children.Add(
                new TextBlock
                {
                    Text =
                        successful
                            ? "✓ CONTROLLED TEST COMPLETE"
                            : $"✕ {test.Status.ToUpperInvariant()}",

                    FontSize = 10,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,

                    Foreground =
                        successful
                            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.LightGreen)
                            : new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.OrangeRed)
                });

            root.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Snapshot: {test.SnapshotId}",

                    FontSize = 9,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),

                    TextWrapping =
                        TextWrapping.Wrap
                });

            if (test.Comparison != null)
            {
                BenchmarkComparison comparison =
                    test.Comparison;

                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "TELEMETRY COMPARISON",

                        FontSize = 10,

                        FontWeight =
                            Microsoft.UI.Text.FontWeights.SemiBold,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gold),

                        Margin =
                            new Thickness(0, 10, 0, 0)
                    });

                root.Children.Add(
                    CreateTestMetricRow(
                        "CPU average",
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.CpuAverageBefore),
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.CpuAverageAfter),
                        OptimizationResultsFormatter
                            .FormatPercentageDelta(
                                comparison.CpuAverageDelta)));

                root.Children.Add(
                    CreateTestMetricRow(
                        "CPU peak",
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.CpuPeakBefore),
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.CpuPeakAfter),
                        OptimizationResultsFormatter
                            .FormatPercentageDelta(
                                comparison.CpuPeakDelta)));

                root.Children.Add(
                    CreateTestMetricRow(
                        "RAM average",
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.RamAverageBefore),
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.RamAverageAfter),
                        OptimizationResultsFormatter
                            .FormatPercentageDelta(
                                comparison.RamAverageDelta)));

                root.Children.Add(
                    CreateTestMetricRow(
                        "GPU average",
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.GpuAverageBefore),
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.GpuAverageAfter),
                        OptimizationResultsFormatter
                            .FormatPercentageDelta(
                                comparison.GpuAverageDelta)));

                root.Children.Add(
                    CreateTestMetricRow(
                        "GPU peak",
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.GpuPeakBefore),
                        OptimizationResultsFormatter
                            .FormatPercentage(
                                comparison.GpuPeakAfter),
                        OptimizationResultsFormatter
                            .FormatPercentageDelta(
                                comparison.GpuPeakDelta)));

                root.Children.Add(
                    CreateTestMetricRow(
                        "GPU clock",
                        OptimizationResultsFormatter
                            .FormatMegahertz(
                                comparison.GpuClockBeforeMHz),
                        OptimizationResultsFormatter
                            .FormatMegahertz(
                                comparison.GpuClockAfterMHz),
                        OptimizationResultsFormatter
                            .FormatMegahertzDelta(
                                comparison.GpuClockDeltaMHz)));

                root.Children.Add(
                    CreateTestMetricRow(
                        "GPU temperature",
                        OptimizationResultsFormatter
                            .FormatTemperature(
                                comparison.GpuTemperatureBeforeC),
                        OptimizationResultsFormatter
                            .FormatTemperature(
                                comparison.GpuTemperatureAfterC),
                        OptimizationResultsFormatter
                            .FormatTemperatureDelta(
                                comparison.GpuTemperatureDeltaC)));

                root.Children.Add(
                    CreateTestMetricRow(
                        "VRAM average",
                        OptimizationResultsFormatter
                            .FormatGigabytes(
                                comparison.VramAverageBeforeGB),
                        OptimizationResultsFormatter
                            .FormatGigabytes(
                                comparison.VramAverageAfterGB),
                        OptimizationResultsFormatter
                            .FormatGigabyteDelta(
                                comparison.VramAverageDeltaGB)));

                root.Children.Add(
                    CreateTestMetricRow(
                        "VRAM peak",
                        OptimizationResultsFormatter
                            .FormatGigabytes(
                                comparison.VramPeakBeforeGB),
                        OptimizationResultsFormatter
                            .FormatGigabytes(
                                comparison.VramPeakAfterGB),
                        OptimizationResultsFormatter
                            .FormatGigabyteDelta(
                                comparison.VramPeakDeltaGB)));

                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "These measurements compare system telemetry before and after the tested change. They do not by themselves prove an FPS, frame-time, or input-latency improvement.",

                        FontSize = 9,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gray),

                        TextWrapping =
                            TextWrapping.Wrap,

                        Margin =
                            new Thickness(0, 12, 0, 0)
                    });
            }
            else
            {
                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            OptimizationResultsFormatter
                                .BuildStatus(test),

                        FontSize = 11,

                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.LightGray),

                        TextWrapping =
                            TextWrapping.Wrap,

                        Margin =
                            new Thickness(0, 12, 0, 0)
                    });
            }

            var dialog =
                new ContentDialog
                {
                    Title =
                        "Optimization Test Details",

                    Content =
                        new ScrollViewer
                        {
                            Content = root,

                            MaxHeight = 650,

                            VerticalScrollBarVisibility =
                                ScrollBarVisibility.Auto
                        },

                    CloseButtonText =
                        "CLOSE",

                    XamlRoot =
                        ((FrameworkElement)
                            this.Content).XamlRoot
                };

            await dialog.ShowAsync();
        }

        private Border CreateTestMetricRow(
            string metric,
            string before,
            string after,
            string change)
        {
            var grid =
                new Grid();

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(2, GridUnitType.Star)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(1, GridUnitType.Star)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(1, GridUnitType.Star)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(1, GridUnitType.Star)
                });

            var metricText =
                new TextBlock
                {
                    Text =
                        metric,

                    FontSize = 10,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGray)
                };

            var beforeText =
                new TextBlock
                {
                    Text =
                        before,

                    FontSize = 10,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.White)
                };

            var afterText =
                new TextBlock
                {
                    Text =
                        after,

                    FontSize = 10,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.White)
                };

            var changeText =
                new TextBlock
                {
                    Text =
                        change,

                    FontSize = 10,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    Foreground =
                        GetChangeBrush(change)
                };

            Grid.SetColumn(
                metricText,
                0);

            Grid.SetColumn(
                beforeText,
                1);

            Grid.SetColumn(
                afterText,
                2);

            Grid.SetColumn(
                changeText,
                3);

            grid.Children.Add(metricText);
            grid.Children.Add(beforeText);
            grid.Children.Add(afterText);
            grid.Children.Add(changeText);

            return new Border
            {
                Padding =
                    new Thickness(10, 8, 10, 8),

                Background =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Transparent),

                Child =
                    grid
            };
        }

        // ============================================================
        // DELETE OPTIMIZATION TEST
        // ============================================================

        private async void DeleteOptimizationTestButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not
                OptimizationBenchmarkResult test)
                return;

            var confirmation =
                new ContentDialog
                {
                    Title =
                        "Delete Optimization Test?",

                    Content =
                        new TextBlock
                        {
                            Text =
                                $"This will permanently delete the " +
                                $"saved test for '{test.OptimizationTitle}'.\n\n" +
                                "The associated snapshot and optimization " +
                                "transaction will not be deleted.",

                            TextWrapping =
                                TextWrapping.Wrap
                        },

                    PrimaryButtonText =
                        "DELETE",

                    CloseButtonText =
                        "CANCEL",

                    DefaultButton =
                        ContentDialogButton.Close,

                    XamlRoot =
                        ((FrameworkElement)
                            this.Content).XamlRoot
                };

            ContentDialogResult result =
                await confirmation.ShowAsync();

            if (result !=
                ContentDialogResult.Primary)
            {
                return;
            }

            bool deleted =
                OptimizationTestStorageService
                    .DeleteTest(test.TestId);

            if (deleted)
            {
                button.Content =
                    "DELETED ✓";

                button.IsEnabled =
                    false;

                Debug.WriteLine(
                    $"BUILDCORE TEST DELETED: " +
                    $"{test.TestId}");
            }
            else
            {
                await ShowRestoreFailureDialog(
                    "BuildCore could not delete the saved optimization test.");
            }
        }

        // ============================================================
        // RESTORE
        // ============================================================

        private async void RestoreTransactionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not
                OptimizationTransaction transaction)
                return;

            var confirmDialog =
                new ContentDialog
                {
                    Title =
                        "Restore Optimization?",

                    Content =
                        new TextBlock
                        {
                            Text =
                                $"BuildCore will restore " +
                                $"'{transaction.OptimizationTitle}' " +
                                $"from '{transaction.TargetValue}' " +
                                $"back to '{transaction.BeforeValue}'.\n\n" +
                                "BuildCore will verify the restored " +
                                "state afterward.",

                            TextWrapping =
                                TextWrapping.Wrap
                        },

                    PrimaryButtonText =
                        "RESTORE",

                    CloseButtonText =
                        "CANCEL",

                    DefaultButton =
                        ContentDialogButton.Primary,

                    XamlRoot =
                        ((FrameworkElement)
                            this.Content).XamlRoot
                };

            ContentDialogResult confirmation =
                await confirmDialog.ShowAsync();

            if (confirmation !=
                ContentDialogResult.Primary)
            {
                return;
            }

            button.IsEnabled =
                false;

            button.Content =
                "RESTORING...";

            try
            {
                OptimizationRestoreResult result =
                    OptimizationRestoreService.Restore(
                        transaction);

                OptimizationTransactionService
                    .CompleteRestore(
                        transaction,
                        result);

                if (result.Success &&
                    result.Verified)
                {
                    button.Content =
                        "RESTORED ✓";

                    OptimizationStatusText.Text =
                        "Optimization restored";

                    OptimizationSummaryText.Text =
                        result.Message;

                    LoadSystemInformation();

                    LoadWindowsSystemInformation();

                    RunOptimizationAnalysis();

                    await ShowRestoreSuccessDialog(
                        result.Message);
                }
                else
                {
                    button.Content =
                        "RESTORE FAILED";

                    button.IsEnabled =
                        true;

                    OptimizationStatusText.Text =
                        "Restore failed";

                    OptimizationSummaryText.Text =
                        result.Message;

                    await ShowRestoreFailureDialog(
                        result.Message);
                }
            }
            catch (Exception ex)
            {
                button.Content =
                    "RESTORE FAILED";

                button.IsEnabled =
                    true;

                OptimizationStatusText.Text =
                    "Restore failed";

                OptimizationSummaryText.Text =
                    ex.Message;

                await ShowRestoreFailureDialog(
                    ex.Message);
            }
        }

        private async Task ShowRestoreSuccessDialog(
            string message)
        {
            var dialog =
                new ContentDialog
                {
                    Title =
                        "Restore Complete",

                    Content =
                        new TextBlock
                        {
                            Text =
                                message,

                            TextWrapping =
                                TextWrapping.Wrap
                        },

                    CloseButtonText =
                        "DONE",

                    XamlRoot =
                        ((FrameworkElement)
                            this.Content).XamlRoot
                };

            await dialog.ShowAsync();
        }

        private async Task ShowRestoreFailureDialog(
            string message)
        {
            var dialog =
                new ContentDialog
                {
                    Title =
                        "Restore Failed",

                    Content =
                        new TextBlock
                        {
                            Text =
                                message,

                            TextWrapping =
                                TextWrapping.Wrap
                        },

                    CloseButtonText =
                        "CLOSE",

                    XamlRoot =
                        ((FrameworkElement)
                            this.Content).XamlRoot
                };

            await dialog.ShowAsync();
        }

        // ============================================================
        // PERFORMANCE
        // ============================================================

        private void UpdatePerformance(
            DispatcherQueueTimer sender,
            object args)
        {
            try
            {
                PerformanceData data =
                    _performanceService.GetPerformance();

                CpuUsageText.Text =
                    $"{data.CpuUsage:F0}%";

                CpuPerformanceLarge.Text =
                    $"{data.CpuUsage:F0}%";

                RamUsageText.Text =
                    $"{data.RamUsage:F0}%";

                RamPerformanceLarge.Text =
                    $"{data.RamUsage:F0}%";

                DiskUsageText.Text =
                    $"{data.DiskUsage:F0}%";

                DiskPerformanceLarge.Text =
                    $"{data.DiskUsage:F0}%";

                _performanceHistory.Add(
                    data.CpuUsage,
                    data.RamUsage,
                    data.DiskUsage);

                UpdatePerformanceStatus(data);

                UpdateHardwareMonitoring();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Performance update error: {ex}");
            }
        }

        private void UpdatePerformanceStatus(
            PerformanceData data)
        {
            CpuStatusText.Text =
                data.CpuUsage >= 90
                    ? "HIGH LOAD"
                    : data.CpuUsage >= 70
                        ? "ACTIVE"
                        : "NORMAL";

            RamStatusText.Text =
                data.RamUsage >= 90
                    ? "HIGH USAGE"
                    : data.RamUsage >= 75
                        ? "ACTIVE"
                        : "NORMAL";

            DiskStatusText.Text =
                data.DiskUsage >= 90
                    ? "HIGH ACTIVITY"
                    : data.DiskUsage >= 70
                        ? "ACTIVE"
                        : "NORMAL";
        }

        // ============================================================
        // HARDWARE MONITORING
        // ============================================================

        private void UpdateHardwareMonitoring()
        {
            try
            {
                HardwareMonitorData hardwareData =
                    _hardwareMonitorService
                        .GetHardwareData();

                UpdateCpuTelemetry(
                    hardwareData);

                UpdateGpuTelemetry(
                    hardwareData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Hardware monitoring error: {ex}");
            }
        }

        private void UpdateCpuTelemetry(
            HardwareMonitorData data)
        {
            CpuTemperatureLarge.Text =
                "N/A";

            CpuTemperatureText.Text =
                "Sensor unavailable";

            if (data.CpuClock.HasValue &&
                data.CpuClock.Value > 0)
            {
                double cpuClockGHz =
                    data.CpuClock.Value /
                    1000.0;

                CpuClockLarge.Text =
                    $"{cpuClockGHz:F2} GHz";

                CpuClockText.Text =
                    $"Clock: {cpuClockGHz:F2} GHz";
            }
            else
            {
                CpuClockLarge.Text =
                    "-- GHz";

                CpuClockText.Text =
                    "Clock: -- GHz";
            }
        }

        private void UpdateGpuTelemetry(
            HardwareMonitorData data)
        {
            if (data.GpuUsage.HasValue)
            {
                double usage =
                    Math.Clamp(
                        data.GpuUsage.Value,
                        0,
                        100);

                GpuUsageText.Text =
                    $"{usage:F0}%";

                GpuUsageSensorText.Text =
                    $"{usage:F0}% GPU usage";
            }
            else
            {
                GpuUsageText.Text =
                    "--%";

                GpuUsageSensorText.Text =
                    "Sensor unavailable";
            }

            if (data.GpuTemperature.HasValue &&
                data.GpuTemperature.Value > 0)
            {
                GpuTemperatureLarge.Text =
                    $"{data.GpuTemperature.Value:F0}°C";
            }
            else
            {
                GpuTemperatureLarge.Text =
                    "N/A";
            }

            if (data.GpuClock.HasValue &&
                data.GpuClock.Value > 0)
            {
                GpuClockLarge.Text =
                    $"{data.GpuClock.Value:F0} MHz";
            }
            else
            {
                GpuClockLarge.Text =
                    "-- MHz";
            }

            if (data.GpuMemoryUsed.HasValue &&
                data.GpuMemoryUsed.Value >= 0)
            {
                double vramGB =
                    data.GpuMemoryUsed.Value /
                    1024.0;

                GpuMemoryLarge.Text =
                    $"{vramGB:F1} GB";
            }
            else
            {
                GpuMemoryLarge.Text =
                    "-- GB";
            }
        }

        // ============================================================
        // BENCHMARK
        // ============================================================

        private async void RunBenchmarkButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_benchmarkRunning)
                return;

            _benchmarkRunning =
                true;

            RunBenchmarkButton.IsEnabled =
                false;

            RunBenchmarkButton.Content =
                "RUNNING...";

            BenchmarkProgressBar.Value =
                0;

            BenchmarkProgressText.Text =
                "STARTING";

            BenchmarkStatusText.Text =
                "BENCHMARK RUNNING";

            BenchmarkStatusText.Foreground =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gold);

            BenchmarkSummaryText.Text =
                "Collecting system telemetry...";

            _benchmarkStartedAt =
                DateTime.Now;

            _benchmarkUiTimer.Start();

            try
            {
                BenchmarkResult result =
                    await _benchmarkService.RunAsync(
                        5,
                        100);

                _benchmarkUiTimer.Stop();

                BenchmarkProgressBar.Value =
                    100;

                BenchmarkProgressText.Text =
                    "COMPLETE";

                DisplayBenchmarkResult(
                    result);

                BenchmarkStatusText.Text =
                    "BENCHMARK COMPLETE";

                BenchmarkStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGreen);

                BenchmarkSummaryText.Text =
                    result.Summary;

                RunBenchmarkButton.Content =
                    "RUN BENCHMARK";
            }
            catch (Exception ex)
            {
                _benchmarkUiTimer.Stop();

                BenchmarkProgressBar.Value =
                    0;

                BenchmarkProgressText.Text =
                    "FAILED";

                BenchmarkStatusText.Text =
                    "BENCHMARK FAILED";

                BenchmarkStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.OrangeRed);

                BenchmarkSummaryText.Text =
                    "BuildCore could not complete " +
                    "the benchmark.";

                RunBenchmarkButton.Content =
                    "RUN BENCHMARK";

                Debug.WriteLine(
                    "BUILDCORE BENCHMARK ERROR");

                Debug.WriteLine(
                    ex.ToString());
            }
            finally
            {
                _benchmarkRunning =
                    false;

                RunBenchmarkButton.IsEnabled =
                    true;
            }
        }

        private void UpdateBenchmarkProgress(
            DispatcherQueueTimer sender,
            object args)
        {
            if (!_benchmarkRunning)
                return;

            double elapsed =
                (DateTime.Now -
                 _benchmarkStartedAt)
                .TotalSeconds;

            double progress =
                Math.Clamp(
                    elapsed /
                    5.0 *
                    100.0,
                    0,
                    99);

            BenchmarkProgressBar.Value =
                progress;

            BenchmarkProgressText.Text =
                $"{Math.Min(elapsed, 5.0):F1}s / 5.0s";
        }

        private void DisplayBenchmarkResult(
            BenchmarkResult result)
        {
            BenchmarkCpuText.Text =
                $"{result.CpuAverageUsage:F1}%";

            BenchmarkRamText.Text =
                $"{result.RamAverageUsage:F1}%";

            BenchmarkGpuText.Text =
                result.GpuAverageUsage > 0
                    ? $"{result.GpuAverageUsage:F1}%"
                    : "N/A";

            BenchmarkScoreText.Text =
                "BASELINE";

            BenchmarkCpuPeakText.Text =
                $"{result.CpuPeakUsage:F1}%";

            BenchmarkGpuPeakText.Text =
                result.GpuPeakUsage > 0
                    ? $"{result.GpuPeakUsage:F1}%"
                    : "N/A";

            BenchmarkGpuTempText.Text =
                result.GpuAverageTemperature > 0
                    ? $"{result.GpuAverageTemperature:F1}°C"
                    : "N/A";

            BenchmarkVramText.Text =
                result.GpuAverageMemoryUsedGB > 0
                    ? $"{result.GpuAverageMemoryUsedGB:F2} GB"
                    : "N/A";
        }

        // ============================================================
        // PAGE NAVIGATION
        // ============================================================

        private void ShowDashboard()
        {
            DashboardPage.Visibility =
                Visibility.Visible;

            OptimizePage.Visibility =
                Visibility.Collapsed;
        }

        private void ShowOptimize()
        {
            DashboardPage.Visibility =
                Visibility.Collapsed;

            OptimizePage.Visibility =
                Visibility.Visible;
        }

        // ============================================================
        // NAVIGATION
        // ============================================================

        private void DashboardButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowDashboard();
        }

        private void SystemButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowDashboard();
        }

        private void PerformanceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowDashboard();
        }

        private void OptimizeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowOptimize();
        }

        private void SettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowDashboard();
        }
    }
}