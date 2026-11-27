using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Self-service requests, case states and confirmed care appointments; not an emergency channel.</summary>
    [UsesData(typeof(CarePage.CareRequestData))]
    public sealed class CarePage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly Picker _area=new(){Title="Tipo de orientación",TextColor=Ui.Plum};
        private readonly Entry _subject=Ui.Input("Asunto general · sin detalles íntimos");
        private readonly Label _feedback=Ui.Text(string.Empty,12);
        private readonly VerticalStackLayout _requests=new(){Spacing=9};
        private readonly VerticalStackLayout _appointments=new(){Spacing=9};
        private readonly VerticalStackLayout _requestPanel=new(){Spacing=12};
        private readonly VerticalStackLayout _historyPanel=new(){Spacing=12};
        #endregion Operation Data
        #region Initialization
        public CarePage(ApiClient api)
        {
            _api=api;Title="Mi atención";NavigationPage.SetHasNavigationBar(this,false);
            foreach(var item in new[]{"Atención psicológica","Orientación jurídica","Trabajo social","No estoy segura"})_area.Items.Add(item);
            _area.SelectedIndex=0;_subject.MaxLength=160;
            var content=new VerticalStackLayout{Spacing=13,Padding=new Thickness(17,12,17,38)};
            content.Children.Add(Ui.Header(this,api,"Solicitudes y citas"));content.Children.Add(Ui.Heading("Mi atención",25));
            content.Children.Add(Ui.Notice("Solicitud académica y privada. No se atienden emergencias desde esta aplicación. Si existe peligro inmediato llama al 911."));
            var form=new VerticalStackLayout{Spacing=10};
            form.Children.Add(Ui.Text("¿En qué área necesitas orientación?",12));form.Children.Add(Ui.Card(_area,10));
            form.Children.Add(Ui.Text("Asunto general (no incluyas relatos sensibles)",12));form.Children.Add(Ui.Field(_subject));
            var send=Ui.Primary("Solicitar atención");send.Clicked+=async (_,_)=>await RequestAsync(send);
            form.Children.Add(send);form.Children.Add(_feedback);
            var switcher=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)},ColumnSpacing=8};
            var requestTab=Ui.Primary("Solicitar atención");
            var historyTab=Ui.Secondary("Mis citas");
            requestTab.Clicked+=(_,_)=>ShowPanel(true);
            historyTab.Clicked+=(_,_)=>ShowPanel(false);
            switcher.Add(requestTab,0,0);switcher.Add(historyTab,1,0);
            content.Children.Add(switcher);
            _requestPanel.Children.Add(Ui.Section("Nueva solicitud",form));
            _historyPanel.Children.Add(Ui.Heading("Mis solicitudes",19));_historyPanel.Children.Add(_requests);
            _historyPanel.Children.Add(Ui.Heading("Citas y seguimiento",19));_historyPanel.Children.Add(_appointments);
            content.Children.Add(_requestPanel);
            content.Children.Add(_historyPanel);
            ShowPanel(true);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            root.Add(Ui.Scroll(content),0,0);root.Add(Ui.MainTabs(this,api,"Atención"),0,1);Content=root;
        }
        #endregion Initialization
        private void ShowPanel(bool requesting)
        {
            _requestPanel.IsVisible=requesting;
            _historyPanel.IsVisible=!requesting;
        }
        #region Arrays
        private static readonly string[] Areas={"Psychology","Legal","SocialWork","Other"};
        #endregion Arrays
        #region Presentation
        private static string Spanish(string? value)=>value switch
        {
            "Psychology"=>"Psicología","Legal"=>"Jurídica","SocialWork"=>"Trabajo social","Other"=>"Otra área",
            "Pending"=>"En espera","Approved"=>"Aprobada","Rejected"=>"Rechazada",
            "Scheduled"=>"Programada","Confirmed"=>"Confirmada","Completed"=>"Cumplida",
            "Cancelled"=>"Cancelada","NoShow"=>"No asistió",
            "Initial"=>"Inicial","FollowUp"=>"Seguimiento","Interdisciplinary"=>"Interdisciplinaria",
            "InPerson"=>"Presencial","Virtual"=>"Virtual","Telephone"=>"Telefónica",
            _=>value??string.Empty
        };
        #endregion Presentation
        #region Structs
        public readonly record struct CareRequestData(string Area,string Subject);
        #endregion Structs
        #region Task
        protected override async void OnAppearing(){base.OnAppearing();await RefreshAsync();}
        private async Task RequestAsync(Button button)
        {
            string subject=(_subject.Text??string.Empty).Trim();
            if(subject.Length is <4 or >160){_feedback.Text="Escribe un asunto general de 4 a 160 caracteres.";return;}
            if(!await DisplayAlertAsync("Solicitud privada","Gestión revisará tu solicitud. No es un canal de emergencia. ¿Continuar?","Enviar","Cancelar"))return;
            button.IsEnabled=false;
            try
            {
                var result=await _api.RequestAsync(HttpMethod.Post,"api/care/requests",
                    new CareRequestData(Areas[_area.SelectedIndex],subject));
                _feedback.Text=result.Success?"Solicitud recibida. Espera la revisión institucional.":result.Error;
                if(result.Success){_subject.Text=string.Empty;await RefreshAsync();ShowPanel(false);}
            }
            finally{button.IsEnabled=true;}
        }
        private async Task RefreshAsync()
        {
            _requests.Children.Clear();_appointments.Children.Clear();
            var requests=await _api.RequestAsync(HttpMethod.Get,"api/care/requests");
            if(!requests.Success)_requests.Children.Add(Ui.Notice(requests.Error));
            else
            {
                foreach(var item in requests.Data.EnumerateArray())
                {
                    var block=new VerticalStackLayout{Spacing=5};
                    block.Children.Add(Ui.Heading(item.GetProperty("subject").GetString()??"Solicitud",15));
                    block.Children.Add(Ui.Tag( Spanish(item.GetProperty("area").GetString())+" · "+Spanish(item.GetProperty("status").GetString())));
                    block.Children.Add(Ui.Text("Registro: "+item.GetProperty("createdUtc").GetDateTime().ToLocalTime().ToString("dd/MM/yyyy HH:mm"),11));
                    _requests.Children.Add(Ui.Card(block,12));
                }
                if(_requests.Children.Count==0)_requests.Children.Add(Ui.Notice("Todavía no tienes solicitudes de atención."));
            }
            var appointments=await _api.RequestAsync(HttpMethod.Get,"api/care/appointments");
            if(!appointments.Success)_appointments.Children.Add(Ui.Notice(appointments.Error));
            else
            {
                foreach(var item in appointments.Data.EnumerateArray())
                {
                    var block=new VerticalStackLayout{Spacing=5};
                    block.Children.Add(Ui.Heading("Cita · "+Spanish(item.GetProperty("appointmentType").GetString()),16));
                    block.Children.Add(Ui.Tag(Spanish(item.GetProperty("status").GetString())));
                    block.Children.Add(Ui.Text("Fecha: "+item.GetProperty("scheduledUtc").GetDateTime().ToLocalTime().ToString("dd/MM/yyyy HH:mm"),12));
                    block.Children.Add(Ui.Text("Profesional: "+item.GetProperty("professionalName").GetString(),12));
                    block.Children.Add(Ui.Text("Modalidad: "+Spanish(item.GetProperty("modality").GetString()),12));
                    if(item.TryGetProperty("locationLabel",out var place)&&place.ValueKind==JsonValueKind.String)
                        block.Children.Add(Ui.Text("Sede de referencia (demostración): "+place.GetString(),12));
                    block.Children.Add(Ui.Notice("Las sedes del Hackathon son ficticias; no acudas a una dirección no verificada."));
                    _appointments.Children.Add(Ui.Card(block,12));
                }
                if(_appointments.Children.Count==0)_appointments.Children.Add(Ui.Notice("Cuando Gestión programe una cita, aquí verás su fecha, profesional y modalidad."));
            }
        }
        #endregion Task
    }
}
