using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class BloggingController : Controller
    {
        private readonly BloggingDbContext _dbContext;

        public BloggingController(BloggingDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost("[action]")]
        public IActionResult AddBlog([FromBody] Blog blog)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _dbContext.Blogs.Add(blog);
            _dbContext.SaveChanges();
            return Ok("Blog added");
        }

        [HttpGet("[action]")]
        public IEnumerable<Blog> GetBlogs()
        {
            return _dbContext.Blogs.Select(blog => blog);
        }
    }

    #region Model

    /// <summary>
    /// BloggingDbContext.
    /// </summary>
    public class BloggingDbContext : DbContext
    {
        public BloggingDbContext(DbContextOptions<BloggingDbContext> options)
            : base(options)
        { }

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<ValidationMetadata> Metadata { get; set; }
    }

    /// <summary>
    /// The blog.
    /// </summary>
    public class Blog
    {
        /// <summary>
        /// Id.
        /// </summary>
        public int BlogId { get; set; }

        /// <summary>
        /// The url of blog.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Blog Author.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Blog posts list.
        /// </summary>
        public List<Post> Posts { get; set; }

        /// <summary>
        /// Optional description.
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Blog post.
    /// </summary>
    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }
    }

    /// <summary>
    /// Property validation metadata.
    /// </summary>
    public class ValidationMetadata
    {
        /// <summary>
        /// Id.
        /// </summary>
        public int ValidationMetadataId { get; set; }

        /// <summary>
        /// Type name.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// The name of property.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Property is required.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Sets MaxLength for property.
        /// </summary>
        public int? MaxLength { get; set; }
    }

    #endregion
    
    #region Validation

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

    #endregion
}