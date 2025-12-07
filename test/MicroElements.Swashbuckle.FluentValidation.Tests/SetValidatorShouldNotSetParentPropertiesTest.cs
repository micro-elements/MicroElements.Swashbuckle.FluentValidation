using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
#if OPENAPI_V2
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public class SetValidatorShouldNotSetParentPropertiesTest : UnitTestBase
    {
        public class Model
        {
            public IList<string> ListItems { get; set; }
            public SubModel SubModel { get; set; }
        }

        public class SubModel
        {
            public IList<string> ListItems { get; set; }
        }

        public class ModelValidator : AbstractValidator<Model>
        {
            public ModelValidator()
            {
                RuleFor(e => e.SubModel).SetValidator(new SubModelValidator());
            }
        }
        public class SubModelValidator : AbstractValidator<SubModel>
        {
            public SubModelValidator()
            {
                RuleFor(e => e.ListItems).NotEmpty();
            }
        }

        /// <summary>
        /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/76
        /// </summary>
        [Fact]
        public void SetValidatorShouldNotSetParentProperties()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new ModelValidator()).GenerateSchema(typeof(Model), schemaRepository);

            var modelSchema = schemaRepository.GetSchema("Model");
            var listItemsProperty = modelSchema.Properties[nameof(Model.ListItems)];
            var subModelProperty = modelSchema.Properties[nameof(Model.SubModel)];
            listItemsProperty.Should().NotBeNull();
            subModelProperty.Should().NotBeNull();
            modelSchema.Required.Should().BeEmpty(because: "No required in Model");

            var subModelSchema = schemaRepository.GetSchema("SubModel");
            var subModelListItemsProperty = subModelSchema.Properties[nameof(SubModel.ListItems)];
            subModelListItemsProperty.Should().NotBeNull();
            subModelSchema.Required.Should().Contain(nameof(SubModel.ListItems), because: "ListItems is required in SubModel");
            subModelSchema.Required.Should().HaveCount(1);
        }
    }
}