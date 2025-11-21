using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VNLib.Plugins.Extensions.Loading.Configuration.Tests
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
            Validate.NotNull<object>(new object(), nameof(NotNullTest));

            //Test strings
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull<string>(null, nameof(NotNullTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull("", nameof(NotNullTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotNull("        ", nameof(NotNullTest)));
            Validate.NotNull<string>("Hello", nameof(NotNullTest));
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
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotEqual<string>(null!, "test", nameof(NotEqualTest)));
            Assert.ThrowsExactly<ConfigurationValidationException>(() => Validate.NotEqual<string>("test", null!, nameof(NotEqualTest)));
            
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