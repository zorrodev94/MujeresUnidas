using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Text.Json;
using MujeresUnidas.Gestion.Connection;
using MujeresUnidas.Gestion.Models.User.Subclasses;
using MujeresUnidas.Gestion.Models.Publications;
using GestionNotification = MujeresUnidas.Gestion.Models.Notifications.Notification;

namespace MujeresUnidas.Gestion.Presentation
{
/// <summary>Institutional dashboard with separate community activation and staff registration workflows.</summary>
[UsesData(typeof(CommunityMember.AccountData))]
[UsesData(typeof(CommunityMember.ActivationData))]
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
    private readonly Dictionary<Guid,CommunityMember.AccountData> _activationDetails=new();
    private ComboBox? _approvalRole;
    private readonly bool _canReview;
    private readonly bool _owner;
    private readonly System.Windows.Forms.Timer _securityTimer = new(){Interval=30000};
    private readonly ActivityWatcher _activityWatcher = new();
    private DateTime _lastActivityUtc=DateTime.UtcNow;
    private bool _locking;
    private sealed class ActivityWatcher : IMessageFilter
    {
        public Action? OnActivity {get;set;}
        public bool PreFilterMessage(ref Message m)
        {
            // Mouse, keyboard and touch-equivalent WinForms input.
            if(m.Msg is >= 0x0100 and <= 0x0109 or >= 0x0200 and <= 0x020D)
                OnActivity?.Invoke();
            return false;
        }
    }
    private sealed record SiteOption(Guid Id,string Name)
    {
        public override string ToString()=>Name;
    }
    /// <summary>Display safe staff attributes in the directory, never raw tuple/SQL identifiers.</summary>
    private sealed record StaffChoice(Guid Id,string Name,string Email,string Status,string Role)
    {
        public override string ToString()=>Name+"  ·  "+Role+"  ·  "+Spanish(Status);
    }

