using Microsoft.AspNetCore.Mvc;
using ServicesyncWebApp.Services;

namespace ServicesyncWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly DatabaseService _db;

        public CategoriesController(DatabaseService db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var data = await _db.GetCategoriesAsync();
            return Ok(data);
        }
    }
}
