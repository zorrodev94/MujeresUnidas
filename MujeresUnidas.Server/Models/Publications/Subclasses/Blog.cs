using MujeresUnidas.Server.Models.Publications.Repositories;

namespace MujeresUnidas.Server.Models.Publications.Subclasses
{
    /// <summary>Narrative or reflective community publication.</summary>
    public sealed class Blog : Post
    {
        #region Arrays
        /// <summary>Allowed subcategories; this is a demo catalogue, not a SQL authority catalogue.</summary>
        public static readonly IReadOnlyCollection<string> Subcategories = new[] { "Ciencia", "Historia", "Poemas", "Juegos", "Reflexiones", "Bienestar" };
        #endregion Arrays
        #region Structs
        /// <summary>Type-specific content shape used by this publication validation.</summary>
        public readonly record struct BlogData(string Topic,string Narrative);
        #endregion Structs
        #region Initialization
        public Blog(PostRepository repository) : base(repository) { }
        #endregion Initialization
        #region Task
        /// <summary>Provides category-specific validation through polymorphic dispatch.</summary>
        public override ValidationData ValidateCreate(CreateData data)
        {
            if(data.Category != "Blog") return new(false,"The publication type does not match its model.");
            var basic=ValidateCommon(data,8000,Subcategories);
            if(!basic.Success)return basic;
            var details=new BlogData(data.Subcategory??string.Empty,data.Body);
            return string.IsNullOrWhiteSpace(details.Narrative)
                ? new(false,"Publication body cannot be empty.") : basic;
        }
        #endregion Task
    }
}
