using System;
using System.Data;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ServicesyncWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // => /api/data/*
    public class DataController : ControllerBase
    {
        private readonly IConfiguration _config;
        public DataController(IConfiguration config) => _config = config;

        // Quick sanity check: /api/data/ping and /api/ping -> "ok"
        [HttpGet("ping")]
        [HttpGet("~/api/ping")]
        public IActionResult Ping() => Ok("ok");

        // GET /api/data/categories
        // Supports ?stub=true (mock data) and ?debug=true (returns exception text)
        [HttpGet("categories")]
        public IActionResult GetCategories([FromQuery] bool stub = false, [FromQuery] bool debug = false)
        {
            try
            {
                if (stub)
                {
                    return Ok(new[]
                    {
                        new CategoryDto(1, "Plumbing",""),
                        new CategoryDto(2, "Electrical",""),
                        new CategoryDto(3, "Cleaning","")
                    });
                }

                var list = new List<CategoryDto>();
                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    // DB columns were renamed to cateogryid / cateogryname – alias them back to Id / Name
                    using (var cmd = new SqlCommand("SELECT [CategoryID] AS Id, [CategoryName] AS Name,[ImagePath] AS ImagePath FROM [dbo].[Categories] ORDER BY [CategoryName];",
                        con))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            list.Add(new CategoryDto(
                                rdr.GetInt32(0),   // Id
                                rdr.GetString(1),   // Name
                                rdr.IsDBNull(2) ? null : rdr.GetString(2)
                            ));
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                if (debug) return StatusCode(500, ex.ToString());
                return StatusCode(500, "Failed to load categories");
            }
        }

        // Example extra endpoints – keep if you use them
        [HttpGet("users")]
        public IActionResult GetUsers([FromQuery] bool debug = false)
        {
            try
            {
                var data = ExecuteQuery("SELECT * FROM dbo.Users");
                return Ok(data);
            }
            catch (Exception ex)
            {
                if (debug) return StatusCode(500, ex.ToString());
                return StatusCode(500, "Failed to load users");
            }
        }

        [HttpGet("orders")]
        public IActionResult GetOrders([FromQuery] bool debug = false)
        {
            try
            {
                var data = ExecuteQuery("SELECT * FROM dbo.Orders");
                return Ok(data);
            }
            catch (Exception ex)
            {
                if (debug) return StatusCode(500, ex.ToString());
                return StatusCode(500, "Failed to load orders");
            }
        }

        private DataTable ExecuteQuery(string query)
        {
            var table = new DataTable();
            using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var cmd = new SqlCommand(query, con))
                using (var reader = cmd.ExecuteReader())
                {
                    table.Load(reader);
                }
            }
            return table;
        }

        public record CategoryDto(int Id, string Name, string? ImagePath);
    }
}
