using System;
using System.Collections.Generic;

namespace BuildCore
{
    public sealed class OptimizationTweakDefinition
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public bool DefaultEnabled { get; init; }
        public OptimizationRisk Risk { get; init; } = OptimizationRisk.Low;
        public bool RequiresReboot { get; init; }
        public bool RollbackSupported { get; init; } = true;
    }

    public sealed class OptimizationGroupDefinition
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public OptimizationCategory Category { get; init; }
        public IReadOnlyList<OptimizationTweakDefinition> Tweaks { get; init; } =
            Array.Empty<OptimizationTweakDefinition>();
    }

    public static class OptimizationLibrary
    {
        public static IReadOnlyList<OptimizationGroupDefinition> Groups { get; } =
            new List<OptimizationGroupDefinition>
            {
                Group("windows-settings", "Best Windows Settings", OptimizationCategory.Windows,
                    "Core Windows settings commonly reviewed for performance and gaming.",
                    T("windows-game-mode", "Windows Game Mode", "Enable Game Mode for supported games.", true, OptimizationRisk.Low),
                    T("visual-effects", "Reduce visual effects", "Reduce non-essential Windows visual effects.", true, OptimizationRisk.Low),
                    T("background-apps", "Limit background apps", "Review and limit unnecessary background activity.", true, OptimizationRisk.Low),
                    T("delivery-optimization", "Review Delivery Optimization", "Limit peer-to-peer update activity when it is not needed.", false, OptimizationRisk.Low)),

                Group("registry", "Best System Registry", OptimizationCategory.Registry,
                    "Targeted registry settings with explicit scope and reversible state.",
                    T("registry-gaming", "Gaming scheduler review", "Review supported scheduler-related settings instead of applying undocumented values.", false, OptimizationRisk.Medium),
                    T("registry-mouse", "Mouse response settings", "Review supported pointer-related Windows settings.", false, OptimizationRisk.Low),
                    T("registry-ui", "Explorer/UI behavior", "Review selected Explorer behaviors without deleting registry data.", false, OptimizationRisk.Low)),

                Group("explorer", "Unseen Explorer Tweaks", OptimizationCategory.Windows,
                    "Small Explorer changes intended to reduce unnecessary shell behavior.",
                    T("explorer-extensions", "Review shell extensions", "Identify unnecessary third-party Explorer extensions before disabling them.", false, OptimizationRisk.Low),
                    T("explorer-animations", "Reduce Explorer animations", "Reduce non-essential shell animation effects.", true, OptimizationRisk.Low),
                    T("explorer-recent", "Review recent-item activity", "Review recent-item collection and privacy settings.", false, OptimizationRisk.Low)),

                Group("network", "Best Network Settings", OptimizationCategory.Network,
                    "Network settings are presented for review before any adapter or TCP changes.",
                    T("network-power", "Disable adapter power saving", "Disable supported network-adapter power-saving behavior.", true, OptimizationRisk.Medium),
                    T("network-rss", "Review RSS", "Review Receive Side Scaling configuration for supported adapters.", true, OptimizationRisk.Low),
                    T("network-offloads", "Review network offloads", "Review offload features rather than blindly disabling them.", false, OptimizationRisk.Medium),
                    T("network-dns", "Use selected DNS", "Allow a user-selected DNS configuration.", false, OptimizationRisk.Low)),

                Group("power-saving", "Disable All Power Saving", OptimizationCategory.Power,
                    "Performance-focused power behavior with explicit, reversible controls.",
                    T("cpu-idle", "CPU idle policy", "Review processor idle behavior for sustained workloads.", false, OptimizationRisk.Medium),
                    T("usb-selective", "USB selective suspend", "Disable USB selective suspend when a workload requires uninterrupted device availability.", false, OptimizationRisk.Medium),
                    T("pcie-link", "PCIe link state power management", "Review PCIe link-state power management.", false, OptimizationRisk.Medium)),

                Group("custom-power", "Custom Power Plan", OptimizationCategory.Power,
                    "BuildCore-managed performance power-plan settings.",
                    T("custom-plan", "Use BuildCore performance plan", "Use a dedicated BuildCore-managed plan instead of modifying the user's existing plan.", true, OptimizationRisk.Low),
                    T("processor-min", "Processor minimum state", "Set a workload-specific minimum processor state when supported.", false, OptimizationRisk.Medium),
                    T("processor-boost", "Processor boost policy", "Review processor boost behavior for the selected workload.", false, OptimizationRisk.Medium)),

                Group("power-settings", "Best Power Settings", OptimizationCategory.Power,
                    "Review the highest-impact Windows power settings individually.",
                    T("high-performance", "High Performance plan", "Use Windows High Performance when appropriate for the workload.", true, OptimizationRisk.Low),
                    T("sleep", "Prevent sleep during workload", "Keep the system awake during long performance tests.", false, OptimizationRisk.Low),
                    T("display-timeout", "Workload display timeout", "Prevent display timeout during active benchmarks.", false, OptimizationRisk.Low)),

                Group("game-priority", "Best Game Priority", OptimizationCategory.Gaming,
                    "Game scheduling and process-priority controls with explicit scope.",
                    T("gaming-game-mode", "Game Mode", "Enable Windows Game Mode.", true, OptimizationRisk.Low),
                    T("fullscreen", "Fullscreen optimization review", "Review fullscreen optimization behavior for the selected game.", false, OptimizationRisk.Low),
                    T("priority", "Game process priority", "Apply a controlled priority policy only to an explicitly selected game process.", false, OptimizationRisk.Medium)),

                Group("latency", "Best Latency Optimization", OptimizationCategory.Gaming,
                    "Latency-focused controls that avoid undocumented registry recipes.",
                    T("hags", "Hardware-Accelerated GPU Scheduling", "Test HAGS as a workload-dependent setting.", false, OptimizationRisk.Medium, true),
                    T("timer-resolution", "Timer behavior review", "Measure timer behavior before changing system-wide timing settings.", false, OptimizationRisk.Medium),
                    T("fullscreen-latency", "Fullscreen latency review", "Review presentation mode and fullscreen behavior.", false, OptimizationRisk.Low)),

                Group("graphics-driver", "Custom Graphics Driver", OptimizationCategory.Graphics,
                    "GPU-driver controls that remain tied to detected hardware and driver capabilities.",
                    T("driver-profile", "Create application profile", "Create a per-game graphics profile when supported.", false, OptimizationRisk.Low),
                    T("low-latency", "Driver low-latency mode", "Review the driver low-latency mode for the selected application.", false, OptimizationRisk.Medium),
                    T("shader-cache", "Shader cache review", "Review shader-cache behavior without deleting active caches blindly.", false, OptimizationRisk.Low)),

                Group("nvidia", "NVIDIA GPU Tweaks", OptimizationCategory.Graphics,
                    "NVIDIA-specific controls are only enabled when compatible hardware is detected.",
                    T("nvidia-power", "Prefer maximum performance", "Use maximum-performance application behavior where supported.", false, OptimizationRisk.Low),
                    T("nvidia-reflex", "NVIDIA Reflex review", "Review Reflex configuration for supported games.", false, OptimizationRisk.Low),
                    T("nvidia-vrr", "Variable refresh review", "Review G-SYNC/VRR behavior for the selected display.", false, OptimizationRisk.Low)),

                Group("radeon", "Radeon GPU Tweaks", OptimizationCategory.Graphics,
                    "AMD-specific controls are only enabled when compatible hardware is detected.",
                    T("radeon-power", "Radeon performance profile", "Review supported Radeon performance-profile settings.", false, OptimizationRisk.Low),
                    T("radeon-anti-lag", "Radeon Anti-Lag review", "Review Anti-Lag behavior for supported games.", false, OptimizationRisk.Low),
                    T("radeon-chill", "Radeon Chill review", "Review Chill settings because frame limiting can affect latency.", false, OptimizationRisk.Low)),

                Group("windows-cleaner", "Custom Windows Cleaner", OptimizationCategory.Cleanup,
                    "Targeted cleanup with explicit file locations and no blanket deletion.",
                    T("temp-files", "Temporary files", "Clean supported user and Windows temporary files.", true, OptimizationRisk.Low),
                    T("shader-cache", "Stale shader caches", "Remove only supported stale shader-cache data after verification.", false, OptimizationRisk.Low),
                    T("update-cache", "Windows Update cache review", "Review stale update-cache data before cleanup.", false, OptimizationRisk.Medium)),

                Group("directx", "DirectX Optimization", OptimizationCategory.Graphics,
                    "DirectX-related maintenance focused on supported caches and presentation settings.",
                    T("dx-cache", "DirectX shader cache", "Review and maintain the DirectX shader cache.", true, OptimizationRisk.Low),
                    T("gpu-scheduling", "GPU scheduling review", "Review GPU scheduling settings against the current driver.", false, OptimizationRisk.Medium),
                    T("presentation", "Presentation mode review", "Review the game's presentation mode for latency and stability.", false, OptimizationRisk.Low)),

                Group("chrome", "Google Chrome Debloat", OptimizationCategory.Cleanup,
                    "Browser cleanup limited to explicit Chrome settings and user-approved data.",
                    T("chrome-startup", "Chrome startup behavior", "Review unnecessary startup behavior and background execution.", true, OptimizationRisk.Low),
                    T("chrome-background", "Chrome background apps", "Disable unnecessary background execution where supported.", true, OptimizationRisk.Low),
                    T("chrome-extensions", "Review extensions", "Identify unused extensions before disabling them.", false, OptimizationRisk.Low)),

                Group("windows-debloat", "Windows Debloat", OptimizationCategory.Cleanup,
                    "Conservative Windows cleanup that avoids removing core system components.",
                    T("optional-apps", "Review optional apps", "Identify optional components that can be removed by the user.", false, OptimizationRisk.Medium),
                    T("startup-items", "Startup items", "Review non-essential startup applications.", true, OptimizationRisk.Low),
                    T("background-services", "Background services", "Review non-essential third-party services before changing them.", false, OptimizationRisk.Medium)),

                Group("discord", "Discord Debloat", OptimizationCategory.Cleanup,
                    "Discord-specific controls intended to reduce unnecessary background work.",
                    T("discord-startup", "Discord startup", "Control Discord startup with an explicit toggle.", true, OptimizationRisk.Low),
                    T("discord-overlay", "Discord overlay", "Disable the overlay when it is not required for the selected game.", false, OptimizationRisk.Low),
                    T("discord-hardware", "Discord hardware acceleration", "Review hardware acceleration for the selected workload.", false, OptimizationRisk.Low)),

                Group("latency-timing", "Best Latency Timing", OptimizationCategory.Timing,
                    "Timing controls are treated as experimental until measured against a baseline.",
                    T("system-timer", "System timer review", "Measure timer behavior before changing timer policies.", false, OptimizationRisk.Medium),
                    T("dynamic-ticks", "Dynamic tick review", "Review dynamic-tick behavior for the target workload.", false, OptimizationRisk.Medium, true),
                    T("platform-clock", "Platform clock review", "Review platform-clock behavior without forcing undocumented boot settings.", false, OptimizationRisk.High, true)),

                Group("audio", "Complete Sound Optimization", OptimizationCategory.Audio,
                    "Audio latency controls with device-specific safeguards.",
                    T("audio-enhancements", "Disable unnecessary enhancements", "Review Windows audio enhancements for the active device.", true, OptimizationRisk.Low),
                    T("exclusive-mode", "Exclusive mode review", "Review exclusive-mode settings for the active audio device.", false, OptimizationRisk.Low),
                    T("audio-power", "Audio power management", "Review power management that can introduce device wake latency.", false, OptimizationRisk.Medium)),

                Group("alt-tab", "Better Alt Tab", OptimizationCategory.Windows,
                    "Windows multitasking controls focused on predictable game switching.",
                    T("alt-tab-mode", "Alt-Tab window mode", "Choose a focused Alt-Tab presentation mode.", true, OptimizationRisk.Low),
                    T("background-priority", "Background app behavior", "Review background app behavior while a game is active.", false, OptimizationRisk.Low),
                    T("game-bar", "Game Bar capture review", "Review capture features that may remain active while gaming.", false, OptimizationRisk.Low)),

                Group("bcdedit", "Best BCDEdit", OptimizationCategory.Boot,
                    "Boot configuration options are presented as measured experiments, not blanket tweaks.",
                    T("boot-timeout", "Boot menu timeout", "Adjust the boot menu timeout only when the machine uses a boot menu.", false, OptimizationRisk.Low),
                    T("disabledynamictick", "Dynamic tick review", "Review this boot setting only after measuring its workload impact.", false, OptimizationRisk.High, true),
                    T("useplatformclock", "Platform clock review", "Do not force platform-clock settings without evidence from the target system.", false, OptimizationRisk.High, true))
            };

        private static OptimizationGroupDefinition Group(
            string id,
            string title,
            OptimizationCategory category,
            string description,
            params OptimizationTweakDefinition[] tweaks) =>
            new OptimizationGroupDefinition
            {
                Id = id,
                Title = title,
                Category = category,
                Description = description,
                Tweaks = tweaks
            };

        private static OptimizationTweakDefinition T(
            string id,
            string title,
            string description,
            bool defaultEnabled,
            OptimizationRisk risk,
            bool requiresReboot = false) =>
            new OptimizationTweakDefinition
            {
                Id = id,
                Title = title,
                Description = description,
                DefaultEnabled = defaultEnabled,
                Risk = risk,
                RequiresReboot = requiresReboot,
                RollbackSupported = true
            };
    }
}
