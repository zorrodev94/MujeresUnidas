using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Presentation;

/// <summary>Catalog and accountable award workflow. Trophy is never awarded automatically.</summary>
public sealed class BadgesPanel:UserControl
{
    private readonly ApiConnection _api;
    private readonly ComboBox _badges=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox _users=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly TextBox _reason=Theme.Input("Motivo verificable (requerido)");
    private readonly Label _status=Theme.Label("Solo la Owner puede otorgar reconocimientos",11,false,Theme.Muted);
    private sealed record Choice(Guid Id,string Name)
    {
        public override string ToString()=>Name;
    }
    public BadgesPanel(ApiConnection api)
    {
        _api=api;Dock=DockStyle.Fill;BackColor=Theme.Background;
        var root=Theme.Rows((90,SizeType.Absolute),(90,SizeType.Absolute),(0,SizeType.Percent),(63,SizeType.Absolute));
        Controls.Add(root);root.Controls.Add(Theme.BannerTitle("Reconocimientos · medallas",25),0,0);
        root.Controls.Add(Theme.Label("Catálogo: Participante · Colaboración · Ganadora. Los reconocimientos no conceden permisos.",12),0,1);
        var frame=Theme.Card();root.Controls.Add(frame,0,2);
        var fields=Theme.Rows((50,SizeType.Absolute),(50,SizeType.Absolute),(55,SizeType.Absolute),(60,SizeType.Absolute),(0,SizeType.Percent));
        frame.Controls.Add(fields);
        fields.Controls.Add(_badges,0,0);fields.Controls.Add(_users,0,1);fields.Controls.Add(_reason,0,2);
        var grant=Theme.Button("🏅 Otorgar medalla");fields.Controls.Add(grant,0,3);
        fields.Controls.Add(Theme.Label("La medalla «Ganadora» requiere un resultado oficial. Cada otorgamiento se audita en SQL.",11,false,Theme.Muted),0,4);
        root.Controls.Add(_status,0,3);
        grant.Click+=async (_,_)=>
        {
            if(_badges.SelectedItem is not Choice badge||_users.SelectedItem is not Choice user)return;
            if(_reason.Text.Trim().Length<5){MessageBox.Show("Indica el motivo del reconocimiento.");return;}
            if(badge.Name.Contains("Ganadora",StringComparison.OrdinalIgnoreCase) &&
               MessageBox.Show("¿Existe un resultado oficial verificado para esta medalla?","Confirmación institucional",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            var r=await _api.SendAsync(HttpMethod.Post,"api/badges/grant",new{userId=user.Id,badgeId=badge.Id,reason=_reason.Text.Trim()});
            _status.Text=r.Success?"Reconocimiento registrado en SQL con historial.":r.Error;
        };
        Load+=async (_,_)=>await ReloadAsync();
    }
    private async Task ReloadAsync()
    {
        var badges=await _api.SendAsync(HttpMethod.Get,"api/badges");
        var accounts=await _api.SendAsync(HttpMethod.Get,"api/management/access");
        _badges.Items.Clear();_users.Items.Clear();
        if(!badges.Success||!accounts.Success){_status.Text=badges.Success?accounts.Error:badges.Error;return;}
        foreach(var b in badges.Data.EnumerateArray())
            if(b.GetProperty("isActive").GetBoolean())_badges.Items.Add(new Choice(b.GetProperty("id").GetGuid(),b.GetProperty("name").GetString()??"Medalla"));
        var seen=new HashSet<Guid>();
        foreach(var a in accounts.Data.EnumerateArray())
        {
            var id=a.GetProperty("accountId").GetGuid();
            if(seen.Add(id))_users.Items.Add(new Choice(id,a.GetProperty("displayName").GetString()??"Cuenta"));
        }
        if(_badges.Items.Count>0)_badges.SelectedIndex=0;
        if(_users.Items.Count>0)_users.SelectedIndex=0;
    }
}
