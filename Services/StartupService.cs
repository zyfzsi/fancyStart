#nullable enable

using FancyStart.Models;

namespace FancyStart.Services;

public class StartupService
{
    private readonly List<IStartupProvider> _providers;

    public StartupService()
    {
        _providers = new List<IStartupProvider>
        {
            new RegistryStartupProvider(),
            new FolderStartupProvider(),
            new TaskSchedulerProvider(),
        };
    }

    public List<StartupItem> GetAllStartupItems()
    {
        var items = new List<StartupItem>();

        foreach (var provider in _providers)
        {
            try
            {
                items.AddRange(provider.GetStartupItems());
            }
            catch
            {
                // One provider failing should not prevent others from loading
            }
        }

        return items;
    }

    public void Enable(StartupItem item)
    {
        var provider = GetProviderFor(item);
        provider.Enable(item);
    }

    public void Disable(StartupItem item)
    {
        var provider = GetProviderFor(item);
        provider.Disable(item);
    }

    public void Delete(StartupItem item)
    {
        var provider = GetProviderFor(item);
        provider.Delete(item);
    }

    public void Add(string filePath, StartupSourceType source = StartupSourceType.Registry)
    {
        var provider = GetProviderFor(source);
        provider.Add(filePath);
    }

    private IStartupProvider GetProviderFor(StartupItem item)
    {
        return GetProviderFor(item.Source);
    }

    private IStartupProvider GetProviderFor(StartupSourceType source)
    {
        return source switch
        {
            StartupSourceType.Registry => _providers.OfType<RegistryStartupProvider>().First(),
            StartupSourceType.StartupFolder => _providers.OfType<FolderStartupProvider>().First(),
            StartupSourceType.TaskScheduler => _providers.OfType<TaskSchedulerProvider>().First(),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source,
                "No provider registered for this startup source type."),
        };
    }
}
