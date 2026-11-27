using MujeresUnidas.Comunidad.Models.Publications;
namespace MujeresUnidas.Comunidad.Models.User.Contracts
{
    /// <summary>Requests the publication lifecycle. Server checks the real role for each action.</summary>
    public interface IPublicationPublisher
    {
        Task<Post.ResultData> CreatePostAsync(Post.CreateData data);
        Task<Post.ResultData> EditPostAsync(Guid id,Post.EditData data);
        Task<Post.ResultData> DeletePostAsync(Guid id);
    }
}
