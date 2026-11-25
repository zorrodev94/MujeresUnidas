using System.Text.Json;
using MujeresUnidas.Gestion.Connection;
using MujeresUnidas.Gestion.Models.User.Subclasses;

namespace MujeresUnidas.Gestion.Presentation
{
/// <summary>Institutional dashboard with separate community activation and staff registration workflows.</summary>
public sealed class DashboardForm : Form
{
    private readonly ApiConnection _api;
    private readonly OwnerUser _ownerModel;
    private readonly ManagementUser _management;
    private readonly CommunityMember _community;
    private readonly Panel _contentHost;
    private DataGridView? _accounts;
    private Label? _selection;
    private Label? _feedback;
    private string _selectedId = string.Empty;
    private readonly bool _canReview;
    private readonly bool _owner;

    public DashboardForm(ApiConnection api)
    {
        _api=api;
        _ownerModel=new OwnerUser(api);
        _management=new ManagementUser(api);
        _community=new CommunityMember(api);
        Text="Mujeres Unidas · Panel institucional";
        BackColor=Theme.Background;
        AutoScaleMode=AutoScaleMode.Dpi;
        WindowState=FormWindowState.Maximized;
        MinimumSize=new Size(1050,690);
        string kind=UserField("kind");
        string role=UserField("role");
        _owner=kind=="Owner";
        _canReview=_owner || (kind=="Staff" && role=="Administrator");

        // Layout cells, not overlapping Dock controls: sidebar never covers the content.
        var root=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=1,ColumnCount=2,Margin=Padding.Empty};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,245));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        Controls.Add(root);
        var nav=new Panel {Dock=DockStyle.Fill,BackColor=Theme.Sidebar,Padding=new Padding(13,25,13,15),AutoScroll=true};
        root.Controls.Add(nav,0,0);
        var menu=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=0};
        menu.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        nav.Controls.Add(menu);
        var mark=Theme.Picture("mu_mark.png");mark.Height=130;
        AddNavControl(mark,145);
        AddNavControl(Theme.Label("Mujeres Unidas\nGestión",17,true,Color.White),84);
        AddNav("⌂  Inicio",async()=>await ShowHomeAsync());
        if(_canReview) AddNav("✓  Activaciones",async()=>await ShowActivationsAsync());
        if(_owner) AddNav("+  Altas de personal",async()=>await ShowStaffAsync());
        AddNav("▣  Citas · próximamente",()=>Future("Citas"));
        AddNav("▤  Casos · próximamente",()=>Future("Casos"));
        AddNav("▥  Reportes · próximamente",()=>Future("Reportes"));
        AddNav("◉  Perfil",ShowProfile);
        AddNav("↪  Cerrar sesión",()=>{_api.SignOut();Close();});
        _contentHost=new Panel {Dock=DockStyle.Fill,BackColor=Theme.Background,Padding=new Padding(17),AutoScroll=true};
        root.Controls.Add(_contentHost,1,0);
        Shown+=async (_,_)=>await ShowHomeAsync();

        void AddNavControl(Control control,int height)
        {
            control.Margin=new Padding(3,4,3,5);
            control.Dock=DockStyle.Fill;
            menu.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            menu.Controls.Add(control,0,menu.RowCount++);
        }
        void AddNav(string title,Action action)
        {
            var btn=Theme.Button(title,true);
            btn.TextAlign=ContentAlignment.MiddleLeft;
            btn.BackColor=Theme.Sidebar;btn.ForeColor=Color.White;
            btn.FlatAppearance.BorderSize=0;
            btn.Click+=(_,_)=>action();
            AddNavControl(btn,48);
        }
    }

    #region Task
    private string UserField(string name)
    {
        var user=_api.CurrentUser;
        return user.ValueKind==JsonValueKind.Object && user.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String
            ? value.GetString()??string.Empty:string.Empty;
    }
    private void Present(Control view)
    {
        _contentHost.Controls.Clear();
        view.Dock=DockStyle.Fill;
        _contentHost.Controls.Add(view);
    }
    private static TableLayoutPanel CreateRows(params (float Height,SizeType Unit)[] rows) => Theme.Rows(rows);
    private static Panel Surface() => Theme.Card();
    private static Label Section(string caption,int size=20) => Theme.Label(caption,size,true);
    private static void Future(string name) => MessageBox.Show($"{name} se implementará en una revisión posterior. No se muestran datos simulados como si fueran reales.","Mujeres Unidas · R1.1",MessageBoxButtons.OK,MessageBoxIcon.Information);

    private async Task ShowHomeAsync()
    {
        var container=CreateRows((142,SizeType.Absolute),(136,SizeType.Absolute),(0,SizeType.Percent));
        Present(container);
        var heading=new Panel {Dock=DockStyle.Fill,BackgroundImage=Theme.LoadImage("mu_header.png"),BackgroundImageLayout=ImageLayout.Stretch,Padding=new Padding(24,18,15,10)};
        var label=Theme.Label("Panel institucional",28,true);label.Dock=DockStyle.Top;label.Height=60;
        heading.Controls.Add(label);
        var sub=Theme.Label("Mujeres Unidas · Gestión de activaciones y acceso comunitario",12);
        sub.Dock=DockStyle.Top;sub.Height=28;heading.Controls.Add(sub);sub.BringToFront();label.BringToFront();
        container.Controls.Add(heading,0,0);

        var metrics=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=4,RowCount=1};
        for(int i=0;i<4;i++)metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        var pending=Metric(metrics,0,"Solicitudes pendientes","—","◎");
        var active=Metric(metrics,1,"Cuentas activas","—","✓");
        var staff=Metric(metrics,2,"Personal institucional","—","♙");
        var blocked=Metric(metrics,3,"Cuentas bloqueadas","—","⊘");
        container.Controls.Add(metrics,0,1);

        var middle=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));
        var welcome=Surface();
        var welcomeRows=CreateRows((76,SizeType.Absolute),(126,SizeType.Absolute),(0,SizeType.Percent));
        welcome.Controls.Add(welcomeRows);
        welcomeRows.Controls.Add(Theme.Label("¡Bienvenida a Gestión!",23,true),0,0);
        welcomeRows.Controls.Add(Theme.Label("Desde aquí se revisan solicitudes y se autoriza el acceso a la comunidad.\nLas citas y los casos estarán disponibles en próximas revisiones.",12),0,1);
        welcomeRows.Controls.Add(Theme.Picture("mu_women.png"),0,2);
        middle.Controls.Add(welcome,0,0);
        var recent=Surface();
        var recentRows=CreateRows((60,SizeType.Absolute),(0,SizeType.Percent));
        recent.Controls.Add(recentRows);
        recentRows.Controls.Add(Theme.Label("Actividad reciente",19,true),0,0);
        var activity=Theme.Label("Consultando auditoría de Server...",11);
        activity.TextAlign=ContentAlignment.TopLeft;
        recentRows.Controls.Add(activity,0,1);
        middle.Controls.Add(recent,1,0);
        container.Controls.Add(middle,0,2);

        if(!_canReview)
        {
            activity.Text="Tu cuenta institucional tiene acceso al panel.\nLas operaciones de revisión están reservadas a las autoridades autorizadas.";
            return;
        }
        var result=await _management.GetSummaryAsync();
        if(!result.Success){activity.Text=result.Error;return;}
        var doc=result.Data;
        pending.Text=doc.PendingAccounts.ToString();
        active.Text=doc.ActiveAccounts.ToString();
        staff.Text=doc.StaffAccounts.ToString();
        blocked.Text=doc.BlockedAccounts.ToString();
        var lines=doc.RecentEvents.Select(e=>$"• {e.Action} — {e.Account}").ToList();
        activity.Text=lines.Count==0?"Todavía no hay movimientos registrados.\nAquí aparecerán activaciones y bloqueos reales.":string.Join("\n\n",lines);
    }
    private static Label Metric(TableLayoutPanel grid,int col,string name,string value,string glyph)
    {
        var panel=Surface();
        var rows=CreateRows((38,SizeType.Absolute),(60,SizeType.Absolute));
        panel.Controls.Add(rows);
        var label=Theme.Label($"{glyph}  {name}",10,true,Theme.Muted);
        var number=Theme.Label(value,30,true,Theme.Violet);
        rows.Controls.Add(label,0,0);rows.Controls.Add(number,0,1);
        grid.Controls.Add(panel,col,0);
        return number;
    }

    private async Task ShowActivationsAsync()
    {
        _selectedId=string.Empty;
        if(!_canReview){Future("Activaciones");return;}
        var root=CreateRows((85,SizeType.Absolute),(60,SizeType.Absolute),(0,SizeType.Percent),(190,SizeType.Absolute));
        Present(root);
        var heading=Theme.Label("Activación de cuentas",24,true);root.Controls.Add(heading,0,0);
        var filter=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,12));
        var search=Theme.Input("Buscar por nombre, CURP o correo");
        var state=new ComboBox {Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(11),Margin=new Padding(8,6,8,6)};
        state.Items.AddRange(["Todas","Pendientes","Activas","Bloqueadas"]);state.SelectedIndex=1;
        var reload=Theme.Button("↻",true);
        reload.Click+=async (_,_)=>await RefreshAccountsAsync();
        filter.Controls.Add(search,0,0);filter.Controls.Add(state,1,0);filter.Controls.Add(reload,2,0);
        root.Controls.Add(filter,0,1);
        _accounts=new DataGridView
        {
            Dock=DockStyle.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,
            ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
            MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible=false,Font=Theme.Text(10),RowTemplate={Height=37},
            EnableHeadersVisualStyles=false
        };
        _accounts.ColumnHeadersDefaultCellStyle.BackColor=Theme.Lavender;
        _accounts.ColumnHeadersDefaultCellStyle.ForeColor=Theme.Plum;
        _accounts.Columns.Add("Id","ID");_accounts.Columns[0].Visible=false;
        _accounts.Columns.Add("Nombre","Nombre");_accounts.Columns.Add("Curp","CURP (prueba)");
        _accounts.Columns.Add("Correo","Correo");_accounts.Columns.Add("Estado","Estado");
        _accounts.Columns.Add("Revision","Revisión");_accounts.Columns.Add("Fecha","Fecha UTC");
        _accounts.SelectionChanged+=(_,_)=>
        {
            if(_accounts.SelectedRows.Count==0){_selectedId="";if(_selection is not null)_selection.Text="Selecciona una solicitud.";return;}
            var row=_accounts.SelectedRows[0];
            _selectedId=row.Cells["Id"].Value?.ToString()??"";
            if(_selection is not null)_selection.Text=$"{row.Cells["Nombre"].Value}\n{row.Cells["Correo"].Value}\nCURP de prueba: {row.Cells["Curp"].Value}\nEstado: {row.Cells["Estado"].Value}\nRevisión: {row.Cells["Revision"].Value}";
        };
        root.Controls.Add(_accounts,0,2);
        var bottom=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));
        var details=Surface();
        var detailsRows=CreateRows((35,SizeType.Absolute),(0,SizeType.Percent));
        details.Controls.Add(detailsRows);
        detailsRows.Controls.Add(Theme.Label("Detalle de la solicitud",15,true),0,0);
        _selection=Theme.Label("Selecciona una solicitud.",11);
        _selection.TextAlign=ContentAlignment.TopLeft;
        detailsRows.Controls.Add(_selection,0,1);
        bottom.Controls.Add(details,0,0);
        var actions=Surface();
        var buttons=CreateRows((45,SizeType.Absolute),(45,SizeType.Absolute),(45,SizeType.Absolute),(0,SizeType.Percent));
        actions.Controls.Add(buttons);
        var activate=Theme.Button("✓  Activar cuenta");activate.Click+=async (_,_)=>await ChangeAsync("activate");
        var verify=Theme.Button("Registrar verificación (simulada)",true);
        verify.Click+=(_,_)=>MessageBox.Show("Prototipo: aquí se registraría la verificación presencial.\nNo se ha validado ninguna identidad ni enviado un correo.","Verificación simulada");
        var block=Theme.Button("⊘  Bloquear cuenta",true);block.Click+=async (_,_)=>await ChangeAsync("block");
        buttons.Controls.Add(activate,0,0);buttons.Controls.Add(verify,0,1);buttons.Controls.Add(block,0,2);
        _feedback=Theme.Label("Verificación presencial: solo simulación de hackathon.",9,false,Theme.Muted);
        buttons.Controls.Add(_feedback,0,3);
        bottom.Controls.Add(actions,1,0);
        root.Controls.Add(bottom,0,3);
        search.TextChanged+=(_,_)=>ApplyFilter();state.SelectedIndexChanged+=(_,_)=>ApplyFilter();
        await RefreshAccountsAsync();

        void ApplyFilter()
        {
            if(_accounts is null)return;
            _accounts.ClearSelection();
            _accounts.CurrentCell=null;
            foreach(DataGridViewRow row in _accounts.Rows)
            {
                string text=string.Join(" ",new[]{row.Cells["Nombre"].Value,row.Cells["Curp"].Value,row.Cells["Correo"].Value}.Select(v=>v?.ToString()??""));
                string status=row.Cells["Estado"].Value?.ToString()??"";
                bool matches=string.IsNullOrWhiteSpace(search.Text)||text.Contains(search.Text,StringComparison.OrdinalIgnoreCase);
                bool byStatus=state.SelectedIndex switch {1=>status=="Pending",2=>status=="Active",3=>status=="Blocked",_=>true};
                row.Visible=matches && byStatus;
            }
        }
        async Task RefreshAccountsAsync()
        {
            var response=await _community.GetAccountsAsync();
            if(!response.Success){if(_feedback is not null)_feedback.Text=response.Error;return;}
            if(_accounts is null)return;
            _accounts.Rows.Clear();
            foreach(var entry in response.Accounts)
                _accounts.Rows.Add(entry.Id.ToString(),entry.DisplayName,entry.Curp??string.Empty,
                    entry.Email,entry.Status,entry.ActivationStatus,entry.CreatedUtc.ToString("yyyy-MM-dd"));
            ApplyFilter();
            if(_feedback is not null)_feedback.Text=$"{_accounts.Rows.Count} registros recuperados · datos de prueba.";
        }
    }
    private async Task ChangeAsync(string operation)
    {
        if(!Guid.TryParse(_selectedId,out var id)){MessageBox.Show("Selecciona una cuenta de la lista.");return;}
        string verb=operation=="activate"?"activar":"bloquear";
        if(MessageBox.Show($"¿Confirmas {verb} esta cuenta?\n\nEn R1.1 se simula la verificación presencial. No se ha comprobado identidad real.",
            "Confirmar operación",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        bool success;
        string message;
        if(operation=="activate" && _owner)
        {
            var answer=await _ownerModel.AcceptAccountAsync(id);
            success=answer.Success;
            message=answer.Message;
        }
        else
        {
            var answer=operation=="activate"?await _community.ActivateAsync(id):await _community.BlockAsync(id);
            success=answer.Success;
            message=answer.Message;
        }
        MessageBox.Show(success?$"Operación confirmada: {verb}.":message,"Mujeres Unidas");
        if(success)await ShowActivationsAsync();
    }

    private async Task ShowStaffAsync()
    {
        if(!_owner){Future("Altas de personal");return;}
        var root=CreateRows((100,SizeType.Absolute),(0,SizeType.Percent));Present(root);
        root.Controls.Add(Theme.Label("Alta de personal institucional",24,true),0,0);
        var cols=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,61));
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39));
        root.Controls.Add(cols,0,1);
        var form=Surface();cols.Controls.Add(form,0,0);
        var fields=CreateRows((58,SizeType.Absolute),(56,SizeType.Absolute),(56,SizeType.Absolute),(56,SizeType.Absolute),
            (56,SizeType.Absolute),(60,SizeType.Absolute),(38,SizeType.Absolute),
            (56,SizeType.Absolute),(56,SizeType.Absolute),(0,SizeType.Percent));form.Controls.Add(fields);
        fields.Controls.Add(Theme.Label("Datos de la nueva cuenta",19,true),0,0);
        var name=Theme.Input("Nombre completo");fields.Controls.Add(name,0,1);
        var email=Theme.Input("Correo electrónico");fields.Controls.Add(email,0,2);
        var password=Theme.Input("Contraseña inicial (mínimo 10 caracteres)",true);fields.Controls.Add(password,0,3);
        var roles=new ComboBox {Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(11)};
        fields.Controls.Add(roles,0,4);
        var submit=Theme.Button("Crear cuenta institucional");fields.Controls.Add(submit,0,5);
        fields.Controls.Add(Theme.Label("Agregar nuevo puesto (sin permisos de administración)",10,false,Theme.Muted),0,6);
        var roleName=Theme.Input("Nombre del nuevo puesto");fields.Controls.Add(roleName,0,7);
        var addRole=Theme.Button("Agregar puesto al catálogo",true);fields.Controls.Add(addRole,0,8);
        var notice=Theme.Label("Los puestos vienen de SQL Server. No se envían correos.\nLos puestos nuevos no obtienen permisos de activación por defecto.",10,false,Theme.Muted);
        fields.Controls.Add(notice,0,9);
        var info=Surface();cols.Controls.Add(info,1,0);
        var infoRows=CreateRows((64,SizeType.Absolute),(200,SizeType.Absolute),(0,SizeType.Percent));info.Controls.Add(infoRows);
        infoRows.Controls.Add(Theme.Label("Permisos institucionales",17,true),0,0);
        var explanation=Theme.Label("Administradora: revisa cuentas y activaciones.\n\nLos demás puestos: acceso institucional básico. Sus expedientes y citas aún no existen.",11);
        explanation.TextAlign=ContentAlignment.TopLeft;
        infoRows.Controls.Add(explanation,0,1);
        infoRows.Controls.Add(Theme.Picture("mu_women.png"),0,2);

        async Task RefreshRolesAsync()
        {
            var result=await _ownerModel.GetRolesAsync();
            if(!result.Success){notice.Text=result.Error;return;}
            roles.Items.Clear();
            foreach(var option in result.Roles) roles.Items.Add(option);
            if(roles.Items.Count>0) roles.SelectedIndex=0;
        }

        submit.Click+=async (_,_)=>
        {
            if(roles.SelectedItem is not OwnerUser.RoleOption selected)
            {notice.Text="Selecciona un puesto antes de crear la cuenta.";return;}
            submit.Enabled=false;
            try
            {
                var result=await _ownerModel.CreateStaffAsync(new ManagementUser.StaffRegistrationData
                {
                    DisplayName=name.Text.Trim(),Email=email.Text.Trim(),Password=password.Text,
                    Role=selected.Code
                });
                notice.Text=result.Message;
                if(result.Success){name.Clear();email.Clear();password.Clear();}
            }
            finally {submit.Enabled=true;}
        };
        addRole.Click+=async (_,_)=>
        {
            addRole.Enabled=false;
            try
            {
                var result=await _ownerModel.AddRoleAsync(new OwnerUser.RoleRegistrationData
                {DisplayName=roleName.Text.Trim()});
                notice.Text=result.Message;
                if(result.Success){roleName.Clear();await RefreshRolesAsync();}
            }
            finally {addRole.Enabled=true;}
        };
        await RefreshRolesAsync();
    }

    private void ShowProfile()
    {
        var root=CreateRows((82,SizeType.Absolute),(0,SizeType.Percent));Present(root);
        root.Controls.Add(Theme.Label("Perfil institucional",24,true),0,0);
        var card=Surface();root.Controls.Add(card,0,1);
        var rows=CreateRows((64,SizeType.Absolute),(52,SizeType.Absolute),(52,SizeType.Absolute),(0,SizeType.Percent));card.Controls.Add(rows);
        rows.Controls.Add(Theme.Label("Cuenta en Gestión",19,true),0,0);
        rows.Controls.Add(Theme.Label($"Nombre: {UserField("displayName")}",12),0,1);
        rows.Controls.Add(Theme.Label($"Correo: {UserField("email")}  ·  Rol: {UserField("role")}",11),0,2);
        rows.Controls.Add(Theme.Label("La edición del perfil institucional y el cambio de contraseña están planificados para una revisión posterior.",11),0,3);
    }
    #endregion Task
}
}
