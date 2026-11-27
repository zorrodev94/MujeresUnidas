using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Presentation;

/// <summary>Second activation inbox, for institutional accounts only. Community inbox remains separate.</summary>
public sealed class StaffActivationsPanel:UserControl
{
    private readonly ApiConnection _api;
    private readonly DataGridView _people=new(){Dock=DockStyle.Fill,ReadOnly=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AllowUserToAddRows=false};
    private readonly Label _feedback=Theme.Label("Personal pendiente de revisión institucional.",11,false,Theme.Muted);
    public StaffActivationsPanel(ApiConnection api,Action communityView)
    {
        _api=api;Dock=DockStyle.Fill;BackColor=Theme.Background;
        var root=Theme.Rows((90,SizeType.Absolute),(66,SizeType.Absolute),(0,SizeType.Percent),(118,SizeType.Absolute),(55,SizeType.Absolute));
        Controls.Add(root);root.Controls.Add(Theme.BannerTitle("Centro de activaciones · Gestión",24),0,0);
        var tabs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
        tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        var community=Theme.Button("Comunidad",true);community.Click+=(_,_)=>communityView();
        tabs.Controls.Add(community,0,0);tabs.Controls.Add(Theme.Button("Gestión · seleccionado"),1,0);
        var reload=Theme.Button("↻ Actualizar",true);reload.Click+=async (_,_)=>await ReloadAsync();tabs.Controls.Add(reload,2,0);
        root.Controls.Add(tabs,0,1);
        Theme.StyleGrid(_people);_people.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        _people.Columns.Add("Id","ID");_people.Columns[0].Visible=false;
        _people.Columns.Add("Name","Nombre");_people.Columns.Add("Email","Correo");_people.Columns.Add("Role","Cargo");_people.Columns.Add("Status","Estado");
        root.Controls.Add(_people,0,2);
        
        var buttons=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
        for(int i=0;i<3;i++)buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        var verify=Theme.Button("✓ Verificar identidad (demo)");
        var activate=Theme.Button("Activar personal");
        var cross=Theme.Button("Autorizar en Comunidad",true);
        buttons.Controls.Add(verify,0,0);buttons.Controls.Add(activate,1,0);buttons.Controls.Add(cross,2,0);root.Controls.Add(buttons,0,4);
        root.Controls.Add(_feedback,0,3);
        var selected=Guid.Empty;
        _people.SelectionChanged+=(_,_)=>
        {
            selected=_people.SelectedRows.Count>0&&Guid.TryParse(_people.SelectedRows[0].Cells[0].Value?.ToString(),out var id)?id:Guid.Empty;
        };
        verify.Click+=async (_,_)=>
        {
            if(selected==Guid.Empty)return;
            if(MessageBox.Show("¿Registrar verificación ficticia sin almacenar documentos?", "Confirmar",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            var r=await _api.SendAsync(HttpMethod.Post,$"api/management/staff/{selected}/verify-demo");
            _feedback.Text=r.Success?"Verificación ficticia registrada.":r.Error;
        };
        activate.Click+=async (_,_)=>
        {
            if(selected==Guid.Empty)return;
            var r=await _api.SendAsync(HttpMethod.Post,$"api/management/staff/{selected}/activate");
            _feedback.Text=r.Success?"Personal activado.":r.Error;
            await ReloadAsync();
        };
        cross.Click+=async (_,_)=>
        {
            if(selected==Guid.Empty)return;
            var r=await _api.SendAsync(HttpMethod.Post,$"api/management/access/{selected}/community-professional");
            _feedback.Text=r.Success?"Perfil profesional comunitario habilitado sin copiar fotos.":r.Error;
            await ReloadAsync();
        };
        Load+=async (_,_)=>await ReloadAsync();
    }
    private async Task ReloadAsync()
    {
        var res=await _api.SendAsync(HttpMethod.Get,"api/management/staff");_people.Rows.Clear();
        if(!res.Success){_feedback.Text=res.Error;return;}
        foreach(var p in res.Data.EnumerateArray())
            _people.Rows.Add(p.GetProperty("id").GetGuid(),p.GetProperty("displayName").GetString()??string.Empty,
                p.GetProperty("email").GetString()??string.Empty,p.GetProperty("role").GetString()??string.Empty,
                p.GetProperty("status").GetString()??string.Empty);
        _feedback.Text=$"{_people.Rows.Count} cuentas institucionales registradas.";
    }
}
