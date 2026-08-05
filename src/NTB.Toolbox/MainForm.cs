using NTB.Toolbox.Modules;

namespace NTB.Toolbox;

internal sealed class MainForm : Form
{
    private readonly ModuleHost _moduleHost = new(BuiltInModules.Create());
    private readonly FlowLayoutPanel _navigation = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly Label _heading = new() { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold) };
    private readonly Label _description = new() { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(720, 0) };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Werkzeuge durchsuchen …" };

    public MainForm()
    {
        Text = "NTB Toolbox 0.2.0-dev";
        Width = 1120;
        Height = 720;
        MinimumSize = new Size(900, 580);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(242, 245, 249);

        BuildShell();
        _search.TextChanged += (_, _) => RefreshNavigation();
        RefreshNavigation();
        OpenModule(_moduleHost.All.First());
    }

    private void BuildShell()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(28, 42, 59), Padding = new Padding(14) };
        var sidebarLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebarLayout.Controls.Add(new Label { Text = "NTB Toolbox\nWerkzeuge für Technik & Büro", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold) }, 0, 0);
        sidebarLayout.Controls.Add(_search, 0, 1);
        sidebarLayout.Controls.Add(_navigation, 0, 2);
        sidebar.Controls.Add(sidebarLayout);

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(22) };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new Panel { Dock = DockStyle.Fill };
        _heading.Location = new Point(0, 0);
        _description.Location = new Point(2, 42);
        header.Controls.Add(_heading);
        header.Controls.Add(_description);
        main.Controls.Add(header, 0, 0);
        main.Controls.Add(_content, 0, 1);

        shell.Controls.Add(sidebar, 0, 0);
        shell.Controls.Add(main, 1, 0);
        Controls.Add(shell);
    }

    private void RefreshNavigation()
    {
        _navigation.SuspendLayout();
        _navigation.Controls.Clear();
        string? currentCategory = null;
        foreach (var module in _moduleHost.Search(_search.Text))
        {
            if (!string.Equals(currentCategory, module.Category, StringComparison.Ordinal))
            {
                currentCategory = module.Category;
                _navigation.Controls.Add(new Label
                {
                    Text = currentCategory.ToUpperInvariant(),
                    ForeColor = Color.FromArgb(145, 164, 188),
                    AutoSize = false,
                    Width = 225,
                    Height = 28,
                    Padding = new Padding(6, 9, 0, 0),
                    Font = new Font("Segoe UI", 8, FontStyle.Bold)
                });
            }

            var button = new Button
            {
                Text = module.Title,
                Width = 225,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(39, 57, 78),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Margin = new Padding(0, 2, 0, 2),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => OpenModule(module);
            _navigation.Controls.Add(button);
        }
        _navigation.ResumeLayout();
    }

    private void OpenModule(IToolboxModule module)
    {
        _heading.Text = module.Title;
        _description.Text = $"{module.Category} · {module.Description}";
        _content.Controls.Clear();
        var view = module.CreateView();
        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
    }
}
