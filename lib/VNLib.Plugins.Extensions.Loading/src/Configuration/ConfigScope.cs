/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: ConfigScope.cs 
*
* ConfigScope.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
* VNLib collection of libraries and utilities.
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

using System;
using System.Linq;
using System.Text.Json;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;


namespace VNLib.Plugins.Extensions.Loading
{
    internal sealed class ConfigScope: IConfigScope
    {

        private readonly Lazy<IReadOnlyDictionary<string, JsonElement>> _config;

        private readonly JsonElement _element;

        internal ConfigScope(JsonElement element, string scopeName)
        {
            _element = element;
            ScopeName = scopeName;
            _config = new(LoadTable);
        }

        private IReadOnlyDictionary<string, JsonElement> LoadTable()
        {
            return _element.EnumerateObject().ToDictionary(static k => k.Name, static k => k.Value);
        }

        ///<inheritdoc/>
        public JsonElement this[string key] => _config.Value[key];

        ///<inheritdoc/>
        public IEnumerable<string> Keys => _config.Value.Keys;

        ///<inheritdoc/>
        public IEnumerable<JsonElement> Values => _config.Value.Values;

        ///<inheritdoc/>
        public int Count => _config.Value.Count;

        ///<inheritdoc/>
        public string ScopeName { get; }

        ///<inheritdoc/>
        public bool ContainsKey(string key) => _config.Value.ContainsKey(key);

        ///<inheritdoc/>
        public T Deserialze<T>() => _element.Deserialize<T>()!;

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<string, JsonElement>> GetEnumerator() => _config.Value.GetEnumerator();

        ///<inheritdoc/>
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out JsonElement value) => _config.Value.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => _config.Value.GetEnumerator();
    }
}
