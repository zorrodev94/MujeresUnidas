using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Community Staff-only moderation queue; Server independently enforces the permission.</summary>
    public sealed class ModerationPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly Post _posts;
        private readonly VerticalStackLayout _list = new() {Spacing=12};
        private readonly Label _feedback = Ui.Text(string.Empty,12);
        #endregion Operation Data
        #region Initialization
        public ModerationPage(ApiClient api)
        {
            _api=api;
            _posts=new Post(api);
            Title="Moderación";
            var content=new VerticalStackLayout {Padding=new Thickness(17,20,17,30),Spacing=13};
            content.Children.Add(Ui.Heading("Moderación de publicaciones",24));
            content.Children.Add(Ui.Notice("Solo cuentas Staff aprobadas pueden revisar publicaciones. No es un canal de atención institucional."));
            content.Children.Add(_feedback);
            content.Children.Add(_list);
            Content=Ui.Scroll(content);
        }
        #endregion Initialization
        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            var result=await _posts.PendingAsync();
            _list.Children.Clear();
            _feedback.Text=result.Success ? $"Pendientes por revisar: {result.Items.Count}" : result.Error;
            if(!result.Success)return;
            foreach(var item in result.Items)
            {
                var stack=new VerticalStackLayout {Spacing=8};
                stack.Children.Add(Ui.Heading(item.Title,17));
                stack.Children.Add(Ui.Text(PostDetailPage.CategoryName(item.Category)+" · "+item.Author,12));
                stack.Children.Add(Ui.Text(item.Summary,13));
                var open=Ui.Secondary("Leer contenido completo");
                open.Clicked+=async (_,_)=>await Navigation.PushAsync(new PostDetailPage(item,_api));
                stack.Children.Add(open);
                var approve=Ui.Primary("Aprobar");
                var feature=Ui.Secondary("Aprobar y destacar");
                var reject=Ui.Secondary("Rechazar");
                approve.Clicked+=async (_,_)=>await Decide(item.Id,true);
                feature.Clicked+=async (_,_)=>await Decide(item.Id,true,true);
                reject.Clicked+=async (_,_)=>await Decide(item.Id,false);
                stack.Children.Add(approve);stack.Children.Add(feature);stack.Children.Add(reject);
                _list.Children.Add(Ui.Card(stack,14));
            }
        }

        private async Task Decide(Guid id,bool approved,bool featured=false)
        {
            bool confirm=await DisplayAlertAsync("Confirmar moderación",
                approved?"¿Publicar esta entrada en Comunidad?":"¿Rechazar esta entrada?", "Sí", "Cancelar");
            if(!confirm)return;
            var result=await _posts.ReviewPostAsync(id,approved,featured);
            _feedback.Text=result.Message;
            if(result.Success)await RefreshAsync();
        }
        #endregion Task
    }
}
