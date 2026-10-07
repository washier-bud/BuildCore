using System;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Linq;

namespace BuildCore
{
    public sealed class OptimizationTweakHandler
    {
        public string TweakId { get; }
        public string HandlerId { get; }
        public string Description { get; }
        public bool RequiresReboot { get; }
        public bool RollbackSupported { get; }
        public bool CanApply { get; }

        private readonly Func<OptimizationRecommendation?> _createRecommendation;

        public OptimizationTweakHandler(
            string tweakId,
            string handlerId,
            string description,
            bool requiresReboot,
            bool rollbackSupported,
            bool canApply,
            Func<OptimizationRecommendation?> createRecommendation)
        {
            TweakId = tweakId;
            HandlerId = handlerId;
            Description = description;
            RequiresReboot = requiresReboot;
            RollbackSupported = rollbackSupported;
            CanApply = canApply;
            _createRecommendation = createRecommendation;
        }

        public OptimizationRecommendation? CreateRecommendation() =>
            _createRecommendation();
    }

    public static class OptimizationTweakHandlerRegistry
    {
        private static readonly IReadOnlyList<OptimizationTweakHandler> Handlers =
            new List<OptimizationTweakHandler>
            {
                Real("windows-game-mode", "windows-game-mode-v2", "Verified Windows Game Mode handler.", CreateGameModeRecommendation),
                Real("visual-effects", "windows-visual-effects-v2", "Disables Windows visual effects through a reversible per-user setting.", () => RegistryOptimizationHandler.CreateRecommendation("visual-effects")),
                Real("background-apps", "windows-background-apps-v2", "Disables Windows per-user background app execution with reversible state capture.", () => RegistryOptimizationHandler.CreateRecommendation("background-apps")),
                Review("delivery-optimization", "windows-delivery-optimization-v1", "Review-only until Delivery Optimization policy state capture is implemented.", false),

                Review("registry-gaming", "registry-gaming-review-v1", "Review-only scheduler analysis; no undocumented registry values are applied.", false),
                Review("registry-mouse", "registry-mouse-review-v1", "Review-only pointer settings analysis.", false),
                Real("registry-ui", "registry-ui-v2", "Sets the current user menu-show delay to zero with reversible state capture.", () => RegistryOptimizationHandler.CreateRecommendation("registry-ui")),

                Review("explorer-extensions", "explorer-extensions-review-v1", "Review-only shell extension inventory.", false),
                Real("explorer-animations", "explorer-animations-v2", "Disables the Windows minimize/maximize animation setting with reversible state capture.", () => RegistryOptimizationHandler.CreateRecommendation("explorer-animations")),
                Real("explorer-recent", "explorer-recent-v2", "Disables recent-document tracking for the current user with reversible state capture.", () => RegistryOptimizationHandler.CreateRecommendation("explorer-recent")),

                Review("network-power", "network-power-v1", "Review-only until adapter-specific power state capture and rollback are implemented.", true),
                Review("network-rss", "network-rss-v1", "Review-only RSS analysis for supported adapters.", true),
                Review("network-offloads", "network-offloads-v1", "Review-only network offload analysis.", false),
                Review("network-dns", "network-dns-v1", "Review-only until the user-selected DNS target is explicitly captured.", false),

                Real("cpu-idle", "cpu-idle-v2", "Sets the active plan AC processor minimum state to 100% with exact rollback.", () => CreatePowerPercentageRecommendation("cpu-idle", "processor-min-ac", 100)),
                Real("usb-selective", "usb-selective-v2", "Disables AC USB selective suspend with exact rollback.", () => CreatePowerFlagRecommendation("usb-selective", "usb-selective-ac", false)),
                Real("pcie-link", "pcie-link-v2", "Disables AC PCIe link-state power management with exact rollback.", () => CreatePowerFlagRecommendation("pcie-link", "pcie-link-ac", false)),

                Review("custom-plan", "custom-buildcore-plan-v1", "Review-only until BuildCore-managed plan creation and lifecycle cleanup are implemented.", false),
                Review("processor-min", "processor-min-v1", "Review-only processor minimum-state analysis.", false),
                Real("processor-boost", "processor-boost-v2", "Sets the active plan AC processor boost policy to the Windows aggressive mode value and records the original setting.", () => CreatePowerIntegerRecommendation("processor-boost", "processor-boost-ac", 2)),

                Real("high-performance", "high-performance-power-plan-v2", "Verified High Performance power-plan handler.", CreateHighPerformanceRecommendation),
                Real("sleep", "workload-sleep-v1", "Disables AC sleep timeout for an active workload and verifies the power policy.", CreatePreventSleepRecommendation),
                Real("display-timeout", "workload-display-timeout-v1", "Disables AC display timeout for an active workload and verifies the power policy.", CreateDisplayTimeoutRecommendation),

                Real("gaming-game-mode", "gaming-game-mode-v2", "Verified Windows Game Mode handler shared with the Windows Game Mode implementation.", CreateGameModeRecommendation),
                Review("fullscreen", "fullscreen-optimization-review-v1", "Review-only fullscreen optimization analysis.", false),
                Review("priority", "game-process-priority-v1", "Review-only until an explicitly selected game process and reversible priority policy are supplied.", false),

                Review("hags", "hags-experimental-v1", "Experimental reboot-aware HAGS workflow; apply remains disabled in the immediate handler path.", true),
                Review("timer-resolution", "timer-resolution-review-v1", "Review-only timer behavior measurement.", false),
                Review("fullscreen-latency", "fullscreen-latency-review-v1", "Review-only presentation-mode latency analysis.", false),

                Review("driver-profile", "driver-profile-v1", "Review-only application profile discovery.", false),
                Review("low-latency", "driver-low-latency-v1", "Review-only driver low-latency discovery until vendor APIs are detected.", false),
                Review("shader-cache", "shader-cache-review-v1", "Review-only shader cache analysis.", false),

                Review("nvidia-power", "nvidia-power-v1", "Review-only NVIDIA application power-policy discovery until a supported driver control API is detected.", false),
                Review("nvidia-reflex", "nvidia-reflex-v1", "Review-only NVIDIA Reflex configuration discovery.", false),
                Review("nvidia-vrr", "nvidia-vrr-v1", "Review-only NVIDIA VRR/G-SYNC display discovery.", false),

                Review("radeon-power", "radeon-power-v1", "Review-only Radeon performance-policy discovery.", false),
                Review("radeon-anti-lag", "radeon-anti-lag-v1", "Review-only Radeon Anti-Lag discovery.", false),
                Review("radeon-chill", "radeon-chill-v1", "Review-only Radeon Chill discovery.", false),

                Review("temp-files", "temp-files-cleanup-v1", "Review-only until cleanup manifests and rollback-safe quarantine are implemented.", false),
                Review("cleanup-shader-cache", "shader-cache-cleanup-v1", "Review-only stale-cache discovery; active caches are never blindly deleted.", false),
                Review("update-cache", "update-cache-review-v1", "Review-only Windows Update cache analysis.", false),

                Review("dx-cache", "directx-cache-v1", "Review-only DirectX shader-cache maintenance.", false),
                Review("gpu-scheduling", "gpu-scheduling-review-v1", "Review-only GPU scheduling analysis.", false),
                Review("presentation", "presentation-review-v1", "Review-only presentation-mode analysis.", false),

                Real("chrome-startup", "chrome-startup-v2", "Disables Chrome Startup Boost through its per-user setting with reversible state capture.", () => RegistryOptimizationHandler.CreateRecommendation("chrome-startup")),
                Real("chrome-background", "chrome-background-v2", "Disables Chrome background execution through its per-user setting when present.", () => RegistryOptimizationHandler.CreateRecommendation("chrome-background")),
                Review("chrome-extensions", "chrome-extensions-v1", "Review-only Chrome extension discovery.", false),

                Review("optional-apps", "windows-optional-apps-v1", "Review-only optional-component inventory.", false),
                Review("startup-items", "windows-startup-items-v1", "Review-only startup-item inventory.", false),
                Review("background-services", "windows-services-v1", "Review-only third-party service inventory.", false),

                Review("discord-startup", "discord-startup-v1", "Review-only Discord startup discovery.", false),
                Review("discord-overlay", "discord-overlay-v1", "Review-only Discord overlay discovery.", false),
                Review("discord-hardware", "discord-hardware-v1", "Review-only Discord hardware-acceleration discovery.", false),

                Review("system-timer", "system-timer-review-v1", "Review-only system timer measurement.", false),
                Review("dynamic-ticks", "dynamic-ticks-review-v1", "Review-only dynamic-tick experiment preparation.", true),
                Review("platform-clock", "platform-clock-review-v1", "Review-only platform-clock experiment preparation.", true),

                Review("audio-enhancements", "audio-enhancements-v1", "Review-only active audio-device enhancement discovery.", true),
                Review("exclusive-mode", "audio-exclusive-mode-v1", "Review-only exclusive-mode discovery.", false),
                Review("audio-power", "audio-power-v1", "Review-only audio-device power management discovery.", false),

                Review("alt-tab-mode", "alt-tab-mode-v1", "Review-only Alt-Tab configuration discovery.", true),
                Review("background-priority", "background-priority-v1", "Review-only background application behavior analysis.", false),
                Real("game-bar", "game-bar-v2", "Disables Game Bar capture through the current user's GameDVR setting.", () => RegistryOptimizationHandler.CreateRecommendation("game-bar")),

                Review("boot-timeout", "boot-timeout-v1", "Review-only boot menu timeout analysis.", false),
                Review("disabledynamictick", "bcdedit-disabledynamictick-v1", "Review-only BCDEdit experiment preparation.", true),
                Review("useplatformclock", "bcdedit-useplatformclock-v1", "Review-only BCDEdit experiment preparation.", true)
            };

