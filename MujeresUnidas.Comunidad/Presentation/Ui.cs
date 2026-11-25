using Microsoft.Maui.Controls.Shapes;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Real MAUI controls and reusable visual tokens; mockup screenshots are not used as UI backgrounds.</summary>
    internal static class Ui
    {
        #region Theme
        internal static readonly Color Background = Color.FromArgb("#FCF5FF");
        internal static readonly Color Plum = Color.FromArgb("#59206F");
        internal static readonly Color Violet = Color.FromArgb("#8F35A9");
        internal static readonly Color Muted = Color.FromArgb("#74587F");
        internal static readonly Color Lavender = Color.FromArgb("#F0D9FA");
        internal static readonly Color Pink = Color.FromArgb("#FCE6F8");
        internal static readonly Color BorderColor = Color.FromArgb("#E7C5F0");
        #endregion Theme

        #region Components
        internal static Label Heading(string text, double size = 26) => new()
        {
            Text = text, FontSize = size, FontAttributes = FontAttributes.Bold,
            TextColor = Plum, LineBreakMode = LineBreakMode.WordWrap
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
            CornerRadius = 17, HeightRequest = 56
        };

        internal static Button Secondary(string label) => new()
        {
            Text = label, TextColor = Plum, BackgroundColor = Pink,
            FontAttributes = FontAttributes.Bold, FontSize = 14,
            CornerRadius = 17, HeightRequest = 51
        };

        internal static Border Card(View contents, int padding = 20) => new()
        {
            BackgroundColor = Colors.White, Stroke = BorderColor,
            StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 26 },
            Padding = new Thickness(padding), Content = contents,
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#270F0733")),
                Offset = new Point(0, 6), Radius = 18, Opacity = 0.3f
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
            Source = "mu_women.png", HeightRequest = height, Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill
        };

        internal static ScrollView Scroll(View content) => new()
        {
            Content = content, BackgroundColor = Background,
            VerticalScrollBarVisibility = ScrollBarVisibility.Default
        };

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
