using System.Linq;
using FluentValidation;
using SampleAlternativeNamingStrategy.DbModels;

namespace SampleAlternativeNamingStrategy.Validators
{
    /// <summary>
    /// Sample validator with database dependency.
    /// </summary>
    public class BlogValidator : AbstractValidator<Blog>
    {
        public BlogValidator(BloggingDbContext dbContext)
        {
            // Case0: No dependency on DbContext
            RuleFor(blog => blog.BlogId).NotEmpty();
            RuleFor(blog => blog.Url).NotNull();

            // Case1: You need DbContext for runtime validation.
            // DbContext can be null on swagger generation and active on request validation.
            RuleFor(blog => blog.Url)
                .Must(url => dbContext.Blogs.Count(b => b.Url == url) == 0)
                .WithMessage("Url must be unique");

            // Case2: You need DbContext for defining rules.
            // DbContext must be active on swagger generation!!!
            var propertyMetadata = dbContext.Metadata
                .FirstOrDefault(metadata => metadata.TypeName == nameof(Blog) && metadata.PropertyName == nameof(Blog.Author));
            if (propertyMetadata != null)
            {
                var ruleBuilder = RuleFor(blog => blog.Author);
                if (propertyMetadata.IsRequired)
                    ruleBuilder.NotNull();
                if (propertyMetadata.MaxLength.HasValue)
                    ruleBuilder.MaximumLength(propertyMetadata.MaxLength.Value);
            }
        }
    }
}