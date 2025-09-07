
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
