using System;

namespace VNLib.Plugins.Extensions.Data.Abstractions
{
    /// <summary>
    /// Defines an entity base that has an owner, identified by its user-id
    /// </summary>
    public interface IUserEntity
    {
        /// <summary>
        /// The user-id of the owner of the entity
        /// </summary>
        string? UserId { get; set; }
    }
}
