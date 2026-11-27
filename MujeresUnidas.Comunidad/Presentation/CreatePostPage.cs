using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;
using MujeresUnidas.Comunidad.Models.User.Subclasses;
using System.Text.Json;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Typed, moderated publication editor with bounded author image upload.</summary>
    [UsesData(typeof(global::MujeresUnidas.Comunidad.Models.Publications.Post.CreateData))]
    public sealed class CreatePostPage : ContentPage
    {
        #region Operation Data
        private readonly CommunityMember _member;
        private readonly ApiClient _api;
        private readonly bool _staff;
        private readonly bool _onlyMoment;
        private readonly Picker _category = new() {Title="Tipo de publicación",TextColor=Ui.Plum};
        private readonly Picker _subcategory = new() {Title="Tema",TextColor=Ui.Plum};
        private readonly Picker _mode = new() {Title="Modalidad",TextColor=Ui.Plum};
        private readonly VerticalStackLayout _modeBox=new(){Spacing=7};
        private readonly VerticalStackLayout _titleBox=new(){Spacing=7};
        private readonly VerticalStackLayout _summaryBox=new(){Spacing=7};
        private readonly Label _categoryHint=Ui.Text(string.Empty,12);
        private readonly Entry _title = Ui.Input("Título · máximo 120 caracteres");
        private readonly Editor _summary = new() {Placeholder="Resumen breve (8 a 260 caracteres)",MaxLength=260,HeightRequest=95,TextColor=Ui.Plum,BackgroundColor=Ui.Lavender};
        private readonly Editor _body = new() {Placeholder="Contenido del Blog (20 a 8000 caracteres)",MaxLength=8000,HeightRequest=235,TextColor=Ui.Plum,BackgroundColor=Ui.Lavender};
        private readonly Label _feedback = Ui.Text(string.Empty,12);
        private Label? _preview;
        private readonly List<FileResult> _images=new();
        private FileResult? _cover;
        private readonly Label _coverStatus=Ui.Text("Portada independiente · se muestra antes de abrir el post",12);
        private readonly Label _imageCount=Ui.Text("0 de 5 imágenes seleccionadas · PNG/JPEG hasta 2 MB",12);
        #endregion Operation Data
        #region Initialization
        public CreatePostPage(ApiClient api,bool onlyMoment=false)
        {
            _onlyMoment=onlyMoment;
            _api=api;_member = new CommunityMember(api);
            _staff=api.CurrentUser.ValueKind==JsonValueKind.Object && api.CurrentUser.TryGetProperty("communityRole",out var role) && role.GetString()=="Staff";
            Title="Crear publicación";
            _title.MaxLength=120;
            foreach(var option in (_onlyMoment?new[]{"Momento"}:_staff?new[]{"Blog","Guía","Empleo","Evento"}:new[]{"Blog"})) _category.Items.Add(option);
            _category.SelectedIndex=0;
            foreach(var mode in new[]{"Presencial","Virtual","Híbrido"})_mode.Items.Add(mode);
            _mode.SelectedIndex=0;
            _modeBox.Children.Add(Ui.Text("Modalidad del evento",12));
            _modeBox.Children.Add(Ui.Card(_mode,8));
            _category.SelectedIndexChanged+=(_,_)=>UpdateType();
            var content=new VerticalStackLayout {Padding=new Thickness(19,18,19,32),Spacing=12};
            NavigationPage.SetHasNavigationBar(this,false);
            content.Children.Add(Ui.Back(this));
            content.Children.Add(Ui.Heading(_onlyMoment?"Compartir un momento":"Crear publicación",25));
            content.Children.Add(Ui.Notice(_onlyMoment?"Comparte una reflexión desde tu perfil. Tus Momentos permanecen separados del catálogo editorial.":_staff?"Publica Blogs y prepara Guías, Empleos y Eventos para revisión institucional.":"Tus Blogs pasan por revisión para cuidar la comunidad."));
            if(!_onlyMoment){content.Children.Add(Ui.Text("Tipo de publicación",12));content.Children.Add(Ui.Card(_category,8));}
            content.Children.Add(Ui.Text("Categoría temática",12));content.Children.Add(Ui.Card(_subcategory,8));
            content.Children.Add(_modeBox);content.Children.Add(_categoryHint);
            _titleBox.Children.Add(Ui.Text("Título",12));_titleBox.Children.Add(Ui.Field(_title));
            _summaryBox.Children.Add(Ui.Text("Descripción breve",12));_summaryBox.Children.Add(Ui.Card(_summary,11));
            content.Children.Add(_titleBox);content.Children.Add(_summaryBox);
            content.Children.Add(Ui.Text(_onlyMoment?"Tu momento":"Contenido del artículo",13));
            if(!_onlyMoment)
            {
                content.Children.Add(Ui.Notice("Editor seguro: **negrita**, *cursiva*, ~~tachado~~ y [imagen1]…[imagen5]. No admite código HTML."));
                content.Children.Add(BuildRichToolbar());
            }
            content.Children.Add(Ui.Card(_body,11));
            if(!_onlyMoment)
            {
                content.Children.Add(Ui.Text("Vista previa del texto",12));
                _preview=Ui.RichParagraph(string.Empty,14);
                content.Children.Add(Ui.Card(_preview,12));
                _body.TextChanged+=(_,_)=>_preview.FormattedText=RichPostText.Parse(_body.Text??string.Empty);
                content.Children.Add(BuildEmoticonTray());
            }
            if(!_onlyMoment)content.Children.Add(Ui.Heading("Portada de la publicación",18));
            if(!_onlyMoment)content.Children.Add(_coverStatus);
            var coverButton=Ui.Secondary("Elegir portada · recomendada 1280 × 720");
            coverButton.Clicked+=SelectCover;
            if(!_onlyMoment)content.Children.Add(coverButton);
            content.Children.Add(Ui.Heading(_onlyMoment?"Foto opcional":"Galería interior · hasta 5 fotografías",18));
            content.Children.Add(_imageCount);
            var addPhoto=Ui.Secondary("Elegir imagen para el artículo");addPhoto.Clicked+=SelectImage;
            content.Children.Add(addPhoto);
            var submit=Ui.Primary(_onlyMoment?"Publicar momento":"Enviar publicación");submit.Clicked+=Submit;
            content.Children.Add(submit);content.Children.Add(_feedback);
            UpdateType();
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            root.Add(Ui.Scroll(content),0,0);
            root.Add(Ui.MainTabs(this,api,string.Empty),0,1);
            Content=root;
        }
        #endregion Initialization
        #region Task
        // Toolbar produces a deliberately limited Markdown subset; rendering is native, never HTML.
        private View BuildRichToolbar()
        {
            var row=new HorizontalStackLayout{Spacing=6};
            foreach(var (caption,left,right,hint) in new (string,string,string,string)[]
            {
                ("𝐁","**","**","texto en negrita"),
                ("𝑰","*","*","texto en cursiva"),
                ("S̶","~~","~~","texto tachado"),
                ("❝","❝ "," ❞","cita breve"),
                ("♡","","","♡"),
                ("☆","","","☆")
            })
            {
                var button=Ui.Secondary(caption);
                button.MinimumWidthRequest=42;button.HeightRequest=41;button.Padding=new Thickness(10,0);
                button.Clicked+=(_,_)=>RichPostText.Insert(_body,left,right,hint);
                row.Children.Add(button);
            }
            return new ScrollView{Orientation=ScrollOrientation.Horizontal,HorizontalScrollBarVisibility=ScrollBarVisibility.Never,Content=row};
        }

        private View BuildEmoticonTray()
        {
            var panel=new VerticalStackLayout{Spacing=8};
            panel.Children.Add(Ui.Text("Emoticonos ASCII y decoradores",12));
            var items=new[]{"(≧▽≦)","(T_T)","(^_^) ","(｡♥‿♥｡)","☆*: .｡.","♡","✿","── ✦ ──"};
            var grid=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)},RowSpacing=6,ColumnSpacing=6};
            for(var row=0;row<3;row++)grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for(int i=0;i<items.Length;i++)
            {
                var glyph=items[i];var choice=Ui.Secondary(glyph);
                choice.FontSize=11;choice.HeightRequest=37;choice.Padding=new Thickness(3,0);
                choice.Clicked+=(_,_)=>RichPostText.Insert(_body,string.Empty,string.Empty,glyph);
                grid.Add(choice,i%3,i/3);
            }
            panel.Children.Add(grid);
            return Ui.Card(panel,12);
        }

        private string SelectedType()=>_category.SelectedItem?.ToString() switch
        {"Momento"=>"Moment","Guía"=>"Guide","Empleo"=>"Job","Evento"=>"Event",_=>"Blog"};
        private int MaxBody()=>Post.BodyLimit(SelectedType());
        private void UpdateType()
        {
            string type=SelectedType();
            _subcategory.Items.Clear();
            foreach(var item in Post.Subcategories(type))_subcategory.Items.Add(item);
            _subcategory.SelectedIndex=0;
            _modeBox.IsVisible=type=="Event";
            _titleBox.IsVisible=type!="Moment";_summaryBox.IsVisible=type!="Moment";
            _imageCount.Text=type=="Moment"?"Una fotografía opcional · PNG/JPEG hasta 2 MB":
                $"{_images.Count} de 5 imágenes seleccionadas · PNG/JPEG hasta 2 MB";
            _body.MaxLength=MaxBody();
            _body.Placeholder=type=="Moment"?"¿Qué te gustaría compartir hoy? (8 a 600 caracteres)":
                $"Desarrolla tu {PostDetailPage.CategoryName(type).ToLowerInvariant()} (20 a {MaxBody()} caracteres)";
            _categoryHint.Text=type switch
            {
                "Moment"=>"Una reflexión o acontecimiento breve. Máximo una fotografía. Los momentos de miembros pasan a revisión.",
                "Event"=>"La modalidad y categoría son independientes. Gestión revisa los eventos.",
                "Guide"=>"Organiza instrucciones claras, recursos y recomendaciones. Requiere revisión institucional.",
                "Job"=>"Describe requisitos, ubicación y datos de contacto sin solicitar información sensible en público.",
                _=>"Comparte experiencias con respeto. Los Blogs de miembros pasan a revisión de Staff."
            };
        }
        private async void SelectCover(object? sender,EventArgs e)
        {
            try
            {
                var picked=await FilePicker.Default.PickAsync(new PickOptions
                {PickerTitle="Portada horizontal JPEG/PNG",FileTypes=FilePickerFileType.Images});
                if(picked is null)return;
                await using var stream=await picked.OpenReadAsync();
                if(stream.Length>2_097_152){_coverStatus.Text="La portada debe pesar máximo 2 MiB.";return;}
                _cover=picked;_coverStatus.Text="Portada seleccionada: "+picked.FileName;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {_coverStatus.Text="No se pudo leer la portada.";}
        }
        private async void SelectImage(object? sender,EventArgs e)
        {
            if(_images.Count>=(SelectedType()=="Moment"?1:5))
            {_feedback.Text=SelectedType()=="Moment"?"Un Momento admite una imagen.":"Máximo cinco imágenes.";return;}
            try
            {
                var picked=await FilePicker.Default.PickAsync(new PickOptions
                {PickerTitle="Selecciona una imagen PNG o JPEG",FileTypes=FilePickerFileType.Images});
                if(picked is null)return;
                await using var stream=await picked.OpenReadAsync();
                if(stream.Length>2_097_152){_feedback.Text="Cada imagen debe pesar menos de 2 MB.";return;}
                _images.Add(picked);
                _imageCount.Text=$"{_images.Count} de {(SelectedType()=="Moment"?1:5)} imágenes seleccionadas · última: {picked.FileName}";
                // R4.3 inline marker; plain text is never interpreted as HTML.
                if(SelectedType()=="Moment")_feedback.Text="Imagen añadida al Momento.";
                else
                {
                    var marker=$"[imagen{_images.Count}]";
                    _body.Text=(_body.Text??string.Empty).TrimEnd()+Environment.NewLine+marker+Environment.NewLine;
                    _feedback.Text="Marcador "+marker+" añadido. Puedes moverlo en el artículo.";
                }
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {_feedback.Text="No se pudo leer esa imagen.";}
        }
        private async void Submit(object? sender,EventArgs args)
        {
            if(sender is not Button button) return;
            string body=(_body.Text??string.Empty).Trim();
            bool isMoment=SelectedType()=="Moment";
            string title=isMoment?"Momento · "+DateTime.Now.ToString("dd/MM/yyyy"):(_title.Text??string.Empty).Trim();
            string summary=isMoment?body[..Math.Min(body.Length,180)] : (_summary.Text??string.Empty).Trim();
            if(title.Length is < 4 or > 120 || summary.Length is < 8 or > 260 ||
                body.Length<(isMoment?8:20) || body.Length>MaxBody())
            { _feedback.Text=isMoment?"El momento requiere de 8 a 600 caracteres.":
                    $"Revisa: título 4–120, resumen 8–260 y contenido 20–{MaxBody()}.";return; }
            string category=SelectedType();
            button.IsEnabled=false;
            try
            {
                var result=await _member.CreatePostAsync(new Post.CreateData
                { Category=category,Subcategory=_subcategory.SelectedItem?.ToString()??string.Empty,
                    Mode=category=="Event"?_mode.SelectedItem?.ToString():null,
                    Title=title,Summary=summary,Body=body });
                _feedback.Text=result.Message;
                if(result.Success)
                {
                    int uploaded=0;
                    if(result.Id.HasValue)
                    {
                        var media=new Post(_api);
                        if(_cover is not null)
                        {
                            var coverResult=await media.UploadCoverAsync(result.Id.Value,_cover);
                            if(!coverResult.Success)_feedback.Text="Publicación creada, pero no se guardó la portada: "+coverResult.Error;
                        }
                        foreach(var image in _images)
                        {
                            var saved=await media.UploadImageAsync(result.Id.Value,image);
                            if(saved.Success)uploaded++;
                            else _feedback.Text=$"Publicación creada, pero una imagen falló: {saved.Error}";
                        }
                    }
                    await DisplayAlertAsync("Publicación registrada",
                        result.Message+($" Imágenes guardadas: {uploaded}/{_images.Count}."),"Entendido");
                    await Navigation.PopAsync();
                }
            }
            finally {button.IsEnabled=true;}
        }
        #endregion Task
    }
}
