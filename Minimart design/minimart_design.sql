USE Minimart

-- Categories table
CREATE TABLE Categories (
    CategoryID INT IDENTITY(1,1),
    CategoryName NVARCHAR(255) NOT NULL UNIQUE,
    CategoryDescription NVARCHAR(MAX) NULL,
    CONSTRAINT PK_Categories PRIMARY KEY (CategoryID)
);

-- Suppliers table
CREATE TABLE Suppliers (
    SupplierID INT IDENTITY(1,1),
    SupplierName NVARCHAR(255) NOT NULL UNIQUE,
    SupplierPhoneNumber CHAR(10) NOT NULL UNIQUE,
    SupplierAddress NVARCHAR(255) NOT NULL,
    SupplierEmail NVARCHAR(255) NOT NULL UNIQUE,
    CONSTRAINT PK_Suppliers PRIMARY KEY (SupplierID)
);

-- Measurement units table
CREATE TABLE MeasurementUnits (
    MeasurementUnitID INT IDENTITY(1,1),
    UnitName NVARCHAR(50) NOT NULL UNIQUE,
    UnitDescription NVARCHAR(MAX) NULL,
    IsContinuous BIT NOT NULL,  -- 1: continuous like kg, 0: discrete like pieces
    CONSTRAINT PK_MeasurementUnits PRIMARY KEY (MeasurementUnitID)
);

-- Product types (catalog)
CREATE TABLE ProductTypes (
    ProductTypeID INT IDENTITY(1,1),
    ProductName NVARCHAR(255) NOT NULL UNIQUE,
    ProductDescription NVARCHAR(MAX) NOT NULL,
    CategoryID INT NOT NULL,
    SupplierID INT NOT NULL,
    Price DECIMAL(10,2) NOT NULL CHECK (Price >= 0),
    StockAmount DECIMAL(10,2) NOT NULL DEFAULT 0 CHECK (StockAmount >= 0),
    MeasurementUnitID INT NOT NULL,
    ExpirationDurationDays INT NULL CHECK (ExpirationDurationDays >= 0),
    IsActive BIT NOT NULL DEFAULT 1,
    DateAdded DATETIME NOT NULL DEFAULT GETDATE(),
    ImagePath NVARCHAR(512) NULL,
    CONSTRAINT PK_ProductTypes PRIMARY KEY (ProductTypeID),
    CONSTRAINT FK_ProductTypes_Category FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID) ON DELETE CASCADE,
    CONSTRAINT FK_ProductTypes_Supplier FOREIGN KEY (SupplierID) REFERENCES Suppliers(SupplierID) ON DELETE CASCADE,
    CONSTRAINT FK_ProductTypes_MeasurementUnit FOREIGN KEY (MeasurementUnitID) REFERENCES MeasurementUnits(MeasurementUnitID) ON DELETE CASCADE,
    CONSTRAINT CK_ProductTypes_Price CHECK (Price >= 0),
    CONSTRAINT CK_ProductTypes_StockAmount CHECK (StockAmount >= 0)
);

