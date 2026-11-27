using Microsoft.Maui.Controls.Shapes;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Real MAUI controls and reusable visual tokens; mockup screenshots are not used as UI backgrounds.</summary>
    internal static class Ui
    {
        #region Theme
        internal static readonly Color Background = Color.FromArgb("#F8F3FE");
        internal static readonly Color Plum = Color.FromArgb("#521A70");
        internal static readonly Color Violet = Color.FromArgb("#842BA2");
        internal static readonly Color Coral = Color.FromArgb("#CF365A");
        internal static readonly Color Muted = Color.FromArgb("#74587F");
        internal static readonly Color Lavender = Color.FromArgb("#EBD8F8");
        internal static readonly Color Pink = Color.FromArgb("#F9E8FA");
        internal static readonly Color BorderColor = Color.FromArgb("#E9D5F3");
        internal static readonly Color Frost = Color.FromArgb("#F1E6FC");
        #endregion Theme
        #region Components
        internal static Label Heading(string text, double size = 26) => new()
        {
            Text = text, FontSize = size, FontAttributes = FontAttributes.Bold,
            TextColor = Plum, LineBreakMode = LineBreakMode.WordWrap
        };

        /// <summary>Native text formatting for posts; no HTML/WebView injection.</summary>
        internal static Label RichParagraph(string text,double size=15) => new()
        {
            FormattedText=RichPostText.Parse(text),FontSize=size,TextColor=Plum,
            LineBreakMode=LineBreakMode.WordWrap
        };

        internal static Label Text(string text, double size = 14) => new()
        {
            Text = text, FontSize = size, TextColor = Muted,
            LineBreakMode = LineBreakMode.WordWrap
        };

        internal static Entry Input(string placeholder, bool password = false) => new()
        {
            Placeholder = placeholder, IsPassword = password, HeightRequest = 51,
            TextColor = Plum, PlaceholderColor = Muted, FontSize = 15,
            BackgroundColor = Colors.Transparent,
            ClearButtonVisibility = password ? ClearButtonVisibility.Never : ClearButtonVisibility.WhileEditing
        };

        internal static Border Field(Entry control) => new()
        {
            Stroke = BorderColor, StrokeThickness = 1.5, BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 17 },
            Padding = new Thickness(13, 1), Content = control
        };

        internal static Button Primary(string label) => new()
        {
            Text = label, TextColor = Colors.White, BackgroundColor = Violet,
            FontAttributes = FontAttributes.Bold, FontSize = 15,
            CornerRadius = 23, HeightRequest = 54
        };

        internal static Button Secondary(string label) => new()
        {
            Text = label, TextColor = Plum, BackgroundColor = Pink,
            FontAttributes = FontAttributes.Bold, FontSize = 14,
            CornerRadius = 22, HeightRequest = 48
        };

        internal static Border Card(View contents, int padding = 20) => new()
        {
            BackgroundColor = Colors.White, Stroke = BorderColor,
            StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 25 },
            Padding = new Thickness(padding), Content = contents,
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#270F0733")),
                Offset = new Point(0, 4), Radius = 14, Opacity = 0.22f
            }
        };

        internal static Border Notice(string message) => new()
        {
            BackgroundColor = Pink, Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(15, 13), Content = Text(message, 12)
        };

        internal static Image Logo(double height = 130) => new()
        {
            Source = "mu_brand.png", HeightRequest = height, Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill
        };

        internal static Image Illustration(double height = 280) => new()
        {
            Source = "mu_r31_people.png", HeightRequest = height, Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill
        };

        internal static ScrollView Scroll(View content) => new()
        {
            Content = content, BackgroundColor = Background,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };

        /// <summary>Creates a circular photo with a branded fallback, without revealing private account fields.</summary>
        internal static Image Avatar(double size = 62) => new()
        {
            Source = "mu_avatar_default.png", WidthRequest = size, HeightRequest = size,
            Aspect = Aspect.AspectFill, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Clip = new EllipseGeometry
            {
                Center = new Point(size / 2, size / 2), RadiusX = size / 2, RadiusY = size / 2
            }
        };

        /// <summary>Native rounded cover container, not an image of a user interface.</summary>
        internal static Border Cover(Image image, double height = 170) => new()
        {
            HeightRequest = height, Stroke = BorderColor, StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            BackgroundColor = Lavender, Padding = 0, Content = image
        };

        /// <summary>Compact numeric metric used consistently for social profiles.</summary>
        internal static VerticalStackLayout ProfileMetric(string number, string label) => new()
        {
            Spacing = 2, HorizontalOptions = LayoutOptions.Fill,
            Children = { new Label { Text = number, TextColor = Plum, FontSize = 21,
                    FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = label, TextColor = Muted, FontSize = 11,
                    HorizontalTextAlignment = TextAlignment.Center } }
        };

        /// <summary>Noninteractive feature flag for workflows not shipped in the Hackathon build.</summary>
        internal static Border FutureBadge(string feature) => Tag(feature + " · Próximamente");

        /// <summary>Reusable brand header with one-tap Search and notifications from every main destination.</summary>
        internal static Grid Header(ContentPage page, ApiClient api, string title, bool search = true)
        {
            var bar = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(new GridLength(43)),
                    new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(39)),
                    new ColumnDefinition(new GridLength(39)) },
                HeightRequest = 62, ColumnSpacing = 4
            };
            bar.Add(new Image { Source = "mu_mark.png", WidthRequest = 42, HeightRequest = 42,
                Aspect = Aspect.AspectFit }, 0, 0);
            var words = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
            words.Children.Add(Heading("Mujeres Unidas", 16));
            words.Children.Add(Text(title, 11));
            bar.Add(words, 1, 0);
            var searchIcon = Icon("nav_search.png", 27);
            searchIcon.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await page.Navigation.PushAsync(new PeopleSearchPage(api)))
            });
            searchIcon.IsVisible = search;
            bar.ColumnDefinitions[2].Width = search ? new GridLength(39) : new GridLength(0);
            if (search) bar.Add(searchIcon, 2, 0);
            var bell = Icon("nav_bell.png", 28);
            bell.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await page.Navigation.PushAsync(new NotificationsPage(api)))
            });
            bar.Add(bell, 3, 0);
            return bar;
        }

        /// <summary>Role label is visual metadata, never a grant of professional or staff privileges.</summary>
        internal static Border Tag(string label,bool prominent=false)
        {
            return new Border
            {
                BackgroundColor=prominent?Color.FromArgb("#E8CBF6"):Pink,
                Stroke=prominent?Violet:BorderColor,
                StrokeThickness=prominent?1:0,
                StrokeShape=new RoundRectangle{CornerRadius=14},
                Padding=new Thickness(12,6),
                Content=new Label{Text=label,FontSize=12,FontAttributes=FontAttributes.Bold,
                    TextColor=Plum,HorizontalOptions=LayoutOptions.Center}
            };
        }
        internal static Border Section(string title,View body)
        {
            var column=new VerticalStackLayout{Spacing=12};
            column.Children.Add(Heading(title,20));column.Children.Add(body);
            return Card(column,17);
        }
        internal static Button Back(ContentPage page)
        {
            var button=Secondary("‹ Volver");
            button.HeightRequest=42;button.FontSize=13;
            button.Clicked+=async (_,_)=>await page.Navigation.PopAsync();
            return button;
        }
        internal static Image Icon(string source,double size=28)=>new()
        {Source=source,WidthRequest=size,HeightRequest=size,Aspect=Aspect.AspectFit,
            HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center};

        /// <summary>Five mobile destinations; one person-search icon belongs in the header, not a duplicate tab.</summary>
        internal static Grid MainTabs(ContentPage page, ApiClient api, string selected)
        {
            var footer = new Grid
            {
                BackgroundColor = Colors.White, HeightRequest = 74, ColumnSpacing = 0,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star) }
            };
            (string Name, string IconFile)[] items =
            [
                ("Inicio", "nav_home.png"),
                ("Mensajes", "nav_messages.png"),
                ("Ayuda", "nav_safety.png"),
                ("Atención", "nav_heart.png"),
                ("Perfil", "nav_profile.png")
            ];
            for (var index = 0; index < items.Length; index++)
            {
                var (name, file) = items[index];
                bool urgent = name == "Ayuda";
                var group = new VerticalStackLayout
                {
                    Spacing = 3, Padding = new Thickness(0, urgent ? 3 : 8, 0, 0),
                    HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center
                };
                if (urgent)
                {
                    group.Children.Add(new Border
                    {
                        StrokeThickness = 0, BackgroundColor = Color.FromArgb("#C83754"),
                        StrokeShape = new RoundRectangle { CornerRadius = 28 },
                        WidthRequest = 43, HeightRequest = 43,
                        HorizontalOptions = LayoutOptions.Center, Content = Icon(file, 27)
                    });
                }
                else group.Children.Add(Icon(file, 22));
                group.Children.Add(new Label
                {
                    Text = name, FontSize = 9, FontAttributes = FontAttributes.Bold,
                    TextColor = urgent ? Color.FromArgb("#9E2342") : name == selected ? Plum : Muted,
                    HorizontalTextAlignment = TextAlignment.Center
                });
                var holder = new Border
                {
                    BackgroundColor = urgent ? Colors.White : name == selected ? Lavender : Colors.White,
                    StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 17 },
                    Margin = new Thickness(1, 3), Content = group
                };
                var destination = name;
                holder.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        if (destination == selected && page.Navigation.NavigationStack.Count <= 2) return;
                        await page.Navigation.PopToRootAsync();
                        switch (destination)
                        {
                            case "Mensajes": await page.Navigation.PushAsync(new MessagesPage(api)); break;
                            case "Ayuda": await page.Navigation.PushAsync(new HelpPage(api)); break;
                            case "Atención": await page.Navigation.PushAsync(new CarePage(api)); break;
                            case "Perfil": await page.Navigation.PushAsync(new ProfilePage(api)); break;
                        }
                    })
                });
                footer.Add(holder, index, 0);
            }
            return footer;
        }

        internal static ImageSource BytesSource(byte[] data)=>ImageSource.FromStream(()=>new MemoryStream(data));

        /// <summary>Uses a compact form on a phone and a two-column layout on large displays.</summary>
        internal static Grid ResponsiveAuth(View artwork, View form, VisualElement page,
            View? compactBrand = null, Action<bool>? onModeChanged = null)
        {
            var grid = new Grid
            {
                ColumnSpacing = 24, RowSpacing = 0, Padding = new Thickness(12, 10, 12, 16),
                MaximumWidthRequest = 1170, HorizontalOptions = LayoutOptions.Fill
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Add(artwork, 0, 0);
            grid.Add(form, 0, 0);
            bool? previousDesktop = null;
            void Relayout(double width)
            {
                bool desktop = width >= 850;
                if (previousDesktop == desktop) return;
                previousDesktop = desktop;
                artwork.IsVisible = desktop;
                if(compactBrand is not null) compactBrand.IsVisible = !desktop;
                grid.ColumnDefinitions.Clear();
                if(desktop)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(46,GridUnitType.Star)));
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(54,GridUnitType.Star)));
                    grid.Padding = new Thickness(18,16,18,25);
                }
                else
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    grid.Padding = new Thickness(7,8,7,12);
                }
                Grid.SetColumn(artwork,0);
                Grid.SetColumn(form,desktop?1:0);
                onModeChanged?.Invoke(desktop);
            }
            page.SizeChanged += (_, _) => Relayout(page.Width);
            Relayout(0);
            return grid;
        }
        #endregion Components
    }
}
