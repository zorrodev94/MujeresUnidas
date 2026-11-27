namespace MujeresUnidas.Server.Models.User.Profiles
{
    /// <summary>Only public, consented-to-community data. No email, CURP, hashes or institutional records.</summary>
    public sealed class CommunityProfile
    {
        #region Structs
        public readonly record struct ProfileData(Guid UserId,string DisplayName,string Biography,
            string CommunityRole,int Followers,int Following,bool IsFollowed,bool IsSelf,
            bool HasAvatar,bool HasCover);
        #endregion Structs
    }
}
