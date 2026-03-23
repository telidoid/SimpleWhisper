namespace SimpleWhisper.Core.Models;

public record GpuInfo(string Name, GpuBackend Backend);

public enum GpuBackend
{
    None,
    Vulkan,
    Cuda,
    CoreML
}
