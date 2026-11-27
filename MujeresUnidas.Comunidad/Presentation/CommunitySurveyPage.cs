using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Api;
using MujeresUnidas.Comunidad.Models.Analytics;
using MujeresUnidas.Shared.Contracts;

namespace MujeresUnidas.Comunidad.Presentation;

/// <summary>A private opt-in survey: one answer per account, edited without private chats.</summary>
public sealed class CommunitySurveyPage:ContentPage
{
    private readonly CommunitySurveyClient _surveys;
    private readonly Label _title=Ui.Heading("Encuesta de progreso",24);
    private readonly Label _description=Ui.Text("Queremos conocer tus oportunidades de estudio y empleo.",13);
    private readonly Label _feedback=Ui.Text("Las respuestas son voluntarias y privadas.",12);
    private readonly Picker _study=new(){Title="Selecciona tu situación",TextColor=Ui.Plum};
    private readonly Picker _work=new(){Title="Selecciona tu situación",TextColor=Ui.Plum};
    private readonly Picker _contribution=new(){Title="Selecciona una opción",TextColor=Ui.Plum};
    private readonly Picker _resource=new(){Title="Selecciona una opción",TextColor=Ui.Plum};
    private readonly DatePicker _studyMonth=new(){Format="MMMM yyyy",MinimumDate=new DateTime(2000,1,1),MaximumDate=DateTime.Today};
    private readonly DatePicker _workMonth=new(){Format="MMMM yyyy",MinimumDate=new DateTime(2000,1,1),MaximumDate=DateTime.Today};
    private readonly CheckBox _studyKnown=new(){Color=Ui.Violet};
    private readonly CheckBox _workKnown=new(){Color=Ui.Violet};
    private readonly CheckBox _consent=new(){Color=Ui.Violet};
    private readonly Button _submit=Ui.Primary("Guardar mi respuesta");
    private readonly Button _withdraw=Ui.Secondary("Retirar esta respuesta");
    private Guid _surveyId;

