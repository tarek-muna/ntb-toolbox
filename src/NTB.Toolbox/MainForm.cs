using NTB.Toolbox.Modules;
using NTB.Toolbox.Services;

namespace NTB.Toolbox;

internal sealed class MainForm : Form
{
    private readonly ModuleHost _moduleHost = new(BuiltInModules.Create());
    private readonly AppSettings _settings = AppSettingsStore.Load();
    private readonly FlowLayoutPanel _navigation = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly Label _heading = new() { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold) };
    private readonly Label _description = new() { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(650, 0) };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Werkzeuge durchsuchen …" };
    private readonly Button _favorite = new() { Width = 44, Height = 34, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Symbol", 14) };
    private IToolboxModule? _currentModule;

    public MainForm()
    {
        Text = "NTB Toolbox 0.4.0-dev";
        Width = 1120;
        Height = 720;
        MinimumSize = new Size(900, 580);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        BuildShell();
        _search.TextChanged += (_, _) => RefreshNavigation();
        _favorite.Click += (_, _) => ToggleFavorite();
        RefreshNavigation();
        OpenModule(_moduleHost.All.First());
        ApplyTheme();
        AppLog.Write("NTB Toolbox gestartet.");
    }

    private void BuildShell()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var sidebar = new Panel { Name = "Sidebar", Dock = DockStyle.Fill, BackColor = Color.FromArgb(28, 42, 59), Padding = new Padding(14), Tag = "sidebar" };
        var sidebarLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Tag = "sidebar" };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        sidebarLayout.Controls.Add(new Label { Text = "NTB Toolbox\nWerkzeuge für Technik & Büro", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), Tag = "sidebar" }, 0, 0);
        sidebarLayout.Controls.Add(_search, 0, 1);
        sidebarLayout.Controls.Add(_navigation, 0, 2);

        var logButton = SidebarButton("Protokoll anzeigen");
        logButton.Click += (_, _) => ShowLog();
        sidebarLayout.Controls.Add(logButton, 0, 3);

        var themeButton = SidebarButton("Hell/Dunkel umschalten");
        themeButton.Click += (_, _) => ToggleTheme();
        sidebarLayout.Controls.Add(themeButton, 0, 4);
        sidebar.Controls.Add(sidebarLayout);

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(22) };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new Panel { Dock = DockStyle.Fill };
        _heading.Location = new Point(0, 0);
        _description.Location = new Point(2, 42);
        _favorite.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _favorite.Location = new Point(720, 0);
        _favorite.FlatAppearance.BorderColor = Color.FromArgb(190, 198, 208);
        header.Controls.Add(_heading);
        header.Controls.Add(_description);
        header.Controls.Add(_favorite);
        header.Resize += (_, _) => _favorite.Left = Math.Max(0, header.ClientSize.Width - _favorite.Width);
        main.Controls.Add(header, 0, 0);
        main.Controls.Add(_content, 0, 1);

        shell.Controls.Add(sidebar, 0, 0);
        shell.Controls.Add(main, 1, 0);
        Controls.Add(shell);
    }

    private static Button SidebarButton(string text)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(39, 57, 78), ForeColor = Color.White, Tag = "sidebar" };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void RefreshNavigation()
    {
        _navigation.SuspendLayout();
        _navigation.Controls.Clear();
        var modules = _moduleHost.Search(_search.Text);
        var ordered = modules
            .OrderByDescending(module => _settings.FavoriteModuleIds.Contains(module.Id))
            .ThenBy(module => module.Category)
            .ThenBy(module => module.Title)
            .ToList();

        string? currentCategory = null;
        foreach (var module in ordered)
        {
            var category = _settings.FavoriteModuleIds.Contains(module.Id) ? "Favoriten" : module.Category;
            if (!string.Equals(currentCategory, category, StringComparison.Ordinal))
            {
                currentCategory = category;
                _navigation.Controls.Add(new Label
                {
                    Text = currentCategory.ToUpperInvariant(),
                    ForeColor = Color.FromArgb(145, 164, 188),
                    BackColor = Color.FromArgb(28, 42, 59),
                    Tag = "sidebar",
                    AutoSize = false,
                    Width = 225,
                    Height = 28,
                    Padding = new Padding(6, 9, 0, 0),
                    Font = new Font("Segoe UI", 8, FontStyle.Bold)
                });
            }

            var button = SidebarButton($"{(_settings.FavoriteModuleIds.Contains(module.Id) ? "★ " : string.Empty)}{module.Title}{(module.RequiresAdministrator ? "  [Admin]" : string.Empty)}");
            button.Width = 225;
            button.Height = 38;
            button.Dock = DockStyle.None;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(8, 0, 0, 0);
            button.Margin = new Padding(0, 2, 0, 2);
            button.Cursor = Cursors.Hand;
            button.Click += (_, _) => OpenModule(module);
            _navigation.Controls.Add(button);
        }
        _navigation.ResumeLayout();
    }

    private void OpenModule(IToolboxModule module)
    {
        _currentModule = module;
        _heading.Text = module.Title;
        var adminText = module.RequiresAdministrator ? " · Administratorrechte erforderlich" : string.Empty;
        _description.Text = $"{module.Category} · {module.Description}{adminText}";
        _description.ForeColor = module.RequiresAdministrator ? Color.DarkOrange : (_settings.Theme == AppTheme.Dark ? Color.Silver : Color.DimGray);
        UpdateFavoriteButton();
        _content.Controls.Clear();
        var view = module.CreateView();
        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
        ThemeService.Apply(view, _settings.Theme);
        AppLog.Write($"Modul geöffnet: {module.Title}");
    }

    private void ToggleFavorite()
    {
        if (_currentModule is null) return;
        if (!_settings.FavoriteModuleIds.Add(_currentModule.Id))
            _settings.FavoriteModuleIds.Remove(_currentModule.Id);
        AppSettingsStore.Save(_settings);
        UpdateFavoriteButton();
        RefreshNavigation();
    }

    private void UpdateFavoriteButton()
    {
        var isFavorite = _currentModule is not null && _settings.FavoriteModuleIds.Contains(_currentModule.Id);
        _favorite.Text = isFavorite ? "★" : "☆";
        _favorite.AccessibleName = isFavorite ? "Aus Favoriten entfernen" : "Zu Favoriten hinzufügen";
    }

    private void ToggleTheme()
    {
        _settings.Theme = _settings.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        AppSettingsStore.Save(_settings);
        ApplyTheme();
        if (_currentModule is not null) OpenModule(_currentModule);
        AppLog.Write($"Theme geändert: {_settings.Theme}");
    }

    private void ApplyTheme()
    {
        BackColor = _settings.Theme == AppTheme.Dark ? Color.FromArgb(30, 33, 38) : Color.FromArgb(242, 245, 249);
        ThemeService.Apply(this, _settings.Theme);
        foreach (Control control in Controls.Find("Sidebar", true))
        {
            control.BackColor = Color.FromArgb(28, 42, 59);
            control.ForeColor = Color.White;
        }
        RefreshNavigation();
    }

    private void ShowLog()
    {
        using var form = new Form { Text = "NTB Toolbox Protokoll", Width = 850, Height = 520, StartPosition = FormStartPosition.CenterParent };
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), Text = string.Join(Environment.NewLine, AppLog.Entries) };
        var export = new Button { Text = "Protokoll exportieren", Dock = DockStyle.Bottom, Height = 40 };
        export.Click += (_, _) => ExportLog();
        void Append(string line)
        {
            if (!output.IsDisposed && output.IsHandleCreated)
                output.BeginInvoke(() => output.AppendText((output.TextLength > 0 ? Environment.NewLine : string.Empty) + line));
        }
        AppLog.EntryAdded += Append;
        form.FormClosed += (_, _) => AppLog.EntryAdded -= Append;
        form.Controls.Add(output);
        form.Controls.Add(export);
        ThemeService.Apply(form, _settings.Theme);
        form.ShowDialog(this);
    }

    private void ExportLog()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Protokoll exportieren",
            Filter = "Textdatei (*.txt)|*.txt|Alle Dateien (*.*)|*.*",
            FileName = $"ntb-toolbox-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllLines(dialog.FileName, AppLog.Entries);
        AppLog.Write($"Protokoll exportiert: {dialog.FileName}");
    }
}
