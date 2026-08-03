/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Tests
* File: ValidateTests.cs 
*
* ValidateTests.cs is part of VNLib.Plugins.Extensions.Loading.Tests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Plugins.Extensions.Loading.Configuration;

namespace VNLib.Plugins.Extensions.Loading.Tests.Configuration
{
    [TestClass()]
    public class ValidateTests
    {
        [TestMethod()]
        public void Range_ThrowsException_WhenValueOutOfRange()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range(-15, 1, 10));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range(0, 1, 10));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range(15, 1, 10));
            Validate.Range(5, 1, 10);
        }

        [TestMethod()]
        public void NotNull_ThrowsException_WhenValueIsNull()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull<object>(null, "value must not be null"));
            Validate.NotNull(new object(), "value must not be null");

            //Test strings
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull<string>(null, "value must not be null"));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull("", "value must not be null"));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull("        ", "value must not be null"));
            Validate.NotNull("Hello", "value must not be null");
        }

        [TestMethod()]
        public void Assert_ThrowsException_WhenConditionIsFalse()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Assert(false, "condition must be true"));
            Validate.Assert(true, "condition must be true");
        }

        [TestMethod()]
        public void NotEqual_ThrowsException_WhenValuesAreEqual()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotEqual(5, 5, "values must not be equal"));
            // Test: Validate.NotEqual should handle null values
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotEqual(null!, "test", "values must not be equal"));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotEqual("test", null!, "values must not be equal"));
            
            Validate.NotEqual(5, 10, "values must not be equal");
            Validate.NotEqual("test1", "test2", "values must not be equal");
        }

        [TestMethod()]
        public void Range_ThrowsException_WhenMaxLessThanMin()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(-15, 1, 10, "value must be in range"));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(0, 1, 10, "value must be in range"));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(15, 1, 10, "value must be in range"));
            Validate.Range2(5, 1, 10, "value must be in range");

            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(-15.0, 1.0, 10.0, "value must be in range"));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(0.0, 1.0, 10.0, "value must be in range"));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(15.0, 1.0, 10.0, "value must be in range"));
            Validate.Range2(5.0, 1.0, 10.0, "value must be in range");
        }

        [TestMethod()]
        public void FileExists_ThrowsException_WhenFileDoesNotExist()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.FileExists("ThisFileDoesNotExist.txt"));

            //Assumes the DLL is in the output directory
            Validate.FileExists("VNLib.Plugins.Extensions.Loading.dll");          
        }

        [TestMethod()]
        public void Matches_ThrowsException_WhenStringDoesNotMatchPattern()
        {
            // Successful matches
            Validate.Matches("hello", "^h.*o$", "should match");
            Validate.Matches("test@example.com", @"^[\w.]+@[\w.]+\.\w+$", "should match email pattern");

            // Failed matches throw
            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => Validate.Matches("world", "^h.*", "expected match failure")
            );
            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => Validate.Matches("", @".+", "expected empty string to fail")
            );
        }

        [TestMethod()]
        public void Matches_ThrowsException_WhenStringDoesNotMatchRegex()
        {
            Regex emailPattern = new(@"^[\w.]+@[\w.]+\.\w+$", RegexOptions.Compiled);
            Regex digitsOnly = new(@"^\d+$", RegexOptions.Compiled);

            // Successful matches
            Validate.Matches("user@test.com", emailPattern, "should match");
            Validate.Matches("12345", digitsOnly, "should match");

            // Failed matches throw
            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => Validate.Matches("not-an-email", emailPattern, "expected match failure")
            );
            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => Validate.Matches("abc123", digitsOnly, "expected match failure")
            );
        }
    }
}