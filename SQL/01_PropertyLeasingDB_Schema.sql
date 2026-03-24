-- =============================================
-- Property Leasing & Maintenance Platform
-- Database Schema Script
-- Brief B - IT8118 Advanced Programming 2025-2026
-- =============================================

USE master;
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = 'PropertyLeasingDB')
    DROP DATABASE PropertyLeasingDB;
GO

CREATE DATABASE PropertyLeasingDB;
GO

USE PropertyLeasingDB;
GO

-- =============================================
-- Table: Property (Building/Location)
-- =============================================
CREATE TABLE Property (
    PropertyID      INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(250) NULL,
    Address         NVARCHAR(200) NOT NULL,
    City            NVARCHAR(50)  NULL,
    PropertyType    NVARCHAR(50)  NULL,  -- Residential / Commercial
    ImgPath         NVARCHAR(100) NULL
);

-- =============================================
-- Table: Unit (Apartment/Office inside a Property)
-- =============================================
CREATE TABLE Unit (
    UnitID              INT IDENTITY(1,1) PRIMARY KEY,
    PropertyID          INT NOT NULL,
    UnitNumber          NVARCHAR(50)   NOT NULL,
    UnitType            NVARCHAR(50)   NULL,   -- Apartment / Studio / Office / Shop
    Sizesqm             FLOAT          NULL,
    MonthlyRent         DECIMAL(10,2)  NULL,
    Amenities           NVARCHAR(250)  NULL,
    AvailabilityStatus  NVARCHAR(50)   NULL DEFAULT 'Available', -- Available / Occupied / UnderMaintenance
    ImgPath             NVARCHAR(100)  NULL,
    CONSTRAINT FK_Unit_Property FOREIGN KEY (PropertyID) REFERENCES Property(PropertyID)
);

-- =============================================
-- Table: [User] (Tenant / PropertyManager / MaintenanceStaff)
-- =============================================
CREATE TABLE [User] (
    UserID              INT IDENTITY(1,1) PRIMARY KEY,
    FullName            NVARCHAR(100) NOT NULL,
    Email               NVARCHAR(100) NOT NULL UNIQUE,
    Phone               NVARCHAR(20)  NULL,
    [Role]              NVARCHAR(50)  NOT NULL DEFAULT 'Tenant', -- Tenant / PropertyManager / MaintenanceStaff
    SkillProfile        NVARCHAR(200) NULL,   -- For MaintenanceStaff
    AvailabilityStatus  NVARCHAR(50)  NULL,   -- Available / Busy / OffDuty (MaintenanceStaff)
    IdentityUserId      NVARCHAR(450) NULL    -- Links to ASP.NET Identity
);

-- =============================================
-- Table: LeaseApplication (Tenant applies for a Unit)
-- =============================================
CREATE TABLE LeaseApplication (
    ApplicationID       INT IDENTITY(1,1) PRIMARY KEY,
    UserID              INT           NOT NULL,
    UnitID              INT           NOT NULL,
    RequestedStartDate  DATETIME      NULL,
    RequestedEndDate    DATETIME      NULL,
    Notes               NVARCHAR(500) NULL,
    Status              NVARCHAR(50)  NOT NULL DEFAULT 'Pending', -- Pending / Screening / Approved / Rejected
    CreatedAt           DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_LeaseApplication_User FOREIGN KEY (UserID) REFERENCES [User](UserID),
    CONSTRAINT FK_LeaseApplication_Unit FOREIGN KEY (UnitID) REFERENCES Unit(UnitID)
);

-- =============================================
-- Table: Lease (Active lease after approval)
-- =============================================
CREATE TABLE Lease (
    LeaseID         INT IDENTITY(1,1) PRIMARY KEY,
    ApplicationID   INT           NOT NULL,
    LeaseStartDate  DATETIME      NOT NULL,
    LeaseEndDate    DATETIME      NOT NULL,
    MonthlyRent     DECIMAL(10,2) NOT NULL,
    SecurityDeposit DECIMAL(10,2) NOT NULL,
    Status          NVARCHAR(50)  NOT NULL DEFAULT 'Active', -- Active / Expired / Terminated / Renewed
    CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Lease_LeaseApplication FOREIGN KEY (ApplicationID) REFERENCES LeaseApplication(ApplicationID)
);

-- =============================================
-- Table: PaymentRecord (Monthly rent payments)
-- =============================================
CREATE TABLE PaymentRecord (
    PaymentID       INT IDENTITY(1,1) PRIMARY KEY,
    LeaseID         INT           NOT NULL,
    AmountDue       DECIMAL(10,2) NOT NULL,
    AmountPaid      DECIMAL(10,2) NULL,
    DueDate         DATETIME      NOT NULL,
    PaidDate        DATETIME      NULL,
    PaymentStatus   NVARCHAR(50)  NOT NULL DEFAULT 'Pending', -- Paid / Pending / Overdue / PartiallyPaid
    Notes           NVARCHAR(250) NULL,
    CONSTRAINT FK_PaymentRecord_Lease FOREIGN KEY (LeaseID) REFERENCES Lease(LeaseID)
);

