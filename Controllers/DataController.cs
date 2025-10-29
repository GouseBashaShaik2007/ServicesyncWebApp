using System;
using System.Data;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Text.Json.Serialization;
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

        [HttpGet("getuserprofile")]
        public IActionResult getuserprofile([FromQuery] int userId)
        {
            try
            {
                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    using (var cmd = new SqlCommand("SELECT UserID, FullName, Email, Phone FROM dbo.Users WHERE UserID = @UserID", con))
                    {
                        cmd.Parameters.AddWithValue("@UserID", userId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var userProfile = new UserProfileDto(
                                    reader.GetInt32(0),
                                    reader.GetString(1),
                                    reader.GetString(2),
                                    reader.GetString(3)
                                );

                                return Ok(userProfile);
                            }
                            else
                            {
                                return NotFound("User not found.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to load user profile: " + ex.Message);
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
                    return BadRequest(new { error = "All fields are required." });
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

                Console.WriteLine("Login attempt for email: " + request.Email);

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

                                Console.WriteLine("User found: " + user.UserID + ", " + user.FullName);
                                return Ok(new { success = true, user = user });
                            }
                            else
                            {
                                Console.WriteLine("No user found for email: " + request.Email);
                                return BadRequest("Invalid email or password.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Login error: " + ex.Message);
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
                        SELECT DISTINCT p.ProfessionalID, p.CompanyName, p.Email, p.Phone, p.Address1, p.Address2, p.City, p.State, p.PostalCode,ISNULL(CAST(Avg(r.Rating) AS DECIMAL(10,2)),0) AS Ratings
                        FROM dbo.Professionals p
                        INNER JOIN dbo.ProfessionalServices ps ON p.ProfessionalID = ps.ProfessionalID
                        LEFT outer JOIN dbo.Reviews r ON r.ProfessionalID = p.ProfessionalID
                        WHERE ps.CategoryID = @CategoryID GROUP BY p.ProfessionalID, p.CompanyName, p.Email, p.Phone, p.Address1, p.Address2, p.City, p.State, p.PostalCode
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
                                    reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    reader.IsDBNull(9) ? 0 : reader.GetDecimal(9)


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



        // GET /api/data/services?professionalId=1
        [HttpGet("services")]
        public IActionResult GetServicesByProfessional([FromQuery] int professionalId)
        {
            try
            {
                var services = new List<ServiceDto>();
                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    var query = @"
                        SELECT ServiceID, ProfessionalID, CategoryID, ServiceName, Title, Price, EstimatedHours, Description, IsActive
                        FROM dbo.Services
                        WHERE ProfessionalID = @ProfessionalID AND IsActive = 1
                    ";

                    using (var cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@ProfessionalID", professionalId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                services.Add(new ServiceDto(
                                    reader.GetInt32(0),
                                    reader.GetInt32(1),
                                    reader.GetInt32(2),
                                    reader.GetString(3),
                                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    reader.GetDecimal(5),
                                    reader.IsDBNull(6) ? null : reader.GetInt32(6),
                                    reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    reader.GetBoolean(8)
                                ));
                            }
                        }
                    }
                }

                return Ok(services);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to load services: " + ex.Message);
            }
        }

        // POST /api/data/orders
        [HttpPost("orders")]
        public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                if (request == null ||
                    request.ProfessionalID <= 0 ||
                    request.CategoryID <= 0 ||
                    string.IsNullOrWhiteSpace(request.ServiceAddress1) ||
                    string.IsNullOrWhiteSpace(request.City) ||
                    string.IsNullOrWhiteSpace(request.State) ||
                    string.IsNullOrWhiteSpace(request.PostalCode) ||
                    request.Subtotal < 0 ||
                    string.IsNullOrWhiteSpace(request.ScheduledStart))
                {
                    return BadRequest(new { error = "Invalid order data." });
                }

                // Parse ScheduledStart
                if (!DateTime.TryParse(request.ScheduledStart, out var scheduledStart))
                {
                    return BadRequest(new { error = "Invalid ScheduledStart format." });
                }

                DateTime? scheduledEnd = null;
                if (!string.IsNullOrWhiteSpace(request.ScheduledEnd))
                {
                    if (!DateTime.TryParse(request.ScheduledEnd, out var end))
                    {
                        return BadRequest(new { error = "Invalid ScheduledEnd format." });
                    }
                    scheduledEnd = end;
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
                        cmd.Parameters.AddWithValue("@ScheduledStart", scheduledStart);
                        cmd.Parameters.AddWithValue("@ScheduledEnd", scheduledEnd ?? (object)DBNull.Value);
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

        

        // GET /api/data/orders?userId=16
        [HttpGet("getorders")]
        public IActionResult GetOrders([FromQuery] int userId)
        {
            try
            {
                var orders = new List<OrderDto>();
                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    var query = @"
                        SELECT
                          o.OrderID,
                          o.UserID,
                          u.FullName AS CustomerName,
                          p.CompanyName,
                          c.CategoryName,
                          o.ScheduledStart,
                          o.Subtotal,
                          o.TaxAmount,
                          o.DiscountAmount,
                          o.PaymentStatus,
                          o.OrderStatus,
                          o.Created_At,
                          CONCAT_WS(', ',
                            NULLIF(LTRIM(RTRIM(
                              o.ServiceAddress1 + CASE WHEN NULLIF(o.ServiceAddress2,'') IS NOT NULL THEN ' ' + o.ServiceAddress2 ELSE '' END
                            )), ''),
                            NULLIF(LTRIM(RTRIM(o.City)), ''),
                            NULLIF(LTRIM(RTRIM(CONCAT(o.State, CASE WHEN NULLIF(o.PostalCode,'') IS NOT NULL THEN ' ' + o.PostalCode ELSE '' END))), '')
                          ) AS FullAddress,r.Rating,R.ReviewText,convert(VARCHAR, r.CreatedAT, 0) AS reviewdate,
                          p.ProfessionalID
                        FROM dbo.Orders o
                        LEFT JOIN dbo.Users         u ON u.UserID = o.UserID
                        LEFT JOIN dbo.Professionals p ON p.ProfessionalID = o.ProfessionalID
                        LEFT JOIN dbo.Categories    c ON c.CategoryID = o.CategoryID
                        LEFT JOIN dbo.Reviews    r ON r.OrderID = o.OrderID
                        WHERE o.UserID = @UserID
                        ORDER BY o.Created_At DESC;
                    ";

                    using (var cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@UserID", userId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                orders.Add(new OrderDto(
                                    (int)reader.GetInt64(0),
                                    (int)reader.GetInt64(1),
                                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    reader.GetDateTime(5),
                                    reader.GetDecimal(6),
                                    reader.GetDecimal(7),
                                    reader.GetDecimal(8),
                                    reader.GetInt32(9),
                                    reader.GetInt32(10),
                                    reader.GetDateTime(11),
                                    reader.IsDBNull(12) ? "" : reader.GetString(12),
                                    reader.IsDBNull(13) ? null : (int?)reader.GetByte(13),
                                    reader.IsDBNull(14) ? null : reader.GetString(14),
                                    reader.IsDBNull(15) ? null : reader.GetString(15),
                                    reader.GetInt32(16)
                                ));
                            }
                        }
                    }
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to load orders: " + ex.Message);
            }
        }

        // GET /api/data/getprofessionalorders?professionalId=1
        [HttpGet("getprofessionalorders")]
        public IActionResult GetProfessionalOrders([FromQuery] int professionalId)
        {
            try
            {
                var orders = new List<OrderDto>();
                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    var query = @"
                        SELECT
                          o.OrderID,
                          o.UserID,
                          u.FullName AS CustomerName,
                          p.CompanyName,
                          c.CategoryName,
                          o.ScheduledStart,
                          o.Subtotal,
                          o.TaxAmount,
                          o.DiscountAmount,
                          o.PaymentStatus,
                          o.OrderStatus,
                          o.Created_At,
                          CONCAT_WS(', ',
                            NULLIF(LTRIM(RTRIM(
                              o.ServiceAddress1 + CASE WHEN NULLIF(o.ServiceAddress2,'') IS NOT NULL THEN ' ' + o.ServiceAddress2 ELSE '' END
                            )), ''),
                            NULLIF(LTRIM(RTRIM(o.City)), ''),
                            NULLIF(LTRIM(RTRIM(CONCAT(o.State, CASE WHEN NULLIF(o.PostalCode,'') IS NOT NULL THEN ' ' + o.PostalCode ELSE '' END))), '')
                          ) AS FullAddress,r.Rating,R.ReviewText,convert(VARCHAR, r.CreatedAT, 0) AS reviewdate,p.ProfessionalID
                        FROM dbo.Orders o
                        LEFT JOIN dbo.Users         u ON u.UserID = o.UserID
                        LEFT JOIN dbo.Professionals p ON p.ProfessionalID = o.ProfessionalID
                        LEFT JOIN dbo.Categories    c ON c.CategoryID = o.CategoryID
                        LEFT JOIN dbo.Reviews    r ON r.OrderID = o.OrderID
                        WHERE p.ProfessionalID = @ProfessionalID
                        ORDER BY o.Created_At DESC;
                    ";

                    using (var cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@ProfessionalID", professionalId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                orders.Add(new OrderDto(
                                    (int)reader.GetInt64(0),
                                    (int)reader.GetInt64(1),
                                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    reader.GetDateTime(5),
                                    reader.GetDecimal(6),
                                    reader.GetDecimal(7),
                                    reader.GetDecimal(8),
                                    reader.GetInt32(9),
                                    reader.GetInt32(10),
                                    reader.GetDateTime(11),
                                    reader.IsDBNull(12) ? "" : reader.GetString(12),
                                    reader.IsDBNull(13) ? null : (int?)reader.GetByte(13),
                                    reader.IsDBNull(14) ? null : reader.GetString(14),
                                    reader.IsDBNull(15) ? null : reader.GetString(15),
                                    reader.GetInt32(16)
                                ));
                            }
                        }
                    }
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to load professional orders: " + ex.Message);
            }
        }

        public record CategoryDto(int Id, string Name, string? ImagePath);
        public record OrderDto(
            int OrderID,
            int UserID,
            string CustomerName,
            string CompanyName,
            string CategoryName,
            DateTime ScheduledStart,
            decimal Subtotal,
            decimal TaxAmount,
            decimal DiscountAmount,
            int PaymentStatus,
            int OrderStatus,
            DateTime CreatedAt,
            string FullAddress,
            int? Rating,
            string? ReviewText,
            string? ReviewDate,
            int ProfessionalID
        );
        public record RegisterRequest(string FullName, string Email, string Phone, string PasswordHash, string? Confirm);
        public record LoginRequest(string Email, string PasswordHash);
        public record PendingRegistration(string FullName, string Email, string Phone, byte[] PasswordHash, string Otp, DateTime Expiry);
        public record VerifyOtpRequest(string Email, string Otp);
        public record ProfessionalRegisterRequest(string CompanyName, string Email, string Phone, string Address1, string Address2, string City, string State, string PostalCode, string PasswordHash);
        public record ProfessionalLoginRequest(string Email, string Password);
        public record ProfessionalEnquiryRequest(string FullName, string CompanyName, string Email, string Phone, string Address1, string City, string State, string PostalCode);

        // POST /api/data/professional-enquiry
        [HttpPost("professional-enquiry")]
        public IActionResult ProfessionalEnquiry([FromBody] ProfessionalEnquiryRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.FullName) ||
                    string.IsNullOrWhiteSpace(request.CompanyName) ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Phone) ||
                    string.IsNullOrWhiteSpace(request.Address1) ||
                    string.IsNullOrWhiteSpace(request.City) ||
                    string.IsNullOrWhiteSpace(request.State) ||
                    string.IsNullOrWhiteSpace(request.PostalCode))
                {
                    return BadRequest(new { error = "All fields are required." });
                }

                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    using (var insertCmd = new SqlCommand(
                        "INSERT INTO dbo.ProfessionalsEnquiry (FullName, CompanyName, Email, Phone, Address1, City, State, PostalCode) VALUES (@FullName, @CompanyName, @Email, @Phone, @Address1, @City, @State, @PostalCode)",
                        con))
                    {
                        insertCmd.Parameters.AddWithValue("@FullName", request.FullName);
                        insertCmd.Parameters.AddWithValue("@CompanyName", request.CompanyName);
                        insertCmd.Parameters.AddWithValue("@Email", request.Email);
                        insertCmd.Parameters.AddWithValue("@Phone", request.Phone);
                        insertCmd.Parameters.AddWithValue("@Address1", request.Address1);
                        insertCmd.Parameters.AddWithValue("@City", request.City);
                        insertCmd.Parameters.AddWithValue("@State", request.State);
                        insertCmd.Parameters.AddWithValue("@PostalCode", request.PostalCode);

                        insertCmd.ExecuteNonQuery();
                    }
                }

                return Ok(new { message = "Our team will contact you back." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Enquiry submission failed: " + ex.Message);
            }
        }

        // POST /api/data/professional-login
        [HttpPost("professional-login")]
        public IActionResult ProfessionalLogin([FromBody] ProfessionalLoginRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.Email) ||  
                    string.IsNullOrWhiteSpace(request.Password)) 
                {
                    return BadRequest("Email and password are required.");
                }

                Console.WriteLine("Professional login attempt for email: " + request.Email);


                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    // Get professional by email and password hash
                    using (var cmd = new SqlCommand(
                        "SELECT ProfessionalID, CompanyName, Email, Phone FROM dbo.Professionals WHERE Email = @Email AND Password = @Password",
                        con))
                    {
                        cmd.Parameters.AddWithValue("@Email", request.Email);
                        cmd.Parameters.AddWithValue("@Password", request.Password);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var professional = new
                                {
                                    ProfessionalID = reader.GetInt32(0),
                                    CompanyName = reader.GetString(1),
                                    Email = reader.GetString(2),
                                    Phone = reader.GetString(3)
                                };

                                Console.WriteLine("Professional found: " + professional.ProfessionalID + ", " + professional.CompanyName);
                                return Ok(new { success = true, professional = professional });
                            }
                            else
                            {
                                Console.WriteLine("No professional found for email: " + request.Email);
                                return BadRequest("Invalid email or password.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Professional login error: " + ex.Message);
                return StatusCode(500, "Login failed: " + ex.Message);
            }
        }

        // GET /api/data/reviews?professionalId=1
        [HttpGet("reviews")]
        public IActionResult GetReviews([FromQuery] int professionalId)
        {
            try
            {
                var reviews = new List<ReviewDto>();
                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    var query = @"
                        SELECT u.FullName,U.Email,r.Rating,R.ReviewText,R.IsVerified,
                        convert(VARCHAR, r.CreatedAT, 0) AS reviewdate FROM Reviews r
                        INNER JOIN users u ON u.userid = r.customerID WHERE professionalID =@ProfessionalID
                         ORDER BY reviewdate desc
                    ";

                    using (var cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@ProfessionalID", professionalId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                reviews.Add(new ReviewDto(
                                    reader.GetString(0),
                                    reader.GetString(1),
                                    (int)reader.GetByte(2),
                                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    reader.GetBoolean(4),
                                    reader.GetString(5)
                                ));
                            }
                        }
                    }
                }

                return Ok(reviews);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to load reviews: " + ex.Message);
            }
        }

        public record ProfessionalDto(int ProfessionalID, string CompanyName, string Email, string Phone, string Address1, string Address2, string City, string State, string PostalCode, decimal Ratings);
        public record ServiceDto(int ServiceID, int ProfessionalID, int CategoryID, string ServiceName, string Title, decimal Price, int? EstimatedHours, string Description, bool IsActive);
        public record ReviewDto(string FullName, string Email, int Rating, string ReviewText, bool IsVerified, string ReviewDate);
        public record UserProfileDto(int UserID, string FullName, string Email, string Phone);

        // POST /api/data/updateorderstatus
        [HttpPost("updateorderstatus")]
        public IActionResult UpdateOrderStatus([FromBody] UpdateOrderStatusRequest request)
        {
            try
            {
                if (request == null || request.OrderID <= 0 || request.OrderStatus < 0 || request.OrderStatus > 2)
                {
                    return BadRequest(new { error = "Invalid order status update data." });
                }

                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.Orders SET OrderStatus = @OrderStatus WHERE OrderID = @OrderID",
                        con))
                    {
                        cmd.Parameters.AddWithValue("@OrderID", request.OrderID);
                        cmd.Parameters.AddWithValue("@OrderStatus", request.OrderStatus);

                        var rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            return NotFound("Order not found.");
                        }

                        return Ok(new { message = "Order status updated successfully." });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to update order status: " + ex.Message);
            }
        }

        public record UpdateOrderStatusRequest(int OrderID, int OrderStatus);

        // POST /api/data/submitreview
        [HttpPost("submitreview")]
        public IActionResult SubmitReview([FromBody] SubmitReviewRequest request)
        {
            try
            {
                Console.WriteLine("SubmitReview request: " + System.Text.Json.JsonSerializer.Serialize(request));
                Console.WriteLine($"CustomerID: {request?.CustomerID}, OrderID: {request?.OrderID}, ProfessionalID: {request?.ProfessionalID}, Rating: {request?.Rating}, ReviewText: '{request?.ReviewText}'");

                if (request == null ||
                    request.CustomerID <= 0 ||
                    request.OrderID <= 0 ||
                    request.ProfessionalID <= 0 ||
                    request.Rating == null ||
                    string.IsNullOrWhiteSpace(request.ReviewText))
                {
                    return BadRequest(new { error = "Invalid review data. All fields are required." });
                }

                if (request.Rating < 1 || request.Rating > 5)
                {
                    return BadRequest(new { error = "Rating must be a number between 1 and 5." });
                }

                using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();

                    using (var cmd = new SqlCommand(
                        @"INSERT INTO dbo.Reviews (CustomerID, OrderID, ProfessionalID, Rating, ReviewText, IsVerified, IsPublic, CreatedAT)
                        VALUES (@CustomerID, @OrderID, @ProfessionalID, @Rating, @ReviewText, @IsVerified, @IsPublic, @CreatedAT)",
                        con))
                    {
                        cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                        cmd.Parameters.AddWithValue("@OrderID", request.OrderID);
                        cmd.Parameters.AddWithValue("@ProfessionalID", request.ProfessionalID);
                        cmd.Parameters.AddWithValue("@Rating", request.Rating);
                        cmd.Parameters.AddWithValue("@ReviewText", request.ReviewText);
                        cmd.Parameters.AddWithValue("@IsVerified", request.IsVerified ?? true);
                        cmd.Parameters.AddWithValue("@IsPublic", request.IsPublic ?? true);
                        cmd.Parameters.AddWithValue("@CreatedAT", DateTime.UtcNow);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new { message = "Review submitted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to submit review: " + ex.Message);
            }
        }

        public record SubmitReviewRequest(int? CustomerID, int? OrderID, int? ProfessionalID, int? Rating, string? ReviewText, bool? IsVerified, bool? IsPublic);
    }
}
