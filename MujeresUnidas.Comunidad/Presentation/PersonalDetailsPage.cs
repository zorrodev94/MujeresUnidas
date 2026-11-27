using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Private, self-only extended account fields; these values never enter the public social profile.</summary>
    [UsesData(typeof(PersonalDetailsPage.PersonalData))]
    public sealed class PersonalDetailsPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly Dictionary<string,Entry> _inputs=new();
        private readonly Label _feedback=Ui.Text(string.Empty,12);
        private readonly DatePicker _birth=new(){Format="dd/MM/yyyy",MinimumDate=new DateTime(1900,1,1),MaximumDate=DateTime.Today};
        private readonly CheckBox _hasBirth=new();
        #endregion Operation Data
        #region Initialization
        public PersonalDetailsPage(ApiClient api)
        {
            _api=api;Title="Datos privados";NavigationPage.SetHasNavigationBar(this,false);
            var body=new VerticalStackLayout{Spacing=10,Padding=new Thickness(18,18,18,35)};
            body.Children.Add(Ui.Back(this));body.Children.Add(Ui.Heading("Mi información privada",23));
            body.Children.Add(Ui.Notice("CURP, RFC, domicilio, teléfono y fecha de nacimiento son confidenciales. No aparecen en perfiles públicos. Usa únicamente datos ficticios en el hackathon."));
            foreach(var (key,label) in new[]{("displayName","Nombre público"),("firstName","Nombre(s)"),
                ("paternalLastName","Apellido paterno"),("maternalLastName","Apellido materno"),
                ("biography","Descripción / biografía"),("phoneNumber","Teléfono"),
                ("alternatePhoneNumber","Teléfono alternativo"),("address","Domicilio"),
                ("city","Ciudad"),("state","Estado"),("postalCode","Código postal"),
                ("curp","CURP de demostración"),("rfc","RFC de demostración")})
            {
                body.Children.Add(Ui.Text(label,12));
                var entry=Ui.Input(label);_inputs[key]=entry;body.Children.Add(Ui.Field(entry));
            }
            body.Children.Add(Ui.Text("Fecha de nacimiento (opcional)",12));
            var birthRow=new HorizontalStackLayout{Spacing=8};
            birthRow.Children.Add(_hasBirth);birthRow.Children.Add(Ui.Text("Guardar fecha de nacimiento",12));
            body.Children.Add(birthRow);body.Children.Add(Ui.Card(_birth,10));
            var save=Ui.Primary("Guardar datos privados");save.Clicked+=async (_,_)=>await SaveAsync(save);
            body.Children.Add(save);body.Children.Add(_feedback);
            var password=Ui.Secondary("Cambiar contraseña");password.Clicked+=async (_,_)=>await Navigation.PushAsync(new ChangePasswordPage(_api));
            body.Children.Add(password);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            root.Add(Ui.Scroll(body),0,0);root.Add(Ui.MainTabs(this,api,string.Empty),0,1);Content=root;
        }
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        public struct PersonalData
        {
            public string DisplayName {get;set;}
            public string? Biography {get;set;}
            public string? FirstName {get;set;}
            public string? PaternalLastName {get;set;}
            public string? MaternalLastName {get;set;}
            public string? PhoneNumber {get;set;}
            public string? AlternatePhoneNumber {get;set;}
            public DateTime? DateOfBirth {get;set;}
            public string? Address {get;set;}
            public string? City {get;set;}
            public string? State {get;set;}
            public string? PostalCode {get;set;}
            public string? Curp {get;set;}
            public string? Rfc {get;set;}
        }
        #endregion Structs
        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            var response=await _api.RequestAsync(HttpMethod.Get,"api/accounts/me/details");
            if(!response.Success){_feedback.Text=response.Error;return;}
            foreach(var (key,entry) in _inputs)
                if(response.Data.TryGetProperty(key,out var value) && value.ValueKind==JsonValueKind.String)
                    entry.Text=value.GetString();
            if(response.Data.TryGetProperty("dateOfBirth",out var birth) && birth.ValueKind==JsonValueKind.String &&
               DateTime.TryParse(birth.GetString(),out var date))
            { _birth.Date=date;_hasBirth.IsChecked=true; }
        }
        private async Task SaveAsync(Button button)
        {
            var name=_inputs["displayName"].Text?.Trim()??string.Empty;
            if(name.Length is <2 or >100){_feedback.Text="Nombre público: entre 2 y 100 caracteres.";return;}
            string? Get(string key)=>string.IsNullOrWhiteSpace(_inputs[key].Text)?null:_inputs[key].Text.Trim();
            // Birth date is sent only when a date is selected; omitting is represented as null.
            var data=new PersonalData{DisplayName=name,Biography=Get("biography"),FirstName=Get("firstName"),
                PaternalLastName=Get("paternalLastName"),MaternalLastName=Get("maternalLastName"),
                PhoneNumber=Get("phoneNumber"),AlternatePhoneNumber=Get("alternatePhoneNumber"),
                Address=Get("address"),City=Get("city"),State=Get("state"),
                PostalCode=Get("postalCode"),Curp=Get("curp"),Rfc=Get("rfc"),
                DateOfBirth=_hasBirth.IsChecked?_birth.Date:null};
            button.IsEnabled=false;
            try
            {
                var response=await _api.RequestAsync(HttpMethod.Put,"api/accounts/me/details",data);
                _feedback.Text=response.Success?"Datos guardados. Solo tu cuenta puede consultar estos campos.":response.Error;
            }
            finally{button.IsEnabled=true;}
        }
        #endregion Task
    }
}
