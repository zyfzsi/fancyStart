#nullable enable

using FancyStart.Models;

namespace FancyStart.Services;

public interface IStartupProvider
{
    List<StartupItem> GetStartupItems();
    void Enable(StartupItem item);
    void Disable(StartupItem item);
    void Delete(StartupItem item);
    void Add(string filePath);
}
