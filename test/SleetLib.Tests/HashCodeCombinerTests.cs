using System;
using System.Reflection;
using AwesomeAssertions;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class HashCodeCombinerTests
    {
        [Fact]
        public void GetHashCode_WithSameObjects_ReturnsSameValue()
        {
            InvokeStaticGetHashCode("a", 1, null).Should().Be(InvokeStaticGetHashCode("a", 1, null));
        }

        [Fact]
        public void GetHashCode_WithDifferentOrder_ReturnsDifferentValue()
        {
            InvokeStaticGetHashCode("a", "b").Should().NotBe(InvokeStaticGetHashCode("b", "a"));
        }

        [Fact]
        public void AddString_UsesCaseSensitiveHashing()
        {
            var lower = CreateCombiner();
            var upper = CreateCombiner();

            Invoke(lower, "AddString", "package");
            Invoke(upper, "AddString", "Package");

            GetCombinedHash(lower).Should().NotBe(GetCombinedHash(upper));
        }

        [Fact]
        public void AddStringCaseInsensitive_IgnoresCase()
        {
            var lower = CreateCombiner();
            var upper = CreateCombiner();

            Invoke(lower, "AddStringCaseInsensitive", "package");
            Invoke(upper, "AddStringCaseInsensitive", "Package");

            GetCombinedHash(lower).Should().Be(GetCombinedHash(upper));
        }

        private static Type CombinerType => typeof(PackageInput).Assembly.GetType("Sleet.HashCodeCombiner", throwOnError: true);

        private static object CreateCombiner()
        {
            return Activator.CreateInstance(CombinerType, nonPublic: true);
        }

        private static int InvokeStaticGetHashCode(params object[] objects)
        {
            return (int)CombinerType.GetMethod("GetHashCode", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { objects });
        }

        private static void Invoke(object instance, string method, object argument)
        {
            CombinerType.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new[] { argument });
        }

        private static int GetCombinedHash(object instance)
        {
            return (int)CombinerType.GetProperty("CombinedHash", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        }
    }
}
