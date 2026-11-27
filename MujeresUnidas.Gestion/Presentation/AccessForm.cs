using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using MujeresUnidas.Gestion.Connection;
using MujeresUnidas.Gestion.Models.User.Subclasses;
using BaseUser = global::MujeresUnidas.Gestion.Models.User.Base.User;

namespace MujeresUnidas.Gestion.Presentation
{
    /// <summary>Owner activation and institutional login with shared, responsive WinForms controls.</summary>
    [UsesData(typeof(BaseUser.LoginData))]
    [UsesData(typeof(OwnerUser.SetupData))]
    public sealed class AccessForm : Form
    {
        #region Operation Data
        private readonly ApiConnection _api;
        private readonly OwnerUser _owner;
        private readonly ManagementUser _management;
        private readonly TableLayoutPanel _fields;
        private readonly Label _headline;
        private readonly Label _subtitle;
        private TextBox _name = null!;
        private TextBox _email = null!;
        private TextBox _password = null!;
        private TextBox _confirm = null!;
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
            Icon=Theme.AppIcon();ShowIcon=true;ShowInTaskbar=true;
            Font = Theme.Text(11);
            BackColor = Theme.Background;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            MinimumSize = new Size(740, 530);
            Size = new Size(1150, 780);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                BackColor = Color.FromArgb(248, 232, 251), Margin = Padding.Empty,
                Padding = new Padding(2)
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(shell);
            shell.Controls.Add(CreateTitleBar(), 0, 0);

            var columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            shell.Controls.Add(columns, 0, 1);

            var brand = new GradientPanel
            {
                Dock = DockStyle.Fill,
                Start = Color.FromArgb(235, 197, 249),
                End = Color.FromArgb(251, 231, 251),
                Padding = new Padding(23, 16, 23, 16)
            };
            columns.Controls.Add(brand, 0, 0);
            var bRows = Theme.Rows(
                (140, SizeType.Absolute), (100, SizeType.Percent),
                (46, SizeType.Absolute), (94, SizeType.Absolute));
            brand.Controls.Add(bRows);
            bRows.Controls.Add(Theme.Picture("mu_brand.png"), 0, 0);
            bRows.Controls.Add(Theme.Picture("mu_r31_people.png"), 0, 1);
            var slogan = Theme.Label("UN ESPACIO DE APOYO, INFORMACIÓN Y OPORTUNIDADES", 12, true);
            slogan.TextAlign = ContentAlignment.MiddleCenter;
            bRows.Controls.Add(slogan, 0, 2);
            var last = Theme.Label("Seguridad, organización y atención cercana.\nPrototipo educativo para Ahome.", 11, false);
            last.TextAlign = ContentAlignment.MiddleCenter;
            bRows.Controls.Add(last, 0, 3);

            var right = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(251, 245, 254),
                Padding = new Padding(29, 24, 29, 24)
            };
            columns.Controls.Add(right, 1, 0);
            var rightRows = Theme.Rows(
                (64, SizeType.Absolute), (40, SizeType.Absolute),
                (100, SizeType.Percent), (39, SizeType.Absolute));
            right.Controls.Add(rightRows);
            _headline = Theme.Label("Mujeres Unidas · Gestión", 23, true);
            rightRows.Controls.Add(_headline, 0, 0);
            _subtitle = Theme.Label("Acceso del personal institucional", 12, false, Theme.Muted);
            rightRows.Controls.Add(_subtitle, 0, 1);

            var card = new Theme.RoundedPanel
            {
                Dock = DockStyle.Fill, BackColor = Color.White,
                Padding = new Padding(22, 14, 22, 14),
                Margin = new Padding(0, 10, 0, 7)
            };

