using MujeresUnidas.Gestion.Connection;
using MujeresUnidas.Gestion.Models.User.Subclasses;
using BaseUser = global::MujeresUnidas.Gestion.Models.User.Base.User;

namespace MujeresUnidas.Gestion.Presentation
{
    /// <summary>First-time Owner setup and institutional login; the two clients use separate accounts.</summary>
    public sealed class AccessForm : Form
    {
        #region Operation Data
        private readonly ApiConnection _api;
        private readonly OwnerUser _owner;
        private readonly ManagementUser _management;
        private readonly TableLayoutPanel _fields;
        private TextBox _name = null!;
        private TextBox _email = null!;
        private TextBox _password = null!;
        private TextBox _setupKey = null!;
        private Label _feedback = null!;
        private bool _isFirstSetup;
        #endregion Operation Data

        #region Initialization
        public AccessForm(ApiConnection api)
        {
            _api = api;
            _owner = new OwnerUser(api);
            _management = new ManagementUser(api);
            Text = "Mujeres Unidas · Gestión";
            BackColor = Theme.Background;
            Font = Theme.Text();
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 540);
            Size = new Size(1050, 720);
            AutoScaleMode = AutoScaleMode.Dpi;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
            Controls.Add(root);

            var brand = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Lavender, Padding = new Padding(30, 26, 30, 26) };
            root.Controls.Add(brand, 0, 0);
            var brandRows = Theme.Rows((132, SizeType.Absolute), (45, SizeType.Absolute),
                (100, SizeType.Percent), (135, SizeType.Absolute));
            brand.Controls.Add(brandRows);
            brandRows.Controls.Add(Theme.Picture("mu_brand.png"), 0, 0);
            brandRows.Controls.Add(Theme.Label("UN ESPACIO DE APOYO, INFORMACIÓN Y OPORTUNIDADES", 10, true, Theme.Plum), 0, 1);
            brandRows.Controls.Add(Theme.Picture("mu_illustration.png"), 0, 2);
            brandRows.Controls.Add(Theme.Label("ACCESO INSTITUCIONAL\n\nSeguridad, organización y atención cercana.\nPrototipo educativo para Ahome.",
                13, false, Theme.Plum), 0, 3);

            var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(26, 22, 26, 18) };
            root.Controls.Add(right, 1, 0);
            var rightRows = Theme.Rows((56, SizeType.Absolute), (38, SizeType.Absolute), (100, SizeType.Percent),
                (39, SizeType.Absolute));
            right.Controls.Add(rightRows);
            rightRows.Controls.Add(Theme.Label("Mujeres Unidas · Gestión", 22, true), 0, 0);
            rightRows.Controls.Add(Theme.Label("Acceso del personal institucional", 12, false, Theme.Muted), 0, 1);

            var card = Theme.Card();
            card.Padding = new Padding(18, 12, 18, 11);
            rightRows.Controls.Add(card, 0, 2);
            _fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 0,
                AutoScroll = true, BackColor = Color.White, Margin = Padding.Empty
            };
            _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Controls.Add(_fields);
            rightRows.Controls.Add(Theme.Label("Servidor HTTPS · Solo datos ficticios durante el hackathon.",
                9, false, Theme.Muted), 0, 3);
            Shown += async (_, _) => await LoadStateAsync();
        }

        /// <summary>Starts inside the actual screen working area even with a scaled desktop.</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var screen = Screen.FromControl(this).WorkingArea;
            int width = Math.Min(1050, Math.Max(640, screen.Width - 24));
            int height = Math.Min(720, Math.Max(500, screen.Height - 24));
            MinimumSize = new Size(Math.Min(760, width), Math.Min(540, height));
            Size = new Size(width, height);
            Location = new Point(screen.Left + Math.Max(0, (screen.Width - width) / 2),
                screen.Top + Math.Max(0, (screen.Height - height) / 2));
        }
        #endregion Initialization

        #region Task
        private async Task LoadStateAsync()
        {
            var response = await _owner.OwnerExistsAsync();
            if (!response.Success)
            {
                _isFirstSetup = false;
                Render(response.Error);
                return;
            }

            _isFirstSetup = !response.OwnerExists;
            Render();
        }

        private void Render(string? message = null)
        {
            _fields.SuspendLayout();
            _fields.Controls.Clear();
            _fields.RowStyles.Clear();
            _fields.RowCount = 0;
            _fields.Padding = new Padding(6, 4, 6, 4);

            void Add(Control control, int height)
            {
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(4, 5, 4, 5);
                _fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                _fields.RowCount++;
                _fields.Controls.Add(control, 0, _fields.RowCount - 1);
            }

            Add(Theme.Label(_isFirstSetup ? "Activar primera cuenta" : "¡Bienvenida!", 21, true), 52);
            Add(Theme.Label(_isFirstSetup
                ? "Establece la única autoridad principal utilizando la clave configurada en Server."
                : "Ingresa con tus credenciales institucionales.", 11, false, Theme.Muted), 50);
            if (_isFirstSetup)
            {
                _name = Theme.Input("Nombre de la autoridad principal");
                Add(_name, 53);
            }
            _email = Theme.Input("Correo electrónico");
            Add(_email, 53);
            _password = Theme.Input("Contraseña", true);
            Add(_password, 53);
            if (_isFirstSetup)
            {
                _setupKey = Theme.Input("Clave de instalación inicial", true);
                Add(_setupKey, 53);
            }
            var submit = Theme.Button(_isFirstSetup ? "Configurar autoridad principal" : "Iniciar sesión");
            submit.Click += async (_, _) => await SubmitAsync(submit);
            Add(submit, 57);
            _feedback = Theme.Label(message ?? "El servidor debe estar iniciado antes de acceder.", 10, false, Theme.Muted);
            Add(_feedback, 57);
            _fields.ResumeLayout(true);
        }

        private async Task SubmitAsync(Button button)
        {
            if (string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_password.Text))
            {
                _feedback.Text = "Ingresa correo electrónico y contraseña.";
                return;
            }
            button.Enabled = false;
            try
            {
                if (_isFirstSetup)
                {
                    var result = await _owner.RegisterAsync(new OwnerUser.SetupData
                    {
                        SetupKey = _setupKey.Text, Email = _email.Text.Trim(), Password = _password.Text,
                        DisplayName = _name.Text.Trim()
                    });
                    if (!result.Success) { _feedback.Text = result.Message; return; }
                    MessageBox.Show("Autoridad principal creada. Inicia sesión para continuar.",
                        "Configuración finalizada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _isFirstSetup = false;
                    Render();
                    return;
                }
                var response = await _management.LoginAsync(new BaseUser.LoginData
                {
                    Email = _email.Text.Trim(), Password = _password.Text
                });
                if (!response.Success) { _feedback.Text = response.Message; return; }
                Hide();
                using (var dashboard = new DashboardForm(_api)) dashboard.ShowDialog(this);
                Close();
            }
            catch (Exception ex)
            {
                _feedback.Text = "No se pudo procesar la respuesta de Server. Consulta la salida de depuración.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                if (!IsDisposed && !button.IsDisposed) button.Enabled = true;
            }
        }
        #endregion Task
    }
}