CREATE TABLE Tags (
    TagID INT IDENTITY(1,1) PRIMARY KEY,
    TagName NVARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE ProductTags (
    ProductTagID INT IDENTITY(1,1) PRIMARY KEY,
    ProductTypeID INT NOT NULL,
    TagID INT NOT NULL,
    CONSTRAINT FK_ProductTags_ProductType FOREIGN KEY (ProductTypeID) REFERENCES ProductTypes(ProductTypeID) ON DELETE CASCADE,
    CONSTRAINT FK_ProductTags_Tag FOREIGN KEY (TagID) REFERENCES Tags(TagID) ON DELETE CASCADE,
    CONSTRAINT UQ_ProductTags_ProductType_Tag UNIQUE (ProductTypeID, TagID)
);

-- Customers
CREATE TABLE Customers (
    CustomerID INT IDENTITY(1,1),
    FirstName NVARCHAR(255) NOT NULL,
    LastName NVARCHAR(255) NOT NULL,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PhoneNumber CHAR(10) NOT NULL UNIQUE,
    ImagePath NVARCHAR(512) NULL,
    Username NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash VARBINARY(64) NOT NULL,
    Salt VARBINARY(64) NOT NULL,
	IsEmailVerified BIT NOT NULL DEFAULT 0,
    EmailVerifiedAt DATETIME2 NULL,
	Is2FAEnabled BIT NOT NULL DEFAULT 0
    CONSTRAINT PK_Customers PRIMARY KEY (CustomerID),
    CONSTRAINT UQ_Customers_Email UNIQUE (Email),
    CONSTRAINT UQ_Customers_PhoneNumber UNIQUE (PhoneNumber),
    CONSTRAINT UQ_Customers_Username UNIQUE (Username)
);

-- Employee roles
CREATE TABLE EmployeeRoles (
    RoleID INT IDENTITY(1,1),
    RoleName NVARCHAR(255) NOT NULL UNIQUE,
    RoleDescription NVARCHAR(MAX) NULL,
    CONSTRAINT PK_EmployeeRoles PRIMARY KEY (RoleID)
);

-- Employees table
CREATE TABLE Employees (
    EmployeeID INT IDENTITY(1,1),
    FirstName NVARCHAR(255) NOT NULL,
    LastName NVARCHAR(255) NOT NULL,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PhoneNumber CHAR(10) NOT NULL UNIQUE,
    Gender NVARCHAR(20) NOT NULL CHECK (Gender IN (N'Nam', N'Nữ', N'Khác')),
    BirthDate DATE NOT NULL,
    CitizenID NVARCHAR(100) NOT NULL UNIQUE,
    Salary DECIMAL(10,2) NULL CHECK (Salary >= 0),
    HireDate DATETIME NOT NULL DEFAULT GETDATE(),
    RoleID INT NOT NULL,
    ImagePath NVARCHAR(512) NULL,
    CONSTRAINT PK_Employees PRIMARY KEY (EmployeeID),
    CONSTRAINT FK_Employees_Role FOREIGN KEY (RoleID) REFERENCES EmployeeRoles(RoleID) ON DELETE CASCADE,
    CONSTRAINT UQ_Employees_Email UNIQUE (Email),
    CONSTRAINT UQ_Employees_PhoneNumber UNIQUE (PhoneNumber),
    CONSTRAINT UQ_Employees_CitizenID UNIQUE (CitizenID)
);

-- EmployeeAccounts table
CREATE TABLE EmployeeAccounts (
    AccountID INT IDENTITY(1,1),
    EmployeeID INT NOT NULL UNIQUE,
    Username NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash VARBINARY(64) NOT NULL,
    Salt VARBINARY(64) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    LastLogin DATETIME NULL,
    IsActive BIT DEFAULT 1,
	IsAdmin BIT DEFAULT 0,
	IsEmailVerified BIT NOT NULL DEFAULT 0,
    EmailVerifiedAt DATETIME2 NULL,
	Is2FAEnabled BIT NOT NULL DEFAULT 0,
    CONSTRAINT PK_Admins PRIMARY KEY (AccountID),
    CONSTRAINT FK_Admins_Employee FOREIGN KEY (EmployeeID) REFERENCES Employees(EmployeeID) ON DELETE CASCADE,
    CONSTRAINT UQ_Admins_Username UNIQUE (Username)
);

-- Payment methods
CREATE TABLE PaymentMethods (
    PaymentMethodID INT IDENTITY(1,1),
    MethodName NVARCHAR(50) NOT NULL UNIQUE,
    CONSTRAINT PK_PaymentMethods PRIMARY KEY (PaymentMethodID)
);

-- Sales table
CREATE TABLE Sales (
    SaleID INT IDENTITY(1,1),
    SaleDate DATETIME NOT NULL DEFAULT GETDATE(),
    CustomerID INT NOT NULL,
    EmployeeID INT NOT NULL,
    PaymentMethodID INT NOT NULL,
    DeliveryAddress NVARCHAR(255) NOT NULL,
    DeliveryTime DATETIME NOT NULL,
    IsPickup BIT NOT NULL DEFAULT 0,
    OrderStatus NVARCHAR(50) NOT NULL DEFAULT N'Chờ xử lý',
    CONSTRAINT PK_Sales PRIMARY KEY (SaleID),
    CONSTRAINT FK_Sales_Customer FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID) ON DELETE CASCADE,
    CONSTRAINT FK_Sales_Employee FOREIGN KEY (EmployeeID) REFERENCES Employees(EmployeeID) ON DELETE CASCADE,
    CONSTRAINT FK_Sales_PaymentMethod FOREIGN KEY (PaymentMethodID) REFERENCES PaymentMethods(PaymentMethodID) ON DELETE CASCADE,
	CONSTRAINT CK_Sales_OrderStatus CHECK (OrderStatus IN (N'Chờ xử lý', N'Đã xác nhận', N'Đang xử lý', N'Hoàn thành', N'Đã hủy', N'Bị từ chối'))
);

