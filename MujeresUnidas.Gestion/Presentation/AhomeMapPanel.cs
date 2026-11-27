using System.Drawing.Drawing2D;
using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Presentation;

/// <summary>Conceptual Ahome sectors, not actual streets, government zones or geolocation of users.</summary>
public sealed class AhomeMapPanel:UserControl
{
    private readonly ApiConnection _api;
    private readonly ComboBox _zones=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox _sites=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly Label _status=Theme.Label("Croquis conceptual. Sin datos de riesgo ni coordenadas personales.",11,false,Theme.Muted);
    private readonly AtlasMap _map=new(){Dock=DockStyle.Fill,BackColor=Color.White,Margin=new Padding(6)};
    private readonly Label _siteDetails=Theme.Label("Selecciona una sede",11,false,Theme.Plum);
    private readonly Dictionary<Guid,(Guid? Zone,string Address)> _siteData=new();
    private sealed record Choice(Guid Id,string Name,string Code)
    {
        public override string ToString()=>Name;
    }
    public AhomeMapPanel(ApiConnection api,bool canEdit)
    {
        _api=api;Dock=DockStyle.Fill;BackColor=Theme.Background;
        var root=Theme.Rows((91,SizeType.Absolute),(70,SizeType.Absolute),(0,SizeType.Percent),(63,SizeType.Absolute));
        Controls.Add(root);
        root.Controls.Add(Theme.BannerTitle("Croquis institucional · Ahome",24),0,0);
        var info=Theme.Label("Sectores conceptuales Norte, Sur, Este y Oeste · No son límites oficiales ni zonas de violencia verificadas.",11,false,Theme.Muted);
        root.Controls.Add(info,0,1);
        var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));
        root.Controls.Add(content,0,2);
        var canvas=Theme.Card();
        var mapHost=new Panel{Dock=DockStyle.Fill,Padding=Padding.Empty};
        mapHost.Controls.Add(_map);canvas.Controls.Add(mapHost);
        content.Controls.Add(canvas,0,0);
        var side=Theme.Card();side.AutoScroll=true;content.Controls.Add(side,1,0);
        var controls=Theme.Rows((58,SizeType.Absolute),(48,SizeType.Absolute),(48,SizeType.Absolute),
            (48,SizeType.Absolute),(76,SizeType.Absolute),(48,SizeType.Absolute),
            (0,SizeType.Percent));
        controls.MinimumSize=new Size(0,420);side.Controls.Add(controls);
        controls.Controls.Add(Theme.Label("Sectores y sedes",18,true),0,0);
        controls.Controls.Add(_zones,0,1);controls.Controls.Add(_sites,0,2);
        var assign=Theme.Button("Vincular sede a sector");assign.Enabled=canEdit;
        controls.Controls.Add(assign,0,3);
        var notes=Theme.Label("Las sedes son ficticias y se registran en Gestión. El color identifica sectores de análisis, no niveles de peligro.",10,false,Theme.Muted);
        notes.TextAlign=ContentAlignment.TopLeft;
        controls.Controls.Add(notes,0,4);
        var reload=Theme.Button("↻ Actualizar croquis",true);controls.Controls.Add(reload,0,5);
        controls.Controls.Add(_siteDetails,0,6);
        _sites.SelectedIndexChanged+=(_,_)=>ShowSite();
        root.Controls.Add(_status,0,3);
        _zones.SelectedIndexChanged+=(_,_)=>{if(_zones.SelectedItem is Choice z){_map.SelectZone(z.Code);}};
        assign.Click+=async (_,_)=>await AssignAsync();
        reload.Click+=async (_,_)=>await ReloadAsync();
        Load+=async (_,_)=>await ReloadAsync();
    }
    private void ShowSite()
    {
        if(_sites.SelectedItem is not Choice site || !_siteData.TryGetValue(site.Id,out var details))return;
        var zone=_zones.Items.OfType<Choice>().FirstOrDefault(z=>z.Id==details.Zone);
        _siteDetails.TextAlign=ContentAlignment.TopLeft;
        _siteDetails.Text=$"Sector registrado: {zone?.Name??"Sin asignar"}\n{details.Address}\nUbicación de demostración, no verificada.";
        if(zone is not null)_map.SelectZone(zone.Code);
    }
    private async Task ReloadAsync()
    {
        var zoneReply=await _api.SendAsync(HttpMethod.Get,"api/management/zones");
        var siteReply=await _api.SendAsync(HttpMethod.Get,"api/sites");
        if(!zoneReply.Success||zoneReply.Data.ValueKind!=JsonValueKind.Array){_status.Text=zoneReply.Error;return;}
        if(!siteReply.Success||siteReply.Data.ValueKind!=JsonValueKind.Array){_status.Text=siteReply.Error;return;}
        _zones.Items.Clear();_sites.Items.Clear();_siteData.Clear();
        foreach(var z in zoneReply.Data.EnumerateArray())
            _zones.Items.Add(new Choice(z.GetProperty("id").GetGuid(),z.GetProperty("displayName").GetString()??"Sector",z.GetProperty("code").GetString()??""));
        foreach(var s in siteReply.Data.EnumerateArray())
        {
            if(s.TryGetProperty("isActive",out var active)&&active.ValueKind==JsonValueKind.False)continue;
            var id=s.GetProperty("id").GetGuid();
            Guid? zone=s.TryGetProperty("zoneId",out var z)&&z.ValueKind==JsonValueKind.String&&z.TryGetGuid(out var zid)?zid:null;
            var address=s.TryGetProperty("addressNote",out var a)?a.GetString()??"Sin dirección":"Sin dirección";
            _siteData[id]=(zone,address);
            _sites.Items.Add(new Choice(id,s.GetProperty("displayName").GetString()??"Sede",""));
        }
        _map.SetCounts(_zones.Items.OfType<Choice>().ToDictionary(z=>z.Code,z=>_siteData.Values.Count(d=>d.Zone==z.Id)));
        if(_zones.Items.Count>0)_zones.SelectedIndex=0;
        if(_sites.Items.Count>0)_sites.SelectedIndex=0;
        _status.Text=$"{_zones.Items.Count} sectores · {_sites.Items.Count} sedes activas · {_siteData.Values.Count(d=>d.Zone is null)} sin sector.";
    }
    private async Task AssignAsync()
    {
        if(_zones.SelectedItem is not Choice zone||_sites.SelectedItem is not Choice site)return;
        var response=await _api.SendAsync(HttpMethod.Put,$"api/sites/{site.Id}/zone",new{zoneId=zone.Id});
        if(response.Success){await ReloadAsync();_status.Text="Sede vinculada al sector conceptual: "+zone.Name;}else _status.Text=response.Error;
    }
    private sealed class AtlasMap:Control
    {
        private string _selected="N";
        private Dictionary<string,int> _counts=new();
        public void SetCounts(Dictionary<string,int> counts){_counts=counts;Invalidate();}
        public void SelectZone(string code) { _selected=code; Invalidate(); }
        public AtlasMap(){DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            var area=ClientRectangle;area.Inflate(-24,-24);
            if(area.Width<100||area.Height<100)return;
            int midX=area.Left+area.Width/2;int midY=area.Top+area.Height/2;
            // Four conceptual directional sectors. Never confuse them with polygons on a real map.
            var boxes=new[]{
                ("N","Norte",new Rectangle(area.Left+area.Width/4,area.Top,area.Width/2,area.Height/2),Color.FromArgb(230,205,250)),
                ("S","Sur",new Rectangle(area.Left+area.Width/4,midY,area.Width/2,area.Bottom-midY),Color.FromArgb(215,239,226)),
                ("O","Oeste",new Rectangle(area.Left,area.Top+area.Height/4,area.Width/4,area.Height/2),Color.FromArgb(249,218,237)),
                ("E","Este",new Rectangle(area.Left+3*area.Width/4,area.Top+area.Height/4,area.Width-area.Width*3/4,area.Height/2),Color.FromArgb(218,229,248))};
            using var baseBrush=new SolidBrush(Color.FromArgb(248,246,252));g.FillRectangle(baseBrush,area);
            foreach(var (code,title,rect,color) in boxes)
            {
                using var brush=new SolidBrush(color);g.FillRectangle(brush,rect);
                using var border=new Pen(_selected==code?Theme.Violet:Color.FromArgb(186,172,202),_selected==code?4:1.5f);
                g.DrawRectangle(border,rect);
                using var font=new Font("Segoe UI",15,FontStyle.Bold);
                using var ink=new SolidBrush(Theme.Plum);
                var label=$"{title}\n{_counts.GetValueOrDefault(code)} sedes";
                var sz=g.MeasureString(label,font);
                g.DrawString(label,font,ink,rect.Left+(rect.Width-sz.Width)/2f,rect.Top+(rect.Height-sz.Height)/2f);
            }
            using var f=new Font("Segoe UI",9,FontStyle.Regular);
            using var ink2=new SolidBrush(Theme.Muted);
            g.DrawString("CROQUIS ILUSTRATIVO · AH-2026 · NO CARTOGRÁFICO",f,ink2,area.Left+10,area.Bottom-24);
        }
    }
}
