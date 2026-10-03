using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;

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

        private string _selectedOptimizationCategory = "All";

        private static string GetRecoveryHandlerRegistryStatus()
        {
            IReadOnlyList<string> errors =
                RebootOptimizationRecoveryHandlerRegistry.ValidateRegistry();

            if (errors.Count > 0)
                return "INVALID: " + string.Join(" | ", errors);

            IReadOnlyList<string> titles =
                RebootOptimizationRecoveryHandlerRegistry.GetRegisteredTitles();

            return titles.Count == 0
                ? "VALID: No recovery handlers registered."
                : $"VALID: {titles.Count} recovery handler(s) registered.";
        }

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public MainWindow()
        {
            this.InitializeComponent();

            this.Loaded += MainWindow_Loaded;

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

            LoadOptimizationLibrary();

            InitializeWorkloadProfiles();

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
        // PHASE 1.14B - REBOOT EXPERIMENT RESUME DETECTION
        // ============================================================

        private async void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            this.Loaded -= MainWindow_Loaded;

            await DetectPendingRebootExperimentAsync();
        }

        private List<string> FinalizeCompletedRecoveryExperiments()
        {
            var failures = new List<string>();

            try
            {
                foreach (RebootOptimizationExperimentState experiment in
                    RebootOptimizationExperimentStorageService.GetExperiments())
                {
                    bool wasPendingRecovery =
                        experiment.IsRecoveryPendingReboot;

                    if (!wasPendingRecovery)
                        continue;

                    bool finalized =
                        RebootOptimizationExperimentRecoveryService
                            .FinalizeRecoveryAfterReboot(experiment);

                    if (!finalized &&
                        experiment.RecoveryPhase ==
                            RebootOptimizationExperimentRecoveryPhase.Failed)
                    {
                        failures.Add(
                            $"{experiment.OptimizationTitle}: " +
                            experiment.RecoveryStatus);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"BUILDCORE RECOVERY FINALIZATION ERROR: {ex}");

                failures.Add(
                    "BuildCore could not complete recovery verification " +
                    $"for one or more experiments: {ex.Message}");
            }

            return failures;
        }

        private async Task DetectPendingRebootExperimentAsync()
        {
            RebootOptimizationExperimentStorageService.CleanupTemporaryFiles();

            IReadOnlyList<string> recoveryRegistryErrors =
                RebootOptimizationRecoveryHandlerRegistry.ValidateRegistry();

            if (recoveryRegistryErrors.Count > 0)
            {
                Debug.WriteLine(
                    "BUILDCORE RECOVERY HANDLER REGISTRY INVALID: " +
                    string.Join(" | ", recoveryRegistryErrors));
            }

            List<string> recoveryFailures =
                FinalizeCompletedRecoveryExperiments();

            if (recoveryFailures.Count > 0)
            {
                var failurePanel =
                    new StackPanel
                    {
                        Spacing = 8
                    };

                failurePanel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "Windows restarted, but BuildCore could not " +
                            "verify one or more recovery operations.",
                        TextWrapping = TextWrapping.Wrap
                    });

                foreach (string failure in recoveryFailures)
                {
                    failurePanel.Children.Add(
                        new TextBlock
                        {
                            Text = failure,
                            TextWrapping = TextWrapping.Wrap
                        });
                }

                failurePanel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "The recovery has NOT been reported as finalized. " +
                            "Open the experiment details to review the saved " +
                            "recovery state before taking further action.",
                        TextWrapping = TextWrapping.Wrap
                    });

                var recoveryDialog =
                    new ContentDialog
                    {
                        Title = "RECOVERY VERIFICATION FAILED",
                        Content = failurePanel,
                        CloseButtonText = "CLOSE",
                        XamlRoot = OptimizePage.XamlRoot
                    };

                await recoveryDialog.ShowAsync();
            }

            try
            {
                RebootOptimizationExperimentState? experiment =
                    RebootOptimizationExperimentStorageService.GetPending();

                if (experiment == null)
                    return;

                if (!RebootOptimizationExperimentStorageService.Validate(
                    experiment.ExperimentId))
                {
                    Debug.WriteLine(
                        "BUILDCORE REBOOT EXPERIMENT: Pending experiment failed validation.");
                    return;
                }

                string validationMessage;

                if (!RebootOptimizationExperimentValidationService.ValidateAfterReboot(
                    experiment,
                    out validationMessage))
                {
                    Debug.WriteLine(
                        $"BUILDCORE REBOOT EXPERIMENT: {validationMessage}");
                    return;
                }

                RebootOptimizationExperimentStorageService.Save(experiment);

                string workloadName =
                    experiment.WorkloadDefinition?.Name ??
                    "Unknown workload";

                var contentPanel =
                    new StackPanel
                    {
                        Spacing = 8
                    };

                contentPanel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "BuildCore detected that Windows restarted " +
                            "after the saved optimization experiment.",

                        TextWrapping = TextWrapping.Wrap
                    });

                contentPanel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            $"Optimization: {experiment.OptimizationTitle}\n" +
                            $"Workload: {workloadName}\n" +
                            $"Snapshot: {experiment.SnapshotId}\n" +
                            $"Status: {validationMessage}",

                        TextWrapping = TextWrapping.Wrap
                    });

                contentPanel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "BuildCore will now continue only if you choose " +
                            "RESUME. For an interactive workload, make sure " +
                            "the same application is running first.",

                        TextWrapping = TextWrapping.Wrap
                    });

                var dialog =
                    new ContentDialog
                    {
                        Title = "REBOOT EXPERIMENT READY",
                        Content = contentPanel,
                        PrimaryButtonText = "RESUME",
                        CloseButtonText = "CLOSE",
                        XamlRoot = Content.XamlRoot
                    };

                ContentDialogResult dialogResult =
                    await dialog.ShowAsync();

                if (dialogResult != ContentDialogResult.Primary)
                    return;

                OptimizationResultsBorder.Visibility =
                    Visibility.Visible;

                OptimizationResultsTitleText.Text =
                    experiment.OptimizationTitle;

                OptimizationResultsStatusText.Text =
                    "RESUMING REBOOT EXPERIMENT...";

                OptimizationResultsStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold);

                OptimizationResultsSummaryText.Text =
                    "BuildCore is running the after-reboot workload benchmark.";

                var resumeService =
                    new RebootOptimizationExperimentResumeService(
                        _benchmarkService);

                RebootOptimizationExperimentState resumed =
                    await resumeService.ResumeAsync(experiment);

                if (resumed.Phase ==
                    RebootOptimizationExperimentPhase.Completed)
                {
                    OptimizationResultsStatusText.Text =
                        "EXPERIMENT COMPLETE";

                    OptimizationResultsStatusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            GetAccentColor());

                    OptimizationResultsSummaryText.Text =
                        resumed.Analysis?.Summary ??
                        resumed.Status;
                }
                else
                {
                    OptimizationResultsStatusText.Text =
                        "EXPERIMENT INCONCLUSIVE";

                    OptimizationResultsStatusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gold);

                    OptimizationResultsSummaryText.Text =
                        resumed.Status;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine(
                    "BUILDCORE REBOOT EXPERIMENT RESUME CANCELED");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "BUILDCORE REBOOT EXPERIMENT RESUME ERROR");

                Debug.WriteLine(ex.ToString());

                OptimizationResultsStatusText.Text =
                    "REBOOT EXPERIMENT FAILED";

                OptimizationResultsSummaryText.Text =
                    ex.Message;
            }
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

            UpdateSystemPageValues();
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

            UpdateSystemPageValues();
        }

        private void UpdateSystemPageValues()
        {
            SystemCpuText.Text =
                CpuText.Text;

            SystemGpuText.Text =
                GpuText.Text;

            SystemRamText.Text =
                RamText.Text;

            SystemStorageText.Text =
                StorageText.Text;

            SystemWindowsText.Text =
                WindowsVersionText.Text;

            SystemWindowsBuildText.Text =
                WindowsBuildText.Text;

            SystemPowerPlanText.Text =
                PowerPlanText.Text;

            SystemGameModeText.Text =
                GameModeText.Text;

            SystemHagsText.Text =
                HagsText.Text;

            SystemMemoryIntegrityText.Text =
                MemoryIntegrityText.Text;

            SystemDefenderText.Text =
                DefenderText.Text;

            SystemGameBarText.Text =
                GameBarText.Text;

            SystemWindowsUpdateText.Text =
                WindowsUpdateText.Text;
        }

        private void UpdatePerformancePageValues()
        {
            PerformanceCpuText.Text =
                CpuPerformanceLarge.Text;

            PerformanceCpuTempText.Text =
                CpuTemperatureLarge.Text;

            PerformanceCpuClockText.Text =
                CpuClockLarge.Text;

            PerformanceGpuText.Text =
                GpuUsageText.Text;

            PerformanceGpuTempText.Text =
                GpuTemperatureLarge.Text;

            PerformanceGpuClockText.Text =
                GpuClockLarge.Text;

            PerformanceGpuVramText.Text =
                GpuMemoryLarge.Text;

            PerformanceRamText.Text =
                RamPerformanceLarge.Text;

            PerformanceDiskText.Text =
                DiskPerformanceLarge.Text;
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
                        new GridLength(110)
                });

            actionRow.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(190)
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
            // INDIVIDUAL OPTIMIZATION TOGGLE
            // ========================================================

            var toggle =
                new ToggleSwitch
                {
                    Header = "ENABLE",
                    IsOn = false,
                    HorizontalAlignment =
                        HorizontalAlignment.Right,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    Tag =
                        new OptimizationToggleContext
                        {
                            Recommendation =
                                recommendation,

                            StatusText =
                                status
                        }
                };

            toggle.Toggled +=
                OptimizationToggle_Toggled;

            Grid.SetColumn(
                toggle,
                1);

            actionRow.Children.Add(
                toggle);

            // Existing APPLY / TEST controls remain available below the
            // toggle until the selected-tweaks apply workflow is connected.

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
                    2);

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
                    2);

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

            border.Tag =
                recommendation.Category;

            RecommendationsPanel.Children.Add(
                border);

            ApplyOptimizationCategoryFilter();
        }

        private class OptimizationToggleContext
        {
            public OptimizationRecommendation
                Recommendation
            { get; set; } =
                new OptimizationRecommendation();

            public TextBlock StatusText { get; set; } =
                new TextBlock();
        }

        private void OptimizationToggle_Toggled(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggle ||
                toggle.Tag is not OptimizationToggleContext context)
            {
                return;
            }

            context.StatusText.Text =
                toggle.IsOn
                    ? "SELECTED • READY TO APPLY"
                    : context.Recommendation.ActionStatus;
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

            if (WorkloadProfileComboBox.SelectedItem
                is not BenchmarkWorkloadProfile workloadProfile)
            {
                statusText.Text = "SELECT A WORKLOAD PROFILE";
                statusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold);
                return;
            }

            BenchmarkWorkload workloadDefinition =
                workloadProfile.Definition;

            if (workloadProfile.UsesRealFrameTimeSource)
            {
                if (WorkloadProcessComboBox.SelectedItem
                    is not RunningProcessInfo workloadProcess)
                {
                    statusText.Text = "SELECT A TARGET PROCESS";
                    statusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gold);
                    return;
                }

                workloadDefinition.TargetProcessId =
                    workloadProcess.ProcessId;

                workloadDefinition.TargetProcessStartTimeUtc =
                    workloadProcess.StartTimeUtc;

                workloadDefinition.TargetProcessPath =
                    workloadProcess.ExecutablePath;
            }

            // ========================================================
            // PHASE 1.14D - REBOOT-BASED EXPERIMENT PREPARATION
            // ========================================================

            if (recommendation.RequiresReboot)
            {
                if (_optimizationTestRunning)
                    return;

                _optimizationTestRunning = true;
                button.IsEnabled = false;
                button.Content = "PREPARING...";

                OptimizationResultsBorder.Visibility =
                    Visibility.Visible;

                OptimizationResultsTitleText.Text =
                    recommendation.Title;

                OptimizationResultsStatusText.Text =
                    "PREPARING REBOOT EXPERIMENT...";

                OptimizationResultsStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold);

                OptimizationResultsPanel.Children.Clear();

                try
                {
                    var preparationService =
                        new RebootOptimizationExperimentPreparationService(
                            _benchmarkService);

                    RebootOptimizationExperimentState experiment =
                        await preparationService.PrepareAsync(
                            recommendation,
                            workloadDefinition);

                    if (experiment.Phase ==
                        RebootOptimizationExperimentPhase.OptimizationPendingReboot)
                    {
                        OptimizationResultsStatusText.Text =
                            "REBOOT REQUIRED";

                        OptimizationResultsSummaryText.Text =
                            "Baseline completed and the optimization change " +
                            "has been prepared. Restart Windows to continue " +
                            "the experiment. BuildCore will not restart Windows " +
                            "without your confirmation.";

                        statusText.Text =
                            "RESTART REQUIRED";

                        statusText.Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gold);

                        await ShowRebootReadyDialog(experiment);
                    }
                    else
                    {
                        OptimizationResultsStatusText.Text =
                            "EXPERIMENT NOT READY";

                        OptimizationResultsSummaryText.Text =
                            experiment.Status;

                        statusText.Text =
                            "TEST INCONCLUSIVE";

                        statusText.Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gold);
                    }
                }
                catch (OperationCanceledException)
                {
                    statusText.Text = "TEST CANCELED";
                }
                catch (Exception ex)
                {
                    statusText.Text = "TEST FAILED";

                    statusText.Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.OrangeRed);

                    OptimizationResultsStatusText.Text =
                        "REBOOT EXPERIMENT FAILED";

                    OptimizationResultsSummaryText.Text =
                        ex.Message;

                    Debug.WriteLine(
                        "BUILDCORE REBOOT EXPERIMENT PREPARATION ERROR");

                    Debug.WriteLine(
                        ex.ToString());
                }
                finally
                {
                    _optimizationTestRunning = false;
                    button.IsEnabled = true;
                    button.Content = "TEST • REBOOT";
                }

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
                var controlledWorkloadTestService =
                    new ControlledWorkloadTestService(
                        _benchmarkService);

                OptimizationBenchmarkResult result =
                    await controlledWorkloadTestService.RunAsync(
                        recommendation,
                        workloadDefinition,
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
                        result.WorkloadAnalysis != null
                            ? result.WorkloadAnalysis.Summary
                            : "BuildCore completed the controlled workload test.";
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

        private async Task ShowRebootReadyDialog(
            RebootOptimizationExperimentState experiment)
        {
            var dialog =
                new ContentDialog
                {
                    Title =
                        "REBOOT REQUIRED",

                    Content =
                        new TextBlock
                        {
                            Text =
                                "BuildCore has completed the baseline and " +
                                "prepared the optimization change.\\n\\n" +
                                $"Optimization: {experiment.OptimizationTitle}\\n" +
                                $"Snapshot: {experiment.SnapshotId}\\n\\n" +
                                "Restart Windows to continue the experiment. " +
                                "After BuildCore starts again, it will detect " +
                                "the saved experiment and validate the reboot.",

                            TextWrapping =
                                TextWrapping.Wrap
                        },

                    PrimaryButtonText =
                        "RESTART WINDOWS",

                    CloseButtonText =
                        "CANCEL",

                    XamlRoot =
                        Content.XamlRoot
                };

            ContentDialogResult result =
                await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = "shutdown.exe",
                            Arguments = "/r /t 0",
                            UseShellExecute = true
                        });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        "BUILDCORE RESTART ERROR");

                    Debug.WriteLine(
                        ex.ToString());
                }
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
                string.IsNullOrWhiteSpace(result.OptimizationTitle)
                    ? "Optimization Test"
                    : result.OptimizationTitle;

            if (result.WorkloadAnalysis != null &&
                result.WorkloadBaseline != null &&
                result.WorkloadAfter != null)
            {
                WorkloadStatisticalAnalysis analysis =
                    result.WorkloadAnalysis;

                OptimizationResultsStatusText.Text =
                    analysis.Outcome == WorkloadAnalysisOutcome.ImprovementDetected
                        ? "WORKLOAD IMPROVEMENT DETECTED"
                        : analysis.Outcome == WorkloadAnalysisOutcome.RegressionDetected
                            ? "WORKLOAD REGRESSION DETECTED"
                            : analysis.Outcome == WorkloadAnalysisOutcome.NoMeaningfulChange
                                ? "NO MEANINGFUL WORKLOAD CHANGE"
                                : "WORKLOAD TEST INCONCLUSIVE";

                OptimizationResultsStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        analysis.Outcome == WorkloadAnalysisOutcome.Inconclusive
                            ? Microsoft.UI.Colors.Gold
                            : Microsoft.UI.Colors.LightGreen);

                OptimizationResultsSummaryText.Text =
                    analysis.Summary;

                AddWorkloadMetricRow("Average FPS", analysis.AverageFps, "FPS", "0.00");
                AddWorkloadMetricRow("1% low FPS", analysis.OnePercentLowFps, "FPS", "0.00");
                AddWorkloadMetricRow("0.1% low FPS", analysis.ZeroPointOnePercentLowFps, "FPS", "0.00");
                AddWorkloadMetricRow("Average frame time", analysis.AverageFrameTime, "ms", "0.000");
                AddWorkloadMetricRow("Frame-time SD", analysis.FrameTimeStandardDeviation, "ms", "0.000");
                AddWorkloadMetricRow("GPU utilization", analysis.Gpu, "%", "0.0");
                AddWorkloadMetricRow("GPU clock", analysis.GpuClock, "MHz", "0");
                AddWorkloadMetricRow("GPU temperature", analysis.GpuTemperature, "°C", "0.0");

                AddOptimizationResultRow(
                    "Confidence",
                    $"{analysis.ConfidenceScore:F0}/100",
                    $"{result.WorkloadBaseline.CompletedRuns}/{result.WorkloadBaseline.RequestedRuns} runs",
                    $"{result.WorkloadAfter.CompletedRuns}/{result.WorkloadAfter.RequestedRuns} runs");

                return;
            }

            if (!result.IsSuccessful)
            {
                OptimizationResultsStatusText.Text =
                    result.Status;

                OptimizationResultsStatusText.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.OrangeRed);

                OptimizationResultsSummaryText.Text =
                    OptimizationResultsFormatter.BuildStatus(result);

                return;
            }

            // Legacy telemetry-only controlled test display.
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
                OptimizationResultsFormatter.FormatPercentage(comparison.CpuAverageBefore),
                OptimizationResultsFormatter.FormatPercentage(comparison.CpuAverageAfter),
                OptimizationResultsFormatter.FormatPercentageDelta(comparison.CpuAverageDelta));

            AddOptimizationResultRow(
                "GPU average",
                OptimizationResultsFormatter.FormatPercentage(comparison.GpuAverageBefore),
                OptimizationResultsFormatter.FormatPercentage(comparison.GpuAverageAfter),
                OptimizationResultsFormatter.FormatPercentageDelta(comparison.GpuAverageDelta));
        }

        private void AddWorkloadMetricRow(
            string name,
            WorkloadMetricAnalysis? metric,
            string unit,
            string format)
        {
            if (metric == null)
            {
                AddOptimizationResultRow(
                    name,
                    "N/A",
                    "N/A",
                    "NO DATA");
                return;
            }

            string before = metric.Before.ToString(format);
            string after = metric.After.ToString(format);
            string change =
                $"{metric.RelativeChangePercent:+0.00;-0.00;0.00}%";

            AddOptimizationResultRow(
                name,
                $"{before} {unit}",
                $"{after} {unit}",
                change);
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
                OptimizationCategory.Registry => "⌘",
                OptimizationCategory.Network => "◉",
                OptimizationCategory.Cleanup => "⌫",
                OptimizationCategory.Audio => "♪",
                OptimizationCategory.Timing => "◷",
                OptimizationCategory.Boot => "↗",
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
            ShowHistoryPage();
        }

        private void ShowHistoryPage()
        {
            HideAllPages();

            HistoryPage.Visibility =
                Visibility.Visible;

            PageTitleText.Text =
                "History";

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
                // WORKLOAD BENCHMARKS
                // ====================================================

                var workloadResults =
                    WorkloadBenchmarkStorageService.GetResults();

                root.Children.Add(
                    new TextBlock
                    {
                        Text = "WORKLOAD BENCHMARKS",
                        FontSize = 11,
                        FontWeight =
                            Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gold),
                        Margin = new Thickness(0, 18, 0, 0)
                    });

                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            $"{workloadResults.Count} workload benchmark(s) recorded",
                        FontSize = 10,
                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gray)
                    });

                if (workloadResults.Count == 0)
                {
                    root.Children.Add(
                        new TextBlock
                        {
                            Text =
                                "No workload benchmarks have been recorded yet.",
                            FontSize = 12,
                            Foreground =
                                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                    Microsoft.UI.Colors.LightGray),
                            TextWrapping = TextWrapping.Wrap
                        });
                }
                else
                {
                    foreach (WorkloadBenchmarkResult workloadResult
                        in workloadResults)
                    {
                        root.Children.Add(
                            CreateWorkloadBenchmarkHistoryCard(
                                workloadResult));
                    }
                }

                // ====================================================
                // OPTIMIZATION TESTS
                // ====================================================

                // ====================================================
                // REBOOT OPTIMIZATION EXPERIMENTS
                // ====================================================

                var rebootExperiments =
                    RebootOptimizationExperimentStorageService.GetExperiments();

                root.Children.Add(
                    new TextBlock
                    {
                        Text = "REBOOT OPTIMIZATION EXPERIMENTS",
                        FontSize = 11,
                        FontWeight =
                            Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gold),
                        Margin = new Thickness(0, 18, 0, 0)
                    });

                root.Children.Add(
                    new TextBlock
                    {
                        Text =
                            $"{rebootExperiments.Count} reboot experiment(s) recorded",
                        FontSize = 10,
                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gray)
                    });

                if (rebootExperiments.Count == 0)
                {
                    root.Children.Add(
                        new TextBlock
                        {
                            Text =
                                "No reboot optimization experiments have been recorded yet.",
                            FontSize = 12,
                            Foreground =
                                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                    Microsoft.UI.Colors.LightGray),
                            TextWrapping = TextWrapping.Wrap
                        });
                }
                else
                {
                    foreach (RebootOptimizationExperimentState experiment
                        in rebootExperiments)
                    {
                        root.Children.Add(
                            CreateRebootExperimentHistoryCard(experiment));
                    }
                }

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

                HistoryPanel.Children.Clear();
                HistoryPanel.Children.Add(root);

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

        private Border CreateRebootExperimentHistoryCard(
            RebootOptimizationExperimentState experiment)
        {
            string workloadName =
                experiment.WorkloadDefinition?.Name ??
                "Unknown workload";

            string resultStatus =
                experiment.IsPendingReboot
                    ? "WAITING FOR WINDOWS RESTART"
                    : experiment.IsRecoveryPendingReboot
                        ? "WAITING FOR WINDOWS RESTART TO FINALIZE RECOVERY"
                        : experiment.IsTerminal
                            ? experiment.Status
                            : experiment.Status;

            string metrics =
                experiment.Analysis != null
                    ? $"Confidence {experiment.Analysis.ConfidenceScore:F0}%"
                    : experiment.AfterBenchmarkCompleted
                        ? "After benchmark recorded"
                        : experiment.BaselineCompleted
                            ? "Baseline recorded"
                            : "No benchmark completed";

            string recovery =
                experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.Finalized
                    ? "✓ RECOVERY FINALIZED"
                    : experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.RebootRequired
                        ? "↻ RECOVERY REBOOT REQUIRED"
                        : experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.Failed
                            ? "✕ RECOVERY FAILED"
                            : experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.Available
                                ? "↻ ROLLBACK AVAILABLE"
                                : "No rollback handler available";

            var panel = new StackPanel { Spacing = 7 };

            panel.Children.Add(new TextBlock
            {
                Text = "↻  " + experiment.OptimizationTitle,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.White)
            });

            panel.Children.Add(new TextBlock
            {
                Text =
                    $"{experiment.CreatedAt:yyyy-MM-dd HH:mm:ss}  •  {workloadName}",
                FontSize = 9,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray)
            });

            panel.Children.Add(new TextBlock
            {
                Text =
                    $"{experiment.Phase.ToString().ToUpperInvariant()}  •  {resultStatus}",
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = experiment.IsPendingReboot ||
                    experiment.IsRecoveryPendingReboot
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold)
                    : experiment.Phase ==
                        RebootOptimizationExperimentPhase.Completed
                        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGreen)
                        : new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGray)
            });

            panel.Children.Add(new TextBlock
            {
                Text = metrics,
                FontSize = 10,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGray),
                TextWrapping = TextWrapping.Wrap
            });

            panel.Children.Add(new TextBlock
            {
                Text = recovery,
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = experiment.RecoveryPhase ==
                    RebootOptimizationExperimentRecoveryPhase.Finalized
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGreen)
                    : experiment.RecoveryPhase ==
                        RebootOptimizationExperimentRecoveryPhase.Failed
                        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.IndianRed)
                        : experiment.RecoveryPhase ==
                            RebootOptimizationExperimentRecoveryPhase.RebootRequired
                            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.Gold)
                            : experiment.RecoveryPhase ==
                                RebootOptimizationExperimentRecoveryPhase.Available
                                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                    Microsoft.UI.Colors.Gold)
                                : new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                    Microsoft.UI.Colors.Gray)
            });

            if (experiment.RecoveryRequiresReboot)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "↻ WINDOWS RESTART REQUIRED TO FINALIZE RESTORATION",
                    FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            panel.Children.Add(new TextBlock
            {
                Text =
                    $"Snapshot: {experiment.SnapshotId}\n" +
                    $"Experiment ID: {experiment.ExperimentId}",
                FontSize = 8,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            });

            var detailsButton = new Button
            {
                Content = "VIEW DETAILS",
                Padding = new Thickness(12, 7, 12, 7),
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = experiment
            };

            detailsButton.Click += RebootExperimentDetailsButton_Click;
            panel.Children.Add(detailsButton);

            var deleteButton = new Button
            {
                Content = "DELETE",
                Padding = new Thickness(12, 7, 12, 7),
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = experiment
            };

            deleteButton.Click += RebootExperimentDeleteButton_Click;
            panel.Children.Add(deleteButton);

            return new Border
            {
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                CornerRadius = new CornerRadius(10),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Transparent),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.DimGray),
                BorderThickness = new Thickness(1),
                Child = panel
            };
        }

        private async void RebootExperimentDetailsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not RebootOptimizationExperimentState experiment)
                return;

            await ShowRebootExperimentDetailsDialog(experiment);
        }

        private async Task ShowRebootExperimentDetailsDialog(
            RebootOptimizationExperimentState experiment)
        {
            string workloadName =
                experiment.WorkloadDefinition?.Name ??
                "Unknown workload";

            string baselineRuns =
                experiment.Baseline != null
                    ? $"{experiment.Baseline.CompletedRuns} / " +
                      $"{experiment.Baseline.RequestedRuns}"
                    : "Not recorded";

            string afterRuns =
                experiment.AfterBenchmark != null
                    ? $"{experiment.AfterBenchmark.CompletedRuns} / " +
                      $"{experiment.AfterBenchmark.RequestedRuns}"
                    : "Not recorded";

            string frameTime =
                experiment.Analysis != null
                    ? $"Paired runs: {experiment.Analysis.PairedRunCount}\n" +
                      $"Average paired difference: {experiment.Analysis.AveragePairedDifferencePercent:F2}%\n" +
                      $"Paired difference SD: {experiment.Analysis.PairedDifferenceStandardDeviationPercent:F2}%\n" +
                      $"Paired Average FPS: {FormatPairedMetric(experiment.Analysis.PairedAverageFps)}\n" +
                      $"Paired average frame time: {FormatPairedMetric(experiment.Analysis.PairedAverageFrameTime)}"
                    : "Statistical analysis not available.";

            string evidence =
                experiment.EvidenceQuality != null
                    ? $"Grade: {experiment.EvidenceQuality.EvidenceGrade}\n" +
                      $"Gate passed: {(experiment.EvidenceGatePassed ? "YES" : "NO")}\n" +
                      $"Frame-time evidence: {(experiment.EvidenceQuality.HasRealFrameTimeData ? "YES" : "NO")}\n" +
                      $"Fingerprints match: {(experiment.EvidenceQuality.FingerprintsMatch ? "YES" : "NO")}\n" +
                      $"Environment comparable: {(experiment.EvidenceQuality.EnvironmentComparable ? "YES" : "NO")}\n" +
                      $"Process identity verified: {(experiment.EvidenceQuality.ProcessIdentityVerified ? "YES" : "NO")}"
                    : "Evidence quality not available.";

            string environment =
                experiment.EnvironmentComparison != null
                    ? experiment.EnvironmentComparison.ToString() ?? "Comparison recorded."
                    : "Environment comparison not available.";

            var root = new StackPanel { Spacing = 10 };

            root.Children.Add(new TextBlock
            {
                Text = experiment.OptimizationTitle,
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.White),
                TextWrapping = TextWrapping.Wrap
            });

            root.Children.Add(new TextBlock
            {
                Text =
                    $"Status: {experiment.Status}\n" +
                    $"Phase: {experiment.Phase}\n" +
                    $"Workload: {workloadName}\n" +
                    $"Created: {experiment.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Updated: {experiment.UpdatedAt:yyyy-MM-dd HH:mm:ss}",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGray),
                TextWrapping = TextWrapping.Wrap
            });

            root.Children.Add(CreateRebootDetailsSection(
                "BENCHMARK RUNS",
                $"Baseline: {baselineRuns}\nAfter reboot: {afterRuns}\n" +
                $"Baseline complete: {(experiment.BaselineCompleted ? "YES" : "NO")}\n" +
                $"After benchmark complete: {(experiment.AfterBenchmarkCompleted ? "YES" : "NO")}"));

            root.Children.Add(CreateRebootDetailsSection(
                "ANALYSIS",
                experiment.Analysis?.Summary ??
                "No completed statistical analysis is available."));

            root.Children.Add(CreateRebootDetailsSection(
                "PAIRED ANALYSIS",
                frameTime));

            root.Children.Add(CreateRebootDetailsSection(
                "EVIDENCE QUALITY",
                evidence));

            root.Children.Add(CreateRebootDetailsSection(
                "ENVIRONMENT",
                environment));

            RebootOptimizationExperimentStorageHealth storageHealth =
                RebootOptimizationExperimentStorageService.GetHealth(
                    experiment.ExperimentId);

            root.Children.Add(CreateRebootDetailsSection(
                "RECOVERY HANDLER",
                GetRecoveryHandlerRegistryStatus()));

            root.Children.Add(CreateRebootDetailsSection(
                "STORAGE / PERSISTENCE",
                $"Primary state: {(storageHealth.PrimaryValid ? "VALID" : storageHealth.PrimaryExists ? "INVALID" : "MISSING")}\n" +
                $"Backup state: {(storageHealth.BackupValid ? "VALID" : storageHealth.BackupExists ? "INVALID" : "MISSING")}\n" +
                $"Storage health: {(storageHealth.IsHealthy ? "HEALTHY" : storageHealth.CanRecoverFromBackup ? "RECOVERABLE FROM BACKUP" : "UNRESOLVED")}"));

            root.Children.Add(CreateRebootDetailsSection(
                "RECOVERY / VALIDATION",
                $"Snapshot: {experiment.SnapshotId}\n" +
                $"Reboot detected: {(experiment.RebootDetected ? "YES" : "NO")}\n" +
                $"After-reboot validation: {(experiment.AfterRebootValidationPassed ? "PASSED" : "NOT PASSED")}\n" +
                $"Expected process: {experiment.ExpectedTargetProcessPath}\n" +
                $"Recovery phase: {experiment.RecoveryPhaseDisplayName}\n" +
                $"Recovery: {experiment.RecoveryStatus}\n" +
                $"Recovery attempted: {(experiment.RecoveryAttemptedAtUtc.HasValue ? experiment.RecoveryAttemptedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "NO")}\n" +
                $"Verification checked: {(experiment.RecoveryFinalizationCheckedAtUtc.HasValue ? experiment.RecoveryFinalizationCheckedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "NO")}\n" +
                $"Recovery finalized: {(experiment.RecoveryFinalized ? "YES" : "NO")}"));

            bool recoveryRequiresReboot =
                experiment.RecoveryRequiresReboot &&
                experiment.RecoverySucceeded &&
                !experiment.RecoveryFinalized;

            bool recoveryVerificationRetryAvailable =
                experiment.RecoveryPhase ==
                    RebootOptimizationExperimentRecoveryPhase.Failed &&
                experiment.RecoverySucceeded &&
                experiment.RecoveryAttemptedAtUtc.HasValue;

            bool canRollback =
                RebootOptimizationExperimentRecoveryService.CanRollback(
                    experiment);

            var dialog = new ContentDialog
            {
                Title = "Reboot Experiment Details",
                Content = new ScrollViewer
                {
                    Content = root,
                    MaxHeight = 650,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                },
                PrimaryButtonText = recoveryRequiresReboot
                    ? "RESTART WINDOWS"
                    : recoveryVerificationRetryAvailable
                        ? "VERIFY RECOVERY"
                        : canRollback
                            ? "ROLL BACK"
                            : null,
                CloseButtonText = "CLOSE",
                XamlRoot = ((FrameworkElement)this.Content).XamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();

            if (result != ContentDialogResult.Primary)
                return;

            if (recoveryRequiresReboot)
            {
                var restartDialog = new ContentDialog
                {
                    Title = "CONFIRM WINDOWS RESTART",
                    Content = "BuildCore has restored the original HAGS registry state. Windows must restart before BuildCore can finalize and verify the restoration. Restart now?",
                    PrimaryButtonText = "RESTART WINDOWS",
                    CloseButtonText = "CANCEL",
                    XamlRoot = ((FrameworkElement)this.Content).XamlRoot
                };

                ContentDialogResult restartResult = await restartDialog.ShowAsync();
                if (restartResult != ContentDialogResult.Primary)
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/r /t 0",
                    UseShellExecute = true
                });

                return;
            }

            if (recoveryVerificationRetryAvailable)
            {
                bool verified =
                    RebootOptimizationExperimentRecoveryService
                        .RetryFinalizationAfterFailure(experiment);

                var verificationDialog = new ContentDialog
                {
                    Title = verified
                        ? "RECOVERY VERIFIED"
                        : "RECOVERY STILL UNVERIFIED",
                    Content = verified
                        ? experiment.RecoveryStatus
                        : "BuildCore could not verify the original HAGS state. The recovery remains unresolved.",
                    CloseButtonText = "CLOSE",
                    XamlRoot = ((FrameworkElement)this.Content).XamlRoot
                };

                await verificationDialog.ShowAsync();
                return;
            }

            if (canRollback)
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "CONFIRM ROLLBACK",
                    Content = "BuildCore will restore the exact HAGS state captured before this experiment. Windows restart will be required to finalize the restoration. Continue?",
                    PrimaryButtonText = "RESTORE HAGS",
                    CloseButtonText = "CANCEL",
                    XamlRoot = ((FrameworkElement)this.Content).XamlRoot
                };

                ContentDialogResult confirmResult = await confirmDialog.ShowAsync();
                if (confirmResult != ContentDialogResult.Primary)
                    return;

                OptimizationApplyResult rollback =
                    RebootOptimizationExperimentRecoveryService.Rollback(experiment);

                var resultDialog = new ContentDialog
                {
                    Title = rollback.Success ? "ROLLBACK COMPLETE" : "ROLLBACK FAILED",
                    Content = rollback.Success
                        ? rollback.Message + "\n\nUse VIEW DETAILS again to restart Windows and finalize recovery."
                        : rollback.Message,
                    CloseButtonText = "CLOSE",
                    XamlRoot = ((FrameworkElement)this.Content).XamlRoot
                };

                await resultDialog.ShowAsync();
            }
        }

        private static string FormatPairedMetric(
            WorkloadMetricAnalysis? metric)
        {
            if (metric == null)
                return "Not available";

            return $"{metric.Before:F2} → {metric.After:F2} " +
                   $"({metric.RelativeChangePercent:+0.00;-0.00;0.00}%)";
        }

        private Border CreateRebootDetailsSection(
            string title,
            string value)
        {
            var panel = new StackPanel { Spacing = 5 };

            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gold)
            });

            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGray),
                TextWrapping = TextWrapping.Wrap
            });

            return new Border
            {
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.DimGray),
                BorderThickness = new Thickness(1),
                Child = panel
            };
        }

        private async void RebootExperimentDeleteButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not RebootOptimizationExperimentState experiment)
                return;

            if (experiment.IsPendingReboot ||
                experiment.IsRecoveryPendingReboot ||
                (experiment.RecoveryAvailable &&
                 !experiment.RecoveryFinalized &&
                 !experiment.RecoveryAttempted))
            {
                var blockedDialog = new ContentDialog
                {
                    Title = "EXPERIMENT ACTIVE",
                    Content = new TextBlock
                    {
                        Text = "This experiment cannot be deleted while a Windows restart, recovery action, or rollback is still pending. Complete the lifecycle first.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = "OK",
                    XamlRoot = ((FrameworkElement)this.Content).XamlRoot
                };

                await blockedDialog.ShowAsync();
                return;
            }

            if (RebootOptimizationExperimentStorageService.Delete(
                experiment.ExperimentId))
            {
                ShowHistoryPage();
            }
        }

        // ============================================================
        // WORKLOAD BENCHMARK HISTORY CARD
        // ============================================================

        private Border CreateWorkloadBenchmarkHistoryCard(
            WorkloadBenchmarkResult result)
        {
            bool complete = result.IsComplete;

            var panel = new StackPanel { Spacing = 7 };

            panel.Children.Add(
                new TextBlock
                {
                    Text = "◈  " +
                        (string.IsNullOrWhiteSpace(result.WorkloadName)
                            ? "Workload Benchmark"
                            : result.WorkloadName),
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
                        result.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    FontSize = 9,
                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray)
                });

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        complete
                            ? $"✓ {result.ReliabilityStatus.ToUpperInvariant()} — " +
                              $"{result.CompletedRuns}/{result.RequestedRuns} RUNS"
                            : $"✕ {result.ReliabilityStatus.ToUpperInvariant()} — " +
                              $"{result.CompletedRuns}/{result.RequestedRuns} RUNS",
                    FontSize = 9,
                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground =
                        complete
                            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.LightGreen)
                            : new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.OrangeRed)
                });

            if (complete && result.Runs.Count > 0)
            {
                panel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            $"CPU {result.Runs.Average(r => r.CpuAverageUsage):F1}%   •   " +
                            $"GPU {result.Runs.Average(r => r.GpuAverageUsage):F1}%   •   " +
                            $"Duration {result.Duration.TotalSeconds:F1}s",
                        FontSize = 10,
                        Foreground =
                            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.Colors.LightGray)
                    });
            }

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Type: {result.WorkloadType}   •   ID: {result.ResultId}",
                    FontSize = 8,
                    Foreground =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });

            var viewButton = new Button
            {
                Content = "VIEW BENCHMARK",
                Padding = new Thickness(12, 7, 12, 7),
                Tag = result
            };

            viewButton.Click += ViewWorkloadBenchmarkButton_Click;

            panel.Children.Add(viewButton);

            var deleteButton = new Button
            {
                Content = "DELETE",
                Padding = new Thickness(12, 7, 12, 7),
                Tag = result
            };

            deleteButton.Click += DeleteWorkloadBenchmarkButton_Click;

            panel.Children.Add(deleteButton);

            return new Border
            {
                Background =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Transparent),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.DimGray),
                BorderThickness = new Thickness(1),
                Child = panel
            };
        }

        private async void ViewWorkloadBenchmarkButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not WorkloadBenchmarkResult result)
                return;

            var lines = new List<string>
            {
                $"Workload: {result.WorkloadName}",
                $"Type: {result.WorkloadType}",
                $"Status: {result.Status}",
                $"Reliability: {result.ReliabilityStatus}",
                $"Runs: {result.CompletedRuns}/{result.RequestedRuns}",
                $"Started: {result.StartedAt:yyyy-MM-dd HH:mm:ss}",
                $"Completed: {result.CompletedAt:yyyy-MM-dd HH:mm:ss}",
                $"Result ID: {result.ResultId}"
            };

            if (result.Runs.Count > 0)
            {
                lines.Add("");
                lines.Add("RUN DETAILS");

                foreach (BenchmarkRun run in result.Runs)
                {
                    lines.Add(
                        $"Run {run.RunNumber}: CPU {run.CpuAverageUsage:F1}% | " +
                        $"GPU {run.GpuAverageUsage:F1}% | " +
                        $"GPU Clock {run.GpuAverageClockMHz:F0} MHz | " +
                        $"GPU Temp {run.GpuAverageTemperature:F1}°C");

                    if (run.FrameTime != null &&
                        run.FrameTime.HasData)
                    {
                        lines.Add(
                            $"  Frame time: {run.FrameTime.AverageFrameTimeMilliseconds:F2} ms | " +
                            $"FPS: {run.FrameTime.AverageFps:F1} | " +
                            $"1% Low: {run.FrameTime.OnePercentLowFps:F1} | " +
                            $"0.1% Low: {run.FrameTime.ZeroPointOnePercentLowFps:F1}");
                    }
                }
            }

            var dialog = new ContentDialog
            {
                Title = "Workload Benchmark Details",
                Content = new ScrollViewer
                {
                    MaxHeight = 520,
                    Content = new TextBlock
                    {
                        Text = string.Join("\n", lines),
                        TextWrapping = TextWrapping.Wrap
                    }
                },
                CloseButtonText = "CLOSE",
                XamlRoot = ((FrameworkElement)this.Content).XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async void DeleteWorkloadBenchmarkButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not WorkloadBenchmarkResult result)
                return;

            var confirmationContent =
                new StackPanel
                {
                    Spacing = 12,
                    Padding = new Thickness(4)
                };

            confirmationContent.Children.Add(
                new TextBlock
                {
                    Text = "DELETE BENCHMARK",
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold)
                });

            confirmationContent.Children.Add(
                new Border
                {
                    Padding = new Thickness(14),
                    CornerRadius = new CornerRadius(10),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Transparent),
                    BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.DimGray),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text =
                            $"Permanently delete the saved benchmark '{result.WorkloadName}'?",
                        FontSize = 11,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGray),
                        TextWrapping = TextWrapping.Wrap
                    }
                });

            confirmationContent.Children.Add(
                new TextBlock
                {
                    Text = "This action only removes the saved benchmark record.",
                    FontSize = 9,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });

            var confirmation = new ContentDialog
            {
                Title = "CONFIRM DELETION",
                Content = confirmationContent,
                PrimaryButtonText = "DELETE",
                CloseButtonText = "CANCEL",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = ((FrameworkElement)this.Content).XamlRoot
            };

            ContentDialogResult choice =
                await confirmation.ShowAsync();

            if (choice != ContentDialogResult.Primary)
                return;

            try
            {
                bool deleted =
                    WorkloadBenchmarkStorageService.DeleteResult(
                        result.ResultId);

                if (deleted)
                {
                    ShowHistoryPage();
                }
                else
                {
                    button.Content = "DELETE FAILED";
                    button.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                button.Content = "DELETE FAILED";
                button.IsEnabled = true;

                Debug.WriteLine(
                    $"BUILDCORE WORKLOAD BENCHMARK DELETE ERROR: {ex}");
            }
        }

        // ============================================================
        // OPTIMIZATION TEST HISTORY CARD
        // ============================================================

        private Border CreateOptimizationTestHistoryCard(
            OptimizationBenchmarkResult test)
        {
            bool workloadTest =
                test.WorkloadAnalysis != null;

            bool successful =
                workloadTest
                    ? test.IsSuccessful
                    : test.IsSuccessful && test.Comparison != null;

            var panel = new StackPanel { Spacing = 7 };

            panel.Children.Add(new TextBlock
            {
                Text = "◈  " +
                    (string.IsNullOrWhiteSpace(test.OptimizationTitle)
                        ? "Optimization Test"
                        : test.OptimizationTitle),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.White)
            });

            panel.Children.Add(new TextBlock
            {
                Text = test.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                FontSize = 9,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray)
            });

            string status =
                workloadTest
                    ? (successful
                        ? "✓ CONTROLLED WORKLOAD TEST COMPLETE"
                        : $"✕ {test.Status.ToUpperInvariant()}")
                    : (successful
                        ? "✓ CONTROLLED TEST COMPLETE"
                        : $"✕ {test.Status.ToUpperInvariant()}");

            panel.Children.Add(new TextBlock
            {
                Text = status,
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = successful
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGreen)
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.OrangeRed)
            });

            if (workloadTest &&
                test.WorkloadBaseline != null &&
                test.WorkloadAfter != null &&
                test.WorkloadAnalysis != null)
            {
                WorkloadStatisticalAnalysis analysis =
                    test.WorkloadAnalysis;

                panel.Children.Add(new TextBlock
                {
                    Text =
                        $"{analysis.Outcome}  •  " +
                        $"Confidence {analysis.ConfidenceScore:F0}/100  •  " +
                        $"Baseline {test.WorkloadBaseline.CompletedRuns} runs  •  " +
                        $"After {test.WorkloadAfter.CompletedRuns} runs",
                    FontSize = 10,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGray),
                    TextWrapping = TextWrapping.Wrap
                });

                AddWorkloadHistoryMetric(
                    panel, "Average FPS", analysis.AverageFps);
                AddWorkloadHistoryMetric(
                    panel, "1% Low FPS", analysis.OnePercentLowFps);
                AddWorkloadHistoryMetric(
                    panel, "Average Frame Time", analysis.AverageFrameTime);
            }
            else if (successful &&
                     test.Comparison != null)
            {
                BenchmarkComparison comparison = test.Comparison;

                panel.Children.Add(new TextBlock
                {
                    Text =
                        $"CPU {OptimizationResultsFormatter.FormatPercentageDelta(comparison.CpuAverageDelta)}   •   " +
                        $"GPU {OptimizationResultsFormatter.FormatPercentageDelta(comparison.GpuAverageDelta)}   •   " +
                        $"GPU Temp {OptimizationResultsFormatter.FormatTemperatureDelta(comparison.GpuTemperatureDeltaC)}",
                    FontSize = 10,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGray),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            panel.Children.Add(new TextBlock
            {
                Text = $"Snapshot: {test.SnapshotId}",
                FontSize = 8,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            });

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var viewButton = new Button
            {
                Content = "VIEW TEST",
                Padding = new Thickness(12, 7, 12, 7),
                Tag = new OptimizationTestButtonContext
                {
                    Test = test
                }
            };

            viewButton.Click += ViewOptimizationTestButton_Click;

            var deleteButton = new Button
            {
                Content = "DELETE",
                Padding = new Thickness(12, 7, 12, 7),
                Tag = test
            };

            deleteButton.Click += DeleteOptimizationTestButton_Click;

            buttonRow.Children.Add(viewButton);
            buttonRow.Children.Add(deleteButton);
            panel.Children.Add(buttonRow);

            return new Border
            {
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                CornerRadius = new CornerRadius(10),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Transparent),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.DimGray),
                BorderThickness = new Thickness(1),
                Child = panel
            };
        }

        private void AddWorkloadHistoryMetric(
            StackPanel panel,
            string name,
            WorkloadMetricAnalysis? metric)
        {
            if (metric == null)
                return;

            panel.Children.Add(new TextBlock
            {
                Text =
                    $"{name}: {metric.Before:F2} → {metric.After:F2} " +
                    $"({metric.RelativeChangePercent:+0.00;-0.00;0.00}%)",
                FontSize = 9,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray)
            });
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

        private static string FormatWorkloadMetric(
            WorkloadMetricAnalysis? metric,
            bool after = false)
        {
            if (metric == null)
                return "N/A";

            double value = after ? metric.After : metric.Before;

            if (double.IsNaN(value) || double.IsInfinity(value))
                return "N/A";

            string unit =
                metric.Name.Contains("FPS", StringComparison.OrdinalIgnoreCase)
                    ? " FPS"
                    : metric.Name.Contains("frame time", StringComparison.OrdinalIgnoreCase)
                        ? " ms"
                        : "";

            return $"{value:F2}{unit}";
        }

        private static string FormatWorkloadChange(
            WorkloadMetricAnalysis? metric)
        {
            if (metric == null ||
                double.IsNaN(metric.RelativeChangePercent) ||
                double.IsInfinity(metric.RelativeChangePercent))
                return "N/A";

            return $"{metric.RelativeChangePercent:+0.00;-0.00;0.00}%";
        }

        private async Task ShowOptimizationTestDialog(
            OptimizationBenchmarkResult test)
        {
            var root =
                new StackPanel
                {
                    Spacing = 10
                };

            if (test.WorkloadAnalysis != null &&
                test.WorkloadBaseline != null &&
                test.WorkloadAfter != null)
            {
                WorkloadStatisticalAnalysis workloadAnalysis =
                    test.WorkloadAnalysis;

                root.Children.Add(new TextBlock
                {
                    Text = "REAL WORKLOAD PERFORMANCE ANALYSIS",
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold),
                    Margin = new Thickness(0, 10, 0, 0)
                });

                root.Children.Add(new TextBlock
                {
                    Text =
                        $"Workload: {test.WorkloadDefinition?.Name ?? "Unknown"} • " +
                        $"Type: {test.WorkloadDefinition?.Type.ToString() ?? "Unknown"}",
                    FontSize = 10,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGray)
                });

                root.Children.Add(CreateTestMetricRow(
                    "Average FPS",
                    FormatWorkloadMetric(workloadAnalysis.AverageFps),
                    FormatWorkloadMetric(workloadAnalysis.AverageFps, true),
                    FormatWorkloadChange(workloadAnalysis.AverageFps)));

                root.Children.Add(CreateTestMetricRow(
                    "1% low FPS",
                    FormatWorkloadMetric(workloadAnalysis.OnePercentLowFps),
                    FormatWorkloadMetric(workloadAnalysis.OnePercentLowFps, true),
                    FormatWorkloadChange(workloadAnalysis.OnePercentLowFps)));

                root.Children.Add(CreateTestMetricRow(
                    "0.1% low FPS",
                    FormatWorkloadMetric(workloadAnalysis.ZeroPointOnePercentLowFps),
                    FormatWorkloadMetric(workloadAnalysis.ZeroPointOnePercentLowFps, true),
                    FormatWorkloadChange(workloadAnalysis.ZeroPointOnePercentLowFps)));

                root.Children.Add(CreateTestMetricRow(
                    "Average frame time",
                    FormatWorkloadMetric(workloadAnalysis.AverageFrameTime),
                    FormatWorkloadMetric(workloadAnalysis.AverageFrameTime, true),
                    FormatWorkloadChange(workloadAnalysis.AverageFrameTime)));

                root.Children.Add(CreateTestMetricRow(
                    "Frame-time SD",
                    FormatWorkloadMetric(workloadAnalysis.FrameTimeStandardDeviation),
                    FormatWorkloadMetric(workloadAnalysis.FrameTimeStandardDeviation, true),
                    FormatWorkloadChange(workloadAnalysis.FrameTimeStandardDeviation)));

                root.Children.Add(new TextBlock
                {
                    Text =
                        $"Baseline runs: {test.WorkloadBaseline.CompletedRuns}/{test.WorkloadBaseline.RequestedRuns} • " +
                        $"After runs: {test.WorkloadAfter.CompletedRuns}/{test.WorkloadAfter.RequestedRuns} • " +
                        $"Confidence: {workloadAnalysis.ConfidenceScore:F0}/100",
                    FontSize = 9,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });

                root.Children.Add(new TextBlock
                {
                    Text =
                        workloadAnalysis.Summary +
                        "\n\nFrame-time/FPS measurements are workload evidence; " +
                        "they do not automatically prove lower input latency.",
                    FontSize = 9,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });

                var workloadDialog = new ContentDialog
                {
                    Title = "Controlled Workload Test",
                    Content = new ScrollViewer
                    {
                        MaxHeight = 650,
                        Content = root
                    },
                    CloseButtonText = "CLOSE",
                    XamlRoot = ((FrameworkElement)this.Content).XamlRoot
                };

                await workloadDialog.ShowAsync();
                return;
            }

            OptimizationTestAnalysis? analysis = test.Analysis;
            ReliableBenchmarkResult? baseline = test.BaselineReliable;
            ReliableBenchmarkResult? after = test.AfterReliable;

            root.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(test.OptimizationTitle)
                    ? "Optimization Test"
                    : test.OptimizationTitle,
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.White)
            });

            root.Children.Add(new TextBlock
            {
                Text = OptimizationResultsFormatter.BuildStatus(test),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    analysis?.Outcome == OptimizationTestOutcome.ImprovementDetected
                        ? Microsoft.UI.Colors.LightGreen
                        : analysis?.Outcome == OptimizationTestOutcome.RegressionDetected
                            ? Microsoft.UI.Colors.OrangeRed
                            : Microsoft.UI.Colors.Gold)
            });

            root.Children.Add(new TextBlock
            {
                Text = OptimizationResultsFormatter.BuildSummary(test),
                FontSize = 10,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGray),
                TextWrapping = TextWrapping.Wrap
            });

            root.Children.Add(new TextBlock
            {
                Text = $"Confidence: {analysis?.ConfidenceScore ?? 0:F0}%",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.White)
            });

            root.Children.Add(new TextBlock
            {
                Text =
                    $"Baseline: {OptimizationResultsFormatter.FormatReliability(baseline)} • " +
                    $"{OptimizationResultsFormatter.FormatRunCount(baseline)}",
                FontSize = 10,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGray)
            });

            root.Children.Add(new TextBlock
            {
                Text =
                    $"After: {OptimizationResultsFormatter.FormatReliability(after)} • " +
                    $"{OptimizationResultsFormatter.FormatRunCount(after)}",
                FontSize = 10,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGray)
            });

            root.Children.Add(new TextBlock
            {
                Text = $"Snapshot: {test.SnapshotId}",
                FontSize = 9,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            });

            root.Children.Add(new TextBlock
            {
                Text = $"Test ID: {test.TestId}",
                FontSize = 8,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            });

            if (analysis != null)
            {
                root.Children.Add(new TextBlock
                {
                    Text = "RELIABLE TELEMETRY ANALYSIS",
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold),
                    Margin = new Thickness(0, 10, 0, 0)
                });

                root.Children.Add(CreateTestMetricRow(
                    "CPU average",
                    OptimizationResultsFormatter.FormatPercentage(analysis.CpuBefore),
                    OptimizationResultsFormatter.FormatPercentage(analysis.CpuAfter),
                    OptimizationResultsFormatter.FormatPercentageDelta(analysis.CpuDelta)));

                root.Children.Add(CreateTestMetricRow(
                    "GPU average",
                    OptimizationResultsFormatter.FormatPercentage(analysis.GpuBefore),
                    OptimizationResultsFormatter.FormatPercentage(analysis.GpuAfter),
                    OptimizationResultsFormatter.FormatPercentageDelta(analysis.GpuDelta)));

                root.Children.Add(CreateTestMetricRow(
                    "GPU clock",
                    OptimizationResultsFormatter.FormatMegahertz(analysis.GpuClockBeforeMHz),
                    OptimizationResultsFormatter.FormatMegahertz(analysis.GpuClockAfterMHz),
                    OptimizationResultsFormatter.FormatMegahertzDelta(analysis.GpuClockDeltaMHz)));

                root.Children.Add(CreateTestMetricRow(
                    "GPU temperature",
                    OptimizationResultsFormatter.FormatTemperature(analysis.GpuTemperatureBeforeC),
                    OptimizationResultsFormatter.FormatTemperature(analysis.GpuTemperatureAfterC),
                    OptimizationResultsFormatter.FormatTemperatureDelta(analysis.GpuTemperatureDeltaC)));

                root.Children.Add(CreateTestMetricRow(
                    "VRAM average",
                    OptimizationResultsFormatter.FormatGigabytes(analysis.VramBeforeGB),
                    OptimizationResultsFormatter.FormatGigabytes(analysis.VramAfterGB),
                    OptimizationResultsFormatter.FormatGigabyteDelta(analysis.VramDeltaGB)));

                root.Children.Add(new TextBlock
                {
                    Text =
                        $"CPU consistency: before " +
                        $"{OptimizationResultsFormatter.FormatStandardDeviation(analysis.CpuStandardDeviationBefore, "%")}  " +
                        $"after " +
                        $"{OptimizationResultsFormatter.FormatStandardDeviation(analysis.CpuStandardDeviationAfter, "%")}",
                    FontSize = 9,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });

                root.Children.Add(new TextBlock
                {
                    Text =
                        $"GPU consistency: before " +
                        $"{OptimizationResultsFormatter.FormatStandardDeviation(analysis.GpuStandardDeviationBefore, "%")}  " +
                        $"after " +
                        $"{OptimizationResultsFormatter.FormatStandardDeviation(analysis.GpuStandardDeviationAfter, "%")}",
                    FontSize = 9,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });

                root.Children.Add(new TextBlock
                {
                    Text =
                        $"GPU temperature consistency: before " +
                        $"{OptimizationResultsFormatter.FormatStandardDeviation(analysis.GpuTemperatureStandardDeviationBefore, " °C")}  " +
                        $"after " +
                        $"{OptimizationResultsFormatter.FormatStandardDeviation(analysis.GpuTemperatureStandardDeviationAfter, " °C")}",
                    FontSize = 9,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            if (baseline != null || after != null)
            {
                root.Children.Add(new TextBlock
                {
                    Text = "RELIABLE RUN SUMMARY",
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold),
                    Margin = new Thickness(0, 10, 0, 0)
                });

                if (baseline != null)
                {
                    root.Children.Add(new TextBlock
                    {
                        Text =
                            $"BASELINE: {baseline.CompletedRuns}/{baseline.RequestedRuns} runs • " +
                            $"CPU SD {baseline.CpuStandardDeviation:F2}% • " +
                            $"GPU SD {baseline.GpuStandardDeviation:F2}% • " +
                            $"Temp SD {baseline.GpuTemperatureStandardDeviation:F2} °C",
                        FontSize = 9,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGray),
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                if (after != null)
                {
                    root.Children.Add(new TextBlock
                    {
                        Text =
                            $"AFTER: {after.CompletedRuns}/{after.RequestedRuns} runs • " +
                            $"CPU SD {after.CpuStandardDeviation:F2}% • " +
                            $"GPU SD {after.GpuStandardDeviation:F2}% • " +
                            $"Temp SD {after.GpuTemperatureStandardDeviation:F2} °C",
                        FontSize = 9,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGray),
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }

            if (test.Comparison != null)
            {
                root.Children.Add(new TextBlock
                {
                    Text = "LEGACY TELEMETRY COMPARISON",
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold),
                    Margin = new Thickness(0, 10, 0, 0)
                });

                root.Children.Add(CreateTestMetricRow(
                    "RAM average",
                    OptimizationResultsFormatter.FormatPercentage(
                        test.Comparison.RamAverageBefore),
                    OptimizationResultsFormatter.FormatPercentage(
                        test.Comparison.RamAverageAfter),
                    OptimizationResultsFormatter.FormatPercentageDelta(
                        test.Comparison.RamAverageDelta)));

                root.Children.Add(CreateTestMetricRow(
                    "Disk average",
                    OptimizationResultsFormatter.FormatPercentage(
                        test.Comparison.DiskAverageBefore),
                    OptimizationResultsFormatter.FormatPercentage(
                        test.Comparison.DiskAverageAfter),
                    OptimizationResultsFormatter.FormatPercentageDelta(
                        test.Comparison.DiskAverageDelta)));
            }

            root.Children.Add(new TextBlock
            {
                Text =
                    "These measurements describe system telemetry consistency and change. " +
                    "They do not by themselves prove an FPS, frame-time, or input-latency improvement.",
                FontSize = 9,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            });

            var dialog = new ContentDialog
            {
                Title = "Optimization Test Details",
                Content = new ScrollViewer
                {
                    Content = root,
                    MaxHeight = 650,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                },
                CloseButtonText = "CLOSE",
                XamlRoot = ((FrameworkElement)this.Content).XamlRoot
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

            var confirmationContent =
                new StackPanel
                {
                    Spacing = 12,
                    Padding = new Thickness(4)
                };

            confirmationContent.Children.Add(
                new TextBlock
                {
                    Text = "DELETE BENCHMARK",
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gold)
                });

            confirmationContent.Children.Add(
                new Border
                {
                    Padding = new Thickness(14),
                    CornerRadius = new CornerRadius(10),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Transparent),
                    BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.DimGray),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text =
                            $"Permanently delete the saved test for '{test.OptimizationTitle}'?\n\n" +
                            "The associated snapshot and optimization transaction will not be deleted.",
                        FontSize = 11,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.LightGray),
                        TextWrapping = TextWrapping.Wrap
                    }
                });

            confirmationContent.Children.Add(
                new TextBlock
                {
                    Text = "This action only removes the saved benchmark record.",
                    FontSize = 9,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });

            var confirmation = new ContentDialog
            {
                Title = "CONFIRM DELETION",
                Content = confirmationContent,
                PrimaryButtonText = "DELETE",
                CloseButtonText = "CANCEL",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = ((FrameworkElement)this.Content).XamlRoot
            };

            ContentDialogResult result =
                await confirmation.ShowAsync();

            if (result !=
                ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                bool deleted =
                    OptimizationTestStorageService
                        .DeleteTest(test.TestId);

                if (deleted)
                {
                    ShowHistoryPage();

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
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"BUILDCORE TEST DELETE ERROR: {ex}");

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

                UpdatePerformancePageValues();

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

                UpdatePerformancePageValues();
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
            if (data.CpuTemperature.HasValue &&
                data.CpuTemperature.Value >= 0 &&
                data.CpuTemperature.Value <= 120)
            {
                CpuTemperatureLarge.Text =
                    $"{data.CpuTemperature.Value:F0}°C";

                CpuTemperatureText.Text =
                    $"CPU temperature • {data.CpuTemperatureSource}";
            }
            else
            {
                CpuTemperatureLarge.Text =
                    "N/A";

                CpuTemperatureText.Text =
                    "CPU temperature sensor unavailable";
            }

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
        // WORKLOAD BENCHMARK
        // ============================================================

        private void InitializeWorkloadProfiles()
        {
            WorkloadProfileComboBox.Items.Clear();

            foreach (BenchmarkWorkloadProfile profile
                in BenchmarkWorkloadProfiles.GetAll())
            {
                WorkloadProfileComboBox.Items.Add(profile);
            }

            WorkloadProfileComboBox.DisplayMemberPath =
                "Definition.Name";

            WorkloadProfileComboBox.SelectedIndex = 0;
            WorkloadProfileComboBox.SelectionChanged +=
                WorkloadProfileComboBox_SelectionChanged;

            WorkloadProcessComboBox.DisplayMemberPath =
                "DisplayName";

            RefreshWorkloadProcesses();

            UpdateSelectedWorkloadProfile();
        }

        private void WorkloadProfileComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            UpdateSelectedWorkloadProfile();
        }

        private void UpdateSelectedWorkloadProfile()
        {
            if (WorkloadProfileComboBox.SelectedItem
                is not BenchmarkWorkloadProfile profile)
            {
                WorkloadProfileDescriptionText.Text =
                    "Select a profile to view its description.";
                return;
            }

            WorkloadProfileDescriptionText.Text =
                $"{profile.Definition.Description} " +
                $"Runs: {profile.Definition.RunCount} × " +
                $"{profile.Definition.DurationSeconds}s. " +
                profile.RecommendedUse;
        }

        private void RefreshWorkloadProcessesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RefreshWorkloadProcesses();
        }

        private void RefreshWorkloadProcesses()
        {
            try
            {
                int previousPid =
                    WorkloadProcessComboBox.SelectedItem
                        is RunningProcessInfo previous
                        ? previous.ProcessId
                        : 0;

                WorkloadProcessComboBox.Items.Clear();

                IReadOnlyList<RunningProcessInfo> processes =
                    RunningProcessService.GetRunningProcesses();

                foreach (RunningProcessInfo process in processes)
                    WorkloadProcessComboBox.Items.Add(process);

                RunningProcessInfo? previousProcess =
                    processes.FirstOrDefault(
                        p => p.ProcessId == previousPid);

                if (previousProcess != null)
                    WorkloadProcessComboBox.SelectedItem = previousProcess;

                WorkloadProcessStatusText.Text =
                    $"{processes.Count} running processes detected. " +
                    "Select the application whose rendered frames should be measured.";
            }
            catch (Exception ex)
            {
                WorkloadProcessStatusText.Text =
                    "PROCESS SCAN FAILED";

                Debug.WriteLine(
                    $"BUILDCORE PROCESS SCAN ERROR: {ex}");
            }
        }

        private async void RunWorkloadBenchmarkButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (WorkloadProfileComboBox.SelectedItem
                is not BenchmarkWorkloadProfile profile)
            {
                WorkloadBenchmarkStatusText.Text =
                    "SELECT A WORKLOAD";
                return;
            }

            BenchmarkWorkload definition = profile.Definition;

            if (profile.UsesRealFrameTimeSource)
            {
                if (WorkloadProcessComboBox.SelectedItem
                    is not RunningProcessInfo process)
                {
                    WorkloadBenchmarkStatusText.Text =
                        "SELECT A TARGET PROCESS";
                    return;
                }

                definition.TargetProcessId =
                    process.ProcessId;
            }

            RunWorkloadBenchmarkButton.IsEnabled = false;
            WorkloadProfileComboBox.IsEnabled = false;
            WorkloadProcessComboBox.IsEnabled = false;
            RefreshWorkloadProcessesButton.IsEnabled = false;
            WorkloadBenchmarkStatusText.Text =
                profile.UsesRealFrameTimeSource
                    ? "RUNNING REAL FRAME-TIME CAPTURE..."
                    : "RUNNING TELEMETRY...";

            try
            {
                IBenchmarkWorkload workload;

                if (profile.UsesRealFrameTimeSource)
                {
                    var frameTimeSource =
                        new PresentMonFrameTimeSource();

                    if (!frameTimeSource.IsAvailable)
                    {
                        WorkloadBenchmarkStatusText.Text =
                            frameTimeSource.Description;

                        return;
                    }

                    workload =
                        new PresentMonWorkload(
                            _benchmarkService,
                            frameTimeSource,
                            definition);
                }
                else
                {
                    workload =
                        new TelemetryWorkload(
                            _benchmarkService,
                            definition);
                }

                var service =
                    new WorkloadBenchmarkService(
                        workload);

                WorkloadBenchmarkResult result =
                    await service.RunAsync();

                WorkloadBenchmarkStorageService
                    .SaveResult(result);

                WorkloadBenchmarkStatusText.Text =
                    result.IsComplete
                        ? $"{result.ReliabilityStatus.ToUpperInvariant()} — " +
                          $"{result.CompletedRuns}/{result.RequestedRuns} RUNS SAVED"
                        : $"INCOMPLETE — " +
                          $"{result.CompletedRuns}/{result.RequestedRuns} RUNS";

                Debug.WriteLine(
                    $"BUILDCORE WORKLOAD BENCHMARK: " +
                    $"{result.WorkloadName} | " +
                    $"{result.Status} | " +
                    $"{result.CompletedRuns}/{result.RequestedRuns}");
            }
            catch (Exception ex)
            {
                WorkloadBenchmarkStatusText.Text =
                    "BENCHMARK FAILED";

                Debug.WriteLine(
                    "BUILDCORE WORKLOAD BENCHMARK ERROR");

                Debug.WriteLine(ex.ToString());
            }
            finally
            {
                RunWorkloadBenchmarkButton.IsEnabled = true;
                WorkloadProfileComboBox.IsEnabled = true;
                WorkloadProcessComboBox.IsEnabled = true;
                RefreshWorkloadProcessesButton.IsEnabled = true;
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

        private readonly List<string> _autoTuneSelectedTweaks = new List<string>();
        private readonly List<OptimizationTransaction> _autoTuneAppliedTransactions = new List<OptimizationTransaction>();

        private async void AutoTuneApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_autoTuneSelectedTweaks.Count == 0)
                return;

            var result = await new ContentDialog
            {
                Title = "APPLY AUTOTUNE TWEAKS?",
                Content = $"BuildCore will create a snapshot and attempt to apply {_autoTuneSelectedTweaks.Count} selected verified tweaks. Continue?",
                PrimaryButtonText = "APPLY",
                CloseButtonText = "CANCEL",
                XamlRoot = AutoTunePage.XamlRoot
            }.ShowAsync();

            if (result != ContentDialogResult.Primary)
                return;

            AutoTuneApplyButton.IsEnabled = false;
            AutoTuneUnapplyButton.IsEnabled = false;

            try
            {
                SelectedOptimizationApplyResult applyResult =
                    SelectedOptimizationApplyService.Apply(_autoTuneSelectedTweaks);

                _autoTuneAppliedTransactions.Clear();
                _autoTuneAppliedTransactions.AddRange(
                    applyResult.Items
                        .Where(item => item.Transaction != null && item.Transaction.IsSuccessful)
                        .Select(item => item.Transaction!));

                AutoTuneStatusText.Text =
                    $"{_autoTuneAppliedTransactions.Count} TWEAKS APPLIED";

                AutoTuneSummaryText.Text =
                    $"Snapshot: {applyResult.SnapshotId}. Successful changes were verified before being recorded.";

                bool anyFailed = applyResult.Items.Any(item =>
                    item.Status.StartsWith("FAILED", StringComparison.OrdinalIgnoreCase));

                AutoTuneAssistantText.Text =
                    anyFailed
                        ? "One or more tweaks could not be applied. Review the results before making further changes."
                        : "All selected tweaks were applied and verified.";

                AutoTuneUnapplyButton.IsEnabled = _autoTuneAppliedTransactions.Count > 0;
            }
            catch (Exception ex)
            {
                AutoTuneStatusText.Text = "APPLY FAILED";
                AutoTuneAssistantText.Text = ex.Message;
                Debug.WriteLine($"BUILDCORE AUTOTUNE APPLY ERROR: {ex}");
            }
            finally
            {
                AutoTuneApplyButton.IsEnabled = _autoTuneSelectedTweaks.Count > 0;
            }
        }

        private async void AutoTuneUnapplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_autoTuneAppliedTransactions.Count == 0)
                return;

            var result = await new ContentDialog
            {
                Title = "UNAPPLY AUTOTUNE TWEAKS?",
                Content = $"BuildCore will restore {_autoTuneAppliedTransactions.Count} verified changes to their captured values.",
                PrimaryButtonText = "UNAPPLY",
                CloseButtonText = "CANCEL",
                XamlRoot = AutoTunePage.XamlRoot
            }.ShowAsync();

            if (result != ContentDialogResult.Primary)
                return;

            AutoTuneUnapplyButton.IsEnabled = false;

            int restored = 0;
            foreach (OptimizationTransaction transaction in _autoTuneAppliedTransactions.AsEnumerable().Reverse())
            {
                try
                {
                    OptimizationRestoreResult restoreResult =
                        OptimizationRestoreService.Restore(transaction);

                    if (restoreResult.Success &&
                        restoreResult.Verified)
                    {
                        restored++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BUILDCORE AUTOTUNE RESTORE ERROR: {ex}");
                }
            }

            AutoTuneStatusText.Text = $"{restored}/{_autoTuneAppliedTransactions.Count} TWEAKS UNAPPLIED";
            AutoTuneAssistantText.Text =
                restored == _autoTuneAppliedTransactions.Count
                    ? "All recorded AutoTune changes were restored and verified."
                    : "Some changes could not be restored. Check History before retrying.";

            if (restored == _autoTuneAppliedTransactions.Count)
                _autoTuneAppliedTransactions.Clear();

            AutoTuneUnapplyButton.IsEnabled = _autoTuneAppliedTransactions.Count > 0;
        }

        private void AutoTuneButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAutoTune();
        }

        private void AutoTuneAnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            RunAutoTuneAnalysis("SAFE");
        }

        private void AutoTuneSafeButton_Click(object sender, RoutedEventArgs e)
        {
            RunAutoTuneAnalysis("SAFE");
        }

        private void AutoTunePerformanceButton_Click(object sender, RoutedEventArgs e)
        {
            RunAutoTuneAnalysis("PERFORMANCE");
        }

        private void RunAutoTuneAnalysis(string mode)
        {
            AutoTuneAnalyzeButton.IsEnabled = false;
            AutoTuneSafeButton.IsEnabled = false;
            AutoTunePerformanceButton.IsEnabled = false;
            AutoTuneRecommendationsPanel.Children.Clear();
            AutoTuneStatusText.Text = "ANALYZING YOUR PC...";
            AutoTuneSummaryText.Text = "Checking detected hardware and BuildCore optimization metadata.";
            AutoTuneAssistantText.Text = "No system changes are being made.";

            try
            {
                SystemInfo info = SystemInfoService.GetSystemInfo();
                AutoTuneRecommendationsPanel.Children.Add(CreateAutoTuneSystemCard(info));

                int count = 0;
                foreach (OptimizationGroupDefinition group in OptimizationLibrary.Groups)
                {
                    foreach (OptimizationTweakDefinition tweak in group.Tweaks)
                    {
                        if (!IsAutoTuneCandidate(tweak, mode, info))
                            continue;

                        bool match =
                            IsCpuGuidanceRelevant(tweak, info.Cpu) &&
                            IsGpuGuidanceRelevant(tweak, info.Gpu);

                        AutoTuneRecommendationsPanel.Children.Add(
                            CreateAutoTuneRecommendationCard(tweak, match));
                        count++;
                    }
                }

                _autoTuneSelectedTweaks.Clear();
            _autoTuneAppliedTransactions.Clear();
            AutoTuneApplyButton.IsEnabled = false;
            AutoTuneUnapplyButton.IsEnabled = false;

            AutoTuneStatusText.Text = $"{count} CANDIDATES FOUND";
                AutoTuneSummaryText.Text =
                    $"Mode: {mode}. These are recommendations only; nothing was applied.";
                AutoTuneAssistantText.Text =
                    "Review the compatibility, risk, hardware effect, and rollback information before applying any change.";
            }
            catch (Exception ex)
            {
                AutoTuneStatusText.Text = "ANALYSIS FAILED";
                AutoTuneSummaryText.Text = "BuildCore could not safely complete the analysis.";
                AutoTuneAssistantText.Text = ex.Message;
                Debug.WriteLine($"BUILDCORE AUTOTUNE ERROR: {ex}");
            }
            finally
            {
                AutoTuneAnalyzeButton.IsEnabled = true;
                AutoTuneSafeButton.IsEnabled = true;
                AutoTunePerformanceButton.IsEnabled = true;
            }
        }

        private static bool IsAutoTuneCandidate(OptimizationTweakDefinition tweak, string mode, SystemInfo info)
        {
            if (!tweak.RollbackSupported)
                return false;

            if (mode.Equals("SAFE", StringComparison.OrdinalIgnoreCase) &&
                (tweak.Risk == OptimizationRisk.High || tweak.RequiresReboot))
                return false;

            if (tweak.Id is "nvidia-power" or "nvidia-reflex" or "nvidia-vrr")
                return info.Gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);

            if (tweak.Id is "radeon-power" or "radeon-anti-lag" or "radeon-chill")
                return info.Gpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                       info.Gpu.Contains("Radeon", StringComparison.OrdinalIgnoreCase);

            return true;
        }

        private static Border CreateAutoTuneSystemCard(SystemInfo info)
        {
            var card = new Border
            {
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                CornerRadius = new CornerRadius(10),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DimGray),
                BorderThickness = new Thickness(1)
            };

            card.Child = new TextBlock
            {
                Text = $"DETECTED SYSTEM\nCPU: {info.Cpu}\nGPU: {info.Gpu}\nRAM: {info.Ram}\nWINDOWS: {info.Windows}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            };
            return card;
        }

        private Border CreateAutoTuneRecommendationCard(
            OptimizationTweakDefinition tweak,
            bool hardwareMatch)
        {
            var card = new Border
            {
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(10),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.DimGray),
                BorderThickness = new Thickness(1)
            };

            var panel = new StackPanel();

            panel.Children.Add(new TextBlock
            {
                Text = $"{(hardwareMatch ? "✓ COMPATIBLE" : "△ REVIEW")} • {tweak.Title}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    hardwareMatch
                        ? Microsoft.UI.Colors.LightGreen
                        : Microsoft.UI.Colors.Gold),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"WHAT IT DOES\n{GetAutoTuneWhatItDoes(tweak)}\n\nPOSSIBLE DOWNSIDE\n{GetAutoTunePossibleDownside(tweak)}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGray),
                FontSize = 9,
                Margin = new Thickness(0, 7, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"CPU FIT: {tweak.CpuGuidance}\nGPU FIT: {tweak.GpuGuidance}\nHARDWARE EFFECT: {tweak.HardwareEffect}\nRISK: {tweak.Risk} • REBOOT: {(tweak.RequiresReboot ? "YES" : "NO")} • ROLLBACK: {(tweak.RollbackSupported ? "YES" : "NO")}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                FontSize = 9,
                Margin = new Thickness(0, 7, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var toggle = new ToggleSwitch
            {
                Header = "SELECT FOR AUTOTUNE",
                IsOn = _autoTuneSelectedTweaks.Contains(tweak.Id),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 0),
                Tag = tweak.Id
            };

            toggle.Toggled += AutoTuneTweakToggle_Toggled;
            panel.Children.Add(toggle);

            panel.Children.Add(new TextBlock
            {
                Text = "SELECTION ONLY • NO SYSTEM CHANGE",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                FontSize = 8,
                Margin = new Thickness(0, 6, 0, 0)
            });

            card.Child = panel;
            return card;
        }

        private static string GetAutoTuneWhatItDoes(
            OptimizationTweakDefinition tweak)
        {
            return tweak.Id switch
            {
                "high-performance" =>
                    "Switches Windows to the High Performance power plan so the system is less aggressive about reducing CPU and device power when performance is needed.",

                "windows-game-mode" or "gaming-game-mode" =>
                    "Enables Windows Game Mode, which prioritizes resources for games and reduces some background activity while a game is running.",

                "sleep" =>
                    "Prevents Windows from automatically entering sleep during a controlled workload so a benchmark or long-running task is not interrupted.",

                "display-timeout" =>
                    "Prevents the display from automatically turning off during a controlled workload.",

                "processor-min" =>
                    "Changes the minimum processor performance policy. This can keep the CPU at a higher performance state instead of allowing it to scale down as aggressively.",

                "processor-boost" =>
                    "Changes Windows processor boost behavior, affecting when the CPU is allowed to use higher boost frequencies.",

                "cpu-idle" =>
                    "Changes CPU idle-state behavior, which controls how deeply processor cores can enter low-power idle states.",

                "nvidia-power" =>
                    "Changes the NVIDIA driver power-management preference so the GPU can stay at higher performance states more consistently.",

                "nvidia-reflex" =>
                    "Enables NVIDIA Reflex where supported, coordinating CPU and GPU work to reduce render queueing and input-to-display latency in supported games.",

                "nvidia-vrr" =>
                    "Configures variable refresh behavior for supported NVIDIA displays so refresh timing can follow the rendered frame rate.",

                "radeon-power" =>
                    "Changes AMD Radeon power behavior to favor performance instead of allowing the GPU to reduce clocks as aggressively.",

                "radeon-anti-lag" =>
                    "Enables AMD Radeon Anti-Lag where supported to reduce the amount of CPU work queued ahead of the GPU.",

                "radeon-chill" =>
                    "Configures AMD Radeon Chill, which dynamically limits frame rate based on movement and activity to reduce power and heat.",

                "hags" =>
                    "Changes Hardware-Accelerated GPU Scheduling, allowing supported Windows GPU scheduling work to be handled differently between the operating system and GPU driver.",

                "shader-cache" or "cleanup-shader-cache" =>
                    "Removes or rebuilds graphics shader cache data so Windows and applications can regenerate cached shaders.",

                "directx-cache" =>
                    "Cleans DirectX-related cached graphics data that applications can regenerate.",

                _ when tweak.Title.Contains("Network", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("TCP", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("NIC", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("DNS", StringComparison.OrdinalIgnoreCase) =>
                    "Changes a Windows networking or adapter setting intended to alter packet handling, connection behavior, or network power/performance characteristics.",

                _ when tweak.Title.Contains("Audio", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("Sound", StringComparison.OrdinalIgnoreCase) =>
                    "Changes Windows audio processing or device behavior to prioritize a particular latency, quality, or processing path.",

                _ when tweak.Title.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("Discord", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("Windows Debloat", StringComparison.OrdinalIgnoreCase) =>
                    "Disables, removes, or reduces selected background features or components to reduce unnecessary background activity.",

                _ when tweak.Title.Contains("BCDEdit", StringComparison.OrdinalIgnoreCase) ||
                       GetTweakCategory(tweak.Id) == OptimizationCategory.Boot =>
                    "Changes a Windows boot configuration setting that can affect how Windows initializes or schedules system components.",

                _ =>
                    tweak.Description
            };
        }

        private static OptimizationCategory GetTweakCategory(string tweakId)
        {
            OptimizationTweakDefinition? definition =
                OptimizationLibrary.Groups
                    .SelectMany(group => group.Tweaks)
                    .FirstOrDefault(tweak =>
                        string.Equals(tweak.Id, tweakId, StringComparison.OrdinalIgnoreCase));

            return definition == null
                ? OptimizationCategory.Background
                : OptimizationLibrary.Groups
                    .First(group => group.Tweaks.Contains(definition))
                    .Category;
        }

        private static string GetAutoTunePossibleDownside(
            OptimizationTweakDefinition tweak)
        {
            return tweak.Id switch
            {
                "high-performance" =>
                    "Higher idle power use, more heat, more fan activity, and potentially higher electricity use. On laptops it can reduce battery life.",

                "windows-game-mode" or "gaming-game-mode" =>
                    "Usually low risk, but the benefit varies by game and Windows version. In unusual workloads it can change background scheduling behavior without improving performance.",

                "sleep" =>
                    "The PC can stay awake longer than expected and consume more power if the workload is left running. This is not intended as a permanent power-saving change.",

                "display-timeout" =>
                    "The display can remain on longer than expected, increasing display power use and potentially causing unnecessary screen-on time.",

                "processor-min" =>
                    "Higher minimum performance can increase idle power, temperature, fan noise, and battery drain. It is not automatically faster in every workload.",

                "processor-boost" =>
                    "Aggressive boost behavior can increase temperature, power consumption, and fan noise. Some CPUs or workloads may become less efficient or less stable if other tuning is involved.",

                "cpu-idle" =>
                    "Reducing CPU idle behavior can increase idle temperature and power use. It can also interfere with normal power-management behavior and is not universally lower latency.",

                "nvidia-power" or "radeon-power" =>
                    "Higher GPU performance states can increase power draw, heat, fan speed, and potentially noise. The extra performance may be small when the workload is already GPU-bound.",

                "nvidia-reflex" or "radeon-anti-lag" =>
                    "The effect depends on the game, driver, frame rate, and CPU/GPU balance. It can change frame pacing or performance behavior rather than guaranteeing lower latency.",

                "nvidia-vrr" =>
                    "Requires compatible display/driver settings. Incorrect VRR configuration can cause flicker, frame-pacing differences, or unexpected synchronization behavior.",

                "radeon-chill" =>
                    "Can reduce maximum FPS during movement or activity. That may be undesirable for competitive gaming where an unrestricted frame rate is preferred.",

                "hags" =>
                    "Performance and latency can vary by GPU, driver, Windows build, and workload. Some systems may see no benefit or worse frame-time behavior.",

                "shader-cache" or "cleanup-shader-cache" or "directx-cache" =>
                    "The next launch can temporarily stutter while shaders are compiled again. Repeatedly clearing caches can make performance worse rather than better.",

                _ when tweak.Title.Contains("Network", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("TCP", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("NIC", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("DNS", StringComparison.OrdinalIgnoreCase) =>
                    "Networking changes are adapter-, driver-, ISP-, and workload-dependent. An aggressive setting can reduce throughput, increase CPU usage, increase power use, or make connectivity less reliable.",

                _ when tweak.Title.Contains("Audio", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("Sound", StringComparison.OrdinalIgnoreCase) =>
                    "Some audio changes can cause compatibility problems, missing enhancements, higher CPU use, or worse sound quality on particular drivers/devices.",

                _ when tweak.Title.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("Discord", StringComparison.OrdinalIgnoreCase) ||
                       tweak.Title.Contains("Windows Debloat", StringComparison.OrdinalIgnoreCase) =>
                    "Removing or disabling a component can break a feature you use. Background apps may also restore settings after updates.",

                _ when tweak.Title.Contains("BCDEdit", StringComparison.OrdinalIgnoreCase) ||
                       GetTweakCategory(tweak.Id) == OptimizationCategory.Boot =>
                    "Boot configuration changes can affect Windows startup, recovery, drivers, and system behavior. Incorrect settings can make Windows harder to boot or troubleshoot.",

                _ =>
                    "The result depends on the hardware, drivers, Windows build, and workload. A setting that helps one PC can do nothing—or make another PC less stable or less efficient."
            };
        }

        private void AutoTuneTweakToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggle ||
                toggle.Tag is not string tweakId)
            {
                return;
            }

            if (toggle.IsOn)
            {
                if (!_autoTuneSelectedTweaks.Contains(tweakId))
                    _autoTuneSelectedTweaks.Add(tweakId);
            }
            else
            {
                _autoTuneSelectedTweaks.Remove(tweakId);
            }

            AutoTuneApplyButton.IsEnabled =
                _autoTuneSelectedTweaks.Count > 0;
        }

        private void HideAllPages()
        {
            AutoTunePage.Visibility =
                Visibility.Collapsed;

            DashboardPage.Visibility =
                Visibility.Collapsed;

            SystemPage.Visibility =
                Visibility.Collapsed;

            PerformancePage.Visibility =
                Visibility.Collapsed;

            HistoryPage.Visibility =
                Visibility.Collapsed;

            OptimizePage.Visibility =
                Visibility.Collapsed;

            SettingsPage.Visibility =
                Visibility.Collapsed;
        }

        private void ShowAutoTune()
        {
            HideAllPages();
            AutoTunePage.Visibility = Visibility.Visible;
            PageTitleText.Text = "AutoTune";
        }

        private void ShowDashboard()
        {
            HideAllPages();

            DashboardPage.Visibility =
                Visibility.Visible;

            PageTitleText.Text =
                "Dashboard";
        }

        private void ShowSystem()
        {
            HideAllPages();

            SystemPage.Visibility =
                Visibility.Visible;

            PageTitleText.Text =
                "System";
        }

        private void ShowPerformance()
        {
            HideAllPages();

            PerformancePage.Visibility =
                Visibility.Visible;

            PageTitleText.Text =
                "Performance";
        }

        private void ShowOptimize()
        {
            HideAllPages();

            OptimizePage.Visibility =
                Visibility.Visible;

            PageTitleText.Text =
                "Optimize";
        }

        private void ShowSettings()
        {
            HideAllPages();
            SettingsPage.Visibility = Visibility.Visible;
            PageTitleText.Text = "Settings";
            LoadSettings();
        }

        private static ApplicationDataContainer GetSettingsStore()
        {
            return ApplicationData.Current.LocalSettings;
        }

        private static Color GetAccentColor()
        {
            string name =
                GetSettingsStore().Values["AccentColor"] as string ??
                "Lime";

            return name switch
            {
                "Blue" => Color.FromArgb(255, 90, 170, 255),
                "Purple" => Color.FromArgb(255, 180, 120, 255),
                "Orange" => Color.FromArgb(255, 255, 165, 70),
                "Red" => Color.FromArgb(255, 255, 105, 105),
                "White" => Color.FromArgb(255, 245, 245, 248),
                _ => Color.FromArgb(255, 124, 255, 138)
            };
        }

        private void LoadSettings()
        {
            var store = GetSettingsStore();
            string accent =
                store.Values["AccentColor"] as string ?? "Lime";

            AccentColorComboBox.SelectedIndex =
                accent switch
                {
                    "Blue" => 1,
                    "Purple" => 2,
                    "Orange" => 3,
                    "Red" => 4,
                    "White" => 5,
                    _ => 0
                };

            ConfirmOptimizationToggle.IsOn =
                GetBoolSetting(store, "ConfirmOptimization", true);
            AutoSnapshotToggle.IsOn =
                GetBoolSetting(store, "AutoSnapshot", true);
            AnimationsToggle.IsOn =
                GetBoolSetting(store, "Animations", true);
            RememberPageToggle.IsOn =
                GetBoolSetting(store, "RememberPage", false);

            string textStyle =
                store.Values["TextStyle"] as string ?? "Clean";
            TextStyleComboBox.SelectedIndex =
                textStyle switch
                {
                    "Bold" => 1,
                    "Minimal" => 2,
                    _ => 0
                };

            ApplyAccentColor();
        }

        private static bool GetBoolSetting(
            ApplicationDataContainer store,
            string key,
            bool defaultValue)
        {
            return store.Values.TryGetValue(key, out object? value) &&
                   value is bool setting
                ? setting
                : defaultValue;
        }

        private void ApplyAccentColor()
        {
            if (Content is FrameworkElement root &&
                root.Resources["BuildCoreAccentBrush"]
                is Microsoft.UI.Xaml.Media.SolidColorBrush brush)
            {
                brush.Color = GetAccentColor();
            }
        }

        private void TextStyleComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (TextStyleComboBox.SelectedItem is not ComboBoxItem item)
                return;

            string name = item.Content?.ToString() ?? "Clean";
            GetSettingsStore().Values["TextStyle"] = name;
        }

        private void AccentColorComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (AccentColorComboBox.SelectedItem is not ComboBoxItem item)
                return;

            string name = item.Content?.ToString() ?? "Lime";
            GetSettingsStore().Values["AccentColor"] = name;
            ApplyAccentColor();
        }

        private void ResetSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var store = GetSettingsStore();
            store.Values.Clear();

            ConfirmOptimizationToggle.IsOn = true;
            AutoSnapshotToggle.IsOn = true;
            AnimationsToggle.IsOn = true;
            RememberPageToggle.IsOn = false;
            AccentColorComboBox.SelectedIndex = 0;
            TextStyleComboBox.SelectedIndex = 0;

            ApplyAccentColor();
        }

        // ============================================================
        // OPTIMIZATION CATEGORY FILTER
        // ============================================================

        private void OptimizationCategoryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not string category)
            {
                return;
            }

            _selectedOptimizationCategory = category;

            OptimizationCategoryText.Text =
                category.Equals(
                    "All",
                    StringComparison.OrdinalIgnoreCase)
                    ? "ALL OPTIMIZATIONS"
                    : category.ToUpperInvariant();

            ApplyOptimizationCategoryFilter();
        }

        private readonly HashSet<string> _selectedLibraryTweaks =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly List<ToggleSwitch> _libraryToggleControls =
            new List<ToggleSwitch>();

        private void LoadOptimizationLibrary()
        {
            OptimizationLibraryPanel.Children.Clear();

            foreach (OptimizationGroupDefinition group in
                OptimizationLibrary.Groups)
            {
                OptimizationLibraryPanel.Children.Add(
                    CreateOptimizationGroupCard(group));
            }

            OptimizationLibraryCountText.Text =
                $"{OptimizationLibrary.Groups.Count} GROUPS";

            ApplyOptimizationCategoryFilter();
        }

        private Border CreateOptimizationGroupCard(
            OptimizationGroupDefinition group)
        {
            var border = new Border
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Transparent),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.DimGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 10),
                Tag = group.Category
            };

            var panel = new StackPanel();

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(130) });

            var titlePanel = new StackPanel();

            titlePanel.Children.Add(new TextBlock
            {
                Text = group.Title,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.White),
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            titlePanel.Children.Add(new TextBlock
            {
                Text = group.Description,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            });

            Grid.SetColumn(titlePanel, 0);
            header.Children.Add(titlePanel);

            header.Children.Add(new TextBlock
            {
                Text = $"{group.Tweaks.Count} TWEAKS",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            });

            Grid.SetColumn(header.Children[^1], 1);
            panel.Children.Add(header);

            foreach (OptimizationTweakDefinition tweak in group.Tweaks)
            {
                var row = new Grid
                {
                    Margin = new Thickness(0, 14, 0, 0)
                };

                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(150) });

                var textPanel = new StackPanel();

                var titleRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal
                };

                titleRow.Children.Add(new TextBlock
                {
                    Text = tweak.Title,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LightGray),
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var helpButton = new Button
                {
                    Content = "?",
                    Width = 24,
                    Height = 24,
                    MinWidth = 24,
                    Padding = new Thickness(0),
                    Margin = new Thickness(7, -2, 0, 0),
                    FontSize = 11,
                    Tag = tweak
                };

                helpButton.Click += OptimizationTweakHelpButton_Click;
                titleRow.Children.Add(helpButton);
                textPanel.Children.Add(titleRow);

                textPanel.Children.Add(new TextBlock
                {
                    Text = tweak.Description,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray),
                    FontSize = 9,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0)
                });

                Grid.SetColumn(textPanel, 0);
                row.Children.Add(textPanel);

                var toggle = new ToggleSwitch
                {
                    IsOn = _selectedLibraryTweaks.Contains(tweak.Id),
                    OnContent = "ON",
                    OffContent = "OFF",
                    OnContentTemplate = null,
                    OffContentTemplate = null,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 125,
                    MinWidth = 125,
                    Tag = new OptimizationLibraryToggleContext
                    {
                        Tweak = tweak
                    }
                };

                toggle.Toggled += OptimizationLibraryToggle_Toggled;

                Grid.SetColumn(toggle, 1);
                row.Children.Add(toggle);
                panel.Children.Add(row);
            }

            var footer = new TextBlock
            {
                Text = "SELECTION ONLY • NO SYSTEM CHANGE",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                FontSize = 8,
                Margin = new Thickness(0, 14, 0, 0)
            };

            panel.Children.Add(footer);
            border.Child = panel;
            return border;
        }

        private async void OptimizationTweakHelpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not OptimizationTweakDefinition tweak)
            {
                return;
            }

            SystemInfo systemInfo;

            try
            {
                systemInfo = SystemInfoService.GetSystemInfo();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"BUILDCORE TWEAK HARDWARE INFO ERROR: {ex}");

                systemInfo = new SystemInfo();
            }

            string cpu = systemInfo.Cpu;
            string gpu = systemInfo.Gpu;

            bool cpuRelevant =
                IsCpuGuidanceRelevant(tweak, cpu);

            bool gpuRelevant =
                IsGpuGuidanceRelevant(tweak, gpu);

            var content = new StackPanel
            {
                Spacing = 10
            };

            content.Children.Add(new TextBlock
            {
                Text = "HARDWARE IMPACT",
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            content.Children.Add(new TextBlock
            {
                Text = tweak.HardwareEffect,
                TextWrapping = TextWrapping.Wrap
            });

            content.Children.Add(new TextBlock
            {
                Text = $"CPU • {(cpuRelevant ? "GOOD FIT" : "REVIEW")}\n{tweak.CpuGuidance}",
                TextWrapping = TextWrapping.Wrap
            });

            content.Children.Add(new TextBlock
            {
                Text = $"GPU • {(gpuRelevant ? "GOOD FIT" : "REVIEW")}\n{tweak.GpuGuidance}",
                TextWrapping = TextWrapping.Wrap
            });

            content.Children.Add(new TextBlock
            {
                Text = $"Detected CPU: {cpu}\nDetected GPU: {gpu}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            });

            content.Children.Add(new TextBlock
            {
                Text =
                    $"Risk: {tweak.Risk}   •   " +
                    $"Reboot: {(tweak.RequiresReboot ? "YES" : "NO")}   •   " +
                    $"Rollback: {(tweak.RollbackSupported ? "SUPPORTED" : "NOT SUPPORTED")}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            });

            var dialog = new ContentDialog
            {
                Title = tweak.Title,
                Content = content,
                CloseButtonText = "CLOSE",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private static bool IsCpuGuidanceRelevant(
            OptimizationTweakDefinition tweak,
            string cpu)
        {
            if (string.IsNullOrWhiteSpace(cpu) ||
                cpu.Equals("Unknown CPU", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (tweak.Id is "processor-min" or "processor-boost" or "cpu-idle")
                return cpu.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                       cpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                       cpu.Contains("Ryzen", StringComparison.OrdinalIgnoreCase) ||
                       cpu.Contains("Core", StringComparison.OrdinalIgnoreCase);

            return true;
        }

        private static bool IsGpuGuidanceRelevant(
            OptimizationTweakDefinition tweak,
            string gpu)
        {
            if (string.IsNullOrWhiteSpace(gpu) ||
                gpu.Equals("Unknown GPU", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (tweak.Id is "nvidia-power" or "nvidia-reflex" or "nvidia-vrr" or "low-latency")
                return gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                       gpu.Contains("GeForce", StringComparison.OrdinalIgnoreCase);

            if (tweak.Id is "radeon-power" or "radeon-anti-lag" or "radeon-chill")
                return gpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                       gpu.Contains("Radeon", StringComparison.OrdinalIgnoreCase);

            return true;
        }

        private sealed class OptimizationLibraryToggleContext
        {
            public OptimizationTweakDefinition Tweak { get; init; } =
                new OptimizationTweakDefinition();
        }

        private void OptimizationLibraryToggle_Toggled(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggle ||
                toggle.Tag is not OptimizationLibraryToggleContext context)
            {
                return;
            }

            if (toggle.IsOn)
            {
                _selectedLibraryTweaks.Add(context.Tweak.Id);
            }
            else
            {
                _selectedLibraryTweaks.Remove(context.Tweak.Id);
            }

            RefreshLibrarySelectionUi();
        }

        private void ApplyOptimizationCategoryFilter()
        {
            int visibleRecommendations = 0;
            int visibleLibraryGroups = 0;

            foreach (UIElement child in RecommendationsPanel.Children)
            {
                if (child is not FrameworkElement element ||
                    element.Tag is not OptimizationCategory)
                {
                    continue;
                }

                bool visible =
                    _selectedOptimizationCategory.Equals(
                        "All",
                        StringComparison.OrdinalIgnoreCase) ||
                    element.Tag.ToString()!.Equals(
                        _selectedOptimizationCategory,
                        StringComparison.OrdinalIgnoreCase);

                element.Visibility =
                    visible
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                if (visible)
                    visibleRecommendations++;
            }

            foreach (UIElement child in OptimizationLibraryPanel.Children)
            {
                if (child is not FrameworkElement element ||
                    element.Tag is not OptimizationCategory)
                {
                    continue;
                }

                bool visible =
                    _selectedOptimizationCategory.Equals(
                        "All",
                        StringComparison.OrdinalIgnoreCase) ||
                    element.Tag.ToString()!.Equals(
                        _selectedOptimizationCategory,
                        StringComparison.OrdinalIgnoreCase);

                element.Visibility =
                    visible
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                if (visible)
                    visibleLibraryGroups++;
            }

            int totalVisible =
                visibleLibraryGroups + visibleRecommendations;

            OptimizationCategoryCountText.Text =
                $"{totalVisible} AVAILABLE";
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
            ShowSystem();
        }

        private void PerformanceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowPerformance();
        }

        private void OptimizeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowOptimize();
        }

        private void ConfirmOptimizationToggle_Toggled(object sender, RoutedEventArgs e)
        {
            GetSettingsStore().Values["ConfirmOptimization"] =
                ConfirmOptimizationToggle.IsOn;
        }

        private void AutoSnapshotToggle_Toggled(object sender, RoutedEventArgs e)
        {
            GetSettingsStore().Values["AutoSnapshot"] =
                AutoSnapshotToggle.IsOn;
        }

        private void AnimationsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            GetSettingsStore().Values["Animations"] =
                AnimationsToggle.IsOn;
        }

        private void RememberPageToggle_Toggled(object sender, RoutedEventArgs e)
        {
            GetSettingsStore().Values["RememberPage"] =
                RememberPageToggle.IsOn;
        }

        private void SettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettings();
        }
    }
}