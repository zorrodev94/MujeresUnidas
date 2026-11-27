using MujeresUnidas.Comunidad.Models.Publications;
using MujeresUnidas.Comunidad.Connection;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Approved community post with real cover, engagement and author-safe comments.</summary>
    [UsesData(typeof(Post.ViewData))]
    [UsesData(typeof(PostInteraction.SummaryData))]
    public sealed class PostDetailPage : ContentPage
    {
        #region Operation Data
        private readonly Post.ViewData _item;
        private readonly PostInteraction? _interactions;
        private readonly Label _notice=Ui.Text(string.Empty,12);
        private readonly Label _count=Ui.Text(string.Empty,12);
        private readonly VerticalStackLayout _comments=new(){Spacing=10};
        private readonly VerticalStackLayout _article=new(){Spacing=12};
        private readonly Editor _commentDraft=new(){Placeholder="Escribe un comentario respetuoso...",
            TextColor=Ui.Plum,BackgroundColor=Ui.Lavender,MaxLength=600,HeightRequest=88};
        private Button? _likeButton;
        private Button? _saveButton;
        private bool _liked;
        private bool _saved;
        #endregion Operation Data
        #region Initialization
        public PostDetailPage(Post.ViewData item,ApiClient? api=null)
        {
            _item=item;
            _interactions=api is null?null:new PostInteraction(api);
            Title=item.Title;BackgroundColor=Ui.Background;
            NavigationPage.SetHasNavigationBar(this,false);
            var content=new VerticalStackLayout{Padding=new Thickness(17,10,17,28),Spacing=13,
                MaximumWidthRequest=940,HorizontalOptions=LayoutOptions.Fill};
            if(api is not null)
            {
                content.Children.Add(Ui.Header(this,api,"Publicación de la comunidad"));
                content.Children.Add(Ui.Back(this));
            }
            var hero=new Image{Source=FallbackCover(item.Category),HeightRequest=220,
                Aspect=Aspect.AspectFill,HorizontalOptions=LayoutOptions.Fill};
            if(item.Category!="Moment")
            {
                content.Children.Add(Ui.Card(hero,0));
                if(api is not null)_=LoadCoverAsync(new Post(api),item.Id,hero);
            }
            var tags=new HorizontalStackLayout{Spacing=7};
            tags.Children.Add(Ui.Tag(CategoryName(item.Category),true));
            if(!string.IsNullOrWhiteSpace(item.Subcategory))tags.Children.Add(Ui.Tag(item.Subcategory));
            if(item.Featured)tags.Children.Add(Ui.Tag("★ Destacada"));
            content.Children.Add(new ScrollView{Orientation=ScrollOrientation.Horizontal,Content=tags,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Never});
            content.Children.Add(Ui.Heading(item.Title,25));
            content.Children.Add(Ui.Text("Por "+item.Author+"  ·  "+item.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy"),12));
            if(item.Example)content.Children.Add(Ui.Notice("Publicación ilustrativa del hackathon; no es una convocatoria ni atención institucional real."));
            _article.Children.Add(Ui.RichParagraph(item.Body,15));
            content.Children.Add(Ui.Card(new VerticalStackLayout{Spacing=14,Children={
                item.Category=="Moment"?Ui.Text("Momento de la comunidad",12):Ui.Heading(item.Summary,17),_article
            }},15));
            if(api is not null)
            {
                _=LoadImagesAsync(new Post(api),item.Id,_article,item.Body);
                var actions=new HorizontalStackLayout{Spacing=8};
                _likeButton=Ui.Secondary("♡ Me gusta");
                _likeButton.Clicked+=async (_,_)=>await ToggleLikeAsync();
                _saveButton=Ui.Secondary("♧ Guardar");
                _saveButton.Clicked+=async (_,_)=>await ToggleSaveAsync();
                var share=Ui.Secondary("Compartir");
                share.Clicked+=async (_,_)=>
                {
                    await Clipboard.Default.SetTextAsync(_item.Title+Environment.NewLine+_item.Summary);
                    _notice.Text="Título y resumen copiados. Aún no hay enlace público (demo localhost).";
                };
                actions.Children.Add(_likeButton);actions.Children.Add(_saveButton);actions.Children.Add(share);
                content.Children.Add(new ScrollView{Orientation=ScrollOrientation.Horizontal,Content=actions,
                    HorizontalScrollBarVisibility=ScrollBarVisibility.Never});
                content.Children.Add(_count);
                content.Children.Add(Ui.Heading("Comentarios de la comunidad",19));
                content.Children.Add(Ui.Notice("Evita compartir datos privados, expedientes o información de atención personal en comentarios públicos."));
                content.Children.Add(_comments);
                content.Children.Add(Ui.Card(_commentDraft,9));
                var comment=Ui.Primary("Publicar comentario");
                comment.Clicked+=async (_,_)=>await AddCommentAsync(comment);
                content.Children.Add(comment);content.Children.Add(_notice);
            }
            if(api is null)Content=Ui.Scroll(content);
            else
            {
                var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                    new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
                root.Add(Ui.Scroll(content),0,0);
                root.Add(Ui.MainTabs(this,api,string.Empty),0,1);Content=root;
            }
        }
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        #endregion Structs
        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if(_interactions is not null)await ReloadInteractionsAsync();
        }
        private static string FallbackCover(string category)=>category switch
        {
            "Guide"=>"mu_demo_post_guide.jpg","Event"=>"mu_demo_post_event.jpg",
            "Job"=>"mu_demo_post_job.jpg",_=>"mu_demo_post_support.jpg"
        };
        private async Task ReloadInteractionsAsync()
        {
            if(_interactions is null)return;
            var (value,error)=await _interactions.ReadAsync(_item.Id);
            if(!value.HasValue){_notice.Text=error;return;}
            var result=value.Value;
            _liked=result.Liked;_saved=result.Saved;
            if(_likeButton is not null)
            {
                _likeButton.Text=(_liked?"♥ Te gusta":"♡ Me gusta")+" ("+result.Likes+")";
                _likeButton.BackgroundColor=_liked?Ui.Lavender:Ui.Pink;
            }
            if(_saveButton is not null)_saveButton.Text=_saved?"✓ Guardado":"♧ Guardar";
            _count.Text=$"{result.Comments} comentarios · {result.Likes} reacciones";
            _comments.Children.Clear();
            if(result.RecentComments.Count==0)
                _comments.Children.Add(Ui.Notice("Todavía no hay comentarios. Puedes iniciar la conversación comunitaria."));
            foreach(var comment in result.RecentComments)
            {
                var block=new VerticalStackLayout{Spacing=5};
                block.Children.Add(Ui.Text(comment.Author+" · "+comment.CreatedUtc.ToLocalTime().ToString("g"),11));
                block.Children.Add(Ui.Text(comment.Body,14));
                if(comment.IsMine)
                {
                    var remove=Ui.Secondary("Eliminar mi comentario");
                    remove.FontSize=11;remove.HeightRequest=35;
                    var id=comment.Id;
                    remove.Clicked+=async (_,_)=>await RemoveCommentAsync(id);
                    block.Children.Add(remove);
                }
                _comments.Children.Add(Ui.Card(block,13));
            }
        }
        private async Task ToggleLikeAsync()
        {
            if(_interactions is null||_likeButton is null)return;
            _likeButton.IsEnabled=false;
            try
            {
                var response=await _interactions.LikeAsync(_item.Id,!_liked);
                if(response.Success)await ReloadInteractionsAsync();else _notice.Text=response.Error;
            }
            finally{_likeButton.IsEnabled=true;}
        }
        private async Task ToggleSaveAsync()
        {
            if(_interactions is null||_saveButton is null)return;
            _saveButton.IsEnabled=false;
            try
            {
                var response=await _interactions.SaveAsync(_item.Id,!_saved);
                if(response.Success)await ReloadInteractionsAsync();else _notice.Text=response.Error;
            }
            finally{_saveButton.IsEnabled=true;}
        }
        private async Task AddCommentAsync(Button sender)
        {
            if(_interactions is null)return;
            var text=(_commentDraft.Text??string.Empty).Trim();
            if(text.Length is <1 or >600){_notice.Text="El comentario debe tener entre 1 y 600 caracteres.";return;}
            sender.IsEnabled=false;
            try
            {
                var result=await _interactions.CommentAsync(_item.Id,text);
                if(result.Success){_commentDraft.Text=string.Empty;_notice.Text="Comentario publicado.";await ReloadInteractionsAsync();}
                else _notice.Text=result.Error;
            }
            finally{sender.IsEnabled=true;}
        }
        private async Task RemoveCommentAsync(Guid id)
        {
            if(_interactions is null)return;
            if(!await DisplayAlertAsync("Retirar comentario","¿Quieres retirar tu comentario?","Sí","Cancelar"))return;
            var result=await _interactions.RemoveCommentAsync(_item.Id,id);
            if(result.Success)await ReloadInteractionsAsync();else _notice.Text=result.Error;
        }
        private static async Task LoadCoverAsync(Post posts,Guid id,Image target)
        {
            var bytes=await posts.CoverAsync(id);
            if(bytes is not null)target.Source=Ui.BytesSource(bytes);
        }
        /// <summary>
        /// Safely renders inline gallery markers without WebView or raw HTML.
        /// Existing articles with no markers preserve their original end-of-article gallery.
        /// </summary>
        private static async Task LoadImagesAsync(Post posts,Guid id,VerticalStackLayout target,string body)
        {
            var result=await posts.MediaAsync(id);
            if(!string.IsNullOrEmpty(result.Error))return;
            target.Children.Clear();
            var matches=System.Text.RegularExpressions.Regex.Matches(body,@"\[imagen([1-5])\]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            int pointer=0;
            var embedded=new HashSet<int>();
            foreach(System.Text.RegularExpressions.Match match in matches)
            {
                if(match.Index>pointer)
                    target.Children.Add(Ui.RichParagraph(body.Substring(pointer,match.Index-pointer),15));
                int imageNumber=int.Parse(match.Groups[1].Value);
                embedded.Add(imageNumber);
                await PlaceMediaAsync(imageNumber);
                pointer=match.Index+match.Length;
            }
            if(pointer<body.Length)target.Children.Add(Ui.RichParagraph(body[pointer..],15));
            for(int i=1;i<=result.Items.Count;i++)
                if(!embedded.Contains(i))await PlaceMediaAsync(i);

            async Task PlaceMediaAsync(int number)
            {
                if(number>result.Items.Count)
                {
                    target.Children.Add(Ui.Notice("La imagen "+number+" no está disponible."));
                    return;
                }
                byte[]? image=await posts.ImageAsync(id,result.Items[number-1]);
                if(image is null)
                {
                    target.Children.Add(Ui.Notice("Imagen no disponible."));return;
                }
                target.Children.Add(Ui.Card(new Image
                {Source=Ui.BytesSource(image),HeightRequest=240,Aspect=Aspect.AspectFit},8));
            }
        }
        internal static string CategoryName(string category)=>category switch
        {"Moment"=>"Momento","Blog"=>"Blog","Guide"=>"Guía","Job"=>"Empleo","Event"=>"Evento",_=>category};
        #endregion Task
    }
}
