using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Messages;
using MujeresUnidas.Comunidad.Models.User.Profiles;

namespace MujeresUnidas.Comunidad.Presentation
{
    public sealed class InvitePage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly Guid _conversationId;
        private readonly Conversation _conversations;
        private readonly CommunityDirectory _people;
        private readonly VerticalStackLayout _results=new(){Spacing=9};
        private readonly Label _message=Ui.Text(string.Empty,12);
        #endregion Operation Data
        #region Initialization
        public InvitePage(ApiClient api,Guid conversationId)
        {
            _api=api;_conversationId=conversationId;_conversations=new Conversation(api);_people=new CommunityDirectory(api);
            Title="Invitar al grupo";
            var content=new VerticalStackLayout{Padding=new Thickness(16,14,16,24),Spacing=12};
            content.Children.Add(Ui.Heading("Invitar integrante",22));
            content.Children.Add(Ui.Text("Selecciona una persona activa de Comunidad.",13));
            content.Children.Add(_message);content.Children.Add(_results);
            Content=Ui.Scroll(content);
        }
        #endregion Initialization
        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();_results.Children.Clear();
            var response=await _people.SearchAsync(string.Empty);
            if(!string.IsNullOrWhiteSpace(response.Error)){_message.Text=response.Error;return;}
            foreach(var item in response.Items.Where(x=>!x.IsSelf))
            {
                var row=new VerticalStackLayout{Spacing=6};
                row.Children.Add(Ui.Heading(item.DisplayName,15));
                var add=Ui.Secondary("Invitar");
                add.Clicked+=async (_,_)=>
                {
                    var result=await _conversations.AddParticipantAsync(_conversationId,item.UserId);
                    _message.Text=result.Message;
                    if(result.Success)await Navigation.PopAsync();
                };
                row.Children.Add(add);_results.Children.Add(Ui.Card(row,10));
            }
        }
        #endregion Task
    }
}
