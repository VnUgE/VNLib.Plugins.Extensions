/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Tests
* File: TestConfigTypes.cs 
*
* TestConfigTypes.cs is part of VNLib.Plugins.Extensions.Loading.Tests which is part of the larger 
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
using System.Threading.Tasks;

namespace VNLib.Plugins.Extensions.Loading.Tests.PluginConfigStore
{
    [ConfigurationName("simple_config")]
    internal sealed class SimpleConfig
    {
        public string? Name { get; set; }
        public int Port { get; set; }
        public bool Enabled { get; set; }
    }

    [ConfigurationName("validated_config")]
    internal sealed class ValidatedConfig : IOnConfigValidation
    {
        public string? Host { get; set; }
        public int Timeout { get; set; }

        public void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                throw new ConfigurationValidationException("Host is required");
            }

            if (Timeout <= 0)
            {
                throw new ConfigurationValidationException("Timeout must be positive");
            }
        }
    }

    [ConfigurationName("async_config")]
    internal sealed class AsyncConfig : IAsyncConfigurable
    {
        public string? DatabasePath { get; set; }
        public bool IsInitialized { get; private set; }

        public Task ConfigureServiceAsync(PluginBase plugin)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }
    }

    [ConfigurationName("optional_config", Required = false)]
    internal sealed class OptionalConfig
    {
        public string? Value { get; set; }
    }

    internal sealed class NoAttributeConfig
    {
        public string? Data { get; set; }
    }
}
