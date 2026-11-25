using System.Text.RegularExpressions;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.User.Subclasses;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Community registration request; the account remains Pending until approved by Gestión.</summary>
    public sealed class RegisterPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityMember _member;
        private readonly Entry _name = Ui.Input("Nombre completo");
        private readonly Entry _curp = Ui.Input("CURP ficticia · 18 caracteres");
        private readonly Entry _email = Ui.Input("Correo electrónico");
        private readonly Entry _password = Ui.Input("Contraseña · mínimo 10 caracteres", true);
        private readonly Label _feedback = Ui.Text("Para el hackathon utiliza solamente datos ficticios.", 12);
        #endregion Operation Data

        #region Initialization
        public RegisterPage(ApiClient api)
        {
            _api = api;
            _member = new CommunityMember(api);
            Title = "Crear cuenta";
            _email.Keyboard = Keyboard.Email;
            _curp.MaxLength = 18;
            _curp.TextChanged += (_, _) =>
            {
                string normalized = _curp.Text?.ToUpperInvariant() ?? string.Empty;
                if (_curp.Text != normalized) _curp.Text = normalized;
            };

            var brand = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
            brand.Children.Add(Ui.Logo(150));
            brand.Children.Add(Ui.Illustration(300));
            var branding = Ui.Card(brand, 15);

            var form = new VerticalStackLayout { Spacing = 8 };
            var compactBrand = Ui.Logo(72);
            form.Children.Add(compactBrand);
            form.Children.Add(Ui.Heading("Crear cuenta", 26));
            form.Children.Add(Ui.Notice("Tu solicitud quedará pendiente de revisión por Gestión antes de su activación."));
            form.Children.Add(Ui.Text("Nombre", 12));
            form.Children.Add(Ui.Field(_name));
            form.Children.Add(Ui.Text("CURP de demostración", 12));
            form.Children.Add(Ui.Field(_curp));
            form.Children.Add(Ui.Text("Correo electrónico", 12));
            form.Children.Add(Ui.Field(_email));
            form.Children.Add(Ui.Text("Contraseña", 12));
            form.Children.Add(Ui.Field(_password));
            form.Children.Add(Ui.Text("Podrás completar tu perfil público después de iniciar sesión.", 12));
            var submit = Ui.Primary("Enviar solicitud");
            submit.Clicked += Register;
            form.Children.Add(submit);
            var back = Ui.Secondary("Volver a iniciar sesión");
            back.Clicked += async (_, _) => await Navigation.PopAsync();
            form.Children.Add(back);
            form.Children.Add(_feedback);
            var content = Ui.ResponsiveAuth(branding, Ui.Card(form, 16), this, compactBrand,
                desktop => form.Spacing = desktop ? 10 : 7);
            Content = Ui.Scroll(content);
        }
        #endregion Initialization

        #region Task
        private async void Register(object? sender, EventArgs args)
        {
            if (sender is not Button button) return;
            string curp = (_curp.Text ?? string.Empty).Trim().ToUpperInvariant();
            if ((_name.Text ?? string.Empty).Trim().Length < 2)
            { _feedback.Text = "Escribe un nombre de al menos dos caracteres."; return; }
            if (!Regex.IsMatch(curp, "^[A-Z0-9]{18}$"))
            { _feedback.Text = $"La CURP de prueba debe tener exactamente 18 caracteres. Escribiste {curp.Length}."; return; }
            if (string.IsNullOrWhiteSpace(_email.Text) || !_email.Text.Contains('@'))
            { _feedback.Text = "Escribe un correo electrónico válido."; return; }
            if ((_password.Text ?? string.Empty).Length < 10)
            { _feedback.Text = "La contraseña debe tener al menos diez caracteres."; return; }

            button.IsEnabled = false;
            try
            {
                var response = await _member.RegisterAsync(new CommunityMember.RegistrationData
                {
                    DisplayName = _name.Text!.Trim(), Curp = curp,
                    Email = _email.Text!.Trim(), Password = _password.Text ?? string.Empty
                });
                _feedback.Text = response.Message;
                if (response.Success)
                    await DisplayAlert("Solicitud recibida", "La cuenta permanece pendiente hasta ser activada desde Gestión.", "Entendido");
            }
            finally { button.IsEnabled = true; }
        }
        #endregion Task
    }
}
