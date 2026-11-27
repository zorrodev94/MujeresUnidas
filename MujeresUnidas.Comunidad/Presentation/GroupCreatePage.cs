using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Messages;
using MujeresUnidas.Comunidad.Models.User.Profiles;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Invites selected Community profiles into a private group.</summary>
    public sealed class GroupCreatePage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityDirectory _people;
        private readonly Conversation _conversations;
        private readonly Entry _title=Ui.Input("Nombre del grupo");
        private readonly HashSet<Guid> _members=new();
        private readonly VerticalStackLayout _choices=new(){Spacing=7};
        private readonly Label _status=Ui.Text(string.Empty,12);
        #endregion Operation Data
        #region Initialization
        public GroupCreatePage(ApiClient api)
        {
            _api=api;_people=new CommunityDirectory(api);_conversations=new Conversation(api);
            Title="Crear grupo";
            var content=new VerticalStackLayout{Padding=new Thickness(17,15,17,30),Spacing=12};
            content.Children.Add(Ui.Heading("Nuevo grupo",24));
            content.Children.Add(Ui.Field(_title));
            content.Children.Add(Ui.Text("Elige al menos dos personas para un grupo privado.",12));
            content.Children.Add(_choices);content.Children.Add(_status);
            var create=Ui.Primary("Crear conversación grupal");
            create.Clicked+=async (_,_)=>await CreateAsync();
            content.Children.Add(create);Content=Ui.Scroll(content);
        }
        #endregion Initialization
        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();_choices.Children.Clear();
            var result=await _people.SearchAsync(string.Empty);
            if(!string.IsNullOrEmpty(result.Error)){_status.Text=result.Error;return;}
            foreach(var p in result.Items.Where(p=>!p.IsSelf))
            {
                Guid id=p.UserId;
                var check=new CheckBox{Color=Ui.Violet,VerticalOptions=LayoutOptions.Center};
                check.CheckedChanged+=(_,args)=>{if(args.Value)_members.Add(id);else _members.Remove(id);};
                var row=new HorizontalStackLayout{Spacing=8,Children={check,Ui.Text(p.DisplayName,14)}};
                _choices.Children.Add(Ui.Card(row,8));
            }
        }
        private async Task CreateAsync()
        {
            if((_title.Text??string.Empty).Trim().Length<2 || _members.Count<2 || _members.Count>19)
            {_status.Text="Escribe un nombre y elige de 2 a 19 participantes.";return;}
            var result=await _conversations.CreateConversationAsync(new Conversation.CreateData
            {Title=_title.Text!.Trim(),Type="Group",UserIds=_members.ToList()});
            if(result.Id.HasValue)await Navigation.PushAsync(new ConversationPage(_api,result.Id.Value,_title.Text!.Trim(),"Group"));
            else _status.Text=result.Error;
        }
        #endregion Task
    }
}
