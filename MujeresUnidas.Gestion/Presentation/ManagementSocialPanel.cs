using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Presentation;

/// <summary>Internal Moment posts. No content in this panel ever enters the community feed.</summary>
public sealed class ManagementSocialPanel:UserControl
{
    private readonly ApiConnection _api;
    private readonly FlowLayoutPanel _feed=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,
        WrapContents=false,BackColor=Theme.Background,Padding=new Padding(8)};
    private readonly TextBox _editor=new(){Dock=DockStyle.Fill,Multiline=true,MaxLength=1200,
        PlaceholderText="Comparte un Momento con el equipo institucional...",Font=Theme.Text(12),BorderStyle=BorderStyle.FixedSingle};
    private readonly Label _status=Theme.Label("Momentos internos de Gestión · no visibles en Comunidad",10,false,Theme.Muted);
    private bool _saved;
    public ManagementSocialPanel(ApiConnection api)
    {
        _api=api;BackColor=Theme.Background;Dock=DockStyle.Fill;
        var root=Theme.Rows((90,SizeType.Absolute),(128,SizeType.Absolute),(58,SizeType.Absolute),(0,SizeType.Percent),(36,SizeType.Absolute));
        Controls.Add(root);root.Controls.Add(Theme.BannerTitle("Momentos · equipo institucional",25),0,0);
        var form=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(8)};
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,82));form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,18));
        var submit=Theme.Button("＋ Publicar");form.Controls.Add(_editor,0,0);form.Controls.Add(submit,1,0);root.Controls.Add(form,0,1);
        var bar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        var all=Theme.Button("Todos los Momentos",true);var saved=Theme.Button("Guardados",true);var reload=Theme.Button("↻ Actualizar",true);
        bar.Controls.Add(all,0,0);bar.Controls.Add(saved,1,0);bar.Controls.Add(reload,2,0);root.Controls.Add(bar,0,2);
        root.Controls.Add(_feed,0,3);root.Controls.Add(_status,0,4);
        submit.Click+=async (_,_)=>
        {
            var body=_editor.Text.Trim();if(body.Length is <1 or >1200)return;
            var r=await _api.SendAsync(HttpMethod.Post,"api/management/social/moments",new{body});
            if(!r.Success){MessageBox.Show(r.Error);return;}
            _editor.Clear();await ReloadAsync();
        };
        all.Click+=async (_,_)=>{_saved=false;await ReloadAsync();};
        saved.Click+=async (_,_)=>{_saved=true;await ReloadAsync();};
        reload.Click+=async (_,_)=>await ReloadAsync();
        _feed.Resize+=(_,_)=>ResizeCards();
        Load+=async (_,_)=>await ReloadAsync();
    }
    private void ResizeCards()
    {
        foreach(Control card in _feed.Controls)card.Width=Math.Max(320,_feed.ClientSize.Width-35);
    }
    private async Task ReloadAsync()
    {
        var response=await _api.SendAsync(HttpMethod.Get,"api/management/social/moments?saved="+_saved.ToString().ToLowerInvariant());
        _feed.Controls.Clear();
        if(!response.Success){_status.Text=response.Error;return;}
        foreach(var m in response.Data.EnumerateArray())
        {
            Guid id=m.GetProperty("id").GetGuid();
            Guid author=m.GetProperty("authorId").GetGuid();
            var card=Theme.Card();card.Dock=DockStyle.None;card.Width=Math.Max(320,_feed.ClientSize.Width-35);
            card.Height=215;card.Padding=new Padding(16,9,16,9);
            var rows=Theme.Rows((37,SizeType.Absolute),(65,SizeType.Absolute),(55,SizeType.Absolute),(0,SizeType.Percent));
            card.Controls.Add(rows);
            rows.Controls.Add(Theme.Label((m.GetProperty("author").GetString()??"Personal")+" · "+
                m.GetProperty("createdUtc").GetDateTime().ToLocalTime().ToString("g"),12,true),0,0);
            var body=Theme.Label(m.GetProperty("body").GetString()??"",12);body.TextAlign=ContentAlignment.TopLeft;
            rows.Controls.Add(body,0,1);
            var buttons=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4};
            for(int i=0;i<4;i++)buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            var liked=m.GetProperty("liked").GetBoolean();var marked=m.GetProperty("saved").GetBoolean();
            var like=Theme.Button($"♡ {m.GetProperty("likes").GetInt32()}",true);
            var bookmark=Theme.Button(marked?"Guardado":"Guardar",true);
            var comment=Theme.Button($"Comentar · {m.GetProperty("comments").GetInt32()}",true);
            var remove=Theme.Button("Ocultar",true);
            buttons.Controls.Add(like,0,0);buttons.Controls.Add(bookmark,1,0);buttons.Controls.Add(comment,2,0);buttons.Controls.Add(remove,3,0);
            rows.Controls.Add(buttons,0,2);
            like.Click+=async (_,_)=>{await _api.SendAsync(liked?HttpMethod.Delete:HttpMethod.Put,$"api/management/social/moments/{id}/like");await ReloadAsync();};
            bookmark.Click+=async (_,_)=>{await _api.SendAsync(marked?HttpMethod.Delete:HttpMethod.Put,$"api/management/social/moments/{id}/save");await ReloadAsync();};
            comment.Click+=async (_,_)=>await CommentAsync(id);
            remove.Click+=async (_,_)=>
            {
                if(MessageBox.Show("¿Ocultar este Momento del feed institucional?","Confirmar",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
                var result=await _api.SendAsync(HttpMethod.Delete,$"api/management/social/moments/{id}");
                if(!result.Success)MessageBox.Show(result.Error);await ReloadAsync();
            };
            _feed.Controls.Add(card);
        }
        _status.Text=$"{_feed.Controls.Count} Momentos · solo personal autorizado";
    }
    private async Task CommentAsync(Guid id)
    {
        var prior=await _api.SendAsync(HttpMethod.Get,$"api/management/social/moments/{id}/comments");
        var existing=prior.Success?string.Join(Environment.NewLine,prior.Data.EnumerateArray().Select(c=>(c.GetProperty("author").GetString()??"")+": "+(c.GetProperty("body").GetString()??""))):"";
        using var dialog=new Form{Text="Comentarios del Momento",Size=new Size(540,460),StartPosition=FormStartPosition.CenterParent,
            BackColor=Theme.Background,Font=Theme.Text(11)};
        var panel=Theme.Rows((0,SizeType.Percent),(90,SizeType.Absolute),(58,SizeType.Absolute));dialog.Controls.Add(panel);
        var history=new TextBox{Text=existing,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};
        var input=new TextBox{Multiline=true,Dock=DockStyle.Fill,MaxLength=600,PlaceholderText="Escribe tu comentario..."};
        var send=Theme.Button("Publicar comentario");panel.Controls.Add(history,0,0);panel.Controls.Add(input,0,1);panel.Controls.Add(send,0,2);
        send.Click+=async (_,_)=>
        {
            var result=await _api.SendAsync(HttpMethod.Post,$"api/management/social/moments/{id}/comments",new{body=input.Text.Trim()});
            if(result.Success)dialog.Close();else MessageBox.Show(result.Error);
        };
        dialog.ShowDialog(FindForm());await ReloadAsync();
    }
}
