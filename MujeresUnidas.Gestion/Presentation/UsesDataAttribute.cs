namespace MujeresUnidas.Gestion.Presentation
{
    /// <summary>Documents the data contract consumed by a presentation view.
    /// This is metadata only; it neither binds properties nor grants access.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method,AllowMultiple=true,Inherited=false)]
    public sealed class UsesDataAttribute : Attribute
    {
        #region Structs
        public readonly record struct UsesDataData(Type DataType);
        #endregion Structs
        #region Initialization
        public UsesDataAttribute(Type dataType) { DataType=dataType; }
        #endregion Initialization
        #region Operation Data
        public Type DataType { get; }
        #endregion Operation Data
    }
}