    public DashboardForm(ApiConnection api)
    {
        _api=api;
        _ownerModel=new OwnerUser(api);
        _management=new ManagementUser(api);
        _community=new CommunityMember(api);
        Text="Mujeres Unidas · Panel institucional";
        Icon=Theme.AppIcon();ShowIcon=true;ShowInTaskbar=true;
        FormBorderStyle=FormBorderStyle.None;
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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,222));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var window=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=Padding.Empty,Padding=Padding.Empty};
        window.RowStyles.Add(new RowStyle(SizeType.Absolute,49));
        window.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        window.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        Controls.Add(window);
        window.Controls.Add(CreateWindowBar(),0,0);
        window.Controls.Add(root,0,1);
        var nav=new Theme.DecoratedSidebar {Dock=DockStyle.Fill,BackColor=Theme.Sidebar,Padding=new Padding(13,25,13,15),AutoScroll=true};
        root.Controls.Add(nav,0,0);
        var menu=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=0};
        menu.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        nav.Controls.Add(menu);
        var mark=Theme.Picture("mu_mark.png");mark.Height=130;
        AddNavControl(mark,108);
        AddNavControl(Theme.Label("Mujeres Unidas\nGestión",15,true,Color.White),55);
        AddNav("Inicio",async()=>await ShowHomeAsync(),"menu_home.png");
        if(_canReview) AddNav("Activaciones",async()=>await ShowActivationsAsync(),"menu_bell.png");
        if(_owner) AddNav("Personal",async()=>await ShowStaffAsync(),"menu_group.png");
        if(_canReview) AddNav("Publicaciones",async()=>await ShowInstitutionalPostsAsync(),"menu_edit.png");
        AddNav("Mensajes",()=>Present(new ManagementMessagesPanel(_api)),"menu_messages.png");
        AddNav("Mi perfil",async()=>await ShowProfileAsync(),"menu_profile.png");
        if(_canReview) AddNav("Sedes",async()=>await ShowSitesAsync(),"menu_sites.png");
        if(_canReview) AddNav("Croquis Ahome",()=>Present(new AhomeMapPanel(_api,_canReview)),"menu_map.png");
        AddNav("Citas",async()=>await ShowAppointmentsAsync(),"menu_appointments.png");
        AddNav("Casos",async()=>await ShowCasesAsync(),"menu_cases.png");
        AddNav("Reportes",async()=>await ShowReportsAsync(),"menu_reports.png");
        if(_canReview) AddNav("Metas",()=>Present(new GoalsPanel(_api,_canReview)),"menu_goals.png");
        if(_owner) AddNav("Medallas",()=>Present(new BadgesPanel(_api)),"menu_awards.png");
        // Gestión owns institutional screens; Community has its own public profile navigation.
        AddNav("Cerrar sesión",async()=>{await _api.SignOutAsync();Close();});
        _contentHost=new Panel {Dock=DockStyle.Fill,BackColor=Theme.Background,Padding=new Padding(17),AutoScroll=true};
        root.Controls.Add(_contentHost,1,0);
        Shown+=async (_,_)=>await ShowHomeAsync();
        Load+=(_,_)=>MaximizedBounds=Screen.FromControl(this).WorkingArea;
        _activityWatcher.OnActivity=()=>_lastActivityUtc=DateTime.UtcNow;
        Application.AddMessageFilter(_activityWatcher);
        _securityTimer.Tick+=async (_,_)=>
        {
            if(_locking||DateTime.UtcNow-_lastActivityUtc<TimeSpan.FromMinutes(10))return;
            _locking=true;
            _securityTimer.Stop();
            Hide(); // Protect confidential cases before attempting a network operation.
            await _api.SignOutAsync();
            MessageBox.Show("La sesión institucional se bloqueó por 10 minutos sin actividad.",
                "Mujeres Unidas · Seguridad",MessageBoxButtons.OK,MessageBoxIcon.Information);
            Close();
        };
        _securityTimer.Start();
        FormClosed+=(_,_)=>
        {
            _securityTimer.Stop();_securityTimer.Dispose();
            Application.RemoveMessageFilter(_activityWatcher);
        };

        void AddNavControl(Control control,int height)
        {
            control.Margin=new Padding(3,4,3,5);
            control.Dock=DockStyle.Fill;
            menu.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            menu.Controls.Add(control,0,menu.RowCount++);
        }
        void AddNav(string title,Action action,string? iconFile=null)
        {
            var btn=Theme.Button(title,true);
            btn.TextAlign=ContentAlignment.MiddleLeft;
            btn.BackColor=Theme.Sidebar;btn.ForeColor=Color.White;
            btn.Padding=new Padding(12,0,0,0);
            btn.FlatAppearance.BorderSize=0;
            btn.FlatAppearance.MouseOverBackColor=Color.FromArgb(123,55,149);
            if(iconFile is not null)
            {
                btn.Image=Theme.LoadImage(iconFile);
                btn.ImageAlign=ContentAlignment.MiddleLeft;
                btn.TextImageRelation=TextImageRelation.ImageBeforeText;
            }
            btn.Click+=(_,_)=>action();
            AddNavControl(btn,42);
        }
    }

    #region Task
    /// <summary>Single notification entry point and consistent custom titlebar for the whole institutional app.</summary>
    private Control CreateWindowBar()
    {
        var bar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=7,RowCount=1,
            BackColor=Theme.Plum,Margin=Padding.Empty,Padding=new Padding(14,0,6,0)};
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,40));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,58));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,156));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
        bar.Controls.Add(Theme.Picture("mu_mark.png"),0,0);
        var label=Theme.Label("Mujeres Unidas · Gestión",12,true,Color.White);
        label.MouseDown+=DragBar;
        bar.Controls.Add(label,1,0);
        var search=TitleControl("",false);
        search.Image=Theme.LoadImage("menu_search.png");search.ImageAlign=ContentAlignment.MiddleCenter;
        search.AccessibleName="Buscar perfiles institucionales";
        search.Click+=(_,_)=>Present(new InstitutionalDirectoryPanel(_api));
        bar.Controls.Add(search,2,0);
        var bell=TitleControl("  Notificaciones",false);
        bell.Image=Theme.LoadImage("menu_bell.png");
        bell.ImageAlign=ContentAlignment.MiddleLeft;
        bell.TextImageRelation=TextImageRelation.ImageBeforeText;
        bell.Font=Theme.Text(9,true);
        bell.Click+=async (_,_)=>await ShowNotificationsAsync();
        bar.Controls.Add(bell,3,0);
        var minimize=TitleControl("—",false);
        minimize.Click+=(_,_)=>WindowState=FormWindowState.Minimized;
        bar.Controls.Add(minimize,4,0);
        var maximize=TitleControl("□",false);
        maximize.Click+=(_,_)=>WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;
        bar.Controls.Add(maximize,5,0);
        var close=TitleControl("✕",true);
        close.Click+=(_,_)=>Close();
        bar.Controls.Add(close,6,0);
        bar.MouseDown+=DragBar;
        return bar;
    }
    private static Button TitleControl(string label,bool danger)
    {
        var b=new Button{Text=label,Dock=DockStyle.Fill,BackColor=danger?Color.FromArgb(169,53,131):Theme.Plum,
            ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=Theme.Text(13,true),
            Margin=new Padding(3,6,3,6),TabStop=false,Cursor=Cursors.Hand};
        b.FlatAppearance.BorderSize=0;
        b.FlatAppearance.MouseOverBackColor=danger?Color.FromArgb(206,72,117):Theme.Violet;
        return b;
    }
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    private void DragBar(object? sender,MouseEventArgs e)
    {
        if(e.Button!=MouseButtons.Left)return;
        ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)0x2,IntPtr.Zero);
    }
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
        Theme.Polish(view);
    }
    private static TableLayoutPanel CreateRows(params (float Height,SizeType Unit)[] rows) => Theme.Rows(rows);
    private static Panel Surface() => Theme.Card();
    private static Label Section(string caption,int size=20) => Theme.Label(caption,size,true);
    private static void Future(string name) => MessageBox.Show($"{name} se implementará en una revisión posterior. No se muestran datos simulados como si fueran reales.","Mujeres Unidas · R2",MessageBoxButtons.OK,MessageBoxIcon.Information);

    private async Task ShowHomeAsync()
    {
        var container=CreateRows((142,SizeType.Absolute),(136,SizeType.Absolute),(0,SizeType.Percent));
        Present(container);
        var heading=new Theme.RoundedPanel {Dock=DockStyle.Fill,BackColor=Theme.Lavender,
            BackgroundImage=Theme.LoadImage("mu_banner_institutional.png"),BackgroundImageLayout=ImageLayout.Stretch,
            Padding=new Padding(24,18,15,10),Margin=new Padding(7,5,7,4)};
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

        var middle=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,5,0,3)};
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));
        var welcome=Surface();
        var welcomeRows=CreateRows((76,SizeType.Absolute),(126,SizeType.Absolute),(0,SizeType.Percent));
        welcome.Controls.Add(welcomeRows);
        welcomeRows.Controls.Add(Theme.Label("¡Bienvenida a Gestión!",23,true),0,0);
        welcomeRows.Controls.Add(Theme.Label("Desde aquí se revisan solicitudes y se autoriza el acceso a la comunidad.\nLos casos y citas se consultan desde el menú institucional autorizado.",12),0,1);
        welcomeRows.Controls.Add(Theme.Picture("mu_r31_people.png"),0,2);
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
        var shade=col switch
        {
            0=>Color.FromArgb(247,224,252),1=>Color.FromArgb(233,246,239),
            2=>Color.FromArgb(239,230,251),_=>Color.FromArgb(255,237,241)
        };
        var panel=Surface();panel.BackColor=shade;panel.Padding=new Padding(19,12,17,10);
        var rows=CreateRows((34,SizeType.Absolute),(0,SizeType.Percent));
        panel.Controls.Add(rows);
        rows.Controls.Add(Theme.Label(name,10,true,Theme.Plum),0,0);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=shade};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,74));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,26));
        var number=Theme.Label(value,29,true,Theme.Violet);
        layout.Controls.Add(number,0,0);
        var file=col switch{0=>"menu_bell.png",1=>"menu_home.png",2=>"menu_group.png",_=>"menu_heart.png"};
        layout.Controls.Add(Theme.Picture(file),1,0);
        rows.Controls.Add(layout,0,1);
        grid.Controls.Add(panel,col,0);
        return number;
    }

    private async Task ShowActivationsAsync()
    {
        _selectedId=string.Empty;
        if(!_canReview){Future("Activaciones");return;}
        var root=CreateRows((85,SizeType.Absolute),(60,SizeType.Absolute),(0,SizeType.Percent),(275,SizeType.Absolute));
        Present(root);
        var activationTabs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        activationTabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        activationTabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var communityTab=Theme.Button("Comunidad · seleccionado");
        var managementTab=Theme.Button("Gestión · personal",true);
        managementTab.Click+=(_,_)=>ShowStaffActivations();
        activationTabs.Controls.Add(communityTab,0,0);
        activationTabs.Controls.Add(managementTab,1,0);
        root.Controls.Add(activationTabs,0,0);
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
        Theme.StyleGrid(_accounts);
        _accounts.ColumnHeadersDefaultCellStyle.BackColor=Theme.Lavender;
        _accounts.ColumnHeadersDefaultCellStyle.ForeColor=Theme.Plum;
        _accounts.Columns.Add("Id","ID");_accounts.Columns[0].Visible=false;
        _accounts.Columns.Add("Nombre","Nombre");_accounts.Columns.Add("Curp","CURP (prueba)");
        _accounts.Columns.Add("Correo","Correo");_accounts.Columns.Add("Tipo","Rol actual");_accounts.Columns.Add("Estado","Estado");
        _accounts.Columns.Add("Revision","Revisión");_accounts.Columns.Add("Fecha","Fecha UTC");
        _accounts.SelectionChanged+=(_,_)=>
        {
            if(_accounts.SelectedRows.Count==0){_selectedId="";if(_selection is not null)_selection.Text="Selecciona una solicitud.";return;}
            var row=_accounts.SelectedRows[0];
            _selectedId=row.Cells["Id"].Value?.ToString()??"";
            if(_selection is not null)
            {
                var visit=Guid.TryParse(_selectedId,out var id)&&_activationDetails.TryGetValue(id,out var detail)
                    ?"\nVisita DEMO: "+(detail.ProposedVisitUtc?.ToLocalTime().ToString("g")??"Por programar")+
                    "\nSede: "+(detail.LocationLabel??"Por confirmar")+
                    "\nCredencial revisada (simulación): "+(detail.IdentityChecked?"Sí":"No") :"";
                _selection.Text=$"{row.Cells["Nombre"].Value}\n{row.Cells["Correo"].Value}\n"+
                    $"Rol actual: {row.Cells["Tipo"].Value}\nCURP de prueba: {row.Cells["Curp"].Value}\n"+
                    $"Estado: {row.Cells["Estado"].Value}\nRevisión: {row.Cells["Revision"].Value}"+visit;
            }
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
        var buttons=CreateRows((40,SizeType.Absolute),(40,SizeType.Absolute),(46,SizeType.Absolute),(46,SizeType.Absolute),(42,SizeType.Absolute),(0,SizeType.Percent));
        actions.Controls.Add(buttons);
        _approvalRole=new ComboBox {Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(11)};
        _approvalRole.Items.AddRange(["Aprobar como Miembro", "Aprobar como Staff"]);
        _approvalRole.SelectedIndex=0;
        buttons.Controls.Add(_approvalRole,0,0);
        var verifySite=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(9)};
        verifySite.Items.Add("Selecciona sede de verificación");verifySite.SelectedIndex=0;
        buttons.Controls.Add(verifySite,0,1);
        var activate=Theme.Button("✓  Activar con rol elegido");activate.Click+=async (_,_)=>await ChangeAsync("activate");
        var verify=Theme.Button("Registrar verificación de credencial (demo)",true);
        verify.Click+=async (_,_)=>
        {
            if(!Guid.TryParse(_selectedId,out var selected))return;
            if(verifySite.SelectedItem is not SiteOption currentSite)
            {MessageBox.Show("Selecciona una sede administrativa ficticia para registrar la verificación.");return;}
            if(MessageBox.Show("¿Registras que en la DEMOSTRACIÓN ya revisaste la credencial ficticia?\n"+
                "No se validan documentos reales.","Verificación simulada",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            var reply=await _api.SendAsync(HttpMethod.Post,$"api/management/accounts/{selected}/verify-at-site",new{siteId=currentSite.Id});
            MessageBox.Show(reply.Success?"Verificación simulada registrada y auditada.":reply.Error);
            await ShowActivationsAsync();
        };
        var block=Theme.Button("⊘  Bloquear cuenta",true);block.Click+=async (_,_)=>await ChangeAsync("block");
        buttons.Controls.Add(activate,0,2);buttons.Controls.Add(verify,0,3);buttons.Controls.Add(block,0,4);
        _feedback=Theme.Label("Verificación presencial: solo simulación de hackathon.",9,false,Theme.Muted);
        buttons.Controls.Add(_feedback,0,5);
        bottom.Controls.Add(actions,1,0);
        root.Controls.Add(bottom,0,3);
        search.TextChanged+=(_,_)=>ApplyFilter();state.SelectedIndexChanged+=(_,_)=>ApplyFilter();
        await RefreshAccountsAsync();
        var availableSites=await _api.SendAsync(HttpMethod.Get,"api/sites");
        if(availableSites.Success)
            foreach(var site in availableSites.Data.EnumerateArray())
                if(site.GetProperty("isActive").GetBoolean() &&
                   site.GetProperty("siteType").GetString() is "Administrative" or "MultiService")
                    verifySite.Items.Add(new SiteOption(site.GetProperty("id").GetGuid(),site.GetProperty("displayName").GetString()??""));
        if(verifySite.Items.Count>1)verifySite.SelectedIndex=1;

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
            _accounts.Rows.Clear();_activationDetails.Clear();
            foreach(var item in response.Accounts)_activationDetails[item.Id]=item;
            foreach(var entry in response.Accounts)
                _accounts.Rows.Add(entry.Id.ToString(),entry.DisplayName,entry.Curp??string.Empty,
                    entry.Email,entry.CommunityRole,entry.Status,entry.ActivationStatus,entry.CreatedUtc.ToString("yyyy-MM-dd"));
            ApplyFilter();
            if(_feedback is not null)_feedback.Text=$"{_accounts.Rows.Count} registros recuperados · datos de prueba.";
        }
    }
    private async Task ChangeAsync(string operation)
    {
        if(!Guid.TryParse(_selectedId,out var id)){MessageBox.Show("Selecciona una cuenta de la lista.");return;}
        string verb=operation=="activate"?"activar":"bloquear";
        string role=_approvalRole?.SelectedIndex==1?"Staff":"Member";
        if(MessageBox.Show($"¿Confirmas {verb} esta cuenta?\n\n{(operation=="activate"?"Se asignará el rol: "+role:"El acceso quedará bloqueado")}. La verificación presencial es simulada.",
            "Confirmar operación",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        bool success;
        string message;
        if(operation=="activate" && _owner)
        {
            var answer=await _ownerModel.AcceptAccountAsync(id,role);
            success=answer.Success;
            message=answer.Message;
        }
        else
        {
            var answer=operation=="activate"?await _community.ActivateAsync(id,role):await _community.BlockAsync(id);
            success=answer.Success;
            message=answer.Message;
        }
        MessageBox.Show(success?$"Operación confirmada: {verb}.":message,"Mujeres Unidas");
        if(success)await ShowActivationsAsync();
    }

    private void ShowStaffActivations() => Present(new StaffActivationsPanel(_api,async ()=>await ShowActivationsAsync()));

    private async Task ShowStaffAsync()
    {
        if(!_owner){Future("Altas de personal");return;}
        var root=CreateRows((78,SizeType.Absolute),(62,SizeType.Absolute),(0,SizeType.Percent));Present(root);
        root.Controls.Add(Theme.BannerTitle("Personal institucional · Ahome",25),0,0);
        var tabs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(8,0,8,0)};
        tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var registerTab=Theme.Button("+ Registrar personal");
        var directoryTab=Theme.Button("Directorio y sedes",true);
        tabs.Controls.Add(registerTab,0,0);tabs.Controls.Add(directoryTab,1,0);
        root.Controls.Add(tabs,0,1);
        var host=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Background};
        root.Controls.Add(host,0,2);
        var form=Surface();host.Controls.Add(form);
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
        var info=Surface();host.Controls.Add(info);info.Visible=false;
        registerTab.Click+=(_,_)=>{info.Visible=false;form.Visible=true;form.BringToFront();
            registerTab.BackColor=Theme.Violet;registerTab.ForeColor=Color.White;
            directoryTab.BackColor=Theme.Pink;directoryTab.ForeColor=Theme.Plum;};
        directoryTab.Click+=(_,_)=>{form.Visible=false;info.Visible=true;info.BringToFront();
            directoryTab.BackColor=Theme.Violet;directoryTab.ForeColor=Color.White;
            registerTab.BackColor=Theme.Pink;registerTab.ForeColor=Theme.Plum;};
        var infoRows=CreateRows((64,SizeType.Absolute),(85,SizeType.Absolute),(0,SizeType.Percent));info.Controls.Add(infoRows);
        infoRows.Controls.Add(Theme.Label("Permisos institucionales",17,true),0,0);
        var explanation=Theme.Label("Administradora: revisa cuentas y activaciones.\nEl acceso a expedientes y citas depende del rol y de las asignaciones autorizadas.",11);
        explanation.TextAlign=ContentAlignment.TopLeft;
        infoRows.Controls.Add(explanation,0,1);
        var directory=CreateRows((44,SizeType.Absolute),(53,SizeType.Absolute),(0,SizeType.Percent),
            (50,SizeType.Absolute),(50,SizeType.Absolute),(59,SizeType.Absolute));
        infoRows.Controls.Add(directory,0,2);
        directory.Controls.Add(Theme.Label("Directorio institucional · verificación pendiente",12,true),0,0);
        var staffSearch=Theme.Input("Buscar personal por nombre o correo");directory.Controls.Add(staffSearch,0,1);
        var staffList=new ListBox{Dock=DockStyle.Fill,Font=Theme.Text(11),IntegralHeight=false,
            BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Plum,ItemHeight=32};
        directory.Controls.Add(staffList,0,2);
        var verifyStaff=Theme.Button("Registrar verificación presencial (demo)",true);
        var approveStaff=Theme.Button("✓ Activar personal verificado");
        directory.Controls.Add(verifyStaff,0,3);directory.Controls.Add(approveStaff,0,4);
        var placement=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        placement.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,56));
        placement.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,44));
        var sitePicker=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(10),Margin=new Padding(4,5,4,5)};
        var linkSite=Theme.Button("Asignar sede",true);
        placement.Controls.Add(sitePicker,0,0);placement.Controls.Add(linkSite,1,0);
        directory.Controls.Add(placement,0,5);
        async Task ReloadSitesAsync()
        {
            sitePicker.Items.Clear();
            var response=await _api.SendAsync(HttpMethod.Get,"api/sites");
            if(!response.Success){notice.Text=response.Error;return;}
            foreach(var item in response.Data.EnumerateArray())
                if(item.GetProperty("isActive").GetBoolean())
                    sitePicker.Items.Add(new SiteOption(item.GetProperty("id").GetGuid(),
                        item.GetProperty("displayName").GetString()??"Sede"));
            if(sitePicker.Items.Count>0)sitePicker.SelectedIndex=0;
        }
        linkSite.Click+=async (_,_)=>
        {
            if(staffList.SelectedItem is not StaffChoice staff || sitePicker.SelectedItem is not SiteOption site)
            {notice.Text="Selecciona una integrante del personal y una sede.";return;}
            if(staff.Status!="Active") {notice.Text="Activa primero la cuenta institucional.";return;}
            var response=await _api.SendAsync(HttpMethod.Put,$"api/sites/{site.Id}/staff",new{staffId=staff.Id});
            notice.Text=response.Success?$"{staff.Name} vinculada a {site.Name}.":response.Error;
        };
        var staffEntries=new List<StaffChoice>();
        async Task ReloadStaffAsync()
        {
            var reply=await _api.SendAsync(HttpMethod.Get,"api/management/staff");
            if(!reply.Success){notice.Text=reply.Error;return;}
            staffEntries.Clear();
            foreach(var entry in reply.Data.EnumerateArray())
                staffEntries.Add(new StaffChoice(entry.GetProperty("id").GetGuid(),entry.GetProperty("displayName").GetString()??"",
                    entry.GetProperty("email").GetString()??"",entry.GetProperty("status").GetString()??"",
                    entry.GetProperty("role").GetString()??""));
            ShowStaffFilter();
        }
        void ShowStaffFilter()
        {
            staffList.Items.Clear();
            foreach(var item in staffEntries.Where(x=>(x.Name+" "+x.Email).Contains(staffSearch.Text,StringComparison.OrdinalIgnoreCase)))
                staffList.Items.Add(item);
            if(staffList.Items.Count==0)staffList.Items.Add("No hay personal que coincida con la búsqueda.");
        }
        staffSearch.TextChanged+=(_,_)=>ShowStaffFilter();
        verifyStaff.Click+=async (_,_)=>
        {
            if(staffList.SelectedItem is not StaffChoice selected)return;
            if(MessageBox.Show("¿Confirmas la VERIFICACIÓN SIMULADA de la identidad institucional?",
                "Revisión de demostración",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            var reply=await _api.SendAsync(HttpMethod.Post,$"api/management/staff/{selected.Id}/verify-demo");
            notice.Text=reply.Success?"Identidad de demostración registrada; ya puedes activar.":reply.Error;
        };
        approveStaff.Click+=async (_,_)=>
        {
            if(staffList.SelectedItem is not StaffChoice selected)
            {MessageBox.Show("Selecciona personal del directorio.");return;}
            if(selected.Status!="Pending")
            {MessageBox.Show("Esa cuenta no tiene una activación pendiente.");return;}
            if(MessageBox.Show($"¿Confirmas activar a {selected.Name}?\n\n"+
                "La verificación simulada debe haberse registrado por separado.",
                "Confirmar activación institucional",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            var reply=await _api.SendAsync(HttpMethod.Post,$"api/management/staff/{selected.Id}/activate");
            notice.Text=reply.Success?"Cuenta activada. Debe cambiar su contraseña inicial.":reply.Error;
            await ReloadStaffAsync();
        };

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
                if(result.Success){name.Clear();email.Clear();password.Clear();await ReloadStaffAsync();}
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
        await ReloadStaffAsync();
        await ReloadSitesAsync();
    }

    private async Task ShowInstitutionalPostsAsync()
    {
        if(!_canReview)return;
        var posts=new InstitutionalPost(_api);
        var root=CreateRows((80,SizeType.Absolute),(0,SizeType.Percent),(310,SizeType.Absolute));Present(root);
        root.Controls.Add(Theme.Label("Revisión institucional · Guías, Empleos y Eventos",23,true),0,0);
        var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,MultiSelect=false,
            SelectionMode=DataGridViewSelectionMode.FullRowSelect,AllowUserToAddRows=false,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
        Theme.StyleGrid(grid);
        grid.Columns.Add("Id","Id");grid.Columns[0].Visible=false;
        grid.Columns.Add("Tipo","Tipo");grid.Columns.Add("Titulo","Título");
        grid.Columns.Add("Autor","Autor");grid.Columns.Add("Resumen","Resumen");
        root.Controls.Add(grid,0,1);
        var actions=Surface();root.Controls.Add(actions,0,2);
        var cols=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=3};
        for(int i=0;i<4;i++)cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        cols.RowStyles.Add(new RowStyle(SizeType.Absolute,45));
        cols.RowStyles.Add(new RowStyle(SizeType.Percent,55));
        cols.RowStyles.Add(new RowStyle(SizeType.Percent,45));
        actions.Controls.Add(cols);
        var approve=Theme.Button("✓ Aprobar");var reject=Theme.Button("✕ Rechazar",true);
        var reload=Theme.Button("↻ Actualizar",true);
        var deletions=Theme.Button("Solicitudes de retiro",true);
        deletions.Click+=async (_,_)=>await ShowPostDeletionRequestsAsync();
        cols.Controls.Add(approve,0,0);cols.Controls.Add(reject,1,0);cols.Controls.Add(reload,2,0);
        cols.Controls.Add(deletions,3,0);
        var detail=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,
            ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,
            Font=Theme.Text(10),ForeColor=Theme.Plum,
            Text="Selecciona una publicación para leer su contenido completo antes de decidir."};
        cols.SetColumnSpan(detail,4);cols.Controls.Add(detail,0,1);
        var gallery=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,
            BackColor=Theme.Background};
        cols.SetColumnSpan(gallery,4);cols.Controls.Add(gallery,0,2);
        var pendingPosts=new Dictionary<Guid,InstitutionalPost.PostData>();
        grid.SelectionChanged+=async (_,_)=>
        {
            if(grid.SelectedRows.Count==0 || !Guid.TryParse(grid.SelectedRows[0].Cells[0].Value?.ToString(),out var id)
                || !pendingPosts.TryGetValue(id,out var post))return;
            detail.Text=$"{post.Category}: {post.Title}\r\nAutora: {post.Author}\r\n{post.Summary}\r\n\r\n{post.Body}";
            foreach(Control previous in gallery.Controls.Cast<Control>().ToArray())
            {if(previous is PictureBox picture)picture.Image?.Dispose();previous.Dispose();}
            gallery.Controls.Clear();
            var ids=await posts.MediaIdsAsync(id);
            foreach(var mediaId in ids.Take(5))
            {
                var bytes=await posts.ImageAsync(id,mediaId);
                if(bytes is null)continue;
                try
                {
                    using var memory=new MemoryStream(bytes);
                    using var loaded=Image.FromStream(memory);
                    var preview=new PictureBox{Width=134,Height=100,SizeMode=PictureBoxSizeMode.Zoom,
                        BackColor=Color.White,Image=new Bitmap(loaded),Margin=new Padding(5)};
                    gallery.Controls.Add(preview);
                }
                catch(ArgumentException) { /* Skip unreadable uploaded media. */ }
            }
        };
        async Task RefreshAsync()
        {
            var reply=await posts.PendingAsync();
            if(!string.IsNullOrEmpty(reply.Error)){detail.Text=reply.Error;return;}
            grid.Rows.Clear();pendingPosts.Clear();
            foreach(var post in reply.Items)
            {
                pendingPosts[post.Id]=post;
                grid.Rows.Add(post.Id,post.Category,post.Title,post.Author,post.Summary);
            }
            detail.Text=$"{reply.Items.Count} publicaciones pendientes. Selecciona una para leer su texto íntegro.";
        }
        async Task ReviewAsync(bool accepted)
        {
            if(grid.SelectedRows.Count==0 || !Guid.TryParse(grid.SelectedRows[0].Cells[0].Value?.ToString(),out var id))
            {MessageBox.Show("Selecciona una publicación.");return;}
            if(MessageBox.Show(accepted?"¿Autorizar la publicación?":"¿Rechazar la publicación?",
                "Confirmar revisión",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            var answer=await posts.ReviewAsync(id,accepted,false);
            if(!answer.Success)MessageBox.Show(answer.Error);
            await RefreshAsync();
        }
        approve.Click+=async (_,_)=>await ReviewAsync(true);
        reject.Click+=async (_,_)=>await ReviewAsync(false);
        reload.Click+=async (_,_)=>await RefreshAsync();
        await RefreshAsync();
    }

    /// <summary>Institutional approval of deletion requests avoids immediate removal by a member.</summary>
    private async Task ShowPostDeletionRequestsAsync()
    {
        if(!_canReview)return;
        var root=CreateRows((80,SizeType.Absolute),(0,SizeType.Percent),(95,SizeType.Absolute));Present(root);
        root.Controls.Add(Theme.Label("Solicitudes de retiro de publicaciones",24,true),0,0);
        var table=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,
            RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
        Theme.StyleGrid(table);
        table.Columns.Add("Id","Solicitud");table.Columns[0].Visible=false;
        table.Columns.Add("Type","Categoría");table.Columns.Add("Title","Publicación");
        table.Columns.Add("When","Recibida");root.Controls.Add(table,0,1);
        var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
        for(int i=0;i<3;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        var approve=Theme.Button("✓ Autorizar retiro");
        var reject=Theme.Button("Rechazar retiro",true);
        var back=Theme.Button("Volver a publicaciones",true);
        actions.Controls.Add(approve,0,0);actions.Controls.Add(reject,1,0);actions.Controls.Add(back,2,0);
        root.Controls.Add(actions,0,2);
        async Task ReloadAsync()
        {
            var response=await _api.SendAsync(HttpMethod.Get,"api/posts/institutional/deletions/pending");
            table.Rows.Clear();if(!response.Success){MessageBox.Show(response.Error);return;}
            foreach(var item in response.Data.EnumerateArray())
                table.Rows.Add(item.GetProperty("id").GetGuid(),
                    item.GetProperty("category").GetString() ?? string.Empty,
                    item.GetProperty("title").GetString() ?? string.Empty,
                    item.GetProperty("requestedUtc").GetDateTime().ToLocalTime().ToString("g"));
        }
        async Task DecideAsync(bool permitted)
        {
            if(table.SelectedRows.Count==0 || !Guid.TryParse(table.SelectedRows[0].Cells[0].Value?.ToString(),out var id))
            {MessageBox.Show("Selecciona una solicitud de retiro.");return;}
            if(MessageBox.Show(permitted?"¿Aprobar el retiro de la publicación?":"¿Rechazar el retiro?",
                "Revisión institucional",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            var reply=await _api.SendAsync(HttpMethod.Post,$"api/posts/institutional/deletions/{id}/review",
                new{approved=permitted,featured=false});
            if(!reply.Success)MessageBox.Show(reply.Error);await ReloadAsync();
        }
        approve.Click+=async (_,_)=>await DecideAsync(true);
        reject.Click+=async (_,_)=>await DecideAsync(false);
        back.Click+=async (_,_)=>await ShowInstitutionalPostsAsync();
        await ReloadAsync();
    }
    /// <summary>
    /// R4.3 overview of operational account data already returned by the authorized server.
    /// Does not invent clinical statistics, case counts or publish personal records.
    /// </summary>
    private async Task ShowReportsAsync()
    {
        var root=CreateRows((72,SizeType.Absolute),(80,SizeType.Absolute),(155,SizeType.Absolute),(0,SizeType.Percent));
        Present(root);
        root.Controls.Add(Theme.Label("Reportes · resumen operativo",25,true),0,0);
        root.Controls.Add(Theme.Label("Indicadores de activaciones y cuentas desde Server. Los reportes clínicos no están habilitados.",11,false,Theme.Muted),0,1);
        if(!_canReview)
        {
            root.Controls.Add(Theme.Label("El resumen de cuentas es exclusivo de la autoridad administrativa.",15),0,2);
            return;
        }
        var response=await _management.GetSummaryAsync();
        if(!response.Success)
        {
            root.Controls.Add(Theme.Label(response.Error,13),0,2);
            return;
        }
        var stats=response.Data;
        var metrics=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1};
        for(int i=0;i<4;i++)metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        Metric(metrics,0,"Solicitudes pendientes",stats.PendingAccounts.ToString(),string.Empty);
        Metric(metrics,1,"Cuentas activas",stats.ActiveAccounts.ToString(),string.Empty);
        Metric(metrics,2,"Personal",stats.StaffAccounts.ToString(),string.Empty);
        Metric(metrics,3,"Cuentas bloqueadas",stats.BlockedAccounts.ToString(),string.Empty);
        root.Controls.Add(metrics,0,2);
        var recent=Surface();
        recent.Padding=new Padding(22,16,22,15);
        var panel=CreateRows((48,SizeType.Absolute),(0,SizeType.Percent),(55,SizeType.Absolute),(55,SizeType.Absolute));
        recent.Controls.Add(panel);
        panel.Controls.Add(Theme.Label("Actividad registrada",19,true),0,0);
        var activity=Theme.Label(stats.RecentEvents.Count==0?"Todavía no hay eventos recientes.":
            string.Join(Environment.NewLine+Environment.NewLine,
            stats.RecentEvents.Select(e=>e.Action+"  ·  "+e.Account)),11);
        activity.TextAlign=ContentAlignment.TopLeft;
        var eventsScroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
        activity.Dock=DockStyle.Top;activity.AutoSize=true;activity.MaximumSize=new Size(Math.Max(200,Width-420),0);
        eventsScroll.Controls.Add(activity);panel.Controls.Add(eventsScroll,0,1);
        var export=Theme.Button("Exportar resumen CSV",true);
        export.Click+=async (_,_)=>
        {
            using var dialog=new SaveFileDialog{Filter="CSV|*.csv",FileName="MU_Resumen_Cuentas.csv"};
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            var csv="Indicador,Valor"+Environment.NewLine+
                $"Solicitudes pendientes,{stats.PendingAccounts}"+Environment.NewLine+
                $"Cuentas activas,{stats.ActiveAccounts}"+Environment.NewLine+
                $"Personal institucional,{stats.StaffAccounts}"+Environment.NewLine+
                $"Cuentas bloqueadas,{stats.BlockedAccounts}"+Environment.NewLine;
            await File.WriteAllTextAsync(dialog.FileName,csv,System.Text.Encoding.UTF8);
        };
        panel.Controls.Add(export,0,2);
        root.Controls.Add(recent,0,3);
        var annual=Theme.Button("Ver indicadores anuales",true);
        panel.Controls.Add(annual,0,3);
        annual.Click+=async (_,_)=>await ShowAnnualCareAsync();
    }

    /// <summary>Real SQL aggregates: appointments completed are not confused with cases opened.</summary>
    private async Task ShowAnnualCareAsync()
    {
        if(!_canReview)return;
        var root=CreateRows((68,SizeType.Absolute),(65,SizeType.Absolute),(132,SizeType.Absolute),(0,SizeType.Percent));
        Present(root);
        root.Controls.Add(Theme.Label("Atenciones por año · resultados reales",24,true),0,0);
        var toolbar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        var year=new NumericUpDown{Minimum=2020,Maximum=2099,Value=DateTime.Today.Year,Dock=DockStyle.Fill,Font=Theme.Text(13)};
        var refresh=Theme.Button("↻ Consultar año",true);
        var back=Theme.Button("Volver a reportes",true);
        toolbar.Controls.Add(year,0,0);toolbar.Controls.Add(refresh,1,0);toolbar.Controls.Add(back,2,0);
        root.Controls.Add(toolbar,0,1);
        var metrics=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4};
        for(int i=0;i<4;i++)metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        var received=Metric(metrics,0,"Solicitudes recibidas","—","");
        var cases=Metric(metrics,1,"Casos abiertos","—","");
        var completed=Metric(metrics,2,"Citas cumplidas","—","");
        var goal=Metric(metrics,3,"Meta anual","—","");
        root.Controls.Add(metrics,0,2);
        var chart=Surface();root.Controls.Add(chart,0,3);
        var layout=CreateRows((44,SizeType.Absolute),(0,SizeType.Percent),(52,SizeType.Absolute));chart.Controls.Add(layout);
        layout.Controls.Add(Theme.Label("Citas programadas y cumplidas por mes",17,true),0,0);
        var bars=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=12,RowCount=1,BackColor=Color.White};
        for(int i=0;i<12;i++)bars.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/12));
        layout.Controls.Add(bars,0,1);
        var controls=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        var bySite=Theme.Button("Detalle por sede",true);
        var siteLines="Sin datos de sedes.";
        bySite.Click+=(_,_)=>MessageBox.Show(siteLines,"Desglose por sede · datos reales");
        var goalValue=new NumericUpDown{Minimum=0,Maximum=1000000,Dock=DockStyle.Fill,Font=Theme.Text(11)};
        var save=Theme.Button("Administrar en Metas",true);save.Enabled=_canReview;
        controls.Controls.Add(bySite,0,0);controls.Controls.Add(goalValue,1,0);controls.Controls.Add(save,2,0);
        layout.Controls.Add(controls,0,2);
        async Task LoadAsync()
        {
            var response=await _api.SendAsync(HttpMethod.Get,$"api/management/care-analytics/{(int)year.Value}");
            if(!response.Success){MessageBox.Show(response.Error);return;}
            var data=response.Data;
            received.Text=data.GetProperty("requestsReceived").GetInt32().ToString("N0");
            cases.Text=data.GetProperty("casesOpened").GetInt32().ToString("N0");
            completed.Text=data.GetProperty("sessionsCompleted").GetInt32().ToString("N0");
            var target=data.GetProperty("targetCompletedSessions").GetInt32();
            goal.Text=target==0?"Sin meta":target.ToString("N0");goalValue.Value=target;
            bars.Controls.Clear();
            siteLines=string.Join(Environment.NewLine+Environment.NewLine,data.GetProperty("sites").EnumerateArray().Select(site=>
                $"{site.GetProperty("siteName").GetString()} · {site.GetProperty("casesOpened").GetInt32()} casos, " +
                $"{site.GetProperty("scheduled").GetInt32()} citas programadas, {site.GetProperty("completed").GetInt32()} cumplidas"));
            var months=data.GetProperty("months").EnumerateArray().ToList();
            foreach(var m in months)
            {
                var count=m.GetProperty("completed").GetInt32();
                var scheduled=m.GetProperty("scheduled").GetInt32();
                var view=Theme.Card(count>0?Theme.Lavender:Theme.Background);view.Padding=new Padding(4);
                var label=Theme.Label($"{m.GetProperty("month").GetInt32():00}\n✓ {count}\n{scheduled} cit.",10,true);
                label.TextAlign=ContentAlignment.MiddleCenter;view.Controls.Add(label);
                bars.Controls.Add(view,m.GetProperty("month").GetInt32()-1,0);
            }
        }
        refresh.Click+=async (_,_)=>await LoadAsync();back.Click+=async (_,_)=>await ShowReportsAsync();
        save.Click+=(_,_)=>Present(new GoalsPanel(_api,_canReview));
        await LoadAsync();
    }

    private async Task ShowNotificationsAsync()
    {
        var notifications=new GestionNotification(_api);
        var root=CreateRows((80,SizeType.Absolute),(0,SizeType.Percent),(80,SizeType.Absolute));Present(root);
        root.Controls.Add(Theme.BannerTitle("Notificaciones institucionales"),0,0);
        var notificationCard=Theme.Card();root.Controls.Add(notificationCard,0,1);
        var items=new ListBox{Dock=DockStyle.Fill,Font=Theme.Text(12),IntegralHeight=false,
            BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Plum};
        notificationCard.Controls.Add(items);
        var button=Theme.Button("Marcar seleccionada como leída");root.Controls.Add(button,0,2);
        var stored=new List<GestionNotification.NotificationData>();
        async Task ReloadAsync()
        {
            var reply=await notifications.ListAsync();items.Items.Clear();stored.Clear();
            if(!string.IsNullOrEmpty(reply.Error)){items.Items.Add(reply.Error);return;}
            stored.AddRange(reply.Items);
            if(stored.Count==0)
                items.Items.Add("✉  Sin avisos por ahora. Te notificaremos cuando tengas actividad institucional.");
            foreach(var item in stored)items.Items.Add($"{(item.IsRead?"Leída":"Nueva")}  ·  {item.CreatedUtc.ToLocalTime():g}  ·  {item.Title}");
        }
        button.Click+=async (_,_)=>
        {
            if(items.SelectedIndex<0||items.SelectedIndex>=stored.Count)return;
            if(await notifications.ReadAsync(stored[items.SelectedIndex].NotificationId))await ReloadAsync();
        };
        await ReloadAsync();
    }

    /// <summary>Read-only institutional profile with separate editing and private image actions.</summary>
    [UsesData(typeof(global::MujeresUnidas.Gestion.Models.User.Base.User.PersonalDetailsData))]
    private async Task ShowProfileAsync()
    {
        var root=CreateRows((72,SizeType.Absolute),(230,SizeType.Absolute),(0,SizeType.Percent));Present(root);
        root.Controls.Add(Theme.BannerTitle("Mi perfil institucional",26),0,0);
        var banner=new Panel{Dock=DockStyle.Fill,Padding=new Padding(23,19,23,19),BackColor=Theme.Lavender,Margin=new Padding(8)};
        banner.BackgroundImage=Theme.LoadImage("mu_header.png");banner.BackgroundImageLayout=ImageLayout.Stretch;
        root.Controls.Add(banner,0,1);
        var hero=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.Transparent};
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,73));hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,27));
        banner.Controls.Add(hero);
        var left=CreateRows((70,SizeType.Absolute),(47,SizeType.Absolute),(0,SizeType.Percent));
        left.Controls.Add(Theme.Label(UserField("displayName"),27,true),0,0);
        left.Controls.Add(Theme.Label("✓  "+(_owner?"Autoridad principal":UserField("role"))+" · Cuenta institucional",13,true,Theme.Violet),0,1);
        left.Controls.Add(Theme.Label("Tus datos privados y permisos están protegidos. Tu perfil institucional no se publica automáticamente en Comunidad.",10,false,Theme.Muted),0,2);
        hero.Controls.Add(left,0,0);
        var avatar=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Transparent,
            Image=Theme.LoadImage("mu_r31_people.png"),Margin=new Padding(20,8,20,8)};
        avatar.Resize+=(_,_)=>
        {
            if(avatar.Width<2 || avatar.Height<2)return;
            int diameter=Math.Min(avatar.Width,avatar.Height);
            using var circle=new GraphicsPath();
            circle.AddEllipse(new Rectangle((avatar.Width-diameter)/2,(avatar.Height-diameter)/2,diameter-1,diameter-1));
            var previous=avatar.Region;avatar.Region=new Region(circle);previous?.Dispose();
        };
        hero.Controls.Add(avatar,1,0);
        var cover=await _api.DownloadImageAsync("api/accounts/me/cover");
        if(cover is not null)SetImage(cover,image=>banner.BackgroundImage=image);
        var photo=await _api.DownloadImageAsync("api/accounts/me/avatar");
        if(photo is not null)SetImage(photo,image=>avatar.Image=image);
        var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        root.Controls.Add(row,0,2);
        var identity=Surface();row.Controls.Add(identity,0,0);
        var dataRows=CreateRows((60,SizeType.Absolute),(65,SizeType.Absolute),(75,SizeType.Absolute),(0,SizeType.Percent));
        identity.Controls.Add(dataRows);
        dataRows.Controls.Add(Theme.Label("Mi información institucional",20,true),0,0);
        dataRows.Controls.Add(Theme.Label("Correo de acceso: "+UserField("email"),12),0,1);
        var details=await _api.SendAsync(HttpMethod.Get,"api/accounts/me/details");
        var summary="Tu ficha privada se conserva en SQL Server y solo se entrega a tu cuenta.";
        if(details.Success)
        {
            var d=details.Data;
            string F(string key)=>d.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
            summary=$"Nombre: {F("firstName")} {F("paternalLastName")} {F("maternalLastName")}\n"+
                    $"Teléfono: {F("phoneNumber")}    Ciudad: {F("city")}\n"+
                    $"Biografía: {F("biography")}";
        }
        var info=Theme.Label(summary,11,false,Theme.Muted);info.TextAlign=ContentAlignment.TopLeft;
        dataRows.Controls.Add(info,0,2);
        var columns=CreateRows((60,SizeType.Absolute),(60,SizeType.Absolute),(60,SizeType.Absolute),(0,SizeType.Percent));
        dataRows.Controls.Add(columns,0,3);
        var edit=Theme.Button("✎  Editar mi ficha completa");edit.Click+=async (_,_)=>await ShowEditProfileAsync();
        var pass=Theme.Button("▣  Cambiar contraseña",true);pass.Click+=(_,_)=>ShowChangePassword();
        var reload=Theme.Button("↻  Actualizar perfil",true);reload.Click+=async (_,_)=>await ShowProfileAsync();
        columns.Controls.Add(edit,0,0);columns.Controls.Add(pass,0,1);columns.Controls.Add(reload,0,2);
        var media=Surface();row.Controls.Add(media,1,0);
        var mediaRows=CreateRows((55,SizeType.Absolute),(55,SizeType.Absolute),(55,SizeType.Absolute),
            (52,SizeType.Absolute),(52,SizeType.Absolute),(115,SizeType.Absolute),(0,SizeType.Percent));
        media.Controls.Add(mediaRows);
        mediaRows.Controls.Add(Theme.Label("Fotografías del perfil",18,true),0,0);
        var pickAvatar=Theme.Button("Cambiar fotografía",true);
        pickAvatar.Click+=async (_,_)=>await UploadPhotoAsync("avatar");
        var pickCover=Theme.Button("Cambiar portada",true);
        pickCover.Click+=async (_,_)=>await UploadPhotoAsync("cover");
        mediaRows.Controls.Add(pickAvatar,0,1);mediaRows.Controls.Add(pickCover,0,2);
        var momentsLink=Theme.Button("Mis Momentos y guardados",true);
        momentsLink.Click+=(_,_)=>Present(new ManagementSocialPanel(_api));
        var directoryLink=Theme.Button("Seguidores y perfiles",true);
        directoryLink.Click+=(_,_)=>Present(new InstitutionalDirectoryPanel(_api));
        mediaRows.Controls.Add(momentsLink,0,3);mediaRows.Controls.Add(directoryLink,0,4);
        var awardsBox=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(7,5,7,5),BackColor=Theme.Lavender};
        var awarded=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=Padding.Empty};
        awardsBox.Controls.Add(awarded);
        mediaRows.Controls.Add(awardsBox,0,5);
        awarded.Controls.Add(Theme.Label("Logros e insignias · otorgados desde Gestión",12,true));
        if(Guid.TryParse(UserField("id"),out var badgeAccountId))
        {
            var reply=await _api.SendAsync(HttpMethod.Get,$"api/badges/{badgeAccountId}");
            if(reply.Success && reply.Data.ValueKind==JsonValueKind.Array)
            {
                var count=0;
                foreach(var badge in reply.Data.EnumerateArray())
                {
                    count++;
                    var label=badge.GetProperty("name").GetString()??"Reconocimiento";
                    var chip=Theme.Button("✦ "+label,true);
                    chip.AutoSize=true;chip.Height=34;chip.MaximumSize=new Size(230,38);
                    awarded.Controls.Add(chip);
                    chip.Click+=(_,_)=>MessageBox.Show(badge.TryGetProperty("description",out var desc)?desc.GetString()??label:label,
                        "Reconocimiento institucional",MessageBoxButtons.OK,MessageBoxIcon.Information);
                }
                if(count==0)awarded.Controls.Add(Theme.Label("Aún no hay medallas otorgadas.",10,false,Theme.Muted));
            }
            else awarded.Controls.Add(Theme.Label("No se pudieron consultar las medallas.",10,false,Theme.Muted));
        }
        mediaRows.Controls.Add(Theme.Label("Foto recomendada: 512 × 512\nPortada: 1600 × 500\nSolo se permiten JPEG/PNG.\nLos datos de identificación nunca son públicos.",11,false,Theme.Muted),0,6);
    }
    private static void SetImage(byte[] bytes,Action<Image> apply)
    {
        try
        {
            using var stream=new MemoryStream(bytes);
            using var loaded=Image.FromStream(stream);
            apply(new Bitmap(loaded));
        }
        catch(ArgumentException){/* Ignore invalid media without leaking private image details. */}
    }
    private async Task UploadPhotoAsync(string kind)
    {
        using var picker=new OpenFileDialog{Title=kind=="avatar"?"Elige foto de perfil":"Elige portada",
            Filter="Imágenes JPEG o PNG|*.jpg;*.jpeg;*.png",Multiselect=false};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        var result=await _api.UploadFileAsync($"api/accounts/me/{kind}",picker.FileName);
        if(result.Success)await ShowProfileAsync();
        else MessageBox.Show(result.Error,"Fotografía",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    }
    private async Task ShowEditProfileAsync()
    {
        var result=await _api.SendAsync(HttpMethod.Get,"api/accounts/me/details");
        if(!result.Success){MessageBox.Show(result.Error);return;}
        var details=result.Data;
        var root=CreateRows((75,SizeType.Absolute),(0,SizeType.Percent),(69,SizeType.Absolute));Present(root);
        root.Controls.Add(Theme.Label("Editar mi información institucional",25,true),0,0);
        var viewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Theme.Background};
        root.Controls.Add(viewport,0,1);
        var form=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(20,10,30,18)};
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,29));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,71));
        viewport.Controls.Add(form);
        var values=new Dictionary<string,TextBox>();
        string Read(string key)=>details.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
        void Field(string key,string label)
        {
            var index=form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
            form.Controls.Add(Theme.Label(label,11,true),0,index);
            var box=Theme.Input(label);box.Text=Read(key);values[key]=box;
            form.Controls.Add(box,1,index);
        }
        foreach(var pair in new[]{("displayName","Nombre público"),("firstName","Nombre(s)"),
            ("paternalLastName","Apellido paterno"),("maternalLastName","Apellido materno"),
            ("biography","Biografía institucional"),("phoneNumber","Teléfono"),
            ("alternatePhoneNumber","Teléfono alternativo"),("address","Dirección"),
            ("city","Ciudad"),("state","Estado"),("postalCode","Código postal"),
            ("rfc","RFC de prueba"),("curp","CURP de prueba")})Field(pair.Item1,pair.Item2);
        var date=new DateTimePicker{Dock=DockStyle.Fill,Format=DateTimePickerFormat.Short,ShowCheckBox=true};
        var dateIndex=form.RowCount++;form.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
        form.Controls.Add(Theme.Label("Fecha de nacimiento",11,true),0,dateIndex);form.Controls.Add(date,1,dateIndex);
        if(details.TryGetProperty("dateOfBirth",out var birth)&&birth.ValueKind==JsonValueKind.String&&DateTime.TryParse(birth.GetString(),out var parsed))
        {date.Value=parsed;date.Checked=true;}else date.Checked=false;
        var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var cancel=Theme.Button("Volver a mi perfil",true);cancel.Click+=async (_,_)=>await ShowProfileAsync();
        var save=Theme.Button("Guardar cambios");
        actions.Controls.Add(cancel,0,0);actions.Controls.Add(save,1,0);root.Controls.Add(actions,0,2);
        save.Click+=async (_,_)=>
        {
            string Get(string key)=>values[key].Text.Trim();
            if(Get("displayName").Length is <2 or >100){MessageBox.Show("Nombre público inválido.");return;}
            save.Enabled=false;
            try
            {
                var payload=new{displayName=Get("displayName"),firstName=Get("firstName"),
                    paternalLastName=Get("paternalLastName"),maternalLastName=Get("maternalLastName"),
                    biography=Get("biography"),phoneNumber=Get("phoneNumber"),
                    alternatePhoneNumber=Get("alternatePhoneNumber"),address=Get("address"),
                    city=Get("city"),state=Get("state"),postalCode=Get("postalCode"),
                    rfc=Get("rfc"),curp=Get("curp"),dateOfBirth=date.Checked?(DateTime?)date.Value.Date:null};
                var reply=await _api.SendAsync(HttpMethod.Put,"api/accounts/me/details",payload);
                if(!reply.Success){MessageBox.Show(reply.Error,"Revisar información");return;}
                var fresh=await _api.SendAsync(HttpMethod.Get,"api/auth/me");
                if(fresh.Success)_api.UpdateUser(fresh.Data);
                await ShowProfileAsync();
            }
            finally{save.Enabled=true;}
        };
    }
    private void ShowChangePassword()
    {
        var root=CreateRows((88,SizeType.Absolute),(70,SizeType.Absolute),(68,SizeType.Absolute),
            (68,SizeType.Absolute),(68,SizeType.Absolute),(70,SizeType.Absolute),(0,SizeType.Percent));Present(root);
        root.Controls.Add(Theme.Label("Cambiar contraseña",26,true),0,0);
        root.Controls.Add(Theme.Label("Esta operación cerrará todas las sesiones en Server.",11,false,Theme.Muted),0,1);
        var current=Theme.Input("Contraseña actual",true);
        var newer=Theme.Input("Contraseña nueva · mínimo 10 caracteres",true);
        var repeat=Theme.Input("Repetir nueva contraseña",true);
        root.Controls.Add(current,0,2);root.Controls.Add(newer,0,3);root.Controls.Add(repeat,0,4);
        var save=Theme.Button("Actualizar y cerrar sesiones");root.Controls.Add(save,0,5);
        save.Click+=async (_,_)=>
        {
            if(newer.Text.Length<10||newer.Text!=repeat.Text){MessageBox.Show("La nueva contraseña no coincide o es demasiado corta.");return;}
            save.Enabled=false;
            try
            {
                var reply=await _api.SendAsync(HttpMethod.Put,"api/auth/change-password",new{currentPassword=current.Text,newPassword=newer.Text});
                if(!reply.Success){MessageBox.Show(reply.Error);return;}
                MessageBox.Show("Contraseña cambiada. Vuelve a abrir Gestión e inicia sesión nuevamente.");
                _api.SignOut();Close();
            }
            finally{save.Enabled=true;}
        };
    }
    /// <summary>Institutional catalogue for fictitious sites in Ahome, with no invented addresses.</summary>
    private async Task ShowSitesAsync()
    {
        if(!_canReview)return;
        var root=CreateRows((72,SizeType.Absolute),(70,SizeType.Absolute),(0,SizeType.Percent),(240,SizeType.Absolute));
        Present(root);
        root.Controls.Add(Theme.BannerTitle("Sedes de atención · Ahome (simulación)"),0,0);
        root.Controls.Add(Theme.Label("Oficinas y módulos ficticios para vincular casos, citas, personal y verificaciones. No son direcciones reales.",11,false,Theme.Muted),0,1);
        var grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
        Theme.StyleGrid(grid);
        grid.Columns.Add("Code","Código");grid.Columns.Add("Name","Sede");grid.Columns.Add("Type","Tipo");
        grid.Columns.Add("City","Municipio");grid.Columns.Add("Active","Estado");
        root.Controls.Add(grid,0,2);
        var entry=Surface();root.Controls.Add(entry,0,3);
        var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3};
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,23));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent,34));fields.RowStyles.Add(new RowStyle(SizeType.Percent,33));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent,33));
        var code=Theme.Input("Código (AH-DEMO-005)");
        var name=Theme.Input("Nombre de la sede ficticia");
        var kind=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(10)};
        kind.Items.AddRange(new[]{"Administrativa","Psicológica","Jurídica","Actividades comunitarias","Multiservicio"});kind.SelectedIndex=0;
        fields.Controls.Add(code,0,0);fields.Controls.Add(name,1,0);fields.Controls.Add(kind,2,0);
        var explain=Theme.Label("Solo para el Hackathon: no pongas domicilios de centros no verificados.",11,false,Theme.Muted);
        fields.Controls.Add(explain,0,1);fields.SetColumnSpan(explain,2);
        var save=Theme.Button("✓ Agregar sede de demostración");fields.Controls.Add(save,2,1);
        var siteChoices=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(10)};
        var staffChoices=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(10)};
        var availableStaff=new List<Guid>();
        var assign=Theme.Button("Vincular personal a sede");
        fields.Controls.Add(siteChoices,0,2);fields.Controls.Add(staffChoices,1,2);fields.Controls.Add(assign,2,2);
        entry.Controls.Add(fields);
        async Task ReloadAsync()
        {
            var response=await _api.SendAsync(HttpMethod.Get,"api/sites");
            grid.Rows.Clear();siteChoices.Items.Clear();if(!response.Success){MessageBox.Show(response.Error);return;}
            foreach(var v in response.Data.EnumerateArray())
            {
                if(v.GetProperty("isActive").GetBoolean())siteChoices.Items.Add(new SiteOption(
                    v.GetProperty("id").GetGuid(),v.GetProperty("displayName").GetString()??""));
                grid.Rows.Add(v.GetProperty("code").GetString()??string.Empty,v.GetProperty("displayName").GetString()??string.Empty,
                    Spanish(v.GetProperty("siteType").GetString()),v.GetProperty("municipality").GetString()??string.Empty,
                    v.GetProperty("isActive").GetBoolean()?"Activa":"Inactiva");
            }
            if(siteChoices.Items.Count>0)siteChoices.SelectedIndex=0;
        }
        async Task LoadStaffAsync()
        {
            var response=await _api.SendAsync(HttpMethod.Get,_owner?"api/management/staff":"api/care/management/professionals");
            staffChoices.Items.Clear();availableStaff.Clear();if(!response.Success)return;
            foreach(var user in response.Data.EnumerateArray())
            {
                if(user.TryGetProperty("status",out var state)&&state.GetString()!="Active")continue;
                availableStaff.Add(user.GetProperty("id").GetGuid());
                staffChoices.Items.Add(user.GetProperty("displayName").GetString()??"Personal");
            }
            if(staffChoices.Items.Count>0)staffChoices.SelectedIndex=0;
        }
        save.Click+=async (_,_)=>
        {
            var types=new[]{"Administrative","Psychology","Legal","CommunityActivities","MultiService"};
            var response=await _api.SendAsync(HttpMethod.Post,"api/sites",new
            {
                code=code.Text.Trim(),displayName=name.Text.Trim(),siteType=types[kind.SelectedIndex],
                addressNote="Ubicación por confirmar · DEMOSTRACIÓN"
            });
            MessageBox.Show(response.Success?"Sede de demostración guardada.":response.Error);
            if(response.Success){code.Clear();name.Clear();await ReloadAsync();}
        };
        assign.Click+=async (_,_)=>
        {
            if(siteChoices.SelectedItem is not SiteOption selected || staffChoices.SelectedIndex<0)return;
            var response=await _api.SendAsync(HttpMethod.Put,$"api/sites/{selected.Id}/staff",
                new{staffId=availableStaff[staffChoices.SelectedIndex]});
            MessageBox.Show(response.Success?"Personal vinculado a sede.":response.Error);
        };
        await ReloadAsync();await LoadStaffAsync();
    }

    /// <summary>Spanish labels are presentation-only; API/SQL codes remain stable.</summary>
    private static string Spanish(string? code)=>code switch
    {
        "Psychology"=>"Psicología","Legal"=>"Jurídica","SocialWork"=>"Trabajo social","Other"=>"Otras",
        "Administrative"=>"Administrativa","CommunityActivities"=>"Actividades comunitarias","MultiService"=>"Multiservicio",
        "Pending"=>"Pendiente","Approved"=>"Aprobada","Rejected"=>"Rechazada",
        "UnderReview"=>"En revisión","InProgress"=>"En seguimiento","Resolved"=>"Resuelto",
        "Closed"=>"Cerrado","Withdrawn"=>"Retirado","Scheduled"=>"Programada","Confirmed"=>"Confirmada",
        "Completed"=>"Cumplida","Cancelled"=>"Cancelada","NoShow"=>"No asistió",
        "Initial"=>"Inicial","FollowUp"=>"Seguimiento","Interdisciplinary"=>"Interdisciplinaria",
        "InPerson"=>"Presencial","Virtual"=>"Virtual","Telephone"=>"Telefónica",
        _=>code??string.Empty
    };

    /// <summary>Separate institutional review of requests from confidential case follow-up.</summary>
    private async Task ShowCasesAsync()
    {
        var root=CreateRows((72,SizeType.Absolute),(0,SizeType.Percent),(85,SizeType.Absolute));Present(root);
        root.Controls.Add(Theme.BannerTitle("Casos y solicitudes de atención",25),0,0);
        var tabs=new TabControl{Dock=DockStyle.Fill,Font=Theme.Text(11,true)};
        var requestsPage=new TabPage("Solicitudes pendientes"){BackColor=Theme.Background};
        var casesPage=new TabPage("Expedientes asignados / seguimiento"){BackColor=Theme.Background};
        tabs.TabPages.Add(requestsPage);tabs.TabPages.Add(casesPage);root.Controls.Add(tabs,0,1);
        var requestsGrid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,MultiSelect=false,
            SelectionMode=DataGridViewSelectionMode.FullRowSelect,AllowUserToAddRows=false,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,RowHeadersVisible=false};
        Theme.StyleGrid(requestsGrid);
        requestsGrid.Columns.Add("Id","Id");requestsGrid.Columns[0].Visible=false;
        requestsGrid.Columns.Add("Area","Área");requestsGrid.Columns.Add("Subject","Asunto general");
        requestsGrid.Columns.Add("State","Estado");requestsGrid.Columns.Add("Created","Fecha");
        var reqLayout=CreateRows((0,SizeType.Percent),(63,SizeType.Absolute));
        requestsPage.Controls.Add(reqLayout);reqLayout.Controls.Add(requestsGrid,0,0);
        var requestActions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        requestActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));requestActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var accept=Theme.Button("✓ Aprobar solicitud y abrir caso");
        var reject=Theme.Button("× Rechazar solicitud",true);
        requestActions.Controls.Add(accept,0,0);requestActions.Controls.Add(reject,1,0);
        reqLayout.Controls.Add(requestActions,0,1);
        if(!_canReview){accept.Enabled=false;reject.Enabled=false;}
        var caseLayout=CreateRows((0,SizeType.Percent),(58,SizeType.Absolute),(58,SizeType.Absolute),(64,SizeType.Absolute));
        casesPage.Controls.Add(caseLayout);
        var caseGrid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,MultiSelect=false,
            SelectionMode=DataGridViewSelectionMode.FullRowSelect,AllowUserToAddRows=false,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,RowHeadersVisible=false};
        Theme.StyleGrid(caseGrid);
        caseGrid.Columns.Add("Id","Id");caseGrid.Columns[0].Visible=false;
        caseGrid.Columns.Add("Code","Expediente");caseGrid.Columns.Add("Area","Área");
        caseGrid.Columns.Add("Status","Estado");caseGrid.Columns.Add("Opened","Apertura");
        caseGrid.Columns.Add("Site","Sede");caseGrid.Columns.Add("SiteId","SiteId");caseGrid.Columns[6].Visible=false;
        caseLayout.Controls.Add(caseGrid,0,0);
        var assignRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        assignRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,67));assignRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33));
        var professional=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(11)};
        var assign=Theme.Button("Asignar profesional");
        assignRow.Controls.Add(professional,0,0);assignRow.Controls.Add(assign,1,0);caseLayout.Controls.Add(assignRow,0,1);
        var statusRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        var states=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(11)};
        states.Items.AddRange(new[]{"UnderReview","InProgress","Resolved","Closed","Withdrawn"});states.SelectedIndex=1;
        var change=Theme.Button("Actualizar estado",true);
        statusRow.Controls.Add(states,0,0);statusRow.Controls.Add(change,1,0);caseLayout.Controls.Add(statusRow,0,3);
        var siteRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        siteRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,67));siteRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33));
        var sitePicker=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(10)};
        var assignSite=Theme.Button("✓ Asignar sede al caso");
        siteRow.Controls.Add(sitePicker,0,0);siteRow.Controls.Add(assignSite,1,0);caseLayout.Controls.Add(siteRow,0,2);
        assignSite.Enabled=_canReview;
        if(!_canReview)assign.Enabled=false;
        var professionals=new List<(Guid Id,string Name,string Role)>();
        var visiblePros=new List<(Guid Id,string Name,string Role)>();
        var sites=new List<(Guid Id,string Name,string Type)>();
        void UpdateSelection()
        {
            if(caseGrid.SelectedRows.Count==0)return;
            var area=caseGrid.SelectedRows[0].Cells["Area"].Value?.ToString();
            var currentSite=caseGrid.SelectedRows[0].Cells["SiteId"].Value?.ToString();
            professional.Items.Clear();visiblePros.Clear();
            foreach(var person in professionals.Where(p=>p.Role==area || area=="Other"))
            {visiblePros.Add(person);professional.Items.Add(person.Name+" · "+Spanish(person.Role));}
            if(professional.Items.Count>0)professional.SelectedIndex=0;
            sitePicker.Items.Clear();
            foreach(var site in sites.Where(p=>p.Type=="MultiService" ||
                (area=="Psychology" && p.Type=="Psychology") ||
                (area=="Legal" && p.Type=="Legal") ||
                (area=="SocialWork" && p.Type=="Administrative") ||
                (area=="Other" && p.Type=="CommunityActivities")))
                sitePicker.Items.Add(new SiteOption(site.Id,site.Name));
            if(sitePicker.Items.Count>0)
            {
                sitePicker.SelectedIndex=0;
                for(int i=0;i<sitePicker.Items.Count;i++)
                    if(sitePicker.Items[i] is SiteOption selected && selected.Id.ToString()==currentSite)
                    {sitePicker.SelectedIndex=i;break;}
            }
        }
        caseGrid.SelectionChanged+=(_,_)=>UpdateSelection();
        async Task ReloadProfessionalsAsync()
        {
            if(!_canReview)return;
            var response=await _api.SendAsync(HttpMethod.Get,"api/care/management/professionals");
            if(!response.Success)return;
            professionals.Clear();professional.Items.Clear();
            foreach(var entry in response.Data.EnumerateArray())
                professionals.Add((entry.GetProperty("id").GetGuid(),entry.GetProperty("displayName").GetString()??"",
                    entry.GetProperty("role").GetString()??""));
            UpdateSelection();
        }
        async Task ReloadRequestsAsync()
        {
            var response=await _api.SendAsync(HttpMethod.Get,"api/care/requests");
            requestsGrid.Rows.Clear();if(!response.Success)return;
            foreach(var item in response.Data.EnumerateArray())
                requestsGrid.Rows.Add(item.GetProperty("id").GetString() ?? string.Empty,
                    Spanish(item.GetProperty("area").GetString()),
                    item.GetProperty("subject").GetString() ?? string.Empty,
                    Spanish(item.GetProperty("status").GetString()),
                    item.GetProperty("createdUtc").GetDateTime().ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        }
        async Task ReloadCasesAsync()
        {
            var response=await _api.SendAsync(HttpMethod.Get,_canReview?"api/care/management/cases":"api/care/cases");
            caseGrid.Rows.Clear();if(!response.Success)return;
            foreach(var item in response.Data.EnumerateArray())
            {
                var id=item.GetProperty("id").GetGuid();
                caseGrid.Rows.Add(id,id.ToString("N")[..8].ToUpperInvariant(),
                    item.GetProperty("area").GetString() ?? string.Empty,
                    Spanish(item.GetProperty("status").GetString()),
                    item.GetProperty("openedUtc").GetDateTime().ToLocalTime().ToString("dd/MM/yyyy"),
                    item.TryGetProperty("siteName",out var siteName)&&siteName.ValueKind==JsonValueKind.String?(siteName.GetString()??"Sin sede"):"Sin sede",
                    item.TryGetProperty("siteId",out var siteId)&&siteId.ValueKind==JsonValueKind.String?(siteId.GetString()??string.Empty):string.Empty);
            }
        }
        async Task ReviewAsync(bool approved)
        {
            if(!_canReview || requestsGrid.SelectedRows.Count==0||
                !Guid.TryParse(requestsGrid.SelectedRows[0].Cells[0].Value?.ToString(),out var id))return;
            if(!await ConfirmAsync(approved?"¿Crear un caso para esta solicitud?":"¿Rechazar esta solicitud?"))return;
            var result=await _api.SendAsync(HttpMethod.Post,$"api/care/requests/{id}/review",new{approve=approved});
            if(!result.Success)MessageBox.Show(result.Error);
            await ReloadRequestsAsync();await ReloadCasesAsync();
        }
        accept.Click+=async (_,_)=>await ReviewAsync(true);
        reject.Click+=async (_,_)=>await ReviewAsync(false);
        assign.Click+=async (_,_)=>await AssignSelectedAsync();
        async Task AssignSelectedAsync()
        {
            if(caseGrid.SelectedRows.Count==0 || professional.SelectedIndex<0 ||
                !Guid.TryParse(caseGrid.SelectedRows[0].Cells[0].Value?.ToString(),out var caseId))return;
            var answer=await _api.SendAsync(HttpMethod.Post,$"api/care/cases/{caseId}/assign",
                new{professionalId=visiblePros[professional.SelectedIndex].Id});
            MessageBox.Show(answer.Success?"Profesional asignada al expediente.":answer.Error);
        }
        change.Click+=async (_,_)=>await ChangeSelectedAsync();
        async Task ChangeSelectedAsync()
        {
            if(caseGrid.SelectedRows.Count==0 ||
                !Guid.TryParse(caseGrid.SelectedRows[0].Cells[0].Value?.ToString(),out var caseId))return;
            var reply=await _api.SendAsync(HttpMethod.Put,$"api/care/cases/{caseId}/status",
                new{status=states.SelectedItem?.ToString()});
            MessageBox.Show(reply.Success?"Estado actualizado por profesional autorizada.":reply.Error);
            await ReloadCasesAsync();
        }
        async Task LoadSitesAsync()
        {
            var response=await _api.SendAsync(HttpMethod.Get,"api/sites");
            if(response.Success)
            {
                sites.Clear();
                foreach(var item in response.Data.EnumerateArray())
                    if(item.GetProperty("isActive").GetBoolean())
                        sites.Add((item.GetProperty("id").GetGuid(),item.GetProperty("displayName").GetString()??"",
                            item.GetProperty("siteType").GetString()??""));
            }
        }
        assignSite.Click+=async (_,_)=>
        {
            if(caseGrid.SelectedRows.Count==0 || sitePicker.SelectedItem is not SiteOption selected ||
                !Guid.TryParse(caseGrid.SelectedRows[0].Cells["Id"].Value?.ToString(),out var caseId))return;
            var response=await _api.SendAsync(HttpMethod.Put,$"api/sites/cases/{caseId}/site",new{siteId=selected.Id});
            MessageBox.Show(response.Success?"Sede asignada al caso.":response.Error);
            if(response.Success)await ReloadCasesAsync();
        };
        await LoadSitesAsync();
        var footer=Theme.Label("Solo solicitudes administrativas y casos asignados. Sin datos clínicos en pantallas generales.\n"+
            "Esta sección es una demostración; no sustituye atención profesional ni emergencias.",10,false,Theme.Muted);
        root.Controls.Add(footer,0,2);
        await ReloadProfessionalsAsync();await ReloadRequestsAsync();await ReloadCasesAsync();
    }
    /// <summary>Management scheduling; Community receives the schedule from the same API/SQL records.</summary>
    private async Task ShowAppointmentsAsync()
    {
        var root=CreateRows((73,SizeType.Absolute),(0,SizeType.Percent),(222,SizeType.Absolute));Present(root);
        root.Controls.Add(Theme.BannerTitle("Citas · coordinación institucional",25),0,0);
        var list=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,MultiSelect=false,
            SelectionMode=DataGridViewSelectionMode.FullRowSelect,AllowUserToAddRows=false,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,RowHeadersVisible=false};
        Theme.StyleGrid(list);
        list.Columns.Add("Id","Id");list.Columns[0].Visible=false;
        list.Columns.Add("CaseId","CaseId");list.Columns[1].Visible=false;
        list.Columns.Add("Case","Caso");list.Columns.Add("Date","Fecha / hora");
        list.Columns.Add("Pro","Profesional");list.Columns.Add("Mode","Modalidad");list.Columns.Add("State","Estado");
        root.Controls.Add(list,0,1);
        var editor=Surface();root.Controls.Add(editor,0,2);
        var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=3};
        for(int i=0;i<4;i++)fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent,32));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent,32));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent,36));
        editor.Controls.Add(fields);
        var cases=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
        var pros=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
        var when=new DateTimePicker{Dock=DockStyle.Fill,Format=DateTimePickerFormat.Custom,
            CustomFormat="dd/MM/yyyy HH:mm",Value=DateTime.Now.AddDays(1)};
        var mins=new NumericUpDown{Dock=DockStyle.Fill,Minimum=15,Maximum=180,Value=60};
        var type=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
        type.Items.AddRange(new[]{"Inicial","Seguimiento","Interdisciplinaria"});type.SelectedIndex=0;
        var mode=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
        mode.Items.AddRange(new[]{"Presencial","Virtual","Telefónica"});mode.SelectedIndex=0;
        var where=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Text(10)};
        where.Items.Add("Selecciona un caso con sede asignada");where.SelectedIndex=0;
        var schedule=Theme.Button("✓ Programar cita");
        fields.Controls.Add(cases,0,0);fields.Controls.Add(pros,1,0);
        fields.Controls.Add(when,2,0);fields.Controls.Add(mins,3,0);
        fields.Controls.Add(type,0,1);fields.Controls.Add(mode,1,1);
        fields.Controls.Add(where,2,1);fields.SetColumnSpan(where,2);
        fields.Controls.Add(schedule,0,2);fields.SetColumnSpan(schedule,2);
        var confirm=Theme.Button("Confirmar cita",true);fields.Controls.Add(confirm,2,2);
        var cancel=Theme.Button("Cancelar cita",true);fields.Controls.Add(cancel,3,2);
        var allCases=new List<(Guid Id,string Area,Guid? SiteId,string? SiteName)>();
        var allPros=new List<(Guid Id,string Name,string Role)>();
        var availablePros=new List<(Guid Id,string Name,string Role)>();
        void MatchCase()
        {
            if(cases.SelectedIndex<0 || cases.SelectedIndex>=allCases.Count)return;
            var selected=allCases[cases.SelectedIndex];
            pros.Items.Clear();availablePros.Clear();
            foreach(var p in allPros.Where(p=>p.Role==selected.Area || selected.Area=="Other"))
            {availablePros.Add(p);pros.Items.Add(p.Name+" · "+Spanish(p.Role));}
            if(pros.Items.Count>0)pros.SelectedIndex=0;
            where.Items.Clear();where.Items.Add(selected.SiteName??"Primero asigna una sede desde Casos");where.SelectedIndex=0;
            schedule.Enabled=_canReview && selected.SiteId.HasValue && availablePros.Count>0;
        }
        cases.SelectedIndexChanged+=(_,_)=>MatchCase();
        schedule.Enabled=_canReview;confirm.Enabled=_canReview;cancel.Enabled=_canReview;
        if(_canReview)
        {
            var c=await _api.SendAsync(HttpMethod.Get,"api/care/management/cases");
            if(c.Success)foreach(var item in c.Data.EnumerateArray())
            {var id=item.GetProperty("id").GetGuid();allCases.Add((id,item.GetProperty("area").GetString()??"",
                item.TryGetProperty("siteId",out var siteId)&&siteId.ValueKind==JsonValueKind.String?siteId.GetGuid():null,
                item.TryGetProperty("siteName",out var siteName)&&siteName.ValueKind==JsonValueKind.String?siteName.GetString():null));
                cases.Items.Add("CASO-"+id.ToString("N")[..8].ToUpperInvariant()+" · "+Spanish(item.GetProperty("area").GetString()));}
            var p=await _api.SendAsync(HttpMethod.Get,"api/care/management/professionals");
            if(p.Success)foreach(var item in p.Data.EnumerateArray())
            {allPros.Add((item.GetProperty("id").GetGuid(),item.GetProperty("displayName").GetString()??"",
                item.GetProperty("role").GetString()??""));}
            if(cases.Items.Count>0)cases.SelectedIndex=0;
            MatchCase();
        }
        async Task ReloadAsync()
        {
            var reply=await _api.SendAsync(HttpMethod.Get,_canReview?"api/care/management/appointments":"api/care/appointments");
            list.Rows.Clear();if(!reply.Success)return;
            foreach(var item in reply.Data.EnumerateArray())
            {
                var id=item.GetProperty("id").GetGuid();var caseId=item.GetProperty("caseId").GetGuid();
                list.Rows.Add(id,caseId,"CASO-"+caseId.ToString("N")[..8].ToUpperInvariant(),
                    item.GetProperty("scheduledUtc").GetDateTime().ToLocalTime().ToString("g"),
                    item.GetProperty("professionalName").GetString() ?? string.Empty,
                    Spanish(item.GetProperty("modality").GetString()),
                    Spanish(item.GetProperty("status").GetString()));
            }
        }
        schedule.Click+=async (_,_)=>await ScheduleAsync();
        async Task ScheduleAsync()
        {
            if(!_canReview||cases.SelectedIndex<0||pros.SelectedIndex<0 || !allCases[cases.SelectedIndex].SiteId.HasValue)return;
            var reply=await _api.SendAsync(HttpMethod.Post,"api/care/appointments",new
            {
                caseId=allCases[cases.SelectedIndex].Id,professionalId=availablePros[pros.SelectedIndex].Id,
                siteId=allCases[cases.SelectedIndex].SiteId,
                scheduledUtc=when.Value.ToUniversalTime(),durationMinutes=(int)mins.Value,
                appointmentType=new[]{"Initial","FollowUp","Interdisciplinary"}[type.SelectedIndex],
                modality=new[]{"InPerson","Virtual","Telephone"}[mode.SelectedIndex]
            });
            MessageBox.Show(reply.Success?"Cita guardada en SQL. La usuaria la verá en Comunidad.":reply.Error);
            await ReloadAsync();
        }
        async Task StatusAsync(string status)
        {
            if(list.SelectedRows.Count==0||!Guid.TryParse(list.SelectedRows[0].Cells[0].Value?.ToString(),out var id))return;
            var reply=await _api.SendAsync(HttpMethod.Put,$"api/care/appointments/{id}/status",new{status});
            MessageBox.Show(reply.Success?"Cita actualizada.":reply.Error);await ReloadAsync();
        }
        confirm.Click+=async (_,_)=>await StatusAsync("Confirmed");
        cancel.Click+=async (_,_)=>await StatusAsync("Cancelled");
        await ReloadAsync();
    }
    private Task<bool> ConfirmAsync(string question)=>Task.FromResult(MessageBox.Show(question,
        "Confirmar operación de demostración",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes);
    #endregion Task
}
}
