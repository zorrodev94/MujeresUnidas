using MujeresUnidas.Server.Models.Publications.Repositories;

namespace MujeresUnidas.Server.Models.Publications.Subclasses
{
    /// <summary>Step-by-step community information and educational resources.</summary>
    public sealed class Guide : Post
    {
        #region Arrays
        /// <summary>Allowed subcategories; this is a demo catalogue, not a SQL authority catalogue.</summary>
        public static readonly IReadOnlyCollection<string> Subcategories = new[] { "Superación", "Aplicación", "Prevención", "Educación", "Orientación" };
        #endregion Arrays
        #region Structs
        /// <summary>Type-specific content shape used by this publication validation.</summary>
        public readonly record struct GuideData(string Topic,string Instructions);
        #endregion Structs
        #region Initialization
        public Guide(PostRepository repository) : base(repository) { }
        #endregion Initialization
        #region Task
        /// <summary>Provides category-specific validation through polymorphic dispatch.</summary>
        public override ValidationData ValidateCreate(CreateData data)
        {
            if(data.Category != "Guide") return new(false,"The publication type does not match its model.");
            var basic=ValidateCommon(data,12000,Subcategories);
            if(!basic.Success)return basic;
            var details=new GuideData(data.Subcategory??string.Empty,data.Body);
            return string.IsNullOrWhiteSpace(details.Instructions)
                ? new(false,"Publication body cannot be empty.") : basic;
        }
        #endregion Task
    }
}
