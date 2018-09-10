using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SampleWebApi.DbModels;

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
}