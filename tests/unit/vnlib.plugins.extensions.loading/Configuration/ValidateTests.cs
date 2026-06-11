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

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Plugins.Extensions.Loading.Configuration;

namespace VNLib.Plugins.Extensions.Loading.Tests.Configuration
{
    [TestClass()]
    public class ValidateTests
    {
        [TestMethod()]
        public void RangeTest()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range(-15, 1, 10));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range(0, 1, 10));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range(15, 1, 10));
            Validate.Range(5, 1, 10);
        }

        [TestMethod()]
        public void NotNullTest()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull<object>(null, nameof(NotNullTest)));
            Validate.NotNull(new object(), nameof(NotNullTest));

            //Test strings
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull<string>(null, nameof(NotNullTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull("", nameof(NotNullTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull("        ", nameof(NotNullTest)));
            Validate.NotNull("Hello", nameof(NotNullTest));
        }

        [TestMethod()]
        public void AssertTest()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Assert(false, nameof(AssertTest)));
            Validate.Assert(true, nameof(AssertTest));
        }

        [TestMethod()]
        public void NotEqualTest()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotEqual(5, 5, nameof(NotEqualTest)));
            // Test: Validate.NotEqual should handle null values
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotEqual(null!, "test", nameof(NotEqualTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotEqual("test", null!, nameof(NotEqualTest)));
            
            Validate.NotEqual(5, 10, nameof(NotEqualTest));
            Validate.NotEqual("test1", "test2", nameof(NotEqualTest));
        }

        [TestMethod()]
        public void Range2Test()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(-15, 1, 10, nameof(RangeTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(0, 1, 10, nameof(RangeTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(15, 1, 10, nameof(RangeTest)));
            Validate.Range2(5, 1, 10, nameof(RangeTest));

            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(-15.0, 1.0, 10.0, nameof(RangeTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(0.0, 1.0, 10.0, nameof(RangeTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.Range2(15.0, 1.0, 10.0, nameof(RangeTest)));
            Validate.Range2(5.0, 1.0, 10.0, nameof(RangeTest));
        }

        [TestMethod()]
        public void FileExistsTest()
        {
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.FileExists("ThisFileDoesNotExist.txt"));

            //Assumes the DLL is in the output directory
            Validate.FileExists("VNLib.Plugins.Extensions.Loading.dll");          
        }
    }
}