            rightRows.Controls.Add(card, 0, 2);
            _fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true,
                BackColor = Color.White, Margin = Padding.Empty
            };
            _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Controls.Add(_fields);
            var trust = Theme.Label("🔒  Servidor HTTPS · Solo datos ficticios durante el hackathon.", 9, false, Theme.Muted);
            rightRows.Controls.Add(trust, 0, 3);
            Shown += async (_, _) => await LoadStateAsync();
        }
        private Control CreateTitleBar()
        {
            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
                BackColor = Theme.Plum, Margin = Padding.Empty,
                Padding = new Padding(13, 0, 6, 0)
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 41));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 49));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 49));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 49));
            top.Controls.Add(Theme.Picture("mu_mark.png"), 0, 0);
            var title = Theme.Label("Mujeres Unidas · Gestión", 12, true, Color.White);
            title.MouseDown += DragTitleBar;
            top.Controls.Add(title, 1, 0);
            var minimize = TitleButton("—");
            minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
            top.Controls.Add(minimize, 2, 0);
            var maximize = TitleButton("□");
            maximize.Click += (_, _) => WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
            top.Controls.Add(maximize, 3, 0);
            var close = TitleButton("✕", Color.FromArgb(166, 54, 127));
            close.Click += (_, _) => Close();
            top.Controls.Add(close, 4, 0);
            top.MouseDown += DragTitleBar;
            return top;
        }
        private static Button TitleButton(string text, Color? background = null)
        {
            var result = new Button
            {
                Text = text, Dock = DockStyle.Fill,
                BackColor = background ?? Theme.Plum, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = Theme.Text(14, true),
                Cursor = Cursors.Hand, TabStop = false, Margin = new Padding(4, 6, 4, 6)
            };
            result.FlatAppearance.BorderSize = 0;
            return result;
        }
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);
        private void DragTitleBar(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var area = Screen.FromControl(this).WorkingArea;
            int width = Math.Min(1150, Math.Max(690, area.Width - 25));
            int height = Math.Min(780, Math.Max(510, area.Height - 25));
            MinimumSize = new Size(Math.Min(740, width), Math.Min(530, height));
            Size = new Size(width, height);
            Location = new Point(area.Left + Math.Max(0, (area.Width - width) / 2),
                area.Top + Math.Max(0, (area.Height - height) / 2));
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
            _fields.Padding = new Padding(2, 4, 7, 7);
            _headline.Text = _isFirstSetup ? "Activación inicial del propietario" : "Mujeres Unidas · Gestión";
            _headline.Font = Theme.Text(_isFirstSetup ? 19 : 23, true);
            _subtitle.Text = _isFirstSetup
                ? "Configura la única cuenta Owner autorizada."
                : "Acceso del personal institucional";

            void Add(Control control, int height)
            {
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(4, 4, 4, 4);
                _fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                _fields.RowCount++;
                _fields.Controls.Add(control, 0, _fields.RowCount - 1);
            }
            void AddEntry(string label, TextBox input, bool secret = false)
            {
                var wrapper = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                    Margin = Padding.Empty, Padding = Padding.Empty,
                    BackColor = Color.White
                };
                wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                wrapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
                wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                wrapper.Controls.Add(Theme.Label(label, 10, true), 0, 0);
                var inputBorder = new Panel
                {
                    Dock = DockStyle.Fill, BackColor = Color.White,
                    Padding = new Padding(12, 7, 12, 7), Margin = Padding.Empty
                };
                inputBorder.Paint += (_, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RoundedPath(new Rectangle(1, 1,
                        Math.Max(2, inputBorder.Width - 3), Math.Max(2, inputBorder.Height - 3)), 15);
                    using var pen = new Pen(Color.FromArgb(195, 154, 217), 1.5f);
                    e.Graphics.DrawPath(pen, path);
                };
                input.BorderStyle = BorderStyle.None;
                input.BackColor = Color.White;
                input.Font = Theme.Text(11);
                input.Dock = DockStyle.Fill;
                string iconName = label.StartsWith("Correo") ? "mu_auth_mail.png"
                    : label.StartsWith("Clave") ? "mu_auth_key.png"
                    : label.Contains("contraseña", StringComparison.OrdinalIgnoreCase) ? "mu_auth_lock.png"
                    : "mu_auth_user.png";
                var inside = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, RowCount = 1,
                    ColumnCount = secret ? 3 : 2,
                    Margin = Padding.Empty, Padding = Padding.Empty,
                    BackColor = Color.White
                };
                inside.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                inside.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 29));
                inside.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                if (secret) inside.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
                var img = new PictureBox
                {
                    Image = Theme.LoadImage(iconName), SizeMode = PictureBoxSizeMode.Zoom,
                    Dock = DockStyle.Fill, Margin = new Padding(1, 3, 7, 3)
                };
                inside.Controls.Add(img, 0, 0);
                inside.Controls.Add(input, 1, 0);
                if (secret)
                {
                    var reveal = new Button
                    {
                        Text = string.Empty, Dock = DockStyle.Fill,
                        FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(245, 233, 252),
                        ForeColor = Theme.Plum, Font = Theme.Text(9, true),
                        Image=Theme.LoadImage("action_eye_dark.png"),ImageAlign=ContentAlignment.MiddleCenter,
                        AccessibleName="Mostrar contraseña",Cursor = Cursors.Hand, Margin = Padding.Empty
                    };
                    reveal.FlatAppearance.BorderSize = 0;
                    reveal.Click += (_, _) =>
                    {
                        input.UseSystemPasswordChar = !input.UseSystemPasswordChar;
                        reveal.Image=Theme.LoadImage(input.UseSystemPasswordChar?
                            "action_eye_dark.png":"action_eye_off_dark.png");
                        reveal.AccessibleName=input.UseSystemPasswordChar?"Mostrar contraseña":"Ocultar contraseña";
                    };
                    inside.Controls.Add(reveal, 2, 0);
                }
                inputBorder.Controls.Add(inside);
                wrapper.Controls.Add(inputBorder, 0, 1);
                Add(wrapper, 85);
            }
            Add(Theme.Label(_isFirstSetup ? "Configurar autoridad principal" : "¡Bienvenida de nuevo!", 21, true), 55);
            Add(Theme.Label(_isFirstSetup
                ? "Esta activación se realiza una sola vez. Usa la clave de instalación configurada en Server."
                : "Ingresa con tus credenciales institucionales para continuar.",
                10, false, Theme.Muted), 53);
            if (_isFirstSetup)
            {
                _name = Theme.Input("Nombre completo");
                AddEntry("Nombre completo *", _name);
            }
            _email = Theme.Input("nombre@correo.com");
            AddEntry("Correo electrónico *", _email);
            _password = Theme.Input("Contraseña", true);
            AddEntry("Contraseña *", _password, true);
            if (_isFirstSetup)
            {
                _confirm = Theme.Input("Confirmar contraseña", true);
                AddEntry("Confirmar contraseña *", _confirm, true);
                _setupKey = Theme.Input("Clave inicial de instalación", true);
                AddEntry("Clave de instalación *", _setupKey, true);
            }
            var submit = Theme.Button(_isFirstSetup ? "Activar y continuar" : "Iniciar sesión");
            submit.FlatAppearance.BorderSize = 0;
            submit.Resize += (_, _) =>
            {
                if (submit.Width > 12 && submit.Height > 12)
                    submit.Region = new Region(RoundedPath(new Rectangle(0, 0,
                        submit.Width, submit.Height), 16));
            };
            submit.Click += async (_, _) => await SubmitAsync(submit);
            Add(submit, 62);
            _feedback = Theme.Label(message ?? (_isFirstSetup
                ? "Solo la instalación inicial puede crear la autoridad principal."
                : "¿Problemas para acceder? Contacta a la autoridad principal."),
                10, false, Theme.Muted);
            Add(_feedback, 62);
            _fields.ResumeLayout(true);
        }
        private async Task SubmitAsync(Button button)
        {
            if (string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_password.Text))
            {
                _feedback.Text = "Ingresa correo electrónico y contraseña.";
                return;
            }
            if (_isFirstSetup && _password.Text != _confirm.Text)
            {
                _feedback.Text = "Las contraseñas no coinciden.";
                return;
            }
            button.Enabled = false;
            _feedback.Text = "Validando solicitud...";
            try
            {
                if (_isFirstSetup)
                {
                    var result = await _owner.RegisterAsync(new OwnerUser.SetupData
                    {
                        SetupKey = _setupKey.Text, Email = _email.Text.Trim(),
                        Password = _password.Text, DisplayName = _name.Text.Trim()
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
                if (_api.CurrentUser.ValueKind==System.Text.Json.JsonValueKind.Object &&
                    _api.CurrentUser.TryGetProperty("mustChangePassword",out var change) && change.ValueKind==System.Text.Json.JsonValueKind.True)
                {
                    await RequirePasswordChangeAsync(_password.Text);
                    return;
                }
                Hide();
                using (var dashboard = new DashboardForm(_api)) dashboard.ShowDialog(this);
                // Closing the management window must revoke the server session and
                // return to the login form, never leave institutional data exposed.
                await _api.SignOutAsync();
                _password.Clear();
                _feedback.Text = "Sesión finalizada. Inicia sesión para continuar.";
                Show();
            }
            catch (Exception ex)
            {
                _feedback.Text = "No se pudo procesar la respuesta del servidor. Revisa la salida de depuración.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                if (!IsDisposed && !button.IsDisposed) button.Enabled = true;
            }
        }
        /// <summary>Forces invited staff to rotate the temporary password before accessing the dashboard.</summary>
        private async Task RequirePasswordChangeAsync(string temporary)
        {
            using var dialog=new Form{Text="Actualizar contraseña temporal",Icon=Theme.AppIcon(),
                StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,
                Size=new Size(520,330),BackColor=Theme.Background,MaximizeBox=false,MinimizeBox=false};
            var rows=Theme.Rows((66,SizeType.Absolute),(55,SizeType.Absolute),(55,SizeType.Absolute),
                (55,SizeType.Absolute),(64,SizeType.Absolute));
            rows.Padding=new Padding(18);dialog.Controls.Add(rows);
            rows.Controls.Add(Theme.Label("Debes cambiar tu contraseña inicial",17,true),0,0);
            rows.Controls.Add(Theme.Label("Esta contraseña solo es temporal. Elige una nueva, mínimo 10 caracteres.",10),0,1);
            var first=Theme.Input("Nueva contraseña",true);rows.Controls.Add(first,0,2);
            var repeat=Theme.Input("Confirma contraseña",true);rows.Controls.Add(repeat,0,3);
            var submit=Theme.Button("Cambiar contraseña");rows.Controls.Add(submit,0,4);
            submit.Click+=(_,_)=>
            {
                if(first.Text.Length<10 || first.Text!=repeat.Text)
                {MessageBox.Show(dialog,"La contraseña debe tener 10 caracteres y coincidir.");return;}
                dialog.DialogResult=DialogResult.OK;dialog.Close();
            };
            if(dialog.ShowDialog(this)!=DialogResult.OK){await _api.SignOutAsync();_feedback.Text="Debe cambiar la contraseña temporal para acceder.";return;}
            var updated=await _api.SendAsync(HttpMethod.Put,"api/auth/change-password",
                new{currentPassword=temporary,newPassword=first.Text});
            if(!updated.Success){_feedback.Text=updated.Error;await _api.SignOutAsync();return;}
            _api.SignOut();
            _feedback.Text="Contraseña actualizada y sesiones revocadas. Inicia sesión con la nueva contraseña.";
            _password.Clear();
            MessageBox.Show(this,"Contraseña actualizada. Inicia sesión nuevamente.","Seguridad",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        #endregion Task
        #region Components
        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            var result = new GraphicsPath();
            result.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            result.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            result.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            result.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            result.CloseFigure();
            return result;
        }
        private sealed class GradientPanel : Panel
        {
            internal Color Start;
            internal Color End;
            internal GradientPanel() { DoubleBuffered = true; }
            protected override void OnPaintBackground(PaintEventArgs e)
            {
                using var brush = new LinearGradientBrush(ClientRectangle, Start, End, 45f);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }
        #endregion Components
    }
}
