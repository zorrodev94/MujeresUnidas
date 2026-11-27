using MujeresUnidas.Server.Models.Publications.Repositories;

namespace MujeresUnidas.Server.Models.Publications.Subclasses
{
    /// <summary>Short community publication; a Moment inherits Post moderation, engagement and media ownership.</summary>
    public sealed class Moment : Post
    {
        #region Arrays
        public static readonly IReadOnlyCollection<string> Themes =
            new[] { "Vida diaria", "Bienestar", "Reflexiones", "Celebraciones" };
        #endregion Arrays
        #region Initialization
        public Moment(PostRepository repository) : base(repository) { }
        #endregion Initialization
        #region Task
        public override ValidationData ValidateCreate(CreateData data)
        {
            if (data.Category != "Moment") return new(false, "La publicación no corresponde a Moment.");
            if (string.IsNullOrWhiteSpace(data.Body) || data.Body.Trim().Length is <8 or >600)
                return new(false, "El momento requiere de 8 a 600 caracteres.");
            if (string.IsNullOrWhiteSpace(data.Title) || data.Title.Length >120 ||
                string.IsNullOrWhiteSpace(data.Summary) || data.Summary.Length >260)
                return new(false, "Título o resumen inválido.");
            if (!Themes.Contains(data.Subcategory ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                return new(false, "Selecciona un tema válido.");
            return new(true,string.Empty);
        }
        #endregion Task
    }
}
