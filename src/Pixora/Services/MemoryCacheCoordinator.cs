namespace Pixora.Services;

public sealed record MemoryCacheSnapshot(
    long MainImageBudgetBytes,
    long MainImageEstimatedBytes,
    long PreviewBudgetBytes,
    long PreviewEstimatedBytes,
    bool IsUnderPressure,
    long ProcessWorkingSetBytes,
    long TotalAvailableMemoryBytes);

public sealed record MemoryCacheBudgets(int MainImageMegabytes, int PreviewMegabytes);

public sealed record RuntimePerformanceProfile(
    MemoryCacheBudgets CacheBudgets,
    int MainPreloadForwardRadius,
    int MainPreloadOppositeRadius,
    int ThumbnailLoadConcurrency);

public sealed class MemoryCacheCoordinator
{
    private const double SystemPressureThreshold = 0.85;
    private const double ProcessWorkingSetThreshold = 0.25;
    private const long MinimumProcessWorkingSetThresholdBytes = 512L * 1024 * 1024;
    private const long MinimumMainImageBudgetBytes = 64L * 1024 * 1024;
    private const long MinimumPreviewBudgetBytes = 32L * 1024 * 1024;

    private readonly ImageCache _imageCache;
    private readonly BitmapSourceMemoryCache _previewCache;

    public MemoryCacheCoordinator(ImageCache imageCache, BitmapSourceMemoryCache previewCache)
    {
        _imageCache = imageCache;
        _previewCache = previewCache;
    }

    public MemoryCacheBudgets ApplyConfiguredBudgets(
        int mainImageMegabytes,
        int previewMegabytes,
        bool useAutomaticSizing = false)
    {
        return ApplyConfiguredPerformance(
            mainImageMegabytes,
            previewMegabytes,
            useAutomaticSizing).CacheBudgets;
    }

    public RuntimePerformanceProfile ApplyConfiguredPerformance(
        int mainImageMegabytes,
        int previewMegabytes,
        bool useAutomaticSizing = false)
    {
        var memoryInfo = GC.GetGCMemoryInfo();
        var profile = ResolvePerformanceProfile(
            mainImageMegabytes,
            previewMegabytes,
            useAutomaticSizing,
            memoryInfo.TotalAvailableMemoryBytes,
            Environment.ProcessorCount);
        _imageCache.SetMaxMegabytes(profile.CacheBudgets.MainImageMegabytes);
        _previewCache.SetMaxMegabytes(profile.CacheBudgets.PreviewMegabytes);
        return profile;
    }

    public bool ShouldReduceBackgroundLoading(bool isProtectionEnabled)
    {
        if (!isProtectionEnabled)
        {
            return false;
        }

        var memoryInfo = GC.GetGCMemoryInfo();
        return IsUnderPressure(
            memoryInfo.MemoryLoadBytes,
            memoryInfo.HighMemoryLoadThresholdBytes,
            Environment.WorkingSet,
            memoryInfo.TotalAvailableMemoryBytes);
    }

    public void TrimForMemoryPressure()
    {
        _imageCache.TrimToBytes(Math.Max(MinimumMainImageBudgetBytes, _imageCache.MaxBytes / 2));
        _previewCache.TrimToBytes(Math.Max(MinimumPreviewBudgetBytes, _previewCache.MaxBytes / 2));
    }

    public MemoryCacheSnapshot CaptureSnapshot(bool isProtectionEnabled)
    {
        var memoryInfo = GC.GetGCMemoryInfo();
        return new MemoryCacheSnapshot(
            _imageCache.MaxBytes,
            _imageCache.TotalEstimatedBytes,
            _previewCache.MaxBytes,
            _previewCache.TotalEstimatedBytes,
            ShouldReduceBackgroundLoading(isProtectionEnabled),
            Environment.WorkingSet,
            memoryInfo.TotalAvailableMemoryBytes);
    }

    public static MemoryCacheBudgets ResolveBudgets(
        int configuredMainMegabytes,
        int configuredPreviewMegabytes,
        bool useAutomaticSizing,
        long totalAvailableMemoryBytes)
    {
        var main = Math.Max(64, configuredMainMegabytes);
        var preview = Math.Max(32, configuredPreviewMegabytes);
        if (!useAutomaticSizing || totalAvailableMemoryBytes <= 0)
        {
            return new MemoryCacheBudgets(main, preview);
        }

        var availableGigabytes = totalAvailableMemoryBytes / (double)(1024L * 1024 * 1024);
        var automaticCeiling = availableGigabytes switch
        {
            <= 4 => new MemoryCacheBudgets(256, 64),
            <= 6 => new MemoryCacheBudgets(384, 96),
            <= 8 => new MemoryCacheBudgets(512, 128),
            <= 12 => new MemoryCacheBudgets(640, 160),
            <= 20 => new MemoryCacheBudgets(2048, 512),
            <= 48 => new MemoryCacheBudgets(4096, 1024),
            _ => new MemoryCacheBudgets(8192, 2048),
        };

        return new MemoryCacheBudgets(
            Math.Min(main, automaticCeiling.MainImageMegabytes),
            Math.Min(preview, automaticCeiling.PreviewMegabytes));
    }

    public static RuntimePerformanceProfile ResolvePerformanceProfile(
        int configuredMainMegabytes,
        int configuredPreviewMegabytes,
        bool useAutomaticSizing,
        long totalAvailableMemoryBytes,
        int processorCount)
    {
        var budgets = ResolveBudgets(
            configuredMainMegabytes,
            configuredPreviewMegabytes,
            useAutomaticSizing,
            totalAvailableMemoryBytes);
        var availableGigabytes = totalAvailableMemoryBytes > 0
            ? totalAvailableMemoryBytes / (double)(1024L * 1024 * 1024)
            : 8;
        var hardwareTargets = availableGigabytes switch
        {
            <= 4 => (Forward: 2, Opposite: 1, Concurrency: 1),
            <= 8 => (Forward: 3, Opposite: 1, Concurrency: 2),
            <= 12 => (Forward: 3, Opposite: 1, Concurrency: 2),
            <= 20 => (Forward: 4, Opposite: 1, Concurrency: 4),
            <= 48 => (Forward: 6, Opposite: 2, Concurrency: 4),
            _ => (Forward: 8, Opposite: 2, Concurrency: 6),
        };
        var processorConcurrencyLimit = processorCount switch
        {
            <= 2 => 1,
            <= 4 => 2,
            <= 8 => 4,
            _ => 6,
        };

        return new RuntimePerformanceProfile(
            budgets,
            hardwareTargets.Forward,
            hardwareTargets.Opposite,
            Math.Min(hardwareTargets.Concurrency, processorConcurrencyLimit));
    }

    public static bool IsUnderPressure(
        long memoryLoadBytes,
        long highMemoryLoadThresholdBytes,
        long processWorkingSetBytes = 0,
        long totalAvailableMemoryBytes = 0)
    {
        var systemIsUnderPressure = highMemoryLoadThresholdBytes > 0
            && memoryLoadBytes >= (long)(highMemoryLoadThresholdBytes * SystemPressureThreshold);
        var processThreshold = Math.Max(
            MinimumProcessWorkingSetThresholdBytes,
            (long)(totalAvailableMemoryBytes * ProcessWorkingSetThreshold));
        var processIsUnderPressure = totalAvailableMemoryBytes > 0
            && processWorkingSetBytes >= processThreshold;
        return systemIsUnderPressure || processIsUnderPressure;
    }
}
