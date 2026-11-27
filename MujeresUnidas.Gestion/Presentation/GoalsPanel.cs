using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Presentation;

/// <summary>Institutional goals are not a cosmetic part of Reports; each goal has a durable evaluation.</summary>
public sealed class GoalsPanel:UserControl
{
    private readonly ApiConnection _api;
    private readonly bool _canWrite;
    private readonly NumericUpDown _year=new(){Minimum=2020,Maximum=2100,Value=DateTime.Today.Year,Dock=DockStyle.Fill};
    private readonly ComboBox _indicator=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox _scopeZone=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox _scopeSite=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly NumericUpDown _target=new(){Dock=DockStyle.Fill,Maximum=100000000,DecimalPlaces=2,ThousandsSeparator=true};
    private readonly NumericUpDown _observed=new(){Dock=DockStyle.Fill,Maximum=100000000,DecimalPlaces=2,ThousandsSeparator=true};
    private readonly ComboBox _status=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly TextBox _notes=Theme.Input("Observación o fuente de evaluación (obligatoria al evaluar)");
    private readonly DataGridView _goals=new(){Dock=DockStyle.Fill,ReadOnly=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AllowUserToAddRows=false};
    private readonly Label _notice=Theme.Label("Metas y resultados separados: no se confunden casos, citas y personas.",10,false,Theme.Muted);
    private readonly Label _impact=Theme.Label("Encuestas de progreso · consultando indicadores agregados…",11,false,Theme.Plum);
    private sealed record Indicator(Guid Id,string Title,string Unit,string Code)
    {
        public override string ToString()=>Title+" · "+Unit;
    }
    private sealed record Scope(Guid? Id,string Name)
    {
        public override string ToString()=>Name;
    }
    public GoalsPanel(ApiConnection api,bool canWrite)
    {
        _api=api;_canWrite=canWrite;Dock=DockStyle.Fill;BackColor=Theme.Background;
        var root=Theme.Rows((92,SizeType.Absolute),(72,SizeType.Absolute),(75,SizeType.Absolute),(0,SizeType.Percent),(153,SizeType.Absolute),(47,SizeType.Absolute));
        Controls.Add(root);
        root.Controls.Add(Theme.BannerTitle("Metas · impacto y evaluación",24),0,0);
        var filters=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=6,Padding=new Padding(5)};
        foreach(var n in new[]{28f,12f,13f,15f,17f,15f})
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,n));
        filters.Controls.Add(_indicator,0,0);filters.Controls.Add(_year,1,0);filters.Controls.Add(_target,2,0);
        filters.Controls.Add(_scopeZone,3,0);filters.Controls.Add(_scopeSite,4,0);
        var create=Theme.Button("＋ Crear meta");create.Enabled=canWrite;
        filters.Controls.Add(create,5,0);root.Controls.Add(filters,0,1);
        _scopeZone.SelectedIndexChanged+=(_,_)=>
        {
            if(_scopeZone.SelectedItem is Scope{Id:not null}&&_scopeSite.SelectedIndex>0)_scopeSite.SelectedIndex=0;
        };
        _scopeSite.SelectedIndexChanged+=(_,_)=>
        {
            if(_scopeSite.SelectedItem is Scope{Id:not null}&&_scopeZone.SelectedIndex>0)_scopeZone.SelectedIndex=0;
        };
        Theme.StyleGrid(_goals);
        _goals.Columns.Add("Id","ID");_goals.Columns[0].Visible=false;
        _goals.Columns.Add("Metric","Indicador");_goals.Columns.Add("Year","Año");
        _goals.Columns.Add("Target","Objetivo");_goals.Columns.Add("Status","Estado");
        _goals.Columns.Add("Scope","Ámbito");
        var impactCard=Theme.Card(Color.FromArgb(246,232,253));
        impactCard.Padding=new Padding(15,10,15,10);
        impactCard.Controls.Add(_impact);root.Controls.Add(impactCard,0,2);
        root.Controls.Add(_goals,0,3);
        var actions=Theme.Card();root.Controls.Add(actions,0,4);
        var values=Theme.Rows((40,SizeType.Absolute),(49,SizeType.Absolute),(45,SizeType.Absolute));
        actions.Controls.Add(values);
        values.Controls.Add(Theme.Label("Evaluar meta seleccionada · registrar avance real o ficticio identificado",15,true),0,0);
        var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
        _status.Items.AddRange(["En progreso","Cumplida","Evaluada","Reprogramada"]);_status.SelectedIndex=0;
        fields.Controls.Add(_observed,0,0);fields.Controls.Add(_status,1,0);fields.Controls.Add(_notes,2,0);
        values.Controls.Add(fields,0,1);
        var buttons=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
        for(int i=0;i<3;i++)buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        var evaluate=Theme.Button("✓ Registrar evaluación");evaluate.Enabled=canWrite;
        var observe=Theme.Button("Registrar medición",true);observe.Enabled=canWrite;
        var reload=Theme.Button("↻ Actualizar metas",true);
        buttons.Controls.Add(evaluate,0,0);buttons.Controls.Add(observe,1,0);buttons.Controls.Add(reload,2,0);values.Controls.Add(buttons,0,2);
        root.Controls.Add(_notice,0,5);
        create.Click+=async (_,_)=>await CreateAsync();
        evaluate.Click+=async (_,_)=>await EvaluateAsync();
        observe.Click+=async (_,_)=>await RecordObservationAsync();
        reload.Click+=async (_,_)=>await ReloadAsync();
        _year.ValueChanged+=async (_,_)=>{await ReloadAsync();await LoadImpactAsync();};
        Load+=async (_,_)=>{await LoadDefinitionsAsync();await LoadScopesAsync();await ReloadAsync();await LoadImpactAsync();};
    }
    /// <summary>Social indicators are read-only aggregates; no staff access to individual survey answers.</summary>
    private async Task LoadImpactAsync()
    {
        var response=await _api.SendAsync(HttpMethod.Get,
            $"api/community/surveys/management/summary/{(int)_year.Value}");
        if(!response.Success){_impact.Text="Encuestas · "+response.Error;return;}
        if(response.Data.ValueKind!=JsonValueKind.Object ||
           !response.Data.TryGetProperty("available",out var available) || !available.GetBoolean())
        {
            _impact.Text="Encuestas de progreso · Sin muestra suficiente (mínimo 5 respuestas). No se muestran datos personales.";
            return;
        }
        if(!response.Data.TryGetProperty("data",out var data))return;
        string Count(string key)=>data.TryGetProperty(key,out var field)&&field.ValueKind==JsonValueKind.Number&&field.TryGetInt32(out var value)?value.ToString():"—";
        _impact.Text=$"Encuestas · {Count("participants")} participantes  |  {Count("studying")} estudian  |  {Count("working")} trabajan  |  " +
            $"{Count("communityContributed")} perciben apoyo  |  {Count("studyingSinceYearStart")} comenzaron estudios  |  {Count("workingSinceYearStart")} comenzaron a trabajar · — = reservado por privacidad";
    }

    private async Task LoadDefinitionsAsync()
    {
        var res=await _api.SendAsync(HttpMethod.Get,"api/management/goals/definitions");
        _indicator.Items.Clear();if(!res.Success){_notice.Text=res.Error;return;}
        foreach(var d in res.Data.EnumerateArray())
            _indicator.Items.Add(new Indicator(d.GetProperty("id").GetGuid(),d.GetProperty("title").GetString()??"Indicador",
            d.GetProperty("unit").GetString()??"",d.GetProperty("code").GetString()??""));
        if(_indicator.Items.Count>0)_indicator.SelectedIndex=0;
    }
    private async Task LoadScopesAsync()
    {
        _scopeZone.Items.Clear();_scopeSite.Items.Clear();
        _scopeZone.Items.Add(new Scope(null,"Todas las zonas"));
        _scopeSite.Items.Add(new Scope(null,"Todas las sedes"));
        var zones=await _api.SendAsync(HttpMethod.Get,"api/management/zones");
        if(zones.Success&&zones.Data.ValueKind==JsonValueKind.Array)
            foreach(var z in zones.Data.EnumerateArray())
                _scopeZone.Items.Add(new Scope(z.GetProperty("id").GetGuid(),z.GetProperty("displayName").GetString()??"Zona"));
        var sites=await _api.SendAsync(HttpMethod.Get,"api/sites");
        if(sites.Success&&sites.Data.ValueKind==JsonValueKind.Array)
            foreach(var site in sites.Data.EnumerateArray())
                _scopeSite.Items.Add(new Scope(site.GetProperty("id").GetGuid(),site.GetProperty("displayName").GetString()??"Sede"));
        _scopeZone.SelectedIndex=0;_scopeSite.SelectedIndex=0;
    }
    private (Guid? Zone,Guid? Site) CurrentScope()=>
        ((_scopeZone.SelectedItem as Scope)?.Id,(_scopeSite.SelectedItem as Scope)?.Id);

    private async Task ReloadAsync()
    {
        var res=await _api.SendAsync(HttpMethod.Get,$"api/management/goals/{(int)_year.Value}");
        _goals.Rows.Clear();if(!res.Success){_notice.Text=res.Error;return;}
        var names=_indicator.Items.OfType<Indicator>().ToDictionary(x=>x.Id,x=>x.Title);
        foreach(var goal in res.Data.GetProperty("goals").EnumerateArray())
        {
            var code=goal.GetProperty("metricId").GetGuid();
            var zone=goal.GetProperty("zoneId");var site=goal.GetProperty("siteId");
            string scope=zone.ValueKind==JsonValueKind.String?"Zona":site.ValueKind==JsonValueKind.String?"Sede":"General";
            _goals.Rows.Add(goal.GetProperty("id").GetGuid(),names.GetValueOrDefault(code,"Indicador"),
                goal.GetProperty("goalYear").GetInt32(),goal.GetProperty("targetValue").GetDecimal().ToString("N2"),
                Translate(goal.GetProperty("status").ToString()),scope);
        }
        _notice.Text=$"{_goals.Rows.Count} metas registradas · datos persistidos en SQL Server. Reportes observados no equivalen a incidencia real.";
    }
    private static string Translate(string? code)=>code switch
    {
        "0" or "Planned"=>"Planificada","1" or "InProgress"=>"En progreso","2" or "Achieved"=>"Cumplida",
        "3" or "Evaluated"=>"Evaluada","4" or "Rescheduled"=>"Reprogramada",_=>code??""
    };
    private async Task CreateAsync()
    {
        if(_indicator.SelectedItem is not Indicator d)return;
        if(_target.Value==0 && d.Code is not ("MISSED_APPOINTMENTS" or "INCIDENTS_RATE"))
        {
            MessageBox.Show("Elige un objetivo mayor que cero. Un resultado de 0 no es una meta de incremento cumplida.","Definir meta");
            return;
        }
        var scope=CurrentScope();
        var res=await _api.SendAsync(HttpMethod.Post,"api/management/goals",new{metricId=d.Id,goalYear=(int)_year.Value,targetValue=_target.Value,
            zoneId=scope.Zone,siteId=scope.Site,notes="Objetivo de demostración · validar fuente antes de utilizar cifras reales"});
        if(!res.Success){MessageBox.Show(res.Error,"Metas");return;}
        await ReloadAsync();
    }
    private async Task RecordObservationAsync()
    {
        if(_indicator.SelectedItem is not Indicator d)return;
        if(string.IsNullOrWhiteSpace(_notes.Text))
        {MessageBox.Show("Agrega la fuente y aclara si se trata de cifras ficticias.");return;}
        var scope=CurrentScope();
        var res=await _api.SendAsync(HttpMethod.Post,"api/management/goals/observations",new
        {
            metricId=d.Id,year=(int)_year.Value,month=DateTime.Today.Month,observedValue=_observed.Value,
            sourceNote=_notes.Text.Trim(),isDemonstration=true,zoneId=scope.Zone,siteId=scope.Site
        });
        _notice.Text=res.Success?"Medición de demostración registrada en SQL.":res.Error;
        if(res.Success)await ReloadAsync();
    }
    private async Task EvaluateAsync()
    {
        if(_goals.SelectedRows.Count==0)return;
        if(!Guid.TryParse(_goals.SelectedRows[0].Cells[0].Value?.ToString(),out var id))return;
        if(_notes.Text.Trim().Length==0){MessageBox.Show("Registra la fuente o justificación de esta evaluación.");return;}
        var res=await _api.SendAsync(HttpMethod.Post,$"api/management/goals/{id}/evaluate",new{observedValue=_observed.Value,status=_status.SelectedIndex+1,explanation=_notes.Text.Trim()});
        if(!res.Success){MessageBox.Show(res.Error,"Evaluación");return;}
        _notes.Clear();await ReloadAsync();
    }
}
