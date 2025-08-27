/*
* Copyright (c) 2025 Vaughn Nugent
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
        /// Throws a <see cref="ConfigurationValidationException"/> if the specified condition is false.
        /// </summary>
        /// <param name="condition">The condition to assert.</param>
        /// <param name="message">The message to include in the exception if the assertion fails.</param>
        /// <exception cref="ConfigurationValidationException">Thrown when <paramref name="condition"/> is false.</exception>
        public static void Assert([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition)
            {
                throw new ConfigurationValidationException(message);
            }
        }

        /// <summary>
        /// Throws a <see cref="ConfigurationValidationException"/> if <paramref name="a"/> is equal to <paramref name="b"/>, or if either is null.
        /// </summary>
        /// <typeparam name="T">The type of the objects to compare.</typeparam>
        /// <param name="a">The first object to compare.</param>
        /// <param name="b">The second object to compare.</param>
        /// <param name="message">The message to include in the exception if the objects are equal or null.</param>
        /// <exception cref="ConfigurationValidationException">Thrown when <paramref name="a"/> is equal to <paramref name="b"/>, or either is null.</exception>
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

        /// <summary>
        /// Throws a <see cref="ConfigurationValidationException"/> if <paramref name="value"/> is not within the inclusive range defined by <paramref name="min"/> and <paramref name="max"/>.
        /// </summary>
        /// <typeparam name="T">A type that implements <see cref="IComparable{T}"/>.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="min">The minimum allowed value (inclusive).</param>
        /// <param name="max">The maximum allowed value (inclusive).</param>
        /// <param name="message">The message to include in the exception if validation fails.</param>
        /// <exception cref="ConfigurationValidationException">Thrown when <paramref name="value"/> is outside the specified range.</exception>
        public static void Range2<T>(T value, T min, T max, string message)
             where T : IComparable<T>
        {
            //Compare the value against min/max calues and raise exception if it is
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            {
                throw new ConfigurationValidationException(message);
            }
        }

        /// <summary>
        /// Throws a <see cref="ConfigurationValidationException"/> if <paramref name="value"/> is not within the inclusive range defined by <paramref name="min"/> and <paramref name="max"/>.
        /// The exception message includes the parameter name and the invalid value.
        /// </summary>
        /// <typeparam name="T">A type that implements <see cref="IComparable{T}"/>.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="min">The minimum allowed value (inclusive).</param>
        /// <param name="max">The maximum allowed value (inclusive).</param>
        /// <param name="paramName">The name of the parameter being validated (automatically supplied).</param>
        /// <exception cref="ConfigurationValidationException">Thrown when <paramref name="value"/> is outside the specified range.</exception>
        public static void Range<T>(T value, T min, T max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            Range2(value, min, max, $"Value for {paramName} must be between {min} and {max}. Value: {value}");
        }


        /// <summary>
        /// Throws a <see cref="ConfigurationValidationException"/> if the file at <paramref name="path"/> does not exist.
        /// </summary>
        /// <param name="path">The path to the file to check.</param>
        /// <exception cref="ConfigurationValidationException">Thrown when the file does not exist at the specified path.</exception>
        public static void FileExists(string path)
        {
            if (!FileOperations.FileExists(path))
            {
                throw new ConfigurationValidationException($"Required file: {path} not found");
            }
        }
    }
}
