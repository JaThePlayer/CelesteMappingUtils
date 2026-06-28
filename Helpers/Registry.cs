using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.MappingUtils.Helpers;

internal sealed class Registry<T> : IEnumerable<T> where T : IRegistryEntry
{
    private readonly List<T> _entries = [];
    
    public IEnumerable<T> Entries => _entries;

    public void Add(T entry)
    {
        _entries.RemoveAll(x => x.RegistryKey == entry.RegistryKey);
        _entries.Add(entry);
    }

    public IEnumerator<T> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public interface IRegistryEntry
{
    public string Name { get; }
    
    public string? ModName { get; }

    public string RegistryKey => $"{ModName ?? "MappingUtils"}/{Name}";
}