using MujeresUnidas.Server.Models.Publications.Repositories;

namespace MujeresUnidas.Server.Models.Publications.Subclasses
{
    /// <summary>Community event with an explicit participation modality.</summary>
    public sealed class Event : Post
    {
        #region Arrays
        /// <summary>Allowed subcategories; this is a demo catalogue, not a SQL authority catalogue.</summary>
        public static readonly IReadOnlyCollection<string> Subcategories = new[] { "Fiesta", "Inauguración", "Taller", "Conferencia", "Encuentro", "Otros" };
        #endregion Arrays
        #region Structs
        /// <summary>Type-specific content shape used by this publication validation.</summary>
        public readonly record struct EventData(string Topic,string Mode,string Description);
        #endregion Structs
        #region Initialization
        public Event(PostRepository repository) : base(repository) { }
        #endregion Initialization
        #region Task
        /// <summary>Provides category-specific validation through polymorphic dispatch.</summary>
        public override ValidationData ValidateCreate(CreateData data)
        {
            if(data.Category != "Event") return new(false,"The publication type does not match its model.");
            var basic=ValidateCommon(data,4000,Subcategories);
            if(!basic.Success)return basic;
            var details=new EventData(data.Subcategory??string.Empty,data.Mode??string.Empty,data.Body);
            if(details.Mode is not ("Presencial" or "Virtual" or "Híbrido"))
                return new(false,"Choose an event modality: Presencial, Virtual or Híbrido.");
            return basic;
        }
        #endregion Task
    }
}
