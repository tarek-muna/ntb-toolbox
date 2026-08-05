namespace NTB.Toolbox.Services;

internal enum AppTheme
{
    Light,
    Dark
}

internal static class ThemeService
{
    public static void Apply(Control root, AppTheme theme)
    {
        var palette = theme == AppTheme.Dark
            ? new ThemePalette(Color.FromArgb(30, 33, 38), Color.FromArgb(40, 44, 52), Color.Gainsboro, Color.FromArgb(55, 60, 70))
            : new ThemePalette(Color.FromArgb(242, 245, 249), Color.White, Color.FromArgb(35, 40, 48), Color.FromArgb(232, 236, 242));

        ApplyRecursive(root, palette);
    }

    private static void ApplyRecursive(Control control, ThemePalette palette)
    {
        if (control is TextBox textBox)
        {
            textBox.BackColor = palette.Surface;
            textBox.ForeColor = palette.Text;
        }
        else if (control is Button button && button.Tag as string != "sidebar")
        {
            button.BackColor = palette.Button;
            button.ForeColor = palette.Text;
        }
        else if (control is not Form)
        {
            control.BackColor = palette.Surface;
            control.ForeColor = palette.Text;
        }

        foreach (Control child in control.Controls)
            ApplyRecursive(child, palette);
    }

    private sealed record ThemePalette(Color Background, Color Surface, Color Text, Color Button);
}