        public static IReadOnlyList<OptimizationTweakHandler> GetHandlers() => Handlers;

        public static IReadOnlyList<string> ValidateRegistry()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var handlerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (OptimizationTweakHandler handler in Handlers)
            {
                if (handler == null)
                {
                    errors.Add("Registry contains a null handler.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(handler.TweakId))
                    errors.Add("Handler has an empty tweak ID.");

                if (string.IsNullOrWhiteSpace(handler.HandlerId))
                    errors.Add($"Handler for '{handler.TweakId}' has an empty handler ID.");

                if (string.IsNullOrWhiteSpace(handler.Description))
                    errors.Add($"Handler '{handler.HandlerId}' has an empty description.");

                if (!ids.Add(handler.TweakId))
                    errors.Add($"Duplicate tweak ID: '{handler.TweakId}'.");

                if (!handlerIds.Add(handler.HandlerId))
                    errors.Add($"Duplicate handler ID: '{handler.HandlerId}'.");
            }

            var definitions = OptimizationLibrary.Groups
                .SelectMany(group => group.Tweaks)
                .ToList();

            var definitionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (OptimizationTweakDefinition definition in definitions)
            {
                if (!definitionIds.Add(definition.Id))
                    errors.Add($"Duplicate library tweak ID: '{definition.Id}'.");
            }

            foreach (OptimizationTweakHandler handler in Handlers)
            {
                OptimizationTweakDefinition? definition = definitions.FirstOrDefault(
                    tweak => tweak.Id.Equals(handler.TweakId, StringComparison.OrdinalIgnoreCase));

                if (definition == null)
                {
                    errors.Add($"Handler '{handler.HandlerId}' references unknown tweak '{handler.TweakId}'.");
                    continue;
                }

                if (definition.RequiresReboot != handler.RequiresReboot)
                    errors.Add($"Handler '{handler.HandlerId}' reboot metadata does not match tweak '{handler.TweakId}'.");

                if (definition.RollbackSupported != handler.RollbackSupported)
                    errors.Add($"Handler '{handler.HandlerId}' rollback metadata does not match tweak '{handler.TweakId}'.");
            }

            foreach (OptimizationTweakDefinition definition in definitions)
            {
                if (!ids.Contains(definition.Id))
                    errors.Add($"No handler is registered for library tweak '{definition.Id}'.");
            }

            return errors;
        }

        public static OptimizationTweakHandler? Find(string tweakId)
        {
            if (string.IsNullOrWhiteSpace(tweakId) || ValidateRegistry().Count > 0)
                return null;

            return Handlers.FirstOrDefault(
                handler => handler.TweakId.Equals(tweakId, StringComparison.OrdinalIgnoreCase));
        }

        private static OptimizationTweakHandler Real(
            string tweakId,
            string handlerId,
            string description,
            Func<OptimizationRecommendation?> factory)
        {
            OptimizationTweakDefinition definition = Definition(tweakId);

            return new OptimizationTweakHandler(
                tweakId,
                handlerId,
                description,
                definition.RequiresReboot,
                definition.RollbackSupported,
                true,
                factory);
        }

        private static OptimizationTweakHandler Review(
            string tweakId,
            string handlerId,
            string description,
            bool requiresReboot)
        {
            OptimizationTweakDefinition definition = Definition(tweakId);

            return new OptimizationTweakHandler(
                tweakId,
                handlerId,
                description,
                requiresReboot,
                definition.RollbackSupported,
                false,
                () => CreateReviewRecommendation(definition));
        }

        private static OptimizationTweakDefinition Definition(string tweakId) =>
            OptimizationLibrary.Groups
                .SelectMany(group => group.Tweaks)
                .First(tweak => tweak.Id.Equals(tweakId, StringComparison.OrdinalIgnoreCase));

        private static OptimizationRecommendation CreateReviewRecommendation(
            OptimizationTweakDefinition definition)
        {
            OptimizationCategory category = OptimizationLibrary.Groups
                .First(group => group.Tweaks.Any(
                    tweak => tweak.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
                .Category;

            return new OptimizationRecommendation
            {
                Title = definition.Title,
                Category = category,
                CurrentValue = "Review required",
                RecommendedValue = "Review",
                Description = definition.Description,
                Reason = "This control is registered, but BuildCore will not make the change automatically until a verified state capture and rollback path exists.",
                Risk = definition.Risk,
                Impact = OptimizationImpact.Low,
                CanAnalyze = true,
                CanApply = false,
                CanTest = false,
                RequiresReboot = definition.RequiresReboot,
                RollbackSupported = definition.RollbackSupported,
                TestType = OptimizationTestType.None,
                TestDescription = "No automatic change is currently permitted."
            };
        }

        private static OptimizationRecommendation? CreateGameModeRecommendation()
        {
            WindowsSystemData system = WindowsSystemService.Scan();

            return OptimizationAnalyzer.Analyze(system)
                .FirstOrDefault(
                    recommendation => recommendation.Title.Equals(
                        "Enable Windows Game Mode",
                        StringComparison.OrdinalIgnoreCase));
        }

        private static OptimizationRecommendation? CreateHighPerformanceRecommendation()
        {
            WindowsSystemData system = WindowsSystemService.Scan();

            return OptimizationAnalyzer.Analyze(system)
                .FirstOrDefault(
                    recommendation => recommendation.Title.Equals(
                        "Performance Power Plan",
                        StringComparison.OrdinalIgnoreCase));
        }

        private static OptimizationRecommendation CreatePowerPercentageRecommendation(string tweakId, string settingAlias, uint target)
        {
            string current = PowerOptimizationHandler.GetAcPercentage(settingAlias);
            OptimizationTweakDefinition definition = Definition(tweakId);
            return new OptimizationRecommendation
            {
                Title = definition.Title, Category = OptimizationCategory.Power, CurrentValue = current, RecommendedValue = $"{target}%",
                Description = definition.Description,
                Reason = "BuildCore changes only the active power plan's AC setting, verifies the result, and records the original value for rollback.",
                Risk = definition.Risk, Impact = OptimizationImpact.Medium, CanAnalyze = true,
                CanApply = current != "Unknown" && !current.Equals($"{target}%", StringComparison.OrdinalIgnoreCase),
                CanTest = false, RequiresReboot = false, RollbackSupported = true, TestType = OptimizationTestType.None
            };
        }

        private static OptimizationRecommendation CreatePowerIntegerRecommendation(string tweakId, string settingAlias, uint target)
        {
            string current = PowerOptimizationHandler.GetAcInteger(settingAlias);
            OptimizationTweakDefinition definition = Definition(tweakId);
            return new OptimizationRecommendation
            {
                Title = definition.Title,
                Category = OptimizationCategory.Power,
                CurrentValue = current,
                RecommendedValue = target.ToString(),
                Description = definition.Description,
                Reason = "BuildCore changes the active AC processor policy, verifies it, and records the original value for rollback.",
                Risk = definition.Risk,
                Impact = OptimizationImpact.Medium,
                CanAnalyze = true,
                CanApply = current != "Unknown" && current != target.ToString(),
                CanTest = false,
                RequiresReboot = false,
                RollbackSupported = true,
                TestType = OptimizationTestType.None
            };
        }

        private static OptimizationRecommendation CreatePowerFlagRecommendation(string tweakId, string settingAlias, bool targetEnabled)
        {
            string current = PowerOptimizationHandler.GetAcFlag(settingAlias);
            OptimizationTweakDefinition definition = Definition(tweakId);
            string target = targetEnabled ? "Enabled" : "Disabled";
            return new OptimizationRecommendation
            {
                Title = definition.Title, Category = OptimizationCategory.Power, CurrentValue = current, RecommendedValue = target,
                Description = definition.Description,
                Reason = "BuildCore changes only the active power plan's AC setting, verifies the result, and records the original value for rollback.",
                Risk = definition.Risk, Impact = OptimizationImpact.Medium, CanAnalyze = true,
                CanApply = current != "Unknown" && !current.Equals(target, StringComparison.OrdinalIgnoreCase),
                CanTest = false, RequiresReboot = false, RollbackSupported = true, TestType = OptimizationTestType.None
            };
        }

        private static OptimizationRecommendation CreatePreventSleepRecommendation()
        {
            string current = PowerOptimizationHandler.GetAcTimeoutSeconds(
                "standby-timeout-ac");

            return new OptimizationRecommendation
            {
                Title = "Prevent sleep during workload",
                Category = OptimizationCategory.Power,
                CurrentValue = current,
                RecommendedValue = "0 seconds",
                Description = "Keep the system awake while an active workload is running.",
                Reason = "BuildCore changes only the active power plan's AC sleep timeout and records the original value for rollback.",
                Risk = OptimizationRisk.Low,
                Impact = OptimizationImpact.Low,
                CanApply = current != "Unknown" && current != "0 seconds",
                CanTest = false,
                RequiresReboot = false,
                RollbackSupported = true,
                TestType = OptimizationTestType.None
            };
        }

        private static OptimizationRecommendation CreateDisplayTimeoutRecommendation()
        {
            string current = PowerOptimizationHandler.GetAcTimeoutSeconds(
                "monitor-timeout-ac");

            return new OptimizationRecommendation
            {
                Title = "Workload display timeout",
                Category = OptimizationCategory.Power,
                CurrentValue = current,
                RecommendedValue = "0 seconds",
                Description = "Keep the display active while an active benchmark or workload is running.",
                Reason = "BuildCore changes only the active power plan's AC display timeout and records the original value for rollback.",
                Risk = OptimizationRisk.Low,
                Impact = OptimizationImpact.Low,
                CanApply = current != "Unknown" && current != "0 seconds",
                CanTest = false,
                RequiresReboot = false,
                RollbackSupported = true,
                TestType = OptimizationTestType.None
            };
        }
    }
    internal static class RegistryOptimizationHandler
    {
        private sealed class Definition
        {
            public string TweakId { get; init; } = "";
            public string Title { get; init; } = "";
            public string KeyPath { get; init; } = "";
            public string ValueName { get; init; } = "";
            public int TargetValue { get; init; }
            public bool RequiresReboot { get; init; }
            public OptimizationRisk Risk { get; init; }
        }

        private static readonly IReadOnlyDictionary<string, Definition> Definitions =
            new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase)
            {
                ["background-apps"] = new Definition
                {
                    TweakId = "background-apps", Title = "Limit background apps",
                    KeyPath = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                    ValueName = "GlobalUserDisabled", TargetValue = 1, RequiresReboot = false, Risk = OptimizationRisk.Low
                },
                ["registry-ui"] = new Definition
                {
                    TweakId = "registry-ui", Title = "Explorer/UI behavior",
                    KeyPath = @"Control Panel\Desktop",
                    ValueName = "MenuShowDelay", TargetValue = 0, RequiresReboot = false, Risk = OptimizationRisk.Low
                },
                ["explorer-recent"] = new Definition
                {
                    TweakId = "explorer-recent", Title = "Review recent-item activity",
                    KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    ValueName = "Start_TrackDocs", TargetValue = 0, RequiresReboot = false, Risk = OptimizationRisk.Low
                },
                ["chrome-startup"] = new Definition
                {
                    TweakId = "chrome-startup", Title = "Chrome startup behavior",
                    KeyPath = @"Software\Google\Chrome",
                    ValueName = "StartupBoostEnabled", TargetValue = 0, RequiresReboot = true, Risk = OptimizationRisk.Low
                },
                ["visual-effects"] = new Definition
                {
                    TweakId = "visual-effects",
                    Title = "Reduce visual effects",
                    KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    ValueName = "VisualFXSetting",
                    TargetValue = 2,
                    RequiresReboot = true,
                    Risk = OptimizationRisk.Low
                },
                ["explorer-animations"] = new Definition
                {
                    TweakId = "explorer-animations",
                    Title = "Reduce Explorer animations",
                    KeyPath = @"Control Panel\Desktop\WindowMetrics",
                    ValueName = "MinAnimate",
                    TargetValue = 0,
                    RequiresReboot = true,
                    Risk = OptimizationRisk.Low
                },
                ["game-bar"] = new Definition
                {
                    TweakId = "game-bar",
                    Title = "Game Bar capture review",
                    KeyPath = @"Software\Microsoft\Windows\CurrentVersion\GameDVR",
                    ValueName = "AppCaptureEnabled",
                    TargetValue = 0,
                    RequiresReboot = false,
                    Risk = OptimizationRisk.Low
                },
                ["chrome-background"] = new Definition
                {
                    TweakId = "chrome-background",
                    Title = "Chrome background apps",
                    KeyPath = @"Software\Google\Chrome",
                    ValueName = "BackgroundModeEnabled",
                    TargetValue = 0,
                    RequiresReboot = false,
                    Risk = OptimizationRisk.Low
                }
            };

        public static bool IsSupported(string optimizationTitle)
        {
            return Definitions.Values.Any(
                definition => definition.Title.Equals(
                    optimizationTitle,
                    StringComparison.OrdinalIgnoreCase));
        }

        public static OptimizationRecommendation CreateRecommendation(string tweakId)
        {
            if (!Definitions.TryGetValue(tweakId, out Definition? definition))
                throw new InvalidOperationException($"No registry optimization definition exists for '{tweakId}'.");

            string current = ReadValue(definition);
            OptimizationTweakDefinition tweak = OptimizationLibrary.Groups
                .SelectMany(group => group.Tweaks)
                .First(item => item.Id.Equals(tweakId, StringComparison.OrdinalIgnoreCase));
            OptimizationCategory category = OptimizationLibrary.Groups
                .First(group => group.Tweaks.Any(item => item.Id.Equals(tweakId, StringComparison.OrdinalIgnoreCase)))
                .Category;

            return new OptimizationRecommendation
            {
                Title = definition.Title,
                Category = category,
                CurrentValue = current,
                RecommendedValue = definition.TargetValue.ToString(),
                Description = tweak.Description,
                Reason = "BuildCore captures the existing per-user registry value, applies the supported target, verifies it, and can restore the captured value.",
                Risk = definition.Risk,
                Impact = OptimizationImpact.Low,
                CanAnalyze = true,
                CanApply = current != "Unknown",
                CanTest = false,
                RequiresReboot = definition.RequiresReboot,
                RollbackSupported = true,
                TestType = OptimizationTestType.None,
                TestDescription = definition.RequiresReboot
                    ? "Windows may require sign-out or restart before the visual change is fully reflected."
                    : "The setting is persistent in the current user's profile."
            };
        }

        public static OptimizationApplyResult Apply(OptimizationRecommendation recommendation)
        {
            Definition? definition = Definitions.Values.FirstOrDefault(
                item => item.Title.Equals(recommendation.Title, StringComparison.OrdinalIgnoreCase));

            if (definition == null)
                return Failure("No registry handler exists for this optimization.");

            if (!int.TryParse(recommendation.RecommendedValue, out int targetValue))
                return Failure("The registry target value is invalid.");

            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(definition.KeyPath)
                    ?? throw new InvalidOperationException("BuildCore could not open the per-user registry key.");

                key.SetValue(definition.ValueName, targetValue, RegistryValueKind.DWord);
                key.Flush();

                string current = ReadValue(definition);
                bool verified = current.Equals(targetValue.ToString(), StringComparison.Ordinal);

                return new OptimizationApplyResult
                {
                    Success = verified,
                    Verified = verified,
                    Message = verified
                        ? $"{definition.Title} applied and verified."
                        : $"{definition.Title} was written, but verification failed.",
                    Error = verified ? null : "Registry verification failed."
                };
            }
            catch (Exception ex)
            {
                return Failure($"{definition.Title} could not be applied: {ex.Message}");
            }
        }

        public static OptimizationRestoreResult Restore(OptimizationTransaction transaction)
        {
            Definition? definition = Definitions.Values.FirstOrDefault(
                item => item.Title.Equals(transaction.OptimizationTitle, StringComparison.OrdinalIgnoreCase));

            if (definition == null)
            {
                return new OptimizationRestoreResult
                {
                    Success = false,
                    Verified = false,
                    Message = "No registry restore handler exists for this optimization.",
                    Error = "Unknown registry optimization."
                };
            }

            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(definition.KeyPath)
                    ?? throw new InvalidOperationException("BuildCore could not open the per-user registry key.");

                if (transaction.BeforeValue.Equals("missing", StringComparison.OrdinalIgnoreCase))
                {
                    key.DeleteValue(definition.ValueName, false);
                }
                else if (int.TryParse(transaction.BeforeValue, out int previousValue))
                {
                    key.SetValue(definition.ValueName, previousValue, RegistryValueKind.DWord);
                }
                else
                {
                    return new OptimizationRestoreResult
                    {
                        Success = false,
                        Verified = false,
                        Message = "The original registry value could not be parsed.",
                        Error = "Invalid saved registry value."
                    };
                }

                key.Flush();
                string current = ReadValue(definition);
                bool verified = transaction.BeforeValue.Equals("missing", StringComparison.OrdinalIgnoreCase)
                    ? current.Equals("missing", StringComparison.OrdinalIgnoreCase)
                    : current.Equals(transaction.BeforeValue, StringComparison.Ordinal);

                return new OptimizationRestoreResult
                {
                    Success = verified,
                    Verified = verified,
                    Message = verified
                        ? $"{definition.Title} restored and verified."
                        : $"{definition.Title} restore was attempted, but verification failed.",
                    Error = verified ? null : "Registry restore verification failed."
                };
            }
            catch (Exception ex)
            {
                return new OptimizationRestoreResult
                {
                    Success = false,
                    Verified = false,
                    Message = $"{definition.Title} could not be restored: {ex.Message}",
                    Error = ex.Message
                };
            }
        }

        private static string ReadValue(Definition definition)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(definition.KeyPath);
                if (key == null)
                    return "missing";

                object? value = key.GetValue(definition.ValueName);
                if (value == null)
                    return "missing";

                return Convert.ToInt32(value).ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        private static OptimizationApplyResult Failure(string message) =>
            new OptimizationApplyResult
            {
                Success = false,
                Verified = false,
                Message = message,
                Error = message
            };
    }

}