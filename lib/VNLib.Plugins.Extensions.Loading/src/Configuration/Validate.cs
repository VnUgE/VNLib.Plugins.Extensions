/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: Validate.cs 
*
* Validate.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;

namespace VNLib.Plugins.Extensions.Loading.Configuration
{
    /// <summary>
    /// A class that allows for easy configuration validation
    /// </summary>
    public sealed class Validate
    {
        /// <summary>
        /// Ensures the object is not null and not an empty string,
        /// otherwise a <see cref="ConfigurationValidationException"/> is raised
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="message">The message to display to the user on loading</param>
        /// <exception cref="ConfigurationValidationException"></exception>
        [DoesNotReturn]
        public static void NotNull<T>(T? obj, string message) where T : class
        {
            if (obj is null)
            {
                throw new ConfigurationValidationException(message);
            }

            if (obj is string s && string.IsNullOrWhiteSpace(s))
            {
                throw new ConfigurationValidationException(message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        /// <exception cref="ConfigurationValidationException"></exception>
        public static void Assert([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition)
            {
                throw new ConfigurationValidationException(message);
            }
        }

        public static void NotEqual<T>(T a, T b, string message)
        {
            if (a is null || b is null)
            {
                throw new ConfigurationValidationException(message);
            }

            if (a.Equals(b))
            {
                throw new ConfigurationValidationException(message);
            }
        }

        public static void Range2<T>(T value, T min, T max, string message)
             where T : IComparable<T>
        {
            //Compare the value against min/max calues and raise exception if it is
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            {
                throw new ConfigurationValidationException(message);
            }
        }

        public static void Range<T>(T value, T min, T max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {

            Range2(value, min, max, $"Value for {paramName} must be between {min} and {max}. Value: {value}");
        }


        public static void FileExists(string path)
        {
            if (!FileOperations.FileExists(path))
            {
                throw new ConfigurationValidationException($"Required file: {path} not found");
            }
        }
    }
}
