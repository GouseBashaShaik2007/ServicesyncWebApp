
-- Run this in your SQL Server instance (ServiceSyncDb)
IF DB_ID('ServiceSyncDb') IS NULL
BEGIN
    CREATE DATABASE ServiceSyncDb;
END
GO

USE ServiceSyncDb;
GO

IF OBJECT_ID('dbo.Categories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories (
        CategoryID  INT IDENTITY(1,1) PRIMARY KEY,
        CategoryName NVARCHAR(200) NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
BEGIN
    INSERT INTO dbo.Categories (CategoryName) VALUES
    ('Plumbing'),
    ('Electrical'),
    ('Cleaning'),
    ('Lawn'),
    ('PTAC');
END
GO

IF OBJECT_ID('dbo.Professionals', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Professionals (
        ProfessionalID   INT IDENTITY(1,1) PRIMARY KEY,
        FullName         NVARCHAR(200)   NOT NULL,
        CompanyName      NVARCHAR(200)   NOT NULL,
        Email            NVARCHAR(254)   NOT NULL UNIQUE,
        Phone            NVARCHAR(30)    NULL,
        Address1         NVARCHAR(200)   NULL,
        Address2         NVARCHAR(200)   NULL,
        City             NVARCHAR(100)   NULL,
        State            NVARCHAR(100)   NULL,
        PostalCode       NVARCHAR(20)    NULL,
        PasswordHash     VARBINARY(MAX) NOT NULL,
        PasswordSalt     VARBINARY(MAX) NOT NULL,
        Iterations       INT             NOT NULL DEFAULT 100000,
        CreatedAt        DATETIME2(0)    NOT NULL CONSTRAINT DF_Professionals_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2(0)    NULL,
        IsActive         BIT             NOT NULL CONSTRAINT DF_Professionals_IsActive DEFAULT 1
    );
END
GO

IF OBJECT_ID('dbo.ProfessionalsEnquiry', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfessionalsEnquiry (
        ProfessionalID   INT IDENTITY(1,1) PRIMARY KEY,
        FullName         NVARCHAR(200)   NOT NULL,
        CompanyName      NVARCHAR(200)   NOT NULL,
        Email            NVARCHAR(254)   NULL,
        Phone            NVARCHAR(30)    NULL,
        Address1         NVARCHAR(200)   NULL,
        Address2         NVARCHAR(200)   NULL,
        City             NVARCHAR(100)   NULL,
        State            NVARCHAR(100)   NULL,
        PostalCode       NVARCHAR(20)    NULL,
        CreatedAt        DATETIME2(0)    NOT NULL CONSTRAINT DF_ProfessionalsEnquiry_CreatedAt DEFAULT SYSUTCDATETIME(),
        IsActive         BIT             NOT NULL CONSTRAINT DF_ProfessionalsEnquiry_IsActive DEFAULT 1
    );
END
GO
