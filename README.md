# ServicesyncWebApp (ASP.NET Core + AngularJS in wwwroot)

This is a ready-to-run project that serves your AngularJS app and exposes an API to fetch Categories from your AWS RDS SQL Server.

## How to Run

1. Install **.NET 8 SDK** from https://dotnet.microsoft.com/download
2. Open a terminal in this folder.
3. Restore and run:
   ```bash
   dotnet restore
   dotnet run
   ```
4. Open your browser:
   - Frontend: http://localhost:5000 (served from `wwwroot/`)
   - API:      http://localhost:5000/api/categories

> If `dotnet run` chooses a different port (e.g., 5085), use the port it prints in the console.

## Notes
- Connection string is in `appsettings.json` (uses your AWS RDS values).
- The API currently supports **GET /api/categories**, returning objects like `{ "name": "..." }` from the `Categories` table (column: `Name`).
- Place your AngularJS/HTML/JS/CSS assets under `wwwroot/`. This project already contains the contents of your uploaded zip.

## Troubleshooting
- **Firewall / Security Group**: Ensure your AWS RDS allows inbound traffic from your machine (or VPC/EC2) on port 1433.
- **Encrypt/TrustServerCertificate**: Both are enabled in the connection string.
- **Missing table**: Ensure a table `Categories (Name)` exists and has data.


## API Quick Test
- GET `/api/ping` -> ok
- GET `/api/data/categories?stub=true` -> stub JSON
- GET `/api/data/categories` -> typed JSON from DB
- If 500, GET `/api/data/categories?debug=true` to see the exception.

## Database
- Edit `appsettings.json` or `appsettings.Development.json` with a valid SQL Server connection string.
- Optionally run `db-init.sql` on your SQL Server to create and seed `dbo.Categories`.