-- Sale details (line items)
CREATE TABLE SaleDetails (
    SaleDetailID INT IDENTITY(1,1),
    SaleID INT NOT NULL,
    ProductTypeID INT NOT NULL,
    Quantity DECIMAL(10,2) NOT NULL CHECK (Quantity > 0),
    ProductPriceAtPurchase DECIMAL(10,2) NOT NULL CHECK (ProductPriceAtPurchase >= 0),
    CONSTRAINT PK_SaleDetails PRIMARY KEY (SaleDetailID),
    CONSTRAINT FK_SaleDetails_Sale FOREIGN KEY (SaleID) REFERENCES Sales(SaleID) ON DELETE CASCADE,
    CONSTRAINT FK_SaleDetails_ProductType FOREIGN KEY (ProductTypeID) REFERENCES ProductTypes(ProductTypeID) ON DELETE CASCADE,
    CONSTRAINT CK_SaleDetails_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_SaleDetails_ProductPriceAtPurchase CHECK (ProductPriceAtPurchase >= 0)
);

CREATE TABLE Notifications (
    NotificationID INT IDENTITY(1,1) PRIMARY KEY,
    
    -- Mutually exclusive columns
    CustomerID INT NULL,
    EmployeeAccountID INT NULL,
    
    SaleID INT NULL, -- Foreign key to Sales, nullable if not linked to a sale
    Title NVARCHAR(255) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    IsRead BIT NOT NULL DEFAULT 0,
    NotificationType NVARCHAR(50) NOT NULL,
    
    -- Foreign Key Constraints
    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID) ON DELETE CASCADE,
    FOREIGN KEY (EmployeeAccountID) REFERENCES EmployeeAccounts(AccountID) ON DELETE CASCADE,
    FOREIGN KEY (SaleID) REFERENCES Sales(SaleID),

    -- 🔥 Mutually exclusive constraint:
    CONSTRAINT CHK_Notifications_Exclusivity CHECK (
        (CustomerID IS NOT NULL AND EmployeeAccountID IS NULL) OR 
        (CustomerID IS NULL AND EmployeeAccountID IS NOT NULL)
    ),
    
    -- 🔥 Restrict NotificationType to specific values
    CONSTRAINT CHK_Notifications_Type CHECK (NotificationType IN (
        'Account Related', 
        'Order Status Update', 
        'Security Alert', 
        'Promotion', 
        'System Message'
    ))
);


