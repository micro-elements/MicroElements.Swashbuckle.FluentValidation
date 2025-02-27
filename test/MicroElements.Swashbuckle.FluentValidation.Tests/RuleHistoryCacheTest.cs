using FluentValidation;
using FluentValidation.Validators;
using MicroElements.OpenApi.FluentValidation;
using System.Linq;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public class RuleHistoryCacheTest
    {
        public class Sample
        {
            public string? Name { get; set;}
        }

        public class SampleValidator : AbstractValidator<Sample>
        {
            public SampleValidator()
            {
                RuleFor(p => p.Name)
                    .Matches("^[a-zA-Z0-9 ]+$");
            }
        }

        [Fact]
        // Issue 143 https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/143
        public void DuplicateCachItem()
        {
            var key = new object();
            var cacheItem1 = CreateCacheItem();
            var cacheItem2 = CreateCacheItem();

            key.AddRuleHistoryItem(cacheItem1);

            Assert.True(key.ContainsRuleHistoryItem(cacheItem2));
        }

        private RuleHistoryCache.RuleCacheItem CreateCacheItem()
        {
            var sampleValidator = new SampleValidator();
            var propertyValidator = sampleValidator.CreateDescriptor().GetRulesForMember("Name").First().Components.First().Validator;
            return new RuleHistoryCache.RuleCacheItem("MySchema", "MyProperty", propertyValidator, "Pattern");
        }
    }
}
