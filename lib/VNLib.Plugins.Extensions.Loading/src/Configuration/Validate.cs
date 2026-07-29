/*
* Copyright (c) 2026 Vaughn Nugent
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
using System.Text.RegularExpressions;

using VNLib.Utils.IO;

namespace VNLib.Plugins.Extensions.Loading.Configuration
{
    /// <summary>
    /// Provides configuration validation helper methods.
    /// </summary>
    public sealed class Validate
    {
#pragma warning disable CS8763 // Compiler limitation: cannot express [DoesNotReturnIf] with normal return path
        /// <summary>
        /// Asserts that the specified object is not <see langword="null" /> and not an empty string.
        /// </summary>
        /// <typeparam name="T">The reference type to validate.</typeparam>
        /// <param name="obj">The object to test.</param>
        /// <param name="message">The message to display to the user on loading.</param>
        /// <exception cref="ConfigurationValidationException">The object is <see langword="null" /> or an empty string.</exception>
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
        /// Asserts that the specified condition is <see langword="true" />.
        /// </summary>
        /// <param name="condition">The condition to assert.</param>
        /// <param name="message">The message to include in the exception if the assertion fails.</param>
        /// <exception cref="ConfigurationValidationException"><paramref name="condition" /> is <see langword="false" />.</exception>

        public static void Assert([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition)
            {
                throw new ConfigurationValidationException(message);
            }
        }
#pragma warning restore CS8763

        /// <summary>
        /// Asserts that two objects are not equal and neither is <see langword="null" />.
        /// </summary>
        /// <typeparam name="T">The type of the objects to compare.</typeparam>
        /// <param name="a">The first object to compare.</param>
        /// <param name="b">The second object to compare.</param>
        /// <param name="message">The message to include in the exception if the objects are equal or <see langword="null" />.</param>
        /// <exception cref="ConfigurationValidationException"><paramref name="a" /> is equal to <paramref name="b" />, or either is <see langword="null" />.</exception>
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
        /// Asserts that a value is within the specified inclusive range.
        /// </summary>
        /// <typeparam name="T">A type that implements <see cref="IComparable{T}" />.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="min">The minimum allowed value (inclusive).</param>
        /// <param name="max">The maximum allowed value (inclusive).</param>
        /// <param name="message">The message to include in the exception if validation fails.</param>
        /// <exception cref="ConfigurationValidationException"><paramref name="value" /> is outside the specified range.</exception>
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
        /// Asserts that a value is within the specified inclusive range, including the parameter name in the exception message.
        /// </summary>
        /// <typeparam name="T">A type that implements <see cref="IComparable{T}" />.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="min">The minimum allowed value (inclusive).</param>
        /// <param name="max">The maximum allowed value (inclusive).</param>
        /// <param name="paramName">The name of the parameter being validated (automatically supplied).</param>
        /// <exception cref="ConfigurationValidationException"><paramref name="value" /> is outside the specified range.</exception>
        public static void Range<T>(T value, T min, T max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            Range2(value, min, max, $"Value for {paramName} must be between {min} and {max}. Value: {value}");
        }

        /// <summary>
        /// Asserts that a file exists at the specified path.
        /// </summary>
        /// <param name="path">The path to the file to check.</param>
        /// <exception cref="ConfigurationValidationException">The file does not exist at the specified path.</exception>
        public static void FileExists(string path)
        {
            if (!FileOperations.FileExists(path))
            {
                throw new ConfigurationValidationException($"Required file: {path} not found");
            }
        }

        /// <summary>
        /// Asserts that a string matches the specified regular expression pattern.
        /// </summary>
        /// <param name="value">The string to validate.</param>
        /// <param name="pattern">The regular expression pattern to match against.</param>
        /// <param name="message">The message to include in the exception if validation fails.</param>
        /// <exception cref="ConfigurationValidationException">The string does not match the pattern.</exception>
        public static void Matches(string value, string pattern, string message)
        {
            if (!Regex.IsMatch(value, pattern))
            {
                throw new ConfigurationValidationException(message);
            }
        }

        /// <summary>
        /// Asserts that a string matches the specified regular expression.
        /// </summary>
        /// <param name="regex">The regular expression to match against.</param>
        /// <param name="value">The string to validate.</param>
        /// <param name="message">The message to include in the exception if validation fails.</param>
        /// <exception cref="ConfigurationValidationException">The string does not match the pattern.</exception>
        public static void Matches(string value, Regex regex, string message)
        {
            if (!regex.IsMatch(value))
            {
                throw new ConfigurationValidationException(message);
            }
        }
    }
}
