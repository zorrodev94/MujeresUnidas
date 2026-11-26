using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Edits an existing publication; member changes re-enter moderation.</summary>
    public sealed class EditPostPage : ContentPage
    {
        #region Operation Data
        private readonly Post _posts;
        private readonly Post.ViewData _item;
        private readonly Entry _title=Ui.Input("Título");
        private readonly Editor _summary=new() {HeightRequest=98,MaxLength=260,TextColor=Ui.Plum,BackgroundColor=Ui.Lavender};
        private readonly Editor _body=new() {HeightRequest=220,MaxLength=2500,TextColor=Ui.Plum,BackgroundColor=Ui.Lavender};
        private readonly Label _feedback=Ui.Text(string.Empty,12);
        #endregion Operation Data

        #region Initialization
        public EditPostPage(ApiClient api,Post.ViewData item)
        {
            _posts=new Post(api);_item=item;Title="Editar publicación";
            _title.Text=item.Title;_title.MaxLength=120;
            _summary.Text=item.Summary;_body.Text=item.Body;
            var content=new VerticalStackLayout {Padding=new Thickness(18,16,18,30),Spacing=12};
            content.Children.Add(Ui.Heading("Editar "+PostDetailPage.CategoryName(item.Category),23));
            content.Children.Add(Ui.Notice("Los cambios realizados por miembros requieren una nueva revisión."));
            content.Children.Add(Ui.Text("Título",12));content.Children.Add(Ui.Field(_title));
            content.Children.Add(Ui.Text("Resumen",12));content.Children.Add(Ui.Card(_summary,11));
            content.Children.Add(Ui.Text("Contenido",12));content.Children.Add(Ui.Card(_body,11));
            var save=Ui.Primary("Guardar cambios");save.Clicked+=Save;
            content.Children.Add(save);content.Children.Add(_feedback);
            Content=Ui.Scroll(content);
        }
        #endregion Initialization

        #region Task
        private async void Save(object? sender,EventArgs args)
        {
            if(sender is not Button button)return;
            string title=(_title.Text??string.Empty).Trim();
            string summary=(_summary.Text??string.Empty).Trim();
            string body=(_body.Text??string.Empty).Trim();
            if(title.Length is < 4 or > 120 || summary.Length is < 8 or > 260 || body.Length is < 20 or > 2500)
            { _feedback.Text="Verifica: título 4–120, resumen 8–260, texto 20–2500."; return; }
            button.IsEnabled=false;
            try
            {
                var result=await _posts.EditPostAsync(_item.Id,new Post.EditData
                {Title=title,Summary=summary,Body=body});
                _feedback.Text=result.Message;
                if(result.Success)await Navigation.PopAsync();
            }
            finally {button.IsEnabled=true;}
        }
        #endregion Task
    }
}
