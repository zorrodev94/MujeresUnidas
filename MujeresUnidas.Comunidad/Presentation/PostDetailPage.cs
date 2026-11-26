using MujeresUnidas.Comunidad.Models.Publications;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Displays one post's actual title, summary and body text from Server.</summary>
    public sealed class PostDetailPage : ContentPage
    {
        #region Initialization
        public PostDetailPage(Post.ViewData item)
        {
            Title = item.Title;
            BackgroundColor = Ui.Background;
            var content = new VerticalStackLayout { Padding=new Thickness(19,23,19,32),Spacing=14 };
            content.Children.Add(Ui.Heading(item.Title,26));
            content.Children.Add(Ui.Text(CategoryName(item.Category) + " · " + item.Author,12));
            if (item.Example) content.Children.Add(Ui.Notice("Contenido ilustrativo del hackathon; no es una convocatoria oficial."));
            content.Children.Add(Ui.Card(new VerticalStackLayout
            {
                Spacing=12,
                Children={Ui.Text(item.Summary,16),Ui.Text(item.Body,15)}
            }));
            content.Children.Add(Ui.Text("Publicado: " + item.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy"),11));
            Content = Ui.Scroll(content);
        }
        #endregion Initialization

        #region Task
        internal static string CategoryName(string category) => category switch
        {
            "Blog"=>"Blog", "Guide"=>"Guía", "Job"=>"Empleo", "Event"=>"Evento", _=>category
        };
        #endregion Task
    }
}
