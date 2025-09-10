using System;
using System.Data;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

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
                                (int)rdr.GetInt64(0),   // Id - Fixed: cast from Int64 to Int32
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

        // POST /api/data/register
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.FullName) ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Phone) ||
                    string.IsNullOrWhiteSpace(request.PasswordHash))
                {
                    return BadRequest("All fields are required.");
                }

                // Hash the password
                byte[] passwordHash;
                using (var sha256 = SHA256.Create())
                {
                    passwordHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(request.PasswordHash));
                }

                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    // Check if email already exists
                    using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users WHERE Email = @Email", con))
                    {
                        checkCmd.Parameters.AddWithValue("@Email", request.Email);
                        var existingCount = (int)checkCmd.ExecuteScalar();
                        if (existingCount > 0)
                        {
                            return BadRequest("Email already exists.");
                        }
                    }

                    // Insert new user with hashed password
                    using (var insertCmd = new SqlCommand(
                        "INSERT INTO dbo.Users (FullName, Email, Phone, PasswordHash, CreatedAt) VALUES (@FullName, @Email, @Phone, @PasswordHash, @CreatedAt)",
                        con))
                    {
                        insertCmd.Parameters.AddWithValue("@FullName", request.FullName);
                        insertCmd.Parameters.AddWithValue("@Email", request.Email);
                        insertCmd.Parameters.AddWithValue("@Phone", request.Phone);
                        insertCmd.Parameters.Add("@PasswordHash", SqlDbType.VarBinary).Value = passwordHash;
                        insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                        insertCmd.ExecuteNonQuery();
                    }
                }

                return Ok(new { message = "Registration successful" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Registration failed: " + ex.Message);
            }
        }

        // POST /api/data/login
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.PasswordHash))
                {
                    return BadRequest("Email and password are required.");
                }

                // Hash the provided password
                byte[] providedPasswordHash;
                using (var sha256 = SHA256.Create())
                {
                    providedPasswordHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(request.PasswordHash));
                }

                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    // Get user by email and password hash
                    using (var cmd = new SqlCommand(
                        "SELECT UserID, FullName, Email, Phone FROM dbo.Users WHERE Email = @Email AND PasswordHash = @PasswordHash",
                        con))
                    {
                        cmd.Parameters.AddWithValue("@Email", request.Email);
                        cmd.Parameters.Add("@PasswordHash", SqlDbType.VarBinary).Value = providedPasswordHash;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var user = new
                                {
                                    UserID = reader.GetInt32(0),
                                    FullName = reader.GetString(1),
                                    Email = reader.GetString(2),
                                    Phone = reader.GetString(3)
                                };

                                return Ok(new { success = true, user = user });
                            }
                            else
                            {
                                return BadRequest("Invalid email or password.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Login failed: " + ex.Message);
            }
        }

        public record CategoryDto(int Id, string Name, string? ImagePath);
        public record RegisterRequest(string FullName, string Email, string Phone, string PasswordHash, string? Confirm);
        public record LoginRequest(string Email, string PasswordHash);
    }
}
