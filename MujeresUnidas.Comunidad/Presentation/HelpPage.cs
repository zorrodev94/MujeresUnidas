using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Devices;
using MujeresUnidas.Comunidad.Connection;
using Communication = Microsoft.Maui.ApplicationModel.Communication;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Safety resource hub. Opens native apps; never dials or sends messages automatically.</summary>
    [UsesData(typeof(HelpPage.CreateContactData))]
    public sealed class HelpPage : ContentPage
    {
        #region DTO
        public sealed record CreateContactData(string Name, string Relationship, string Phone);
        #endregion

        #region State
        private readonly ApiClient _api;
        private readonly VerticalStackLayout _contacts = new() { Spacing = 12 };
        private readonly Label _feedback = Ui.Text(string.Empty, 12);
        private readonly Entry _name = Ui.Input("Nombre o alias");
        private readonly Entry _relationship = Ui.Input("RelaciÃ³n: mamÃ¡, hermano, amistad...");
        private readonly Entry _phone = Ui.Input("NÃºmero con clave de paÃ­s si aplica");
        #endregion

        #region Initialization
        public HelpPage(ApiClient api)
        {
            _api = api;
            Title = "Ayuda y seguridad";
            NavigationPage.SetHasNavigationBar(this, false);
            var root = new Grid { BackgroundColor = Ui.Background,
                RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
            var body = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(16, 12, 16, 25) };
            body.Children.Add(Ui.Header(this, api, "Centro de ayuda y seguridad"));
            body.Children.Add(Ui.Heading("Ayuda y seguridad", 27));
            body.Children.Add(Ui.Tag("Recursos para actuar con calma y pedir apoyo",true));
            body.Children.Add(Ui.Cover(new Image { Source = "mu_header.png", Aspect = Aspect.AspectFill,
                HeightRequest = 94 }, 94));
            body.Children.Add(Ui.Notice("Si estÃ¡s en peligro inmediato, utiliza el 911. Esta aplicaciÃ³n acadÃ©mica no manda alertas automÃ¡ticas ni sustituye los servicios de emergencia."));
            body.Children.Add(Ui.Section("LÃ­neas oficiales", OfficialNumbers()));
            body.Children.Add(Ui.Section("Mis contactos de confianza", ContactForm()));
            body.Children.Add(_feedback);
            body.Children.Add(_contacts);
            var incident = Ui.Primary("Registrar una situaciÃ³n no urgente");
            incident.Clicked += async (_, _) => await Navigation.PushAsync(new IncidentReportPage(_api));
            body.Children.Add(incident);
            body.Children.Add(Ui.Notice("Los telÃ©fonos externos no se vinculan con seguidores ni mensajes de Comunidad. Puedes borrarlos cuando quieras."));
            root.Add(Ui.Scroll(body), 0, 0);
            root.Add(Ui.MainTabs(this, api, "Ayuda"), 0, 1);
            Content = root;
        }

        private View OfficialNumbers()
        {
            var layout = new VerticalStackLayout { Spacing = 10 };
            foreach (var (name, number, detail) in new (string, string, string)[]
            {
                ("Emergencias", "911", "Riesgo inmediato en MÃ©xico"),
                ("Denuncia anÃ³nima", "089", "InformaciÃ³n no urgente sobre posibles delitos"),
                ("LÃ­nea de las Mujeres", "079", "OpciÃ³n 1 Â· OrientaciÃ³n y acompaÃ±amiento"),
                ("LÃ­nea Mujer Segura Sinaloa", "8008227956", "AtenciÃ³n psicolÃ³gica, jurÃ­dica y social")
            })
            {
                var details = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.Fill };
                details.Children.Add(Ui.Heading(name, 15));
                details.Children.Add(Ui.Text(detail, 12));
                details.Children.Add(Ui.Text("TelÃ©fono: " + number, 12));
                var call = Ui.Secondary("Llamar");
                call.WidthRequest = 96;
                call.Clicked += async (_, _) => await DialAsync(number, name);
                var card = new Grid { ColumnSpacing = 8, ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),new ColumnDefinition(new GridLength(90))
                }};
                card.Add(details,0,0);card.Add(call,1,0);
                layout.Children.Add(Ui.Card(card, 10));
            }
            return layout;
        }

        private View ContactForm()
        {
            _name.MaxLength = 70;
            _relationship.MaxLength = 35;
            _phone.MaxLength = 25;
            _phone.Keyboard = Keyboard.Telephone;
            var form = new VerticalStackLayout { Spacing = 8 };
            form.Children.Add(Ui.Text("Hasta 3 personas externas. Solo se comparte el nÃºmero que elijas.", 12));
            form.Children.Add(Ui.Field(_name));
            form.Children.Add(Ui.Field(_relationship));
            form.Children.Add(Ui.Field(_phone));
            var options = new Grid { ColumnDefinitions = {new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)}, ColumnSpacing = 8 };
            var pick = Ui.Secondary("Desde mi telÃ©fono");
            pick.Clicked += async (_, _) => await PickContactAsync();
            pick.IsVisible = DeviceInfo.Platform != DevicePlatform.WinUI;
            if (!pick.IsVisible)
            {
                options.ColumnDefinitions[0].Width = new GridLength(0);
                options.ColumnDefinitions[1].Width = GridLength.Star;
            }
            var save = Ui.Primary("Guardar");
            save.Clicked += async (_, _) => await AddAsync(save);
            options.Add(pick, 0, 0);
            options.Add(save, 1, 0);
            form.Children.Add(options);
            return form;
        }
        #endregion

        #region Tasks
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshContactsAsync();
        }

        private async Task PickContactAsync()
        {
            try
            {
                var selected = await Communication.Contacts.Default.PickContactAsync();
                if (selected is null) return;
                var number = selected.Phones?.FirstOrDefault()?.PhoneNumber;
                if (string.IsNullOrWhiteSpace(number))
                { _feedback.Text = "Ese contacto no tiene un telÃ©fono disponible."; return; }
                _name.Text = selected.DisplayName;
                _phone.Text = number;
                _feedback.Text = "Revisa el nÃºmero y agrega la relaciÃ³n antes de guardar.";
            }
            catch (Exception)
            { _feedback.Text = "No se pudo abrir la agenda. Puedes capturar el contacto manualmente."; }
        }

        private async Task AddAsync(Button button)
        {
            var data = new CreateContactData((_name.Text ?? "").Trim(),
                (_relationship.Text ?? "").Trim(), (_phone.Text ?? "").Trim());
            if (!await DisplayAlertAsync("Guardar contacto", "Â¿Guardar este telÃ©fono personal cifrado en Server para tu cuenta?", "Guardar", "Cancelar")) return;
            button.IsEnabled = false;
            try
            {
                var result = await _api.RequestAsync(HttpMethod.Post, "api/safety/contacts", data);
                _feedback.Text = result.Success ? "Contacto guardado." : result.Error;
                if (result.Success)
                {
                    _name.Text = _phone.Text = _relationship.Text = "";
                    await RefreshContactsAsync();
                }
            }
            finally { button.IsEnabled = true; }
        }

        private async Task RefreshContactsAsync()
        {
            _contacts.Children.Clear();
            var response = await _api.RequestAsync(HttpMethod.Get, "api/safety/contacts");
            if (!response.Success) { _contacts.Children.Add(Ui.Notice(response.Error)); return; }
            if (response.Data.ValueKind != JsonValueKind.Array) return;
            foreach (var item in response.Data.EnumerateArray())
            {
                var id = item.GetProperty("id").GetGuid();
                var name = item.GetProperty("name").GetString() ?? "Contacto";
                var relation = item.GetProperty("relationship").GetString() ?? "";
                var phone = item.GetProperty("phone").GetString() ?? "";
                var box = new VerticalStackLayout { Spacing = 7 };
                box.Children.Add(Ui.Heading(name, 17));
                box.Children.Add(Ui.Text(relation + " Â· " + phone, 12));
                var actions = new HorizontalStackLayout { Spacing = 6 };
                var call = Ui.Secondary("Llamar");
                call.Clicked += async (_, _) => await DialAsync(phone, name);
                actions.Children.Add(call);
                var sms = Ui.Secondary("SMS");
                sms.Clicked += async (_, _) => await SendSmsAsync(phone);
                actions.Children.Add(sms);
                var whats = Ui.Secondary("WhatsApp");
                whats.Clicked += async (_, _) => await WhatsAppAsync(phone);
                actions.Children.Add(whats);
                box.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = actions });
                var remove = Ui.Secondary("Eliminar contacto");
                remove.Clicked += async (_, _) => await DeleteAsync(id, name);
                box.Children.Add(remove);
                _contacts.Children.Add(Ui.Card(box, 13));
            }
            if (_contacts.Children.Count == 0) _contacts.Children.Add(Ui.Notice("TodavÃ­a no agregaste contactos de confianza."));
        }

        private async Task DeleteAsync(Guid id, string name)
        {
            if (!await DisplayAlertAsync("Eliminar contacto", $"Â¿Eliminar a {name} de tu lista de confianza?", "Eliminar", "Cancelar")) return;
            var result = await _api.RequestAsync(HttpMethod.Delete, $"api/safety/contacts/{id}");
            _feedback.Text = result.Success ? "Contacto eliminado." : result.Error;
            if (result.Success) await RefreshContactsAsync();
        }

        private async Task DialAsync(string number, string displayName)
        {
            if (!await DisplayAlertAsync("Abrir marcador", $"Â¿Abrir el marcador para {displayName} ({number})? La llamada la realizas tÃº.", "Abrir", "Cancelar")) return;
            try
            {
                if (PhoneDialer.Default.IsSupported) PhoneDialer.Default.Open(number);
                else await DisplayAlertAsync("No disponible", "Este dispositivo no tiene marcador telefÃ³nico.", "Aceptar");
            }
            catch (Exception)
            { _feedback.Text = "No se pudo abrir el marcador en este dispositivo."; }
        }

        private const string ReadyMessage = "Hola, necesito hablar contigo. Â¿Puedes llamarme cuando veas este mensaje?";
        private async Task SendSmsAsync(string phone)
        {
            if (!await DisplayAlertAsync("Preparar SMS", "Abriremos la aplicaciÃ³n de mensajes con un texto editable. TÃº decides si enviarlo.", "Continuar", "Cancelar")) return;
            try
            {
                if (Sms.Default.IsComposeSupported)
                    await Sms.Default.ComposeAsync(new SmsMessage(ReadyMessage, new string[] { phone }));
                else _feedback.Text = "No hay una aplicaciÃ³n de SMS disponible.";
            }
            catch (Exception)
            { _feedback.Text = "No se pudo abrir la aplicaciÃ³n SMS."; }
        }

        private async Task WhatsAppAsync(string phone)
        {
            string digits = new(phone.Where(char.IsDigit).ToArray());
            if (!phone.TrimStart().StartsWith('+'))
            {
                _feedback.Text = "Para WhatsApp guarda el nÃºmero con prefijo internacional, por ejemplo +52 en MÃ©xico.";
                return;
            }
            if (!await DisplayAlertAsync("Preparar WhatsApp", "Abriremos WhatsApp con un mensaje editable. Nada se envÃ­a automÃ¡ticamente.", "Abrir", "Cancelar")) return;
            if (digits.Length is < 7 or > 15) { _feedback.Text = "El telÃ©fono no tiene formato internacional vÃ¡lido."; return; }
            try
            {
                await Launcher.Default.OpenAsync(new Uri("https://wa.me/" + digits + "?text=" + Uri.EscapeDataString(ReadyMessage)));
            }
            catch (Exception ex) when (ex is FeatureNotSupportedException or UriFormatException)
            { _feedback.Text = "No fue posible abrir WhatsApp."; }
        }
        #endregion
    }
}