-- =============================================
-- Table: MaintenanceRequest
-- =============================================
CREATE TABLE MaintenanceRequest (
    RequestID       INT IDENTITY(1,1) PRIMARY KEY,
    UnitID          INT           NOT NULL,
    TenantUserID    INT           NOT NULL,
    AssignedStaffID INT           NULL,
    Title           NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    RequestType     NVARCHAR(50)  NULL,   -- Plumbing / Electrical / HVAC / General
    Priority        NVARCHAR(50)  NOT NULL DEFAULT 'Medium', -- Low / Medium / High / Urgent
    Status          NVARCHAR(50)  NOT NULL DEFAULT 'Submitted', -- Submitted / Assigned / InProgress / Resolved / Closed
    TicketNumber    NVARCHAR(20)  NULL UNIQUE,
    SubmittedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
    ResolvedAt      DATETIME      NULL,
    ResolutionNotes NVARCHAR(500) NULL,
    CONSTRAINT FK_MaintenanceRequest_Unit    FOREIGN KEY (UnitID)          REFERENCES Unit(UnitID),
    CONSTRAINT FK_MaintenanceRequest_Tenant  FOREIGN KEY (TenantUserID)    REFERENCES [User](UserID),
    CONSTRAINT FK_MaintenanceRequest_Staff   FOREIGN KEY (AssignedStaffID) REFERENCES [User](UserID)
);

-- =============================================
-- Table: MaintenanceStatusHistory
-- =============================================
CREATE TABLE MaintenanceStatusHistory (
    HistoryID           INT IDENTITY(1,1) PRIMARY KEY,
    RequestID           INT           NOT NULL,
    OldStatus           NVARCHAR(50)  NULL,
    NewStatus           NVARCHAR(50)  NOT NULL,
    Notes               NVARCHAR(250) NULL,
    ChangedAt           DATETIME      NOT NULL DEFAULT GETDATE(),
    ChangedByUserID     INT           NULL,
    CONSTRAINT FK_StatusHistory_MaintenanceRequest FOREIGN KEY (RequestID) REFERENCES MaintenanceRequest(RequestID)
);

-- =============================================
-- Table: Notification
-- =============================================
CREATE TABLE Notification (
    NotificationID      INT IDENTITY(1,1) PRIMARY KEY,
    UserID              INT           NOT NULL,
    Message             NVARCHAR(500) NOT NULL,
    NotificationType    NVARCHAR(50)  NULL, -- LeaseUpdate / MaintenanceUpdate / PaymentReminder / General
    Status              NVARCHAR(20)  NOT NULL DEFAULT 'Unread', -- Read / Unread
    CreatedAt           DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Notification_User FOREIGN KEY (UserID) REFERENCES [User](UserID)
);

-- =============================================
-- Table: Feedback
-- =============================================
CREATE TABLE Feedback (
    FeedbackID  INT IDENTITY(1,1) PRIMARY KEY,
    UserID      INT           NOT NULL,
    UnitID      INT           NOT NULL,
    Rating      INT           NULL CHECK (Rating BETWEEN 1 AND 5),
    Comment     NVARCHAR(500) NULL,
    IsVisible   BIT           NOT NULL DEFAULT 1,
    CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Feedback_User FOREIGN KEY (UserID) REFERENCES [User](UserID),
    CONSTRAINT FK_Feedback_Unit FOREIGN KEY (UnitID) REFERENCES Unit(UnitID)
);

-- =============================================
-- Table: Log (Audit trail)
-- =============================================
CREATE TABLE Log (
    LogID       INT IDENTITY(1,1) PRIMARY KEY,
    UserID      INT           NULL,
    Action      NVARCHAR(100) NULL,
    Details     NVARCHAR(500) NULL,
    LogLevel    NVARCHAR(20)  NULL, -- Info / Warning / Error
    Source      NVARCHAR(20)  NULL, -- Web / API
    CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Log_User FOREIGN KEY (UserID) REFERENCES [User](UserID)
);

-- =============================================
-- Table: Document
-- =============================================
CREATE TABLE Document (
    DocumentID      INT IDENTITY(1,1) PRIMARY KEY,
    UserID          INT           NULL,
    ApplicationID   INT           NULL,
    FileName        NVARCHAR(200) NOT NULL,
    FileType        NVARCHAR(50)  NULL,
    StoragePath     NVARCHAR(500) NULL,
    UploadedAt      DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Document_User        FOREIGN KEY (UserID)        REFERENCES [User](UserID),
    CONSTRAINT FK_Document_Application FOREIGN KEY (ApplicationID) REFERENCES LeaseApplication(ApplicationID)
);

PRINT 'PropertyLeasingDB schema created successfully!';
GO
