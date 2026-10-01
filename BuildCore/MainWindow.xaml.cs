using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private void FinalizeCompletedRecoveryExperiments()
        {
            try
            {
                foreach (RebootOptimizationExperimentState experiment in
                    RebootOptimizationExperimentStorageService.GetExperiments())
                {
                    RebootOptimizationExperimentRecoveryService
                        .FinalizeRecoveryAfterReboot(experiment);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"BUILDCORE RECOVERY FINALIZATION ERROR: {ex}");
            }
        }

        private async Task DetectPendingRebootExperimentAsync()
        {            FinalizeCompletedRecoveryExperiments();


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
                            Microsoft.UI.Colors.LightGreen);

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

            root.Children.Add(CreateRebootDetailsSection(
                "RECOVERY / VALIDATION",
                $"Snapshot: {experiment.SnapshotId}\n" +
                $"Reboot detected: {(experiment.RebootDetected ? "YES" : "NO")}\n" +
                $"After-reboot validation: {(experiment.AfterRebootValidationPassed ? "PASSED" : "NOT PASSED")}\n" +
                $"Expected process: {experiment.ExpectedTargetProcessPath}\n" +
                $"Recovery phase: {experiment.RecoveryPhaseDisplayName}\n" +
                $"Recovery: {experiment.RecoveryStatus}\n" +
                $"Recovery attempted: {(experiment.RecoveryAttemptedAtUtc.HasValue ? experiment.RecoveryAttemptedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "NO")}\n" +
                $"Recovery finalized: {(experiment.RecoveryFinalized ? "YES" : "NO")}"));

            bool recoveryRequiresReboot =
                experiment.RecoveryRequiresReboot &&
                experiment.RecoverySucceeded &&
                !experiment.RecoveryFinalized;

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
                await ShowHistoryDialog();
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

            var confirmation = new ContentDialog
            {
                Title = "Delete Workload Benchmark?",
                Content = new TextBlock
                {
                    Text =
                        $"This will permanently delete the saved benchmark " +
                        $"'{result.WorkloadName}'.",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "DELETE",
                CloseButtonText = "CANCEL",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = ((FrameworkElement)this.Content).XamlRoot
            };

            ContentDialogResult choice =
                await confirmation.ShowAsync();

            if (choice != ContentDialogResult.Primary)
                return;

            if (WorkloadBenchmarkStorageService.DeleteResult(
                result.ResultId))
            {
                button.Content = "DELETED ✓";
                button.IsEnabled = false;
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