    public CommunitySurveyPage(ApiClient api)
    {
        _surveys=new CommunitySurveyClient(new CommunityGateway(api));
        Title="Encuesta de progreso";
        NavigationPage.SetHasNavigationBar(this,false);
        _study.Items.Add("Prefiero no responder");_study.Items.Add("Sí, estudio");_study.Items.Add("No estudio");
        _work.Items.Add("Prefiero no responder");_work.Items.Add("Sí, trabajo");_work.Items.Add("No trabajo");
        _contribution.Items.Add("Prefiero no responder");_contribution.Items.Add("Sí, me ayudó");
        _contribution.Items.Add("Me ayudó parcialmente");_contribution.Items.Add("No influyó");
        _resource.Items.Add("Prefiero no responder");_resource.Items.Add("Empleos");
        _resource.Items.Add("Estudios y guías");_resource.Items.Add("Blogs");
        _resource.Items.Add("Apoyo de la comunidad");_resource.Items.Add("Ninguno por ahora");
        _study.SelectedIndex=0;_work.SelectedIndex=0;_contribution.SelectedIndex=0;_resource.SelectedIndex=0;
        _study.SelectedIndexChanged+=(_,_)=>_studyMonth.IsVisible=_study.SelectedIndex==1&&_studyKnown.IsChecked;
        _work.SelectedIndexChanged+=(_,_)=>_workMonth.IsVisible=_work.SelectedIndex==1&&_workKnown.IsChecked;
        _studyMonth.IsVisible=false;_workMonth.IsVisible=false;
        _studyKnown.CheckedChanged+=(_,_)=>_studyMonth.IsVisible=_study.SelectedIndex==1&&_studyKnown.IsChecked;
        _workKnown.CheckedChanged+=(_,_)=>_workMonth.IsVisible=_work.SelectedIndex==1&&_workKnown.IsChecked;
        var content=new VerticalStackLayout{Spacing=13,Padding=new Thickness(16,16,16,38),
            MaximumWidthRequest=620,HorizontalOptions=LayoutOptions.Fill};
        content.Children.Add(Ui.Back(this));
        content.Children.Add(Ui.Card(new VerticalStackLayout{Spacing=6,Children={
            Ui.Tag("PARTICIPACIÓN VOLUNTARIA"),_title,_description,
            Ui.Text("Podrás modificar o retirar tu respuesta. No afecta tu acceso, citas ni atención.",12)}}));
        content.Children.Add(Question("1. ¿Actualmente estudias?",_study));
        content.Children.Add(Ui.Card(new VerticalStackLayout{Spacing=6,Children={
            Ui.Text("¿Desde cuándo estudias? (opcional, solo si estudias)",12),new HorizontalStackLayout{Children={_studyKnown,Ui.Text("Indicar mes aproximado",12)}},_studyMonth}},12));
        content.Children.Add(Question("2. ¿Actualmente trabajas?",_work));
        content.Children.Add(Ui.Card(new VerticalStackLayout{Spacing=6,Children={
            Ui.Text("¿Desde cuándo trabajas? (opcional, solo si trabajas)",12),new HorizontalStackLayout{Children={_workKnown,Ui.Text("Indicar mes aproximado",12)}},_workMonth}},12));
        content.Children.Add(Question("3. ¿La comunidad contribuyó a tu progreso?",_contribution));
        content.Children.Add(Question("4. ¿Qué recurso fue más útil?",_resource));
        var approval=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Auto),new ColumnDefinition(GridLength.Star)},ColumnSpacing=9};
        approval.Add(_consent,0,0);approval.Add(Ui.Text("Acepto guardar estas respuestas voluntarias para medir resultados agregados.",12),1,0);
        content.Children.Add(Ui.Card(approval,12));
        _submit.Clicked+=async (_,_)=>await SaveAsync();
        _withdraw.Clicked+=async (_,_)=>await WithdrawAsync();
        content.Children.Add(_submit);content.Children.Add(_withdraw);
        content.Children.Add(Ui.Notice("Gestión únicamente recibe estadísticas agrupadas cuando hay al menos cinco participantes. Una respuesta no demuestra que una publicación causó el cambio."));
        content.Children.Add(_feedback);
        var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
            new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
        root.Add(Ui.Scroll(content),0,0);
        root.Add(Ui.MainTabs(this,api,string.Empty),0,1);
        Content=root;
        SetEnabled(false);
    }

    private static Border Question(string title,Picker picker) =>
        Ui.Card(new VerticalStackLayout{Spacing=8,Children={Ui.Heading(title,17),picker}},14);

    private void SetEnabled(bool active)
    {
        _submit.IsEnabled=active;_withdraw.IsEnabled=active;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _surveyId=Guid.Empty;SetEnabled(false);_consent.IsChecked=false;
        var available=await _surveys.ActiveAsync();
        if(!available.Success){_feedback.Text=available.Error;return;}
        var survey=available.Value?.FirstOrDefault();
        if(survey is null){_feedback.Text="No hay encuestas abiertas por ahora.";return;}
        _surveyId=survey.Id;_title.Text=survey.Title;_description.Text=survey.Description;
        SetEnabled(false);
        var mine=await _surveys.MineAsync(_surveyId);
        if(!mine.Success){_feedback.Text=mine.Error;return;}
        SetEnabled(true);
        if(mine.Value is not { } response){_withdraw.IsEnabled=false;return;}
        _study.SelectedIndex=response.IsStudying switch{true=>1,false=>2,_=>0};
        _work.SelectedIndex=response.IsWorking switch{true=>1,false=>2,_=>0};
        _contribution.SelectedIndex=response.Contribution switch{"Yes"=>1,"Partial"=>2,"No"=>3,_=>0};
        _resource.SelectedIndex=response.HelpfulResource switch{
            "Jobs"=>1,"Studies"=>2,"Blogs"=>3,"Community"=>4,"None"=>5,_=>0};
        _studyKnown.IsChecked=response.StudyStartedMonth.HasValue;
        if(response.StudyStartedMonth.HasValue)_studyMonth.Date=response.StudyStartedMonth.Value;
        _workKnown.IsChecked=response.WorkStartedMonth.HasValue;
        if(response.WorkStartedMonth.HasValue)_workMonth.Date=response.WorkStartedMonth.Value;
        _consent.IsChecked=false;
        _feedback.Text="Respuesta anterior encontrada. Para actualizarla, revisa y confirma nuevamente tu consentimiento.";
    }

    private async Task SaveAsync()
    {
        if(_surveyId==Guid.Empty || !_consent.IsChecked)
        {_feedback.Text="Marca el consentimiento voluntario antes de guardar.";return;}
        bool? Decode(Picker choice)=>choice.SelectedIndex switch{1=>true,2=>false,_=>null};
        string contribution=_contribution.SelectedIndex switch{1=>"Yes",2=>"Partial",3=>"No",_=>"PreferNot"};
        string resource=_resource.SelectedIndex switch{
            1=>"Jobs",2=>"Studies",3=>"Blogs",4=>"Community",5=>"None",_=>"PreferNot"};
        var answer=new CommunitySurveyAnswer(Decode(_study),Decode(_work),
            _study.SelectedIndex==1&&_studyKnown.IsChecked?_studyMonth.Date:null,
            _work.SelectedIndex==1&&_workKnown.IsChecked?_workMonth.Date:null,contribution,resource,true);
        bool hadResponse=_withdraw.IsEnabled;
        _submit.IsEnabled=false;_withdraw.IsEnabled=false;
        try
        {
            var result=await _surveys.SaveAsync(_surveyId,answer);
            _feedback.Text=result.Success?"Respuesta guardada de forma privada. Gracias por participar.":result.Error;
            if(result.Success){hadResponse=true;_consent.IsChecked=false;}
        }
        finally{_submit.IsEnabled=true;_withdraw.IsEnabled=hadResponse;}
    }

    private async Task WithdrawAsync()
    {
        if(_surveyId==Guid.Empty)return;
        if(!await DisplayAlertAsync("Retirar respuesta","Se retirará tu respuesta y las declaraciones voluntarias usadas en indicadores. ¿Continuar?","Retirar","Cancelar"))return;
        var result=await _surveys.WithdrawAsync(_surveyId);
        _feedback.Text=result.Success?"Respuesta retirada de la encuesta.":result.Error;
        if(result.Success){_withdraw.IsEnabled=false;_consent.IsChecked=false;_study.SelectedIndex=0;_work.SelectedIndex=0;_contribution.SelectedIndex=0;_resource.SelectedIndex=0;_studyKnown.IsChecked=false;_workKnown.IsChecked=false;}
    }
}
