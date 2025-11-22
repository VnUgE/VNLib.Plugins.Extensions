/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Tests
* File: TestPluginBase.cs 
*
* TestPluginBase.cs is part of VNLib.Plugins.Extensions.Loading.Tests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Text.Json;

namespace VNLib.Plugins.Extensions.Loading.Tests.PluginConfigStore
{
    /// <summary>
    /// Mock implementation of PluginBase for testing purposes
    /// </summary>
    internal sealed class TestPluginBase : PluginBase, IDisposable
    {       

        public TestPluginBase(): this(new { }, new { })
        { }

        public TestPluginBase(object pluginConfig, object hostConfig)
        {
            /*
             * This config object must match the expected PluginBase.cs
             * expected json structure:
             * {  
             *  "host": { ... },
             *  "plugin": { ... }
             *  }
             *  
             *  Ensure the "host" and "plugin" properties are up-to-date
             *  with PluginBase.cs.
             */
            object config = new
            {
                host = hostConfig,
                plugin = pluginConfig
            };

            // The host runtime invokes InitConfig with the binary json data.
            byte[] configData = JsonSerializer.SerializeToUtf8Bytes(config);

            InitConfig(configData);
            InitLog(["--verbose"]);
        }
       

        public override string PluginName => "TestPlugin";

        public void Dispose()
        {
            (this as IPlugin).Unload();
        }

        protected override void OnLoad()
        { }

        protected override void OnUnLoad()
        { }

        protected override void ProcessHostCommand(string cmd)
        {
            throw new NotImplementedException();
        }
    }
}
