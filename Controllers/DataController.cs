using System;
using System.Data;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using ServicesyncWebApp.Services;

namespace ServicesyncWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // => /api/data/*
    public class DataController : ControllerBase
    {
        private readonly IConfiguration _config;
        private static readonly Dictionary<string, PendingRegistration> _pendingRegistrations = new();
        public DataController(IConfiguration config) => _config = config;

        private string GenerateOtp()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

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
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, [FromServices] IEmailSender emailSender)
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
                }

                // Hash the password
                byte[] passwordHash;
                using (var sha256 = SHA256.Create())
                {
                    passwordHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(request.PasswordHash));
                }

                // Generate OTP
                var otp = GenerateOtp();
                var expiry = DateTime.UtcNow.AddMinutes(10); // OTP valid for 10 minutes

                // Store in pending registrations
                _pendingRegistrations[request.Email] = new PendingRegistration(
                    request.FullName, request.Email, request.Phone, passwordHash, otp, expiry);

                // Send OTP email
                var emailBody = $"<p>Your OTP for registration is: <strong>{otp}</strong></p><p>This OTP is valid for 10 minutes.</p>";
                await emailSender.SendAsync(request.Email, "OTP for Registration", emailBody);

                return Ok(new { message = "OTP sent to your email. Please verify to complete registration." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Registration failed: " + ex.Message);
            }
        }

        // POST /api/data/verify-otp
        [HttpPost("verify-otp")]
        public IActionResult VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Otp))
                {
                    return BadRequest("Email and OTP are required.");
                }

                if (!_pendingRegistrations.TryGetValue(request.Email, out var pending))
                {
                    return BadRequest("No pending registration found for this email.");
                }

                if (DateTime.UtcNow > pending.Expiry)
                {
                    _pendingRegistrations.Remove(request.Email);
                    return BadRequest("OTP has expired. Please register again.");
                }

                if (pending.Otp != request.Otp)
                {
                    return BadRequest("Invalid OTP.");
                }

                // OTP verified, save user to database
                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    using (var insertCmd = new SqlCommand(
                        "INSERT INTO dbo.Users (FullName, Email, Phone, PasswordHash, CreatedAt, IsVerified) VALUES (@FullName, @Email, @Phone, @PasswordHash, @CreatedAt, 1)",
                        con))
                    {
                        insertCmd.Parameters.AddWithValue("@FullName", pending.FullName);
                        insertCmd.Parameters.AddWithValue("@Email", pending.Email);
                        insertCmd.Parameters.AddWithValue("@Phone", pending.Phone);
                        insertCmd.Parameters.Add("@PasswordHash", SqlDbType.VarBinary).Value = pending.PasswordHash;
                        insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                        insertCmd.ExecuteNonQuery();
                    }
                }

                // Remove from pending
                _pendingRegistrations.Remove(request.Email);

                return Ok(new { message = "Registration successful" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Verification failed: " + ex.Message);
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

        [HttpGet("professionals")]
        public IActionResult GetProfessionalsByCategory([FromQuery] int categoryId)
        {
            try
            {
                var professionals = new List<ProfessionalDto>();
                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    var query = @"
                        SELECT DISTINCT p.ProfessionalID, p.CompanyName, p.Email, p.Phone, p.Address1, p.Address2, p.City, p.State, p.PostalCode
                        FROM dbo.Professionals p
                        INNER JOIN dbo.ProfessionalServices ps ON p.ProfessionalID = ps.ProfessionalID
                        WHERE ps.CategoryID = @CategoryID
                    ";

                    using (var cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@CategoryID", categoryId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                professionals.Add(new ProfessionalDto(
                                    reader.GetInt32(0),
                                    reader.GetString(1),
                                    reader.GetString(2),
                                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    reader.IsDBNull(8) ? "" : reader.GetString(8)
                                ));
                            }
                        }
                    }
                }

                return Ok(professionals);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to load professionals: " + ex.Message);
            }
        }

        
        public record CategoryDto(int Id, string Name, string? ImagePath);
        public record RegisterRequest(string FullName, string Email, string Phone, string PasswordHash, string? Confirm);
        public record LoginRequest(string Email, string PasswordHash);
        public record PendingRegistration(string FullName, string Email, string Phone, byte[] PasswordHash, string Otp, DateTime Expiry);
        public record VerifyOtpRequest(string Email, string Otp);
        public record ProfessionalRegisterRequest(string CompanyName, string Email, string Phone, string Address1, string Address2, string City, string State, string PostalCode, string PasswordHash);
        public record ProfessionalLoginRequest(string Email, string PasswordHash);
        public record ProfessionalDto(int ProfessionalID, string CompanyName, string Email, string Phone, string Address1, string Address2, string City, string State, string PostalCode);
        public record ServiceDto(int ServiceID, int ProfessionalID, int CategoryID, string ServiceName, string Title, decimal Price, int? EstimatedHours, string Description, bool IsActive);

        // POST /api/data/orders
        [HttpPost("orders")]
        public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                if (request == null ||
                    request.UserID <= 0 ||
                    request.ProfessionalID <= 0 ||
                    request.CategoryID <= 0 ||
                    string.IsNullOrWhiteSpace(request.ServiceAddress1) ||
                    string.IsNullOrWhiteSpace(request.City) ||
                    string.IsNullOrWhiteSpace(request.State) ||
                    string.IsNullOrWhiteSpace(request.PostalCode) ||
                    request.Subtotal < 0)
                {
                    return BadRequest("Invalid order data.");
                }

                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    using (var cmd = new SqlCommand(
                        @"INSERT INTO dbo.Orders 
                        (UserID, ProfessionalID, CategoryID, ServiceAddress1, ServiceAddress2, City, [State], PostalCode, 
                         ScheduledStart, ScheduledEnd, Notes, Subtotal, TaxAmount, DiscountAmount, PaymentStatus, OrderStatus, IsActive)
                        VALUES 
                        (@UserID, @ProfessionalID, @CategoryID, @ServiceAddress1, @ServiceAddress2, @City, @State, @PostalCode,
                         @ScheduledStart, @ScheduledEnd, @Notes, @Subtotal, @TaxAmount, @DiscountAmount, @PaymentStatus, @OrderStatus, @IsActive);
                        SELECT SCOPE_IDENTITY();",
                        con))
                    {
                        cmd.Parameters.AddWithValue("@UserID", request.UserID);
                        cmd.Parameters.AddWithValue("@ProfessionalID", request.ProfessionalID);
                        cmd.Parameters.AddWithValue("@CategoryID", request.CategoryID);
                        cmd.Parameters.AddWithValue("@ServiceAddress1", request.ServiceAddress1);
                        cmd.Parameters.AddWithValue("@ServiceAddress2", string.IsNullOrWhiteSpace(request.ServiceAddress2) ? (object)DBNull.Value : request.ServiceAddress2);
                        cmd.Parameters.AddWithValue("@City", request.City);
                        cmd.Parameters.AddWithValue("@State", request.State);
                        cmd.Parameters.AddWithValue("@PostalCode", request.PostalCode);
                        cmd.Parameters.AddWithValue("@ScheduledStart", request.ScheduledStart);
                        cmd.Parameters.AddWithValue("@ScheduledEnd", request.ScheduledEnd ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(request.Notes) ? (object)DBNull.Value : request.Notes);
                        cmd.Parameters.AddWithValue("@Subtotal", request.Subtotal);
                        cmd.Parameters.AddWithValue("@TaxAmount", request.TaxAmount);
                        cmd.Parameters.AddWithValue("@DiscountAmount", request.DiscountAmount);
                        cmd.Parameters.AddWithValue("@PaymentStatus", request.PaymentStatus);
                        cmd.Parameters.AddWithValue("@OrderStatus", request.OrderStatus);
                        cmd.Parameters.AddWithValue("@IsActive", request.IsActive);

                        var orderId = cmd.ExecuteScalar();
                        return Ok(new { message = "Order created successfully", orderId = orderId });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to create order: " + ex.Message);
            }
        }
    }
}
