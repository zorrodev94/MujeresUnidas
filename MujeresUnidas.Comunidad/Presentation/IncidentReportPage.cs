using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Nonurgent private academic reports; never described as police or emergency dispatch.</summary>
    [UsesData(typeof(IncidentReportPage.CreateIncidentData))]
    public sealed class IncidentReportPage : ContentPage
    {
        #region DTO
        public sealed record CreateIncidentData(string Category, string Summary);
        #endregion

        #region State
        private readonly ApiClient _api;
        private readonly Picker _category = new() { Title = "Tipo de situación", TextColor = Ui.Plum };
        private readonly Editor _summary = new() { Placeholder = "Describe brevemente la situación sin nombres completos ni direcciones privadas...",
            HeightRequest = 130, MaxLength = 900, TextColor = Ui.Plum, BackgroundColor = Colors.White };
        private readonly Label _feedback = Ui.Text(string.Empty, 12);
        private readonly VerticalStackLayout _history = new() { Spacing = 10 };
        private static readonly string[] Categories = ["Violence", "Theft", "Harassment", "Other"];
        #endregion

        #region Initialization
        public IncidentReportPage(ApiClient api)
        {
            _api = api;
            Title = "Reportar una situación";
            NavigationPage.SetHasNavigationBar(this, false);
            var root = new Grid { BackgroundColor = Ui.Background,
                RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
            var body = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 12, 16, 28) };
            body.Children.Add(Ui.Header(this, api, "Registro privado"));
            body.Children.Add(Ui.Heading("Reportar una situación", 24));
            body.Children.Add(Ui.Notice("No es una denuncia oficial ni una alerta a autoridades. Para peligro inmediato utiliza el 911. Usa solo información ficticia durante el Hackathon."));
            foreach (var item in new[] { "Violencia", "Robo o intento de robo", "Acoso", "Otra situación" })
                _category.Items.Add(item);
            _category.SelectedIndex = 0;
            body.Children.Add(Ui.Card(_category, 10));
            body.Children.Add(Ui.Card(_summary, 10));
            var submit = Ui.Primary("Registrar situación de prueba");
            submit.Clicked += async (_, _) => await SubmitAsync(submit);
            body.Children.Add(submit);
            body.Children.Add(_feedback);
            body.Children.Add(Ui.Heading("Mis registros", 20));
            body.Children.Add(_history);
            root.Add(Ui.Scroll(body), 0, 0);
            root.Add(Ui.MainTabs(this, api, "Ayuda"), 0, 1);
            Content = root;
        }
        #endregion

        #region Tasks
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }

        private async Task SubmitAsync(Button button)
        {
            string summary = (_summary.Text ?? "").Trim();
            if (summary.Length is < 15 or > 900) { _feedback.Text = "Resume la situación en 15 a 900 caracteres."; return; }
            if (!await DisplayAlertAsync("Registro académico", "Este formulario guarda un registro de prueba y NO solicita ayuda inmediata. ¿Continuar?", "Registrar", "Cancelar")) return;
            button.IsEnabled = false;
            try
            {
                var result = await _api.RequestAsync(HttpMethod.Post, "api/safety/incidents",
                    new CreateIncidentData(Categories[_category.SelectedIndex], summary));
                _feedback.Text = result.Success ? "Registro de prueba guardado. No se enviaron avisos externos." : result.Error;
                if (result.Success) { _summary.Text = ""; await LoadAsync(); }
            }
            finally { button.IsEnabled = true; }
        }

        private async Task LoadAsync()
        {
            _history.Children.Clear();
            var result = await _api.RequestAsync(HttpMethod.Get, "api/safety/incidents");
            if (!result.Success) { _history.Children.Add(Ui.Notice(result.Error)); return; }
            if (result.Data.ValueKind != JsonValueKind.Array) return;
            foreach (var item in result.Data.EnumerateArray())
            {
                var title = item.GetProperty("category").GetString() switch
                {
                    "Violence" => "Violencia", "Theft" => "Robo", "Harassment" => "Acoso", _ => "Otro"
                };
                var status = item.GetProperty("status").GetString() switch
                {
                    "Received" => "Recibido (sin asignación)", "UnderReview" => "En revisión", "Closed" => "Cerrado", _ => "Registrado"
                };
                var stack = new VerticalStackLayout { Spacing = 4 };
                stack.Children.Add(Ui.Heading(title, 16));
                stack.Children.Add(Ui.Tag(status));
                stack.Children.Add(Ui.Text(item.GetProperty("createdUtc").GetDateTime().ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 12));
                _history.Children.Add(Ui.Card(stack, 12));
            }
            if (_history.Children.Count == 0) _history.Children.Add(Ui.Notice("Aún no tienes registros de demostración."));
        }
        #endregion
    }
}
