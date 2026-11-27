namespace MujeresUnidas.Server.Models.Publications.Engagement
{
    /// <summary>Community reactions, saved posts and moderated personal comments.</summary>
    public sealed class PostEngagement
    {
        #region Operation Data
        #endregion Operation Data
        #region Initialization
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        public readonly record struct CommentData(Guid Id,Guid AuthorId,string Author,
            string Body,DateTime CreatedUtc,bool IsMine);
        public readonly record struct SummaryData(int Likes,int Comments,bool Liked,bool Saved,
            IReadOnlyList<CommentData> RecentComments);
        public readonly record struct CommentCreateData(string? Body);
        #endregion Structs
        #region Task
        #endregion Task
    }
}
