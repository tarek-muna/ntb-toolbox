namespace NTB.Toolbox.Modules;

internal sealed class ModuleHost
{
    private readonly IReadOnlyList<IToolboxModule> _modules;

    public ModuleHost(IEnumerable<IToolboxModule> modules)
    {
        _modules = modules.OrderBy(m => m.Category).ThenBy(m => m.Title).ToList();
    }

    public IReadOnlyList<IToolboxModule> All => _modules;

    public IReadOnlyList<IToolboxModule> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _modules;
        var value = query.Trim();
        return _modules.Where(module =>
            module.Title.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            module.Category.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            module.Description.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            module.Keywords.Any(keyword => keyword.Contains(value, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
