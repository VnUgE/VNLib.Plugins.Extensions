/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: LoggingExtensions.cs 
*
* LoggingExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// Provides advanced QOL features for event logging
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Creates a new <see cref="ILogProvider"/> that scopes all log messages to the specified name
        /// when writing messages
        /// </summary>
        /// <param name="log"></param>
        /// <param name="scopeName">The name of the scope to print log values to</param>
        /// <returns>The scoped log provider instance</returns>
        public static ILogProvider CreateScope(this ILogProvider log, string scopeName)
        {
            return new ScopeLogProvider(log, scopeName);
        }

        private sealed record class ScopeLogProvider(ILogProvider Log, string ScopeName) : ILogProvider
        {
            ///<inheritdoc/>
            public void Flush() => Log.Flush();

            ///<inheritdoc/>
            public object GetLogProvider() => Log.GetLogProvider();

            ///<inheritdoc/>
            public bool IsEnabled(LogLevel level) => Log.IsEnabled(level);

            ///<inheritdoc/>
            public void Write(LogLevel level, string value)
            {
                Log.Write(level, $"[{ScopeName}]: {value}");
            }

            ///<inheritdoc/>
            public void Write(LogLevel level, Exception exception, string value = "")
            {
                Log.Write(level, exception, $"[{ScopeName}]: {value}");
            }

            ///<inheritdoc/>
            public void Write(LogLevel level, string value, params object?[] args)
            {
                Log.Write(level, $"[{ScopeName}]: {value}", args);
            }

            ///<inheritdoc/>
            public void Write(LogLevel level, string value, params ValueType[] args)
            {
                Log.Write(level, $"[{ScopeName}]: {value}", args);
            }
        }
    }
}
