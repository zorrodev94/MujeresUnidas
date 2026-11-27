using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Presentation;

/// <summary>Search institutional profiles including Owner. Does not expose Community personal data.</summary>
public sealed class InstitutionalDirectoryPanel:UserControl
{
    private readonly ApiConnection _api;
    private readonly TextBox _search=Theme.Input("Buscar nombre, cargo o perfil...");
    private readonly DataGridView _profiles=new(){Dock=DockStyle.Fill,ReadOnly=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
        AllowUserToAddRows=false,MultiSelect=false};
    private readonly Label _detail=Theme.Label("Selecciona un perfil para consultar sus relaciones institucionales.",12);
    private readonly Label _feedback=Theme.Label("Perfiles de Gestión · separadas de Comunidad",10,false,Theme.Muted);
    public InstitutionalDirectoryPanel(ApiConnection api)
    {
        _api=api;Dock=DockStyle.Fill;BackColor=Theme.Background;
        var root=Theme.Rows((88,SizeType.Absolute),(58,SizeType.Absolute),(0,SizeType.Percent),(70,SizeType.Absolute),(58,SizeType.Absolute),(40,SizeType.Absolute));
        Controls.Add(root);root.Controls.Add(Theme.BannerTitle("Buscar · perfiles institucionales",25),0,0);
        var toolbar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,80));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
        toolbar.Controls.Add(_search,0,0);var refresh=Theme.Button("↻ Buscar",true);toolbar.Controls.Add(refresh,1,0);root.Controls.Add(toolbar,0,1);
        Theme.StyleGrid(_profiles);_profiles.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        _profiles.Columns.Add("Id","ID");_profiles.Columns[0].Visible=false;
        _profiles.Columns.Add("Name","Nombre");_profiles.Columns.Add("Role","Cargo");
        _profiles.Columns.Add("Followers","Seguidores");_profiles.Columns.Add("Following","Seguidos");_profiles.Columns.Add("Followed","Siguiendo");
        root.Controls.Add(_profiles,0,2);root.Controls.Add(_detail,0,3);
        var buttons=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var follow=Theme.Button("♡ Seguir / dejar de seguir");var messages=Theme.Button("Mensajes: abrir panel",true);
        buttons.Controls.Add(follow,0,0);buttons.Controls.Add(messages,1,0);root.Controls.Add(buttons,0,4);
        _search.TextChanged+=async (_,_)=>await ReloadAsync();refresh.Click+=async (_,_)=>await ReloadAsync();
        _profiles.SelectionChanged+=(_,_)=>
        {
            if(_profiles.SelectedRows.Count==0)return;
            var row=_profiles.SelectedRows[0];
            _detail.Text=$"{row.Cells["Name"].Value} · {row.Cells["Role"].Value}  |  {row.Cells["Followers"].Value} seguidores, {row.Cells["Following"].Value} seguidos";
        };
        follow.Click+=async (_,_)=>
        {
            if(_profiles.SelectedRows.Count==0||!Guid.TryParse(_profiles.SelectedRows[0].Cells[0].Value?.ToString(),out var id))return;
            var isFollowing=_profiles.SelectedRows[0].Cells[5].Value?.ToString()=="Sí";
            var result=await _api.SendAsync(isFollowing?HttpMethod.Delete:HttpMethod.Post,$"api/management/social/profiles/{id}/follow");
            if(!result.Success)MessageBox.Show(result.Error);await ReloadAsync();
        };
        messages.Click+=(_,_)=>MessageBox.Show("Abre Mensajes internos en el menú lateral para elegir a una colega. Los chats de Comunidad siguen aislados.");
        Load+=async (_,_)=>await ReloadAsync();
        root.Controls.Add(_feedback,0,5);
    }
    private async Task ReloadAsync()
    {
        var res=await _api.SendAsync(HttpMethod.Get,"api/management/social/profiles?q="+Uri.EscapeDataString(_search.Text.Trim()));
        _profiles.Rows.Clear();if(!res.Success){_feedback.Text=res.Error;return;}
        foreach(var p in res.Data.EnumerateArray())
            _profiles.Rows.Add(p.GetProperty("id").GetGuid(),p.GetProperty("displayName").GetString()??string.Empty,p.GetProperty("role").GetString()??string.Empty,
                p.GetProperty("followers").GetInt32(),p.GetProperty("following").GetInt32(),p.GetProperty("followed").GetBoolean()?"Sí":"No");
        _feedback.Text=$"{_profiles.Rows.Count} perfiles encontrados · incluye Owner";
    }
}
