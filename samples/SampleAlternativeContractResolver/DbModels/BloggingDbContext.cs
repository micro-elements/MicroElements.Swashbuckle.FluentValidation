using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace SampleAlternativeNamingStrategy.DbModels
{
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
}
