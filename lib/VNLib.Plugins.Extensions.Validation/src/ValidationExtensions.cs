﻿/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Validation
* File: ValidationExtensions.cs 
*
* ValidationExtensions.cs is part of VNLib.Plugins.Extensions.Validation which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Validation is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Validation is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

using FluentValidation;
using FluentValidation.Results;

namespace VNLib.Plugins.Extensions.Validation
{
    /// <summary>
    /// Provides shortcut methods to aid programmatic validation of objects
    /// </summary>
    public static class ValidationExtensions
    {
        /// <summary>
        /// If <paramref name="assertion"/> evaluates to false, sets the specified assertion message
        /// to the <see cref="WebMessage.Result"/> to the specified string
        /// </summary>
        /// <param name="webm"></param>
        /// <param name="assertion">The result of the assertion</param>
        /// <param name="message">The error message to store when the value is false</param>
        /// <returns>The inverse of <paramref name="assertion"/></returns>
        public static bool Assert(this WebMessage webm, [DoesNotReturnIf(false)] bool assertion, string message)
        {
            if (!assertion)
            {
                webm.Success = false;
                webm.Result = message;
            }
            return !assertion;
        }

        /// <summary>
        /// If <paramref name="assertion"/> evaluates to false, sets the specified assertion message
        /// to the <see cref="WebMessage.Result"/> to the specified string
        /// </summary>
        /// <param name="webm"></param>
        /// <param name="assertion">The result of the assertion</param>
        /// <param name="errors">The error collection to send to the client in response</param>
        /// <returns>The inverse of <paramref name="assertion"/></returns>
        public static bool AssertError(this WebMessage webm, [DoesNotReturnIf(false)] bool assertion, ICollection errors)
        {
            if (!assertion)
            {
                webm.Success = false;
                webm.Errors = errors;
            }
            return !assertion;
        }

        /// <summary>
        /// If <paramref name="assertion"/> evaluates to false, sets the specified assertion message
        /// to the <see cref="WebMessage.Errors"/> to the specified string array
        /// </summary>
        /// <param name="webm"></param>
        /// <param name="assertion">The result of the assertion</param>
        /// <param name="errors">The error collection to send to the client in response</param>
        /// <returns>The inverse of <paramref name="assertion"/></returns>
        public static bool AssertError(this WebMessage webm, [DoesNotReturnIf(false)] bool assertion, string[] errors)
        {
            return AssertError(webm, assertion, (ICollection)errors);
        }

        /// <summary>
        /// If <paramref name="assertion"/> evaluates to false, sets the specified assertion message
        /// to the <see cref="WebMessage.Errors"/> to the specified object array
        /// </summary>
        /// <param name="webm"></param>
        /// <param name="assertion">The result of the assertion</param>
        /// <param name="errors">The error collection to send to the client in response</param>
        /// <returns>The inverse of <paramref name="assertion"/></returns>
        public static bool AssertError(this WebMessage webm, [DoesNotReturnIf(false)] bool assertion, object[] errors)
        {
            return AssertError(webm, assertion, (ICollection)errors);
        }

        /// <summary>
        /// If <paramref name="assertion"/> evaluates to false, sets the specified assertion message
        /// to the <see cref="WebMessage.Errors"/> to the specified string
        /// </summary>
        /// <param name="webm"></param>
        /// <param name="assertion">The result of the assertion</param>
        /// <param name="error">A single error to return to the client in the errors field</param>
        /// <returns>The inverse of <paramref name="assertion"/></returns>
        public static bool AssertError(this WebMessage webm, [DoesNotReturnIf(false)] bool assertion, string error)
        {
            return AssertError(webm, assertion, [error]);
        }

        /// <summary>
        /// Validates the specified instance, and stores errors to the specified <paramref name="webMessage"/>
        /// </summary>
        /// <param name="instance">The instance to validate</param>
        /// <param name="validator"></param>
        /// <param name="webMessage">The <see cref="WebMessage"/> to store errors to</param>
        /// <returns>True if the result of the validation is valid, false otherwise and the <paramref name="webMessage"/> is not modified</returns>
        public static bool Validate<T>(this IValidator<T> validator, T instance, WebMessage webMessage)
        {
            ArgumentNullException.ThrowIfNull(validator);

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

        /// <summary>
        /// Validates the specified instance, and stores errors to the specified <paramref name="webMessage"/>
        /// </summary>
        /// <param name="instance">The instance to validate</param>
        /// <param name="validator"></param>
        /// <param name="webMessage">The <see cref="ValErrWebMessage"/> to store errors to</param>
        /// <returns>True if the result of the validation is valid, false otherwise and the <paramref name="webMessage"/> is not modified</returns>
        [Obsolete("Use WebMessage instead")]
        public static bool Validate<T>(this IValidator<T> validator, T instance, ValErrWebMessage webMessage)
        {
            return Validate(validator, instance, (WebMessage)webMessage);
        }
    }
}
