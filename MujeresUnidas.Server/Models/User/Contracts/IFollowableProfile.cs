namespace MujeresUnidas.Server.Models.User.Contracts;

public interface IFollowableProfile
{
    Task<bool> FollowAsync(Guid actorId,Guid targetId,string applicationCode);
    Task<bool> UnfollowAsync(Guid actorId,Guid targetId,string applicationCode);
}
