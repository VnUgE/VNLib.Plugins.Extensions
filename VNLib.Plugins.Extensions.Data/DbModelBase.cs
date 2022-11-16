using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace VNLib.Plugins.Extensions.Data
{
    /// <summary>
    /// Provides a base for DBSet Records with a timestamp/version
    /// a unique ID key, and create/modified timestamps
    /// </summary>
    public abstract class DbModelBase : IDbModel
    {
        ///<inheritdoc/>
        public abstract string Id { get; set; }
        ///<inheritdoc/>
        [Timestamp]
        [JsonIgnore]
        public virtual byte[]? Version { get; set; }
        ///<inheritdoc/>
        public abstract DateTime Created { get; set; }
        ///<inheritdoc/>
        public abstract DateTime LastModified { get; set; }
    }
}
