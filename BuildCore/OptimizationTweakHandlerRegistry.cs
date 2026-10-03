using System;
using System.Collections.Generic;
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
                Review("visual-effects", "windows-visual-effects-v1", "Review-only until the Windows visual-effects policy is wired to a reversible state capture.", false),
                Review("background-apps", "windows-background-apps-v1", "Review-only until supported per-user background-app controls are wired.", false),
                Review("delivery-optimization", "windows-delivery-optimization-v1", "Review-only until Delivery Optimization policy state capture is implemented.", false),

                Review("registry-gaming", "registry-gaming-review-v1", "Review-only scheduler analysis; no undocumented registry values are applied.", false),
                Review("registry-mouse", "registry-mouse-review-v1", "Review-only pointer settings analysis.", false),
                Review("registry-ui", "registry-ui-review-v1", "Review-only Explorer/UI settings analysis.", false),

                Review("explorer-extensions", "explorer-extensions-review-v1", "Review-only shell extension inventory.", false),
                Review("explorer-animations", "explorer-animations-v1", "Review-only until shell animation state capture is implemented.", false),
                Review("explorer-recent", "explorer-recent-review-v1", "Review-only recent-item settings analysis.", false),

                Review("network-power", "network-power-v1", "Review-only until adapter-specific power state capture and rollback are implemented.", true),
                Review("network-rss", "network-rss-v1", "Review-only RSS analysis for supported adapters.", true),
                Review("network-offloads", "network-offloads-v1", "Review-only network offload analysis.", false),
                Review("network-dns", "network-dns-v1", "Review-only until the user-selected DNS target is explicitly captured.", false),

                Review("cpu-idle", "cpu-idle-v1", "Review-only processor idle policy analysis.", false),
                Review("usb-selective", "usb-selective-v1", "Review-only USB selective suspend analysis.", false),
                Review("pcie-link", "pcie-link-v1", "Review-only PCIe link-state power analysis.", false),

                Review("custom-plan", "custom-buildcore-plan-v1", "Review-only until BuildCore-managed plan creation and lifecycle cleanup are implemented.", false),
                Review("processor-min", "processor-min-v1", "Review-only processor minimum-state analysis.", false),
                Review("processor-boost", "processor-boost-v1", "Review-only processor boost-policy analysis.", false),

                Real("high-performance", "high-performance-power-plan-v2", "Verified High Performance power-plan handler.", CreateHighPerformanceRecommendation),
                Real("sleep", "workload-sleep-v1", "Disables AC sleep timeout for an active workload and verifies the power policy.", CreatePreventSleepRecommendation),
                Real("display-timeout", "workload-display-timeout-v1", "Disables AC display timeout for an active workload and verifies the power policy.", CreateDisplayTimeoutRecommendation),

                Real("gaming-game-mode", "windows-game-mode-v2", "Verified Windows Game Mode handler shared with the Windows Game Mode tweak.", CreateGameModeRecommendation),
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
                Review("shader-cache", "shader-cache-cleanup-v1", "Review-only stale-cache discovery; active caches are never blindly deleted.", false),
                Review("update-cache", "update-cache-review-v1", "Review-only Windows Update cache analysis.", false),

                Review("dx-cache", "directx-cache-v1", "Review-only DirectX shader-cache maintenance.", false),
                Review("gpu-scheduling", "gpu-scheduling-review-v1", "Review-only GPU scheduling analysis.", false),
                Review("presentation", "presentation-review-v1", "Review-only presentation-mode analysis.", false),

                Review("chrome-startup", "chrome-startup-v1", "Review-only Chrome startup discovery.", false),
                Review("chrome-background", "chrome-background-v1", "Review-only Chrome background execution discovery.", false),
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
                Review("game-bar", "game-bar-review-v1", "Review-only Game Bar capture analysis.", false),

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
}