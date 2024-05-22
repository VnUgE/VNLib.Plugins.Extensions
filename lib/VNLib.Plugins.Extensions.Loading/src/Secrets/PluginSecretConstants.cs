/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PluginSecretConstants.cs 
*
* PluginSecretConstants.cs is part of VNLib.Plugins.Extensions.Loading which is
* part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

namespace VNLib.Plugins.Extensions.Loading
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    public static class PluginSecretConstants
    {
        public const string VAULT_OBJECT_NAME = "hashicorp_vault";
        public const string SECRETS_CONFIG_KEY = "secrets";
        public const string VAULT_TOKEN_KEY = "token";
        public const string VAULT_ROLE_KEY = "role";
        public const string VAULT_SECRET_KEY = "secret";
        public const string VAULT_TOKEN_ENV_NAME = "VAULT_TOKEN";
        public const string VAULT_KV_VERSION_KEY = "kv_version";

        public const string VAULT_URL_KEY = "url";
        public const string VAULT_TRUST_CERT_KEY = "trust_certificate";

        public const string VAULT_URL_SCHEME = "vault://";
        public const string ENV_URL_SCHEME = "env://";
        public const string FILE_URL_SCHEME = "file://";
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores
}
