using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Real Community publication editor with bounded text; media uploads are not yet enabled.</summary>
    public sealed class CreatePostPage : ContentPage
    {
        #region Operation Data
        private readonly Post _posts;
        private readonly Picker _category = new() {Title="Tipo de publicación",TextColor=Ui.Plum};
        private readonly Entry _title = Ui.Input("Título · máximo 120 caracteres");
        private readonly Editor _summary = new() {Placeholder="Resumen breve (8 a 260 caracteres)",MaxLength=260,HeightRequest=95,TextColor=Ui.Plum,BackgroundColor=Ui.Lavender};
        private readonly Editor _body = new() {Placeholder="Contenido (20 a 2500 caracteres)",MaxLength=2500,HeightRequest=235,TextColor=Ui.Plum,BackgroundColor=Ui.Lavender};
        private readonly Label _feedback = Ui.Text(string.Empty,12);
        #endregion Operation Data

        #region Initialization
        public CreatePostPage(ApiClient api)
        {
            _posts = new Post(api);
            Title="Crear publicación";
            _title.MaxLength=120;
            foreach(var option in new[] {"Blog","Guía","Empleo","Evento"}) _category.Items.Add(option);
            _category.SelectedIndex=0;
            var content=new VerticalStackLayout {Padding=new Thickness(19,18,19,32),Spacing=12};
            content.Children.Add(Ui.Heading("Nueva publicación",25));
            content.Children.Add(Ui.Notice("Las publicaciones de miembros requieren revisión. Staff puede publicar directamente."));
            content.Children.Add(Ui.Text("Categoría",12));content.Children.Add(_category);
            content.Children.Add(Ui.Text("Título",12));content.Children.Add(Ui.Field(_title));
            content.Children.Add(Ui.Text("Descripción breve",12));content.Children.Add(Ui.Card(_summary,11));
            content.Children.Add(Ui.Text("Contenido",12));content.Children.Add(Ui.Card(_body,11));
            var submit=Ui.Primary("Enviar publicación");submit.Clicked+=Submit;
            content.Children.Add(submit);content.Children.Add(_feedback);
            Content=Ui.Scroll(content);
        }
        #endregion Initialization

        #region Task
        private async void Submit(object? sender,EventArgs args)
        {
            if(sender is not Button button) return;
            string title=(_title.Text??string.Empty).Trim();
            string summary=(_summary.Text??string.Empty).Trim();
            string body=(_body.Text??string.Empty).Trim();
            if(title.Length is < 4 or > 120 || summary.Length is < 8 or > 260 || body.Length is < 20 or > 2500)
            { _feedback.Text="Revisa las longitudes: título 4–120, resumen 8–260, contenido 20–2500.";return; }
            string category=_category.SelectedIndex switch {0=>"Blog",1=>"Guide",2=>"Job",3=>"Event",_=>"Blog"};
            button.IsEnabled=false;
            try
            {
                var result=await _posts.CreatePostAsync(new Post.CreateData
                { Category=category,Title=title,Summary=summary,Body=body });
                _feedback.Text=result.Message;
                if(result.Success)
                {
                    await DisplayAlertAsync("Registro recibido",result.Message,"Entendido");
                    await Navigation.PopAsync();
                }
            }
            finally {button.IsEnabled=true;}
        }
        #endregion Task
    }
}
