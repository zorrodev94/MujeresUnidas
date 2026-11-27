namespace MujeresUnidas.Comunidad.Models.User.Contracts
{
    /// <summary>Follows only approved Community profiles; Server owns the restrictions.</summary>
    public interface IFollowableProfile
    {
        Task<(bool Success,string Error)> FollowUserAsync(Guid userId);
        Task<(bool Success,string Error)> UnfollowUserAsync(Guid userId);
    }
}
