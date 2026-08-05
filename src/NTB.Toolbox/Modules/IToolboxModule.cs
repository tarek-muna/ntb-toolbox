namespace NTB.Toolbox.Modules;

internal interface IToolboxModule
{
    string Id { get; }
    string Title { get; }
    string Category { get; }
    string Description { get; }
    bool RequiresAdministrator { get; }
    IReadOnlyCollection<string> Keywords { get; }
    Control CreateView();
}
