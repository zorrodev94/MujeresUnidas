using MujeresUnidas.Server.Models.Publications.Repositories;

namespace MujeresUnidas.Server.Models.Publications.Subclasses
{
    /// <summary>Job opportunity subject to institutional review.</summary>
    public sealed class EmploymentPost : Post
    {
        #region Arrays
        /// <summary>Allowed subcategories; this is a demo catalogue, not a SQL authority catalogue.</summary>
        public static readonly IReadOnlyCollection<string> Subcategories = new[] { "Cine", "Comercio", "Administración", "Ingeniería", "Docencia", "Seguridad", "Otros" };
        #endregion Arrays
        #region Structs
        /// <summary>Type-specific content shape used by this publication validation.</summary>
        public readonly record struct EmploymentData(string Sector,string JobDescription);
        #endregion Structs
        #region Initialization
        public EmploymentPost(PostRepository repository) : base(repository) { }
        #endregion Initialization
        #region Task
        /// <summary>Provides category-specific validation through polymorphic dispatch.</summary>
        public override ValidationData ValidateCreate(CreateData data)
        {
            if(data.Category != "Job") return new(false,"The publication type does not match its model.");
            var basic=ValidateCommon(data,5000,Subcategories);
            if(!basic.Success)return basic;
            var details=new EmploymentData(data.Subcategory??string.Empty,data.Body);
            return string.IsNullOrWhiteSpace(details.JobDescription)
                ? new(false,"Publication body cannot be empty.") : basic;
        }
        #endregion Task
    }
}
