using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Messages;
using MujeresUnidas.Comunidad.Models.User.Profiles;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Community inbox with public room discovery and followed private contacts.</summary>
    [UsesData(typeof(Conversation.ConversationData))]
    public sealed class MessagesPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly Conversation _conversations;
        private readonly CommunityDirectory _directory;
        private readonly VerticalStackLayout _rows = new() { Spacing = 12 };
        private readonly Label _status = Ui.Text(string.Empty,12);
        private readonly SearchBar _search=new(){Placeholder="Buscar conversaciones...",TextColor=Ui.Plum,PlaceholderColor=Ui.Muted,BackgroundColor=Colors.White};
        private readonly bool _staff;
        private string _filter="Todas";
        private readonly Dictionary<string,Button> _filterButtons=new();
        private readonly Guid _myId;
        #endregion Operation Data
        #region Initialization
        public MessagesPage(ApiClient api)
        {
            _api=api;
            _conversations=new Conversation(api);
            _directory=new CommunityDirectory(api);
            _myId=api.CurrentUser.ValueKind==JsonValueKind.Object &&
                api.CurrentUser.TryGetProperty("id",out var id)?id.GetGuid():Guid.Empty;
            _staff=api.CurrentUser.ValueKind==JsonValueKind.Object &&
                api.CurrentUser.TryGetProperty("communityRole",out var role) && role.GetString()=="Staff";
            Title="Mensajes";
            NavigationPage.SetHasNavigationBar(this,false);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            var content=new VerticalStackLayout{Padding=new Thickness(16,12,16,26),Spacing=13};
            content.Children.Add(Ui.Header(this,api,"Mensajes y salas"));
            content.Children.Add(Ui.Heading("Mensajes",26));
            content.Children.Add(Ui.Text("Habla con otras integrantes. Estas conversaciones no son expedientes ni citas institucionales.",12));
            content.Children.Add(_search);
            _search.SearchButtonPressed+=async (_,_)=>await ReloadAsync();
            var controls=new HorizontalStackLayout{Spacing=10};
            var find=Ui.Secondary("Descubrir personas");
            find.Clicked+=async (_,_)=>await Navigation.PushAsync(new SearchPage(_api));
            controls.Children.Add(find);
            content.Children.Add(controls);
            var filters=new HorizontalStackLayout{Spacing=8};
            foreach(var value in new[]{"Todas","Públicas","Privadas"})
            {
                string choice=value;
                var button=choice=="Todas"?Ui.Primary(choice):Ui.Secondary(choice);
                button.Clicked+=async (_,_)=>
                {
                    _filter=choice;
                    foreach(var (name,filterButton) in _filterButtons)
                    {
                        filterButton.BackgroundColor=name==_filter?Ui.Violet:Ui.Pink;
                        filterButton.TextColor=name==_filter?Colors.White:Ui.Plum;
                    }
                    await ReloadAsync();
                };
                _filterButtons[choice]=button;filters.Children.Add(button);
            }
            content.Children.Add(new ScrollView{Orientation=ScrollOrientation.Horizontal,Content=filters,HorizontalScrollBarVisibility=ScrollBarVisibility.Never});
            if(_staff)
            {
                var createPublic=Ui.Primary("Crear sala pública · Staff");
                createPublic.Clicked+=async (_,_)=>await CreatePublicAsync();
                content.Children.Add(createPublic);
            }
            content.Children.Add(_status);
            content.Children.Add(_rows);
            root.Add(Ui.Scroll(content),0,0);
            root.Add(Ui.MainTabs(this,api,"Mensajes"),0,1);
            Content=root;
        }
        #endregion Initialization
        #region Task
        protected override async void OnAppearing(){base.OnAppearing();await ReloadAsync();}
        private async Task CreatePublicAsync()
        {
            var name=await DisplayPromptAsync("Sala comunitaria pública",
                "Elige un nombre temático. No es un canal de emergencias ni una consulta privada.",
                "Crear", "Cancelar", "Soporte Mujeres Unidas",100);
            if(string.IsNullOrWhiteSpace(name))return;
            if(!await DisplayAlertAsync("Confirmar sala pública",
                $"La sala «{name.Trim()}» será visible para todas las cuentas activas. ¿Continuar?",
                "Crear sala","Cancelar"))return;
            var reply=await _conversations.CreateConversationAsync(new Conversation.CreateData
            {Title=name.Trim(),Type="Public",UserIds=new()});
            if(reply.Id.HasValue)
                await Navigation.PushAsync(new ConversationPage(_api,reply.Id.Value,name.Trim(),"Public"));
            else await DisplayAlertAsync("Sala pública",reply.Error,"Aceptar");
        }
        private async Task ReloadAsync()
        {
            _status.Text="Actualizando conversaciones...";_rows.Children.Clear();
            var response=await _conversations.GetConversationsAsync();
            if(!string.IsNullOrWhiteSpace(response.Error))
            {_status.Text=response.Error;return;}
            _status.Text=string.Empty;
            string search=(_search.Text??string.Empty).Trim();
            var publicRooms=response.Items.Where(c=>c.Type=="Public" &&
                (search.Length==0||c.Title.Contains(search,StringComparison.OrdinalIgnoreCase))).ToList();
            if(_filter!="Privadas")
            {
                _rows.Children.Add(Ui.Heading("Chats públicos",19));
                if(publicRooms.Count==0)
                    _rows.Children.Add(Ui.Notice("Todavía no existen salas públicas. Personal autorizado puede abrirlas desde Mensajes."));
                foreach(var chat in publicRooms)await AddConversationAsync(chat,true);
            }
            if(_filter=="Públicas")return;
            _rows.Children.Add(Ui.Heading("Tus conversaciones",19));
            var privateChats=response.Items.Where(c=>c.Type!="Public" &&
                (search.Length==0||c.Title.Contains(search,StringComparison.OrdinalIgnoreCase))).ToList();
            if(privateChats.Count==0)
                _rows.Children.Add(Ui.Notice("Aún no tienes conversaciones privadas. Sigue a otra integrante o abre su perfil para conversar."));
            foreach(var chat in privateChats)await AddConversationAsync(chat,false);
            // Contact discovery belongs in Search, not mixed into the inbox.
            var discover=Ui.Secondary("Buscar integrantes desde Descubrir");
            discover.Clicked+=async (_,_)=>await Navigation.PushAsync(new SearchPage(_api));
            _rows.Children.Add(discover);
        }
        private async Task AddConversationAsync(Conversation.ConversationData chat,bool isPublic)
        {
            var row=new Grid {ColumnSpacing=11,
                ColumnDefinitions={new ColumnDefinition(new GridLength(52)),
                    new ColumnDefinition(GridLength.Star),new ColumnDefinition(new GridLength(36))}};
            var avatar=Ui.Avatar(48);
            if(isPublic)avatar.Source="mu_mark.png";
            else if(chat.Type=="Direct")
            {
                var other=chat.UserIds?.FirstOrDefault(id=>id!=_myId)??Guid.Empty;
                if(other!=Guid.Empty)
                {
                    var face=await _directory.ImageAsync(other,"avatar");
                    if(face is not null)avatar.Source=Ui.BytesSource(face);
                }
            }
            row.Add(avatar,0,0);
            var info=new VerticalStackLayout{Spacing=4};
            info.Children.Add(Ui.Heading(chat.Title,16));
            info.Children.Add(Ui.Text(isPublic?"Sala comunitaria · no compartas datos privados":
                (chat.Type=="Group"?$"Grupo · {chat.UserIds?.Count ?? 0} participantes":"Mensaje directo"),12));
            row.Add(info,1,0);
            var arrow=Ui.Icon("nav_messages.png",24);arrow.VerticalOptions=LayoutOptions.Center;row.Add(arrow,2,0);
            var card=Ui.Card(row,12);
            card.GestureRecognizers.Add(new TapGestureRecognizer{Command=new Command(async ()=>
                await Navigation.PushAsync(new ConversationPage(_api,chat.ConversationId,chat.Title,chat.Type)))});
            _rows.Children.Add(card);
        }
        private async Task StartDirectAsync(Guid id,string name)
        {
            var response=await _conversations.CreateConversationAsync(new Conversation.CreateData
            {Title="Chat con "+name,Type="Direct",UserIds=new List<Guid>{id}});
            if(response.Id.HasValue)
                await Navigation.PushAsync(new ConversationPage(_api,response.Id.Value,"Chat con "+name));
            else await DisplayAlertAsync("Chat privado",response.Error,"Aceptar");
        }
        #endregion Task
    }
}
