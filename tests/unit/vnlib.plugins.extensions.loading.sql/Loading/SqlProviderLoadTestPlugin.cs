/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql.Tests
* File: SqlProviderLoadTestPlugin.cs 
*
* SqlProviderLoadTestPlugin.cs is part of VNLib.Plugins.Extensions.Loading.Sql.Tests which is part of 
* the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Sql.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Sql.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program. If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading.Sql.Tests.Loading
{
    /// <summary>
    /// Plugin used during tests to expose a service that creates SQL provider connections.
    /// </summary>
    public sealed class SqlProviderLoadTestPlugin : PluginBase
    {
        /// <inheritdoc/>
        public override string PluginName => nameof(SqlProviderLoadTestPlugin);
       
        /// <inheritdoc/>
        protected override void ProcessHostCommand(string cmd)
        {
            // No host commands required for test harness.
        }

        /// <inheritdoc/>
        protected override void OnLoad()
        {
            SqlProviderIntegrationService _service = new (
                this.GetConnectionFactoryAsync(), 
                this.GetContextOptionsAsync()
            );

            this.ExportService(_service, ExportFlags.None);

            Log.Information("SqlProviderLoadTestPlugin loaded.");
        }

        /// <inheritdoc/>
        protected override void OnUnLoad()
        {
            Log.Information("SqlProviderLoadTestPlugin unloading.");
        }   
    }
}
