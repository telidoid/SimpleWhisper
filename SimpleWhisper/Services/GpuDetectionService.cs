using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SimpleWhisper.Services;

public record GpuInfo(string Name, GpuBackend Backend);

public enum GpuBackend
{
    None,
    Vulkan,
    Cuda
}

public static class GpuDetectionService
{
    public static GpuInfo Detect()
    {
        // CUDA (NVIDIA) — check across all platforms
        var cudaName = DetectCuda();
        if (cudaName is not null)
            return new GpuInfo(cudaName, GpuBackend.Cuda);

        // Vulkan (AMD, Intel, or NVIDIA without CUDA drivers)
        var vulkanName = DetectVulkan();
        if (vulkanName is not null)
            return new GpuInfo(vulkanName, GpuBackend.Vulkan);

        return new GpuInfo(string.Empty, GpuBackend.None);
    }

    private static string? DetectCuda()
    {
        if (OperatingSystem.IsLinux() && !File.Exists("/dev/nvidia0") && !Directory.Exists("/dev/nvidia0"))
            return null;

        if (OperatingSystem.IsWindows())
        {
            // Check Windows registry or nvidia-smi
            var name = GetGpuNameFromNvidiaSmi();
            return name;
        }

        // Linux and macOS (though macOS doesn't really support CUDA anymore)
        return GetGpuNameFromNvidiaSmi();
    }

    private static string? DetectVulkan()
    {
        // vulkaninfo works on Linux, Windows, and macOS
        var name = GetGpuNameFromVulkanInfo();
        if (name is not null)
            return name;

        // Platform-specific fallbacks
        if (OperatingSystem.IsWindows())
            return GetGpuNameFromWmic();

        if (OperatingSystem.IsLinux())
            return GetGpuNameFromLspci();

        if (OperatingSystem.IsMacOS())
            return GetGpuNameFromSystemProfiler();

        return null;
    }

    private static string? GetGpuNameFromVulkanInfo()
    {
        return RunCommand("vulkaninfo", "--summary", output =>
        {
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("deviceName", StringComparison.Ordinal)) continue;
                var idx = line.IndexOf('=');
                if (idx >= 0)
                    return line[(idx + 1)..].Trim();
            }
            return null;
        });
    }

    private static string? GetGpuNameFromNvidiaSmi()
    {
        return RunCommand("nvidia-smi", "--query-gpu=name --format=csv,noheader", output =>
        {
            var name = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        });
    }

    private static string? GetGpuNameFromLspci()
    {
        return RunCommand("lspci", "", output =>
        {
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("VGA", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("3D controller", StringComparison.OrdinalIgnoreCase))
                    continue;
                var idx = line.IndexOf(": ", StringComparison.Ordinal);
                if (idx >= 0)
                    return line[(idx + 2)..].Trim();
            }
            return null;
        });
    }

    private static string? GetGpuNameFromWmic()
    {
        return RunCommand("cmd", "/c wmic path win32_videocontroller get name /value", output =>
        {
            foreach (var line in output.Split('\n'))
            {
                if (!line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase)) continue;
                var name = line[5..].Trim();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            return null;
        });
    }

    private static string? GetGpuNameFromSystemProfiler()
    {
        return RunCommand("system_profiler", "SPDisplaysDataType", output =>
        {
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("Chipset Model:", StringComparison.OrdinalIgnoreCase)) continue;
                var idx = line.IndexOf(':');
                if (idx >= 0)
                    return line[(idx + 1)..].Trim();
            }
            return null;
        });
    }

    private static string? RunCommand(string command, string args, Func<string, string?> parse)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo(command, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (proc is null) return null;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);
            return parse(output);
        }
        catch
        {
            return null;
        }
    }
}
