using Microsoft.Maui.Controls.Shapes;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Shared, responsive visual components for the two Community authentication screens.</summary>
    internal static class AuthUi
    {
        #region Theme
        internal static readonly Color Deep = Color.FromArgb("#45105F");
        internal static readonly Color Purple = Color.FromArgb("#762590");
        internal static readonly Color Orchid = Color.FromArgb("#C954C2");
        internal static readonly Color Canvas = Color.FromArgb("#F9F0FE");
        internal static readonly Color Light = Color.FromArgb("#F3DFFA");
        internal static readonly Color BorderTint = Color.FromArgb("#DFC0ED");
        #endregion Theme
        #region Components
        internal static LinearGradientBrush Gradient(Color start, Color end, bool horizontal = false)
        {
            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = horizontal ? new Point(1, 0) : new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(start, 0),
                    new GradientStop(end, 1)
                }
            };
        }
        internal static Border Surface(View content, double radius = 24, bool shadow = true)
        {
            var surface = new Border
            {
                Stroke = BorderTint,
                StrokeThickness = 1,
                BackgroundColor = Colors.White,
                StrokeShape = new RoundRectangle { CornerRadius = radius },
                Padding = new Thickness(22),
                Content = content
            };
            if (shadow)
            {
                surface.Shadow = new Shadow
                {
                    Brush = new SolidColorBrush(Color.FromArgb("#25163147")),
                    Radius = 20,
                    Offset = new Point(0, 7),
                    Opacity = 0.5f
                };
            }
            return surface;
        }
        internal static Border TitleBar(string subtitle)
        {
            var row = new Grid
            {
                Padding = new Thickness(17, 6),
                ColumnSpacing = 10,
                HeightRequest = 60,
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(42)),
                    new ColumnDefinition(GridLength.Star)
                }
            };
            row.Add(new Image { Source = "mu_mark.png", HeightRequest = 36, WidthRequest = 36, Aspect = Aspect.AspectFit }, 0, 0);
            var titles = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
            titles.Children.Add(new Label { Text = "Mujeres Unidas · Comunidad", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Colors.White });
            titles.Children.Add(new Label { Text = subtitle, FontSize = 11, TextColor = Color.FromArgb("#F1D6F7") });
            row.Add(titles, 1, 0);
            return new Border
            {
                Background = Gradient(Deep, Purple, true),
                StrokeThickness = 0,
                Padding = new Thickness(0), Content = row
            };
        }
        internal static Label Title(string value, double size = 27) => new()
        {
            Text = value, FontSize = size, FontAttributes = FontAttributes.Bold,
            TextColor = Deep, LineBreakMode = LineBreakMode.WordWrap
        };
        internal static Label Description(string value, double size = 13) => new()
        {
            Text = value, TextColor = Ui.Muted, FontSize = size,
            LineBreakMode = LineBreakMode.WordWrap
        };
        internal static Label Caption(string value) => new()
        {
            Text = value, FontSize = 12, FontAttributes = FontAttributes.Bold,
            TextColor = Deep
        };
        internal static View Field(Entry input, string label, string icon = "mu_auth_user.png", bool reveal = false)
        {
            var wrapper = new VerticalStackLayout { Spacing = 7 };
            wrapper.Children.Add(Caption(label));
            var grid = new Grid
            {
                ColumnSpacing = 7, HeightRequest = 51,
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(25)),
                    new ColumnDefinition(GridLength.Star)
                }
            };
            grid.Add(new Image
            {
                Source = icon, WidthRequest = 23, HeightRequest = 23,
                Aspect = Aspect.AspectFit, VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            input.BackgroundColor = Colors.Transparent;
            input.FontSize = 14;
            input.TextColor = Deep;
            input.VerticalOptions = LayoutOptions.Center;
            grid.Add(input, 1, 0);
            if (reveal)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(77)));
                var toggle = new Button
                {
                    Text = "Mostrar", FontSize = 11, FontAttributes = FontAttributes.Bold,
                    TextColor = Purple, BackgroundColor = Color.FromArgb("#F6ECFB"),
                    CornerRadius = 12, HeightRequest = 36, Padding = new Thickness(5, 0),
                    VerticalOptions = LayoutOptions.Center
                };
                toggle.Clicked += (_, _) =>
                {
                    input.IsPassword = !input.IsPassword;
                    toggle.Text = input.IsPassword ? "Mostrar" : "Ocultar";
                };
                grid.Add(toggle, 2, 0);
            }
            wrapper.Children.Add(new Border
            {
                Stroke = BorderTint, StrokeThickness = 1.5,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                BackgroundColor = Colors.White,
                Padding = new Thickness(13, 0), Content = grid
            });
            return wrapper;
        }
        internal static View PickerField(Picker picker, string label)
        {
            var stack = new VerticalStackLayout { Spacing = 7 };
            stack.Children.Add(Caption(label));
            picker.BackgroundColor = Colors.Transparent;
            picker.TextColor = Deep;
            stack.Children.Add(new Border
            {
                Stroke = BorderTint, StrokeThickness = 1.5, BackgroundColor = Colors.White,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(10, 0), Content = picker
            });
            return stack;
        }
        internal static Button MainButton(string text) => new()
        {
            Text = text, BackgroundColor = Purple, TextColor = Colors.White,
            FontSize = 15, FontAttributes = FontAttributes.Bold,
            CornerRadius = 17, HeightRequest = 56
        };
        internal static Button OutlineButton(string text) => new()
        {
            Text = text, BackgroundColor = Color.FromArgb("#F7EAFB"), TextColor = Deep,
            FontSize = 14, FontAttributes = FontAttributes.Bold,
            CornerRadius = 17, HeightRequest = 49,
            BorderColor = Color.FromArgb("#C49BDA"), BorderWidth = 1
        };
        internal static View PrivacyNote(string title, string subtitle)
        {
            var layout = new VerticalStackLayout { Spacing = 4 };
            layout.Children.Add(Caption(title));
            layout.Children.Add(Description(subtitle, 12));
            return new Border
            {
                StrokeThickness = 0, Background = Gradient(Color.FromArgb("#F2DEFA"), Color.FromArgb("#FBEFFA"), true),
                StrokeShape = new RoundRectangle { CornerRadius = 17 },
                Padding = new Thickness(16, 13), Content = layout
            };
        }
        internal static View ArtPanel(bool includeActions)
        {
            var art = new VerticalStackLayout
            {
                Spacing = 8, VerticalOptions = LayoutOptions.Center,
                Padding = new Thickness(18, 23)
            };
            art.Children.Add(new Image
            {
                Source = "mu_brand.png", HeightRequest = 115,
                Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Fill
            });
            art.Children.Add(new Image
            {
                Source = "mu_r31_people.png", HeightRequest = 320,
                Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Fill
            });
            art.Children.Add(new Label
            {
                Text = "UN ESPACIO DE APOYO, INFORMACIÓN Y OPORTUNIDADES",
                FontAttributes = FontAttributes.Bold, FontSize = 14,
                TextColor = Deep, HorizontalTextAlignment = TextAlignment.Center
            });
            art.Children.Add(Description("Conecta, aprende, comparte y crece en comunidad.", 13));
            if (includeActions)
            {
                var row = new Grid
                {
                    Padding = new Thickness(0, 11, 0, 0), ColumnSpacing = 6,
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    }
                };
                string[] texts = { "Conecta", "Aprende", "Participa" };
                string[] icons = { "nav_messages.png", "mu_books.png", "mu_event.png" };
                for (int i = 0; i < texts.Length; i++)
                {
                    var icon = new Image { Source = icons[i], WidthRequest = 36, HeightRequest = 36, Aspect = Aspect.AspectFit };
                    var item = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.Center };
                    item.Children.Add(icon);
                    item.Children.Add(new Label
                    {
                        Text = texts[i], FontSize = 11, FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center, TextColor = Deep
                    });
                    row.Add(item, i, 0);
                }
                art.Children.Add(row);
            }
            return new Border
            {
                StrokeThickness = 0,
                Background = Gradient(Color.FromArgb("#EAC5F8"), Color.FromArgb("#FCEFFD")),
                StrokeShape = new RoundRectangle { CornerRadius = 24 },
                Padding = new Thickness(2), Content = art
            };
        }
        internal static View Footer() => PrivacyNote("Tu privacidad es importante",
            "Tu cuenta requiere revisión institucional. Usa únicamente datos ficticios durante el hackathon.");
        internal static View Responsive(ContentPage page, View left, View right, bool hideLeftOnMobile = true)
        {
            var grid = new Grid
            {
                Padding = new Thickness(16, 16, 16, 22), ColumnSpacing = 18,
                MaximumWidthRequest = 1240, HorizontalOptions = LayoutOptions.Fill,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
                RowDefinitions = { new RowDefinition(GridLength.Auto) }
            };
            grid.Add(left, 0, 0);
            grid.Add(right, 0, 0);
            bool? lastDesktop = null;
            void Reflow(double width)
            {
                bool desktop = width >= 900;
                if (lastDesktop == desktop) return;
                lastDesktop = desktop;
                left.IsVisible = desktop || !hideLeftOnMobile;
                grid.ColumnDefinitions.Clear();
                if (desktop)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(45, GridUnitType.Star)));
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(55, GridUnitType.Star)));
                    grid.Padding = new Thickness(18, 18, 18, 25);
                }
                else
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    grid.Padding = new Thickness(10, 12, 10, 16);
                }
                Grid.SetColumn(left, 0);
                Grid.SetColumn(right, desktop ? 1 : 0);
            }
            page.SizeChanged += (_, _) => Reflow(page.Width);
            Reflow(0);
            return grid;
        }
        #endregion Components
    }
}
