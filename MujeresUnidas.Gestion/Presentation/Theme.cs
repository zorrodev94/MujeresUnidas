using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    public static readonly Color Lavender = Color.FromArgb(237, 219, 251);
    public static readonly Color BorderColor = Color.FromArgb(225, 201, 237);
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
    /// <summary>Native button with visually rounded bounds and a single consistent hover treatment.</summary>
    public static Button Button(string text, bool secondary = false)
    {
        var b=new Button
        {
            Text=text,Font=Text(10,true),Dock=DockStyle.Fill,Height=44,
            BackColor=secondary?Color.FromArgb(248,232,252):Violet,
            ForeColor=secondary?Plum:Color.White,
            FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,
            Margin=new Padding(3,5,3,5),UseVisualStyleBackColor=false
        };
        b.FlatAppearance.BorderSize=0;
        b.FlatAppearance.MouseOverBackColor=secondary?Lavender:Color.FromArgb(113,36,148);
        b.Resize+=(_,_)=>Round(b,13);
        // Convert legacy unicode prefixes into real image assets without changing handlers.
        var value=text.TrimStart();
        string? icon=value.Length>0?value[0] switch
        {
            '✓' or '✔' => "check", '✕' or '×' or '✖' => "close",
            '↻' or '⟳' => "refresh", '✎' => "edit", _ => null
        }:null;
        if(icon is not null)
        {
            b.Image=LoadImage($"action_{icon}_{(secondary?"dark":"light")}.png");
            b.ImageAlign=ContentAlignment.MiddleLeft;
            b.TextImageRelation=TextImageRelation.ImageBeforeText;
            b.Text=value.Length>1?value[1..].Trim():"";
            b.Padding=new Padding(8,0,3,0);
            b.AccessibleName=text;
        }
        return b;
    }

    /// <summary>Branded native header reused across Gestión views for consistent information hierarchy.</summary>
    public static Panel BannerTitle(string title,int fontSize=24)
    {
        // Text and decoration have separate cells. A stretched bitmap may never cover the title.
        var panel=new RoundedPanel
        {
            Dock=DockStyle.Fill,BackColor=Color.FromArgb(238,218,249),
            Margin=new Padding(7,4,7,5),Padding=new Padding(18,2,10,2),Radius=17
        };
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,
            BackColor=Color.Transparent,Margin=Padding.Empty};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,81));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,19));
        var caption=Label(title,fontSize,true,Plum);
        caption.AutoEllipsis=true;
        caption.Padding=new Padding(4,0,4,0);
        layout.Controls.Add(caption,0,0);
        var art=Picture("mu_mark.png");
        art.Margin=new Padding(10,3,7,3);
        layout.Controls.Add(art,1,0);
        panel.Controls.Add(layout);
        return panel;
    }

    /// <summary>Subtle leaf/wave accents inspired by the approved institutional atlas.</summary>
    public sealed class DecoratedSidebar : Panel
    {
        public DecoratedSidebar() { DoubleBuffered=true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            var w=ClientSize.Width;var h=ClientSize.Height;
            if(w<=0||h<=0)return;
            using var wave1=new GraphicsPath();
            wave1.AddBezier(-30,h-145,w/4f,h-230,w/2f,h-45,w+40,h-115);
            wave1.AddLine(w+40,h+10,-30,h+10);wave1.CloseFigure();
            using var brush1=new SolidBrush(Color.FromArgb(68,171,94,202));
            e.Graphics.FillPath(brush1,wave1);
            using var wave2=new GraphicsPath();
            wave2.AddBezier(-30,h-74,w/4f,h-140,w*.72f,h-12,w+40,h-70);
            wave2.AddLine(w+40,h+10,-30,h+10);wave2.CloseFigure();
            using var brush2=new SolidBrush(Color.FromArgb(86,208,126,231));
            e.Graphics.FillPath(brush2,wave2);
            using var pen=new Pen(Color.FromArgb(62,238,179,248),1.8f);
            e.Graphics.DrawArc(pen,new Rectangle(w-75,h-207,100,105),180,155);
            e.Graphics.DrawArc(pen,new Rectangle(-32,h-185,90,100),-30,140);
        }
    }

    /// <summary>Clips any WinForms control to rounded bounds while keeping native events.</summary>
    public static void Round(Control target,int radius)
    {
        if(target.Width<2||target.Height<2)return;
        using var path=RoundPath(new Rectangle(0,0,target.Width,target.Height),radius);
        var old=target.Region;
        target.Region=new Region(path);
        old?.Dispose();
    }
    private static GraphicsPath RoundPath(Rectangle rect,int radius)
    {
        var path=new GraphicsPath();
        int d=Math.Min(Math.Min(radius*2,rect.Width),rect.Height);
        if(d<=0)return path;
        path.AddArc(rect.X,rect.Y,d,d,180,90);
        path.AddArc(rect.Right-d,rect.Y,d,d,270,90);
        path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);
        path.AddArc(rect.X,rect.Bottom-d,d,d,90,90);
        path.CloseFigure();return path;
    }

    /// <summary>Rounded, lightly graded container without bitmap mockups.</summary>
    public sealed class RoundedPanel : Panel
    {
        /// <summary>Corner radius managed by code; the WinForms designer must not serialize it.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius {get;set;}=19;
        public RoundedPanel() { DoubleBuffered=true; Resize+=(_,_)=>Round(this,Radius); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using var path=RoundPath(new Rectangle(0,0,Width,Height),Radius);
            using var brush=new LinearGradientBrush(new Point(0,0),
                new Point(Math.Max(1,Width),Math.Max(1,Height)),
                BackColor,ControlPaint.Light(BackColor,0.13f));
            e.Graphics.FillPath(brush,path);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using var path=RoundPath(new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1)),Radius);
            using var pen=new Pen(BorderColor,1f);
            e.Graphics.DrawPath(pen,path);
        }
    }

    /// <summary>Single R4.3 table treatment; retains genuine DataGridView sorting/selection.</summary>
    public static void StyleGrid(DataGridView grid)
    {
        grid.BorderStyle=BorderStyle.None;
        grid.BackgroundColor=Color.White;
        grid.EnableHeadersVisualStyles=false;
        grid.RowHeadersVisible=false;
        grid.GridColor=Color.FromArgb(241,232,247);
        grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight=44;
        grid.RowTemplate.Height=43;
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(252,247,255);
        grid.ColumnHeadersDefaultCellStyle.BackColor=Lavender;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Lavender;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Plum;
        grid.ColumnHeadersDefaultCellStyle.ForeColor=Plum;
        grid.ColumnHeadersDefaultCellStyle.Font=Text(10,true);
        grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(9,0,5,0);
        grid.DefaultCellStyle.BackColor=Color.White;
        grid.DefaultCellStyle.ForeColor=Plum;
        grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(231,202,247);
        grid.DefaultCellStyle.SelectionForeColor=Plum;
        grid.DefaultCellStyle.Padding=new Padding(8,3,5,3);
        grid.DefaultCellStyle.Font=Text(10);
    }

    /// <summary>Apply atlas tokens to ALL existing native controls, including secondary panels.</summary>
    public static void Polish(Control root)
    {
        foreach(Control control in root.Controls)
        {
            switch(control)
            {
                case DataGridView grid:
                    StyleGrid(grid);
                    grid.DefaultCellStyle.WrapMode=DataGridViewTriState.False;
                    break;
                case ComboBox combo:
                    combo.FlatStyle=FlatStyle.Flat;
                    combo.BackColor=Color.White;
                    combo.ForeColor=Plum;
                    combo.Font=Text(10);
                    break;
                case NumericUpDown numeric:
                    numeric.BackColor=Color.White;
                    numeric.ForeColor=Plum;
                    numeric.Font=Text(10);
                    numeric.BorderStyle=BorderStyle.FixedSingle;
                    break;
                case ListBox list:
                    list.BorderStyle=BorderStyle.None;
                    list.BackColor=Color.White;
                    list.ForeColor=Plum;
                    list.Font=Text(11);
                    list.IntegralHeight=false;
                    break;
                case TextBox input:
                    input.ForeColor=Plum;
                    if(input.BackColor==SystemColors.Window)input.BackColor=Color.White;
                    if(input.Font.FontFamily.Name==SystemFonts.DefaultFont.FontFamily.Name)
                        input.Font=Text(10);
                    break;
            }
            if(control.HasChildren)Polish(control);
        }
    }

    public static Icon? AppIcon()
    {
        var file=Path.Combine(AppContext.BaseDirectory,"Resources","Images","MujeresUnidas.ico");
        return File.Exists(file)?new Icon(file):null;
    }
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
    public static Panel Card(Color? fill = null) => new RoundedPanel
    {
        Dock=DockStyle.Fill, BackColor=fill??Color.White,
        Padding=new Padding(20), Margin=new Padding(7)
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
