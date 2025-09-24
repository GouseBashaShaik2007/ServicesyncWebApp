-- Update Users table to add OTP fields for email verification
USE ServiceSyncDb;
GO

-- Add OTP columns if they don't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'OtpCode')
BEGIN
    ALTER TABLE dbo.Users ADD OtpCode NVARCHAR(10) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'OtpExpiry')
BEGIN
    ALTER TABLE dbo.Users ADD OtpExpiry DATETIME NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'IsVerified')
BEGIN
    ALTER TABLE dbo.Users ADD IsVerified BIT DEFAULT 0;
END
GO
