using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Edits an existing publication; member changes re-enter moderation.</summary>
    [UsesData(typeof(global::MujeresUnidas.Comunidad.Models.Publications.Post.EditData))]
    public sealed class EditPostPage : ContentPage
    {
        #region Operation Data
        private readonly Post _posts;
        private readonly Post.ViewData _item;
        private readonly Entry _title=Ui.Input("Título");
        private readonly Picker _subcategory=new(){Title="Categoría temática",TextColor=Ui.Plum};
        private readonly Picker _mode=new(){Title="Modalidad",TextColor=Ui.Plum};
        private readonly Editor _summary=new() {HeightRequest=98,MaxLength=260,TextColor=Ui.Plum,BackgroundColor=Ui.Lavender};
        private readonly Editor _body=new() {HeightRequest=220,MaxLength=12000,TextColor=Ui.Plum,BackgroundColor=Ui.Lavender};
        private readonly Label _feedback=Ui.Text(string.Empty,12);
        private readonly ApiClient _api;
        #endregion Operation Data
        #region Initialization
        public EditPostPage(ApiClient api,Post.ViewData item)
        {
            _api=api;_posts=new Post(api);_item=item;Title="Editar publicación";
            NavigationPage.SetHasNavigationBar(this,false);
            _title.Text=item.Title;_title.MaxLength=120;
            _summary.Text=item.Summary;_body.Text=item.Body;
            foreach(var option in Post.Subcategories(item.Category))_subcategory.Items.Add(option);
            _subcategory.SelectedIndex=Math.Max(0,_subcategory.Items.IndexOf(item.Subcategory??string.Empty));
            if(item.Category=="Event")
            {
                foreach(var option in new[]{"Presencial","Virtual","Híbrido"})_mode.Items.Add(option);
                _mode.SelectedIndex=Math.Max(0,_mode.Items.IndexOf(item.Mode??string.Empty));
            }
            _body.MaxLength=MaxBody(item.Category);
            var content=new VerticalStackLayout {Padding=new Thickness(18,16,18,30),Spacing=12};
            content.Children.Add(Ui.Back(this));
            content.Children.Add(Ui.Heading("Editar "+PostDetailPage.CategoryName(item.Category),23));
            content.Children.Add(Ui.Notice("Los cambios realizados por miembros requieren una nueva revisión."));
            content.Children.Add(Ui.Text("Categoría temática",12));content.Children.Add(Ui.Card(_subcategory,8));
            if(item.Category=="Event")
            {content.Children.Add(Ui.Text("Modalidad",12));content.Children.Add(Ui.Card(_mode,8));}
            if(item.Category!="Moment")
            {
                content.Children.Add(Ui.Text("Título",12));content.Children.Add(Ui.Field(_title));
                content.Children.Add(Ui.Text("Resumen",12));content.Children.Add(Ui.Card(_summary,11));
            }
            content.Children.Add(Ui.Text("Contenido",12));
            if(item.Category!="Moment")
            {
                content.Children.Add(Ui.Notice("Formato: **negrita**, *cursiva*, ~~tachado~~ y emoticonos ASCII. Las imágenes usan [imagen1]…[imagen5]."));
                var formatting=new HorizontalStackLayout{Spacing=6};
                foreach(var option in new (string Caption,string Prefix,string Suffix,string Sample)[]
                {
                    ("𝐁","**","**","negrita"),("𝑰","*","*","cursiva"),
                    ("S̶","~~","~~","texto"),("♡","","","♡"),
                    ("(≧▽≦)","","","(≧▽≦)")
                })
                {
                    var action=Ui.Secondary(option.Caption);
                    action.FontSize=12;action.HeightRequest=41;
                    action.Clicked+=(_,_)=>RichPostText.Insert(_body,option.Prefix,option.Suffix,option.Sample);
                    formatting.Children.Add(action);
                }
                content.Children.Add(new ScrollView{Orientation=ScrollOrientation.Horizontal,Content=formatting,HorizontalScrollBarVisibility=ScrollBarVisibility.Never});
            }
            content.Children.Add(Ui.Card(_body,11));
            var preview=Ui.RichParagraph(_body.Text??string.Empty,14);
            _body.TextChanged+=(_,_)=>preview.FormattedText=RichPostText.Parse(_body.Text??string.Empty);
            content.Children.Add(Ui.Text("Vista previa",12));content.Children.Add(Ui.Card(preview,12));
            var save=Ui.Primary("Guardar cambios");save.Clicked+=Save;
            content.Children.Add(save);content.Children.Add(_feedback);
            var add=Ui.Secondary("＋ Añadir imagen a la publicación");
            add.Clicked+=UploadImage;
            content.Children.Add(add);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            root.Add(Ui.Scroll(content),0,0);
            root.Add(Ui.MainTabs(this,api,string.Empty),0,1);
            Content=root;
        }
        #endregion Initialization
        #region Task
        private async void UploadImage(object? sender,EventArgs args)
        {
            try
            {
                var file=await FilePicker.Default.PickAsync(new PickOptions
                {PickerTitle="Foto de la publicación",FileTypes=FilePickerFileType.Images});
                if(file is null)return;
                var response=await _posts.UploadImageAsync(_item.Id,file);
                _feedback.Text=response.Success?"Imagen subida. Puede requerir nueva revisión.":response.Error;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {_feedback.Text="No se pudo seleccionar esa foto.";}
        }
        private static int MaxBody(string category)=>category switch
        {"Moment"=>600,"Guide"=>12000,"Job"=>5000,"Event"=>4000,_=>8000};
        private async void Save(object? sender,EventArgs args)
        {
            if(sender is not Button button)return;
            string title=(_title.Text??string.Empty).Trim();
            string summary=(_summary.Text??string.Empty).Trim();
            string body=(_body.Text??string.Empty).Trim();
            if(title.Length is < 4 or > 120 || summary.Length is < 8 or > 260 || body.Length<(_item.Category=="Moment"?8:20) || body.Length>MaxBody(_item.Category))
            { _feedback.Text=$"Verifica: título 4–120, resumen 8–260, texto 20–{MaxBody(_item.Category)}."; return; }
            button.IsEnabled=false;
            try
            {
                var result=await _posts.EditPostAsync(_item.Id,new Post.EditData
                {Title=title,Summary=summary,Body=body,
                    Subcategory=_subcategory.SelectedItem?.ToString()??string.Empty,
                    Mode=_item.Category=="Event"?_mode.SelectedItem?.ToString():null});
                _feedback.Text=result.Message;
                if(result.Success)await Navigation.PopAsync();
            }
            finally {button.IsEnabled=true;}
        }
        #endregion Task
    }
}
