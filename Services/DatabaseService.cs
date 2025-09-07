using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ServicesyncWebApp.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        public DatabaseService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<List<Category>> GetCategoriesAsync()
        {
            var items = new List<Category>();
            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            // Same aliasing so API stays Id/Name even with cateogryid/cateogryname in DB
            using var cmd = new SqlCommand(
                "SELECT [CategoryID] AS Id, [CategoryName] AS Name FROM [dbo].[Categories] ORDER BY [CategoryName];",
                con);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                items.Add(new Category
                {
                    Id = rdr.GetInt32(0),
                    Name = rdr.GetString(1)
                });
            }
            return items;
        }
    }

    public class Category
    {
        public int Id { get; set; }          // exposed as Id (from cateogryid)
        public string Name { get; set; } = ""; // exposed as Name (from cateogryname)
        public string? ImagePath { get; set; }   // iMAGE PAH
    }
}
