using System.Drawing;
using System.Windows.Forms;

namespace MujeresUnidas.Gestion.Presentation
{
/// <summary>Reusable WinForms design tokens and layout helpers for the institutional interface.</summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(251, 245, 254);
    public static readonly Color Sidebar = Color.FromArgb(97, 36, 121);
    public static readonly Color Violet = Color.FromArgb(137, 54, 161);
    public static readonly Color Plum = Color.FromArgb(78, 28, 99);
    public static readonly Color Muted = Color.FromArgb(123, 94, 133);
    public static readonly Color Lavender = Color.FromArgb(241, 220, 251);
    public static readonly Color Pink = Color.FromArgb(248, 223, 246);

    public static Font Text(float size = 10, bool bold = false) => new("Segoe UI",size,bold ? FontStyle.Bold : FontStyle.Regular);
    public static Label Label(string text, float size = 11, bool bold = false, Color? color = null) => new()
    {
        Text = text, Font = Text(size,bold), ForeColor = color ?? Plum,
        AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill, BackColor = Color.Transparent
    };
    public static TextBox Input(string placeholder, bool secret = false) => new()
    {
        PlaceholderText = placeholder, Dock = DockStyle.Fill, Font = Text(11),
        UseSystemPasswordChar = secret, Margin=new Padding(0,6,0,6), BorderStyle = BorderStyle.FixedSingle
    };
    public static Button Button(string text, bool secondary = false) => new()
    {
        Text = text, Font=Text(10,true), Dock=DockStyle.Fill, Height=44,
        BackColor=secondary ? Color.White : Violet,
        ForeColor=secondary ? Plum : Color.White,
        FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand, Margin=new Padding(2,5,2,5)
    };
    public static Image? LoadImage(string name)
    {
        var file=Path.Combine(AppContext.BaseDirectory,"Resources","Images",name);
        return File.Exists(file) ? Image.FromFile(file) : null;
    }
    public static PictureBox Picture(string name) => new()
    {
        Image=LoadImage(name), SizeMode=PictureBoxSizeMode.Zoom, Dock=DockStyle.Fill,
        BackColor=Color.Transparent
    };
    public static Panel Card(Color? fill = null) => new()
    {
        Dock=DockStyle.Fill, BackColor=fill??Color.White, Padding=new Padding(20), Margin=new Padding(7)
    };
    public static TableLayoutPanel Rows(params (float Height, SizeType Size)[] sizes)
    {
        var t=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=0,Margin=Padding.Empty,Padding=Padding.Empty };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(var s in sizes)
        {
            // A flexible row needs a positive percentage. A zero-height percentage row
            // collapses the login panel and hides its controls.
            float height = s.Size == SizeType.Percent && s.Height <= 0 ? 100 : s.Height;
            t.RowStyles.Add(new RowStyle(s.Size,height));
            t.RowCount++;
        }
        return t;
    }
}
}