-- Drop the trigger if it exists
IF OBJECT_ID('dbo.trg_AfterDelete_Sale_Update_Notifications', 'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_AfterDelete_Sale_Update_Notifications;
GO

-- Create the trigger
CREATE TRIGGER trg_AfterDelete_Sale_Update_Notifications
ON Sales
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Nullify the SaleID in Notifications if the sale is deleted
    UPDATE n
    SET n.SaleID = NULL
    FROM Notifications n
    INNER JOIN deleted d ON n.SaleID = d.SaleID;
END;
GO


-- OtpTypes table
CREATE TABLE OtpTypes (
    OtpTypeID INT IDENTITY(1,1) PRIMARY KEY,  -- Unique identifier for OTP type with auto-increment
    OtpTypeName VARCHAR(255) UNIQUE NOT NULL,        -- Name of the OTP type (e.g., "Email OTP", "SMS OTP")
    Description VARCHAR(255)                  -- Optional description of the OTP type
);

-- OtpRequests table
CREATE TABLE OtpRequests (
    OtpRequestID INT IDENTITY(1,1) PRIMARY KEY,         -- Unique identifier for OTP request
    CustomerID INT NULL,                                -- Nullable foreign key to Customers
    EmployeeAccountID INT NULL,                         -- Nullable foreign key to EmployeeAccounts
    OtpTypeID INT NOT NULL,                             -- OTP type
    OtpCode VARCHAR(6) NOT NULL,                        -- OTP code
    RequestTime DATETIME DEFAULT GETDATE(),             -- Time of request
    ExpirationTime DATETIME,                            -- Expiration time
    IsUsed BIT DEFAULT 0,                               -- Flag for usage
    Status VARCHAR(50),                                 -- Request status

    CONSTRAINT FK_OtpRequests_Customer 
        FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID) ON DELETE CASCADE,

    CONSTRAINT FK_OtpRequests_EmployeeAccount 
        FOREIGN KEY (EmployeeAccountID) REFERENCES EmployeeAccounts(AccountID) ON DELETE CASCADE,

    CONSTRAINT FK_OtpRequests_OtpTypes 
        FOREIGN KEY (OtpTypeID) REFERENCES OtpTypes(OtpTypeID) ON DELETE CASCADE,

    -- Optional: ensure one of CustomerID or EmployeeAccountID is set, but not both
    CONSTRAINT CK_OtpRequests_OneActorOnly 
        CHECK (
            (CustomerID IS NOT NULL AND EmployeeAccountID IS NULL) OR 
            (CustomerID IS NULL AND EmployeeAccountID IS NOT NULL)
        )
);

-- ViewHistories table (Renamed and added column)
CREATE TABLE ViewHistories (
    ViewHistoryID BIGINT IDENTITY(1,1) PRIMARY KEY,
    CustomerID INT NULL,
    SessionID VARCHAR(255) NULL,
    ProductTypeID INT NOT NULL,
    ViewTimestamp DATETIME2 DEFAULT GETDATE(),
    ViewDurationSeconds INT NULL, -- Added ViewDurationSeconds column
    CONSTRAINT FK_ViewHistories_Customer FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID) ON DELETE SET NULL,
    CONSTRAINT FK_ViewHistories_ProductType FOREIGN KEY (ProductTypeID) REFERENCES ProductTypes(ProductTypeID) ON DELETE CASCADE
);
CREATE INDEX IX_ViewHistories_CustomerID_ProductTypeID ON ViewHistories (CustomerID, ProductTypeID);
CREATE INDEX IX_ViewHistories_SessionID_ProductTypeID ON ViewHistories (SessionID, ProductTypeID);
CREATE INDEX IX_ViewHistories_ProductTypeID_ViewTimestamp ON ViewHistories (ProductTypeID, ViewTimestamp DESC);

-- SearchHistories table
CREATE TABLE SearchHistories (
    SearchHistoryID BIGINT IDENTITY(1,1) PRIMARY KEY,
    CustomerID INT NULL,
    SessionID VARCHAR(255) NULL,
    SearchKeyword NVARCHAR(512) NOT NULL,
    SearchTimestamp DATETIME2 DEFAULT GETDATE(),
    NumberOfResults INT NULL,
    CONSTRAINT FK_SearchHistories_Customer FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID) ON DELETE SET NULL
);
CREATE INDEX IX_SearchHistories_CustomerID ON SearchHistories (CustomerID);
CREATE INDEX IX_SearchHistories_SessionID ON SearchHistories (SessionID);
CREATE INDEX IX_SearchHistories_SearchKeyword ON SearchHistories (SearchKeyword);

CREATE TABLE LoginAttempts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AttemptTime DATETIME NOT NULL,
    IsSuccessful BIT NOT NULL,
    IPAddress NVARCHAR(45) NOT NULL,
    CustomerID INT NULL,
    EmployeeAccountID INT NULL,

    -- Enforce mutual exclusivity: only one of these can be filled
    CONSTRAINT CK_LoginAttempts_OnlyOneUser CHECK (
        (CustomerID IS NOT NULL AND EmployeeAccountID IS NULL) OR 
        (CustomerID IS NULL AND EmployeeAccountID IS NOT NULL)
    ),

    -- Foreign Key Constraints
    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID) ON DELETE CASCADE,
    FOREIGN KEY (EmployeeAccountID) REFERENCES EmployeeAccounts(AccountID) ON DELETE CASCADE
);
