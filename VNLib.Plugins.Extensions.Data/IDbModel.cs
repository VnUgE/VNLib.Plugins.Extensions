using System;

namespace VNLib.Plugins.Extensions.Data
{
    /// <summary>
    /// Represents a basic data model for an EFCore entity
    /// for support in data-stores
    /// </summary>
    public interface IDbModel
    {
        /// <summary>
        /// A unique id for the entity
        /// </summary>
        string Id { get; set; }
        /// <summary>
        /// The <see cref="DateTime"/> the entity was created in the store
        /// </summary>
        DateTime Created { get; set; }
        /// <summary>
        /// The <see cref="DateTime"/> the entity was last modified in the store
        /// </summary>
        DateTime LastModified { get; set; }
        /// <summary>
        /// Entity concurrency token
        /// </summary>
        byte[]? Version { get; set; }
    }
}