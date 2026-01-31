/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Validation
* File: ValidatorExtensions.cs 
*
* ValidatorExtensions.cs is part of VNLib.Plugins.Extensions.Validation which is part of the larger 
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
using System.Text.RegularExpressions;

using FluentValidation;
using FluentValidation.Results;

namespace VNLib.Plugins.Extensions.Validation
{
    /// <summary>
    /// Defines extenstion methods for <see cref="IRuleBuilder{T, TProperty}"/>
    /// </summary>
    public static class ValidatorExtensions
    {
        public static readonly Regex PhoneRegex = new(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$", RegexOptions.Compiled);

        public static readonly Regex AlphaRegx = new(@"[a-zA-Z]*", RegexOptions.Compiled);
        public static readonly Regex NumericRegx = new(@"[0-9]*", RegexOptions.Compiled);
        public static readonly Regex AlphaNumRegx = new(@"[a-zA-Z0-9]*", RegexOptions.Compiled);

        public static readonly Regex OnlyAlphaRegx = new(@"^[a-zA-Z\s]*$", RegexOptions.Compiled);
        public static readonly Regex OnlyNumericRegx = new(@"^[0-9]*$", RegexOptions.Compiled);
        public static readonly Regex OnlyAlphaNumRegx = new(@"^[a-zA-Z0-9\s]*$", RegexOptions.Compiled);

        public static readonly Regex PasswordRegx = new(@"^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$ %^&*-])", RegexOptions.Compiled);
        public static readonly Regex IllegalRegx = new(@"[\r\n\t\a\b\e\f|^~`<>{}]", RegexOptions.Compiled);
        public static readonly Regex SpecialCharactersRegx = new(@"[\r\n\t\a\b\e\f#?!@$%^&*\+\-\~`|<>\{}]", RegexOptions.Compiled);


        /// <summary>
        /// Gets a collection of Json-serializable validation errors
        /// </summary>
        /// <param name="result"></param>
        /// <returns>A collection of json errors to return to a user</returns>
        public static ICollection GetErrorsAsCollection(this ValidationResult result)
        {
            return result.Errors.ConvertAll(static err => new ValidationErrorMessage { ErrorMessage = err.ErrorMessage, PropertyName = err.PropertyName });
        }

        /// <summary>
        /// Tests the the property against <see cref="PhoneRegex"/> 
        /// to determine if the string matches the proper phone number form
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string?> PhoneNumber<T>(this IRuleBuilder<T, string?> builder)
        {
            return builder.Must(static phone => phone?.Length > 0 && PhoneRegex.IsMatch(phone))
                          .WithMessage("{PropertyValue} is not a valid phone number.");
        }
        /// <summary>
        /// Tests the the property against <see cref="PhoneRegex"/> 
        /// to determine if the string matches the proper phone number form, or allows emtpy strings
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string> EmptyPhoneNumber<T>(this IRuleBuilder<T, string> builder)
        {
            return builder.Must(static phone => phone == null || phone.Length == 0 || PhoneRegex.IsMatch(phone))
                          .WithMessage("{PropertyValue} is not a valid phone number.");
        }

        /// <summary>
        /// Checks a string against <see cref="SpecialCharactersRegx"/>.
        /// If the string is null or empty, it is allowed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string?> SpecialCharacters<T>(this IRuleBuilder<T, string?> builder)
        {
            return builder.Must(static str => str == null || !SpecialCharactersRegx.IsMatch(str))
                          .WithMessage("{PropertyName} contains illegal characters");
        }
        /// <summary>
        /// Checks a string against <see cref="IllegalRegx"/>.
        /// If the string is null or empty, it is allowed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string?> IllegalCharacters<T>(this IRuleBuilder<T, string?> builder)
        {
            return builder.Must(static str => str == null || !IllegalRegx.IsMatch(str))
                          .WithMessage("{PropertyName} contains illegal characters");
        }
        /// <summary>
        /// Makes sure a field contains at least 1 character a-Z
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string?> Alpha<T>(this IRuleBuilder<T, string?> builder)
        {
            return builder.Must(static str => str == null || AlphaRegx.IsMatch(str))
                          .WithMessage("{PropertyName} requires at least one a-Z character.");
        }
        /// <summary>
        /// Determines if all characters are only a-Z (allows whitespace)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string> AlphaOnly<T>(this IRuleBuilder<T, string> builder)
        {
            return builder.Must(static str => str == null || OnlyAlphaRegx.IsMatch(str))
                          .WithMessage("{PropertyName} can only be a alpha character from a-Z.");
        }
        /// <summary>
        /// Makes sure a field contains at least 1 numeral
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string> Numeric<T>(this IRuleBuilder<T, string> builder)
        {
            return builder.Must(static str => str == null || NumericRegx.IsMatch(str))
                          .WithMessage("{PropertyName} requires at least one number.");
        }
        /// <summary>
        /// Determines if all characters are only 0-9 (not whitespace is allowed)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string?> NumericOnly<T>(this IRuleBuilder<T, string?> builder)
        {
            return builder.Must(static str => str == null || OnlyNumericRegx.IsMatch(str))
                          .WithMessage("{PropertyName} can only be a number 0-9.");
        }
        /// <summary>
        /// Makes sure the field contains at least 1 alpha numeric character (whitespace included)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string?> AlphaNumeric<T>(this IRuleBuilder<T, string?> builder)
        {
            return builder.Must(static str => str == null || AlphaNumRegx.IsMatch(str))
                          .WithMessage("{PropertyName} must contain at least one alpha-numeric character.");
        }
        /// <summary>
        /// Determines if all characters are only alpha-numeric (whitespace allowed)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string?> AlphaNumericOnly<T>(this IRuleBuilder<T, string?> builder)
        {
            return builder.Must(static str => str == null || OnlyAlphaNumRegx.IsMatch(str))
                          .WithMessage("{PropertyName} can only contain alpha numeric characters.");
        }
        /// <summary>
        /// Tests the string against the password regular expression to determine if the 
        /// value meets the basic password requirements
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> builder)
        public static IRuleBuilderOptions<T, string?> Password<T>(this IRuleBuilder<T, string?> builder)
        {
            return builder.Must(static str => str == null || PasswordRegx.IsMatch(str))
                          .WithMessage("{PropertyName} does not meet password requirements.");
        }
        /// <summary>
        /// Defines a length validator on the current rule builder, but only for string properties.
        /// Validation will fail if the length of the string is outside of the specified range.
        /// The range is inclusive
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="lengthRange">The length range of the specified string</param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, string> Length<T>(this IRuleBuilder<T, string> builder, Range lengthRange)
        {
            return builder.Length(lengthRange.Start.Value, lengthRange.End.Value);
        }
    }
}
