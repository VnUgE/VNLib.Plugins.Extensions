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
