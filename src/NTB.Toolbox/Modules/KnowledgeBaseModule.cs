using System.Text.Json;
using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class KnowledgeBaseModule : IToolboxModule
{
    public string Id => "knowledge-base";
    public string Title => "Wissensdatenbank";
    public string Category => "Dokumentation";
    public string Description => "Dokumentationen und Lösungswege lokal speichern, durchsuchen und wiederverwenden.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["wissen", "lösungen", "dokumentation", "tickets", "suche", "knowledge base"];

    public Control CreateView() => new KnowledgeBaseView();
}

internal sealed class KnowledgeBaseEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Title { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

internal static class KnowledgeBaseStore
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NTB Toolbox");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "knowledge-base.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<KnowledgeBaseEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            return JsonSerializer.Deserialize<List<KnowledgeBaseEntry>>(File.ReadAllText(FilePath), JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            AppLog.Write($"Wissensdatenbank konnte nicht geladen werden: {ex.Message}");
            return [];
        }
    }

    public static void Save(IReadOnlyCollection<KnowledgeBaseEntry> entries)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(entries, JsonOptions));
    }
}

internal sealed class KnowledgeBaseView : UserControl
{
    private readonly List<KnowledgeBaseEntry> _entries = KnowledgeBaseStore.Load();
    private readonly TextBox _search = new() { PlaceholderText = "Titel, Tags oder Inhalt durchsuchen ...", Dock = DockStyle.Top };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _title = new() { PlaceholderText = "Titel", Dock = DockStyle.Top };
    private readonly TextBox _tags = new() { PlaceholderText = "Tags, durch Kommas getrennt", Dock = DockStyle.Top };
    private readonly TextBox _content = new() { Multiline = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
    private KnowledgeBaseEntry? _selected;

    public KnowledgeBaseView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(12);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight };
        var create = new Button { Text = "Neu", AutoSize = true };
        var save = new Button { Text = "Speichern", AutoSize = true };
        var import = new Button { Text = "Datei importieren", AutoSize = true };
        var export = new Button { Text = "Exportieren", AutoSize = true };
        var copy = new Button { Text = "Kopieren", AutoSize = true };
        var delete = new Button { Text = "Löschen", AutoSize = true };
        buttons.Controls.AddRange([create, save, import, export, copy, delete]);

        var left = new Panel { Dock = DockStyle.Left, Width = 300, Padding = new Padding(0, 0, 10, 0) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);

        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(_content);
        right.Controls.Add(_tags);
        right.Controls.Add(_title);
        right.Controls.Add(buttons);

        Controls.Add(right);
        Controls.Add(left);

        _search.TextChanged += (_, _) => RefreshList();
        _list.SelectedIndexChanged += (_, _) => ShowSelected();
        create.Click += (_, _) => NewEntry();
        save.Click += (_, _) => SaveEntry();
        import.Click += (_, _) => ImportFile();
        export.Click += (_, _) => ExportEntry();
        copy.Click += (_, _) => CopyEntry();
        delete.Click += (_, _) => DeleteEntry();

        RefreshList();
    }

    private void RefreshList()
    {
        var query = _search.Text.Trim();
        var filtered = _entries
            .Where(entry => string.IsNullOrWhiteSpace(query)
                || entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Tags.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.CreatedAt)
            .ToList();

        _list.DataSource = null;
        _list.DataSource = filtered;
        _list.DisplayMember = nameof(KnowledgeBaseEntry.Title);
    }

    private void ShowSelected()
    {
        _selected = _list.SelectedItem as KnowledgeBaseEntry;
        if (_selected is null) return;
        _title.Text = _selected.Title;
        _tags.Text = _selected.Tags;
        _content.Text = _selected.Content;
    }

    private void NewEntry()
    {
        _selected = null;
        _title.Clear();
        _tags.Clear();
        _content.Clear();
        _title.Focus();
    }

    private void SaveEntry()
    {
        if (string.IsNullOrWhiteSpace(_title.Text) || string.IsNullOrWhiteSpace(_content.Text))
        {
            MessageBox.Show("Titel und Inhalt sind erforderlich.", "Wissensdatenbank", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_selected is null)
        {
            _selected = new KnowledgeBaseEntry();
            _entries.Add(_selected);
        }

        _selected.Title = _title.Text.Trim();
        _selected.Tags = _tags.Text.Trim();
        _selected.Content = _content.Text;
        KnowledgeBaseStore.Save(_entries);
        AppLog.Write($"Wissenseintrag gespeichert: {_selected.Title}");
        RefreshList();
    }

    private void ImportFile()
    {
        using var dialog = new OpenFileDialog { Filter = "Text und Markdown|*.txt;*.md;*.log|Alle Dateien|*.*" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        _selected = null;
        _title.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        _content.Text = File.ReadAllText(dialog.FileName);
        _tags.Text = "importiert";
    }

    private void ExportEntry()
    {
        if (_selected is null) return;
        using var dialog = new SaveFileDialog { Filter = "Markdown|*.md|Textdatei|*.txt", FileName = SafeFileName(_selected.Title) + ".md" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        File.WriteAllText(dialog.FileName, FormatEntry(_selected));
        AppLog.Write($"Wissenseintrag exportiert: {_selected.Title}");
    }

    private void CopyEntry()
    {
        if (_selected is null) return;
        Clipboard.SetText(FormatEntry(_selected));
    }

    private void DeleteEntry()
    {
        if (_selected is null) return;
        if (MessageBox.Show($"Eintrag '{_selected.Title}' wirklich löschen?", "Wissensdatenbank", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _entries.Remove(_selected);
        KnowledgeBaseStore.Save(_entries);
        AppLog.Write($"Wissenseintrag gelöscht: {_selected.Title}");
        NewEntry();
        RefreshList();
    }

    private static string FormatEntry(KnowledgeBaseEntry entry) => $"# {entry.Title}\r\n\r\nErstellt: {entry.CreatedAt:yyyy-MM-dd HH:mm}\r\nTags: {entry.Tags}\r\n\r\n{entry.Content}";
    private static string SafeFileName(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
}