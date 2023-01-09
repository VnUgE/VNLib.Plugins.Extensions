/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Validation
* File: ValidationExtensions.cs 
*
* ValidationExtensions.cs is part of VNLib.Plugins.Extensions.Validation which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Validation is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Validation is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Validation. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Diagnostics.CodeAnalysis;

using FluentValidation;
using FluentValidation.Results;

#nullable enable

namespace VNLib.Plugins.Extensions.Validation
{
    /// <summary>
    /// Provides shortcut methods to aid programmatic validation of objects
    /// </summary>
    public static class ValidationExtensions
    {
        /// <summary>
        /// If <paramref name="assertion"/> evalues to false, sets the specified assertion message
        /// to the <see cref="WebMessage.Result"/> to the specified string
        /// </summary>
        /// <param name="webm"></param>
        /// <param name="assertion">The result of the assertion</param>
        /// <param name="message">The error message to store when the value is false</param>
        /// <returns>The inverse of <paramref name="assertion"/></returns>
        public static bool Assert(this WebMessage webm, [DoesNotReturnIf(false)] bool assertion, string message)
        {
            if(!assertion)
            {
                webm.Success = false;
                webm.Result = message;
            }
            return !assertion;
        }
        /// <summary>
        /// Validates the specified instance, and stores errors to the specified <paramref name="webMessage"/>
        /// and sets the <see cref="ValErrWebMessage.IsError"/>
        /// </summary>
        /// <param name="instance">The instance to validate</param>
        /// <param name="validator"></param>
        /// <param name="webMessage">The <see cref="ValErrWebMessage"/> to store errors to</param>
        /// <returns>True if the result of the validation is valid, false otherwise and the <paramref name="webMessage"/> is not modified</returns>
        public static bool Validate<T>(this IValidator<T> validator, T instance, ValErrWebMessage webMessage)
        {
            //Validate value
            ValidationResult result = validator.Validate(instance);
            //If not valid, set errors on web message
            if (!result.IsValid)
            {
                webMessage.Success = false;
                webMessage.Errors = result.GetErrorsAsCollection();
            }
            return result.IsValid;
        }
    }
}
