USE Minimart;

-- Categories
INSERT INTO Categories (CategoryName, CategoryDescription) VALUES
(N'Trái Cây', N'Trái cây tươi từ các trang trại địa phương'),
(N'Sữa & Các Sản Phẩm Bơ Sữa', N'Sữa, phô mai và các sản phẩm từ bơ sữa khác'),
(N'Bánh Kẹo', N'Chips, bánh quy và các món ăn vặt đóng gói'),
(N'Rau Củ', N'Rau xanh, lá và rau tươi'),
(N'Đồ Uống', N'Nước trái cây, nước giải khát và nước đóng chai'),
(N'Bánh Mì & Bánh Ngọt', N'Bánh mì, bánh ngọt và các loại bánh khác'),
(N'Thực Phẩm Đông Lạnh', N'Thực phẩm sẵn để ăn và kem đông lạnh'),
(N'Thịt', N'Sản phẩm thịt tươi và chế biến sẵn');

-- Suppliers
INSERT INTO Suppliers (SupplierName, SupplierPhoneNumber, SupplierAddress, SupplierEmail) VALUES
(N'Công Ty FreshFarm', N'0912345678', N'123 Đường Nông Trại', N'contact@freshfarm.com'),
(N'Công Ty DairyDelight', N'0987654321', N'456 Đường Sữa', N'support@dairydelight.com'),
(N'Công Ty SnackWorld', N'0911002233', N'789 Đường Snack', N'info@snackworld.com'),
(N'Công Ty VeggieValley', N'0922003344', N'101 Đường Nhà Kính', N'hello@veggievalley.com'),
(N'Công Ty BeverageHub', N'0933004455', N'202 Quảng Trường Đồ Uống', N'orders@beveragehub.com'),
(N'Công Ty BakeryBliss', N'0944005566', N'303 Đường Bánh Mì', N'sales@bakerybliss.com'),
(N'Công Ty FrozenFeast', N'0955006677', N'404 Đường Đông Lạnh', N'support@frozenfeast.com'),
(N'Công Ty MeatMasters', N'0966007788', N'505 Đường Thịt', N'contact@meatmasters.com');

-- Measurement Units
INSERT INTO MeasurementUnits (UnitName, UnitDescription, IsContinuous) VALUES
(N'Kilogram', N'Dùng cho các sản phẩm tính theo trọng lượng', 1),
(N'Cái', N'Dùng cho các sản phẩm đếm được', 0),
(N'Lít', N'Dùng cho các sản phẩm dạng lỏng', 1),
(N'Gói', N'Dùng cho các sản phẩm đóng gói', 0);

-- ProductTypes
INSERT INTO ProductTypes 
(ProductName, ProductDescription, CategoryID, SupplierID, Price, StockAmount, MeasurementUnitID, ExpirationDurationDays, ImagePath) VALUES
(N'Táo Gala New Zealand (Nhập khẩu)', N'Táo Gala New Zealand, giòn, ngọt, kích cỡ trung bình. Sản phẩm nhập khẩu từ New Zealand, đảm bảo chất lượng và độ tươi.', 1, 1, 87500, 120.5, 1, 25, N'tao_gala.jpg'),
(N'Xoài Cát Chu Đồng Tháp', N'Xoài Cát Chu chín cây, cực kỳ ngọt và thơm. Đặc sản Đồng Tháp, vị ngọt đậm đà.', 1, 1, 62500, 150.0, 1, 5, N'xoai_cat_chu.jpg'),
(N'Sữa Tươi TH True Milk Ít Đường 1L', N'Sữa tươi UHT TH, ít đường, chai 1 lít. Công nghệ UHT hiện đại giữ lại tất cả dưỡng chất.', 3, 2, 42500, 200, 3, 180, N'sua_th_itduong.jpg'),
(N'Phô Mai Cheddar Vinamilk Cắt Lát', N'Phô mai Cheddar Vinamilk, gói 100g (5 lát). Phù hợp làm sandwich hoặc burger.', 3, 2, 37500, 150, 2, 90, N'phomai_lat_cheddar.jpg'),
(N'Bánh Snack Lay''s Stax Vị Kem Chua & Hành', N'Bánh snack Lay''s Stax vị kem chua và hành, lon 100g. Giòn tan, đậm đà hương vị, gây nghiện.', 6, 5, 30000, 300, 2, 150, N'lays_kemchua.jpg'),
(N'Cà Rốt Baby Đà Lạt', N'Cà rốt baby hữu cơ, ngọt, giòn, túi 500g. Sản phẩm đạt tiêu chuẩn VietGAP, an toàn cho sức khỏe.', 2, 1, 25000, 100, 1, 10, N'carot_baby.jpg'),
(N'Nước Cam Vfresh 1L', N'Nước cam nguyên chất Vfresh, không đường, hộp 1 lít. Giàu Vitamin C, tốt cho hệ miễn dịch.', 5, 2, 50000, 180, 3, 270, N'nuoc_cam_vfresh.jpg'),
(N'Bánh Mì Brioche Harrys', N'Bánh mì Brioche Harrys, cỡ lớn. Mềm và thơm, lý tưởng cho bữa sáng nhanh chóng.', 6, 6, 80000, 80, 2, 7, N'banhmi_hoacuc.jpg'),
(N'Ba Rọi Heo Hữu Cơ', N'Ba rọi heo hữu cơ, khay 300g. Thịt ngọt, mềm, đảm bảo an toàn vệ sinh thực phẩm.', 4, 3, 137500, 70, 2, 3, N'thitbaroi_huuco.jpg'),
(N'Cá Hồi Phile Na Uy Tươi', N'Cá hồi Phile Na Uy tươi, giàu Omega-3, cắt lát. Nhập khẩu trực tiếp từ Na Uy, chất lượng cao.', 4, 3, 450000, 30.0, 1, 2, N'cahoi_fillet.jpg'),
(N'Nước Tăng Lực Redbull 250ml', N'Nước tăng lực Redbull, lon 250ml. Tăng cường năng lượng và giúp tập trung.', 5, 4, 30000, 400, 3, 365, N'redbull.jpg'),
(N'Bánh Gạo One One Vị Ngọt Nhẹ', N'Bánh gạo One One, giòn, vị ngọt nhẹ, gói 150g. Món ăn vặt tuyệt vời cho cả gia đình.', 6, 5, 22500, 250, 2, 180, N'banhgao_oneone.jpg'),
(N'Bột Nêm Heo Knorr', N'Bột nêm Knorr từ thịt heo và xương heo, gói 400g. Tạo hương vị đậm đà cho món ăn.', 7, 6, 37500, 160, 2, 540, N'hatnem_knorr.jpg'),
(N'Đậu Hà Lan Hộp Ayam Brand', N'Đậu hà lan hộp Ayam Brand, tiện lợi cho việc nấu ăn. Dùng trong salad hoặc xào.', 8, 7, 27500, 190, 2, 730, N'dauhalan_ayam.jpg'),
(N'Dưa Hấu Không Hạt', N'Dưa hấu đỏ không hạt, ngọt mát, cung cấp nhiều vitamin. Giải nhiệt mùa hè.', 1, 1, 50000, 90.0, 1, 10, N'dua_hau.jpg'),
(N'Súp Lơ Đà Lạt', N'Súp lơ (bông cải xanh) Đà Lạt, VietGAP. Giàu chất xơ và vitamin, tốt cho sức khỏe tim mạch.', 2, 1, 35000, 110.0, 1, 7, N'bongcaixanh.jpg'),
(N'Sữa Đặc Ông Thọ Lốc Trắng 380g', N'Sữa đặc có đường Ông Thọ, lon 380g. Dùng pha cà phê, làm bánh hoặc ăn với bánh mì.', 3, 2, 25000, 300, 3, 365, N'suadac_ongtho.jpg'),
(N'Tôm Hùm Đen Tươi', N'Tôm hùm đen tươi, cỡ lớn. Thịt tôm ngọt, chắc, thích hợp hấp hoặc nướng.', 4, 3, 375000, 25.0, 1, 1, N'tom_su.jpg'),
(N'Nước Yến Đà Lạt Với Nha Đam & Đường Phèn', N'Nước yến Đà Lạt với nha đam và đường phèn, hộp 6 lon. Bổ dưỡng, tươi mát, tốt cho làn da.', 5, 5, 65000, 150, 3, 540, N'yen_nha_dam.jpg'),
(N'Bánh Custas Cream Orion', N'Bánh Custas Orion với nhân kem trứng, hộp 12 chiếc. Bánh mềm, nhân kem béo ngậy.', 6, 5, 62500, 180, 2, 150, N'banh_custas.jpg'),
(N'Gạo ST25 Hương Soc Trang', N'Gạo ST25, gạo ngon nhất thế giới, túi 5kg. Hạt dài, dẻo, hương vị đậm đà.', 7, 1, 200000, 100, 2, NULL, N'gao_st25.jpg'),
(N'Nấm Kim Châm Hàn Quốc', N'Nấm kim châm Hàn Quốc, túi 200g. Thích hợp nấu lẩu, xào hoặc nướng.', 2, 1, 25000, 200, 1, 7, N'nam_kim_cham.jpg'),
(N'Thanh Long Đỏ', N'Thanh long đỏ, ngọt nhẹ, giàu vitamin. Tốt cho tiêu hóa và sắc đẹp.', 1, 1, 50000, 130.0, 1, 7, N'thanhlong_ruotdo.jpg'),
(N'Rau Muống Hữu Cơ', N'Rau muống hữu cơ, an toàn cho sức khỏe, bó 300g. Mềm, giòn, thích hợp xào tỏi hoặc luộc.', 2, 1, 20000, 160, 1, 3, N'raumuong_huuco.jpg'),
(N'Sữa Chua Hy Lạp Thùng', N'Sữa chua Hy Lạp, dày và giàu protein, không đường, thùng 1kg. Tốt cho tiêu hóa và quản lý cân nặng.', 3, 2, 75000, 60, 3, 21, N'suachua_hylap.jpg'),
(N'Xúc Xích Heo Tiệt Trùng Vissan', N'Xúc xích heo tiệt trùng Vissan, gói 10 cây. Tiện lợi, ngon miệng và bổ dưỡng.', 4, 3, 45000, 200, 2, 180, N'xucxich_vissan.jpg');

-- Tags
INSERT INTO Tags (TagName) VALUES 
(N'trái cây'),
(N'tươi'),
(N'giòn'),
(N'sữa'),
(N'dồ uống'),
(N'snack'),
(N'mặn'),
(N'rau'),
(N'nước giải khát'),
(N'nước ép'),
(N'bánh mì'),
(N'bữa sáng'),
(N'dông lạnh'),
(N'bữa ăn'),
(N'thịt');


INSERT INTO ProductTags (ProductTypeID, TagID) VALUES
(1, 1), -- Banana - fruit
(1, 2), -- Banana - fresh

(2, 1), -- Apple - fruit
(2, 3), -- Apple - crunchy

(3, 4), -- Milk 1L - dairy
(3, 5), -- Milk 1L - drink

(4, 4), -- Yogurt Cup - dairy
(4, 6), -- Yogurt Cup - snack

(5, 6), -- Potato Chips - snack
(5, 7), -- Potato Chips - salty

(6, 8), -- Carrot - vegetable
(6, 2), -- Carrot - fresh

(7, 9), -- Orange Juice - beverage
(7, 10), -- Orange Juice - juice

(8, 11), -- Croissant - bakery
(8, 12), -- Croissant - breakfast

(9, 13), -- Frozen Pizza - frozen
(9, 14), -- Frozen Pizza - meal

(10, 15), -- Chicken Breast - meat
(10, 2); -- Chicken Breast - fresh

-- Vai trò nhân viên
INSERT INTO EmployeeRoles (RoleName, RoleDescription) VALUES
(N'Thu ngân', N'Xử lý giao dịch tại quầy'),
(N'Quản lý kho', N'Quản lý kho và tồn kho'),
(N'Nhân viên giao hàng', N'Xử lý giao hàng sản phẩm'),
(N'Giám sát', N'Giám sát nhân viên và hoạt động'),
(N'Quản trị viên', N'Thực hiện các thao tác CRUD');

-- Nhân viên
INSERT INTO Employees (FirstName, LastName, Email, PhoneNumber, Gender, BirthDate, CitizenID, Salary, RoleID, ImagePath)
VALUES
('Alice', 'Smith', 'alice@minimart.com', '0900111222', N'Nữ', '1990-05-01', 'C12345678', 2500.00, 5, 'women/3.jpg'),
('Bob', 'Johnson', 'bob@minimart.com', '0900333444', N'Nam', '1985-11-12', 'C87654321', 2700.00, 2, 'men/3.jpg'),
('Charlie', 'Brown', 'charlie@minimart.com', '0900555666', N'Nam', '1992-03-14', 'C99887766', 2400.00, 3, 'men/4.jpg'),
('Diana', 'Lopez', 'diana@minimart.com', '0900777888', N'Nữ', '1988-07-22', 'C11223344', 3000.00, 4, 'women/4.jpg'),
('Ethan', 'Wright', 'ethan@minimart.com', '0900888999', N'Nam', '1991-09-17', 'C22334455', 2300.00, 2, 'men/5.jpg'),
('Fiona', 'Clark', 'fiona@minimart.com', '0900999000', N'Nữ', '1993-04-28', 'C33445566', 2600.00, 3, 'women/5.jpg'),
('George', 'Miller', 'george@minimart.com', '0900123123', N'Nam', '1987-06-06', 'C44556677', 2800.00, 2, 'men/6.jpg'),
('Hannah', 'Baker', 'hannah@minimart.com', '0900233233', N'Nữ', '1995-10-15', 'C55667788', 2550.00, 1, 'women/6.jpg'),
('Ivan', 'Turner', 'ivan@minimart.com', '0900343344', N'Nam', '1986-12-30', 'C66778899', 2900.00, 4, 'men/7.jpg'),
('Julia', 'Parker', 'julia@minimart.com', '0900453455', N'Nữ', '1994-08-08', 'C77889900', 2750.00, 5, 'women/7.jpg');

-- Quản trị viên
-- Insert 10 associated employee accounts
INSERT INTO EmployeeAccounts (EmployeeID, Username, PasswordHash, Salt)
VALUES
(1, 'alice', 0x5C67AB2B362E04BEE0BDDF45DDDADE29FF3BBF29E4F75A74AFD03CD8360222C1, 0x3514E223D890B6B9750C01F6CF7BB0D6),
(2, 'bob', 0x5C67AB2B362E04BEE0BDDF45DDDADE29FF3BBF29E4F75A74AFD03CD8360222C1, 0x3514E223D890B6B9750C01F6CF7BB0D6),
(3, 'charlie', 0x5C67AB2B362E04BEE0BDDF45DDDADE29FF3BBF29E4F75A74AFD03CD8360222C1, 0x3514E223D890B6B9750C01F6CF7BB0D6),
(4, 'admindiana', 0x12345, 0x12345),
(5, 'ethanw', 0x12345, 0x12345),
(6, 'fionac', 0x12345, 0x12345),
(7, 'georgem', 0x12345, 0x12345),
(8, 'hannahb', 0x12345, 0x12345),
(9, 'ivant', 0x12345, 0x12345),
(10, 'juliap', 0x12345, 0x12345);


-- Khách hàng
INSERT INTO Customers (FirstName, LastName, Email, PhoneNumber, ImagePath, Username, PasswordHash, Salt) VALUES
(N'Nguyễn', N'Văn An', 'an.nguyen@example.com', '0901122334', 'men/10.jpg', 'annguyen', 0x5C67AB2B362E04BEE0BDDF45DDDADE29FF3BBF29E4F75A74AFD03CD8360222C1, 0x3514E223D890B6B9750C01F6CF7BB0D6),
(N'Trần', N'Thị Bình', 'binh.tran@example.com', '0902233445', 'women/10.jpg', 'binhtran', 0x5C67AB2B362E04BEE0BDDF45DDDADE29FF3BBF29E4F75A74AFD03CD8360222C1, 0x3514E223D890B6B9750C01F6CF7BB0D6),
(N'Lê', N'Quang Cường', 'cuong.le@example.com', '0903344556', 'men/11.jpg', 'cuongle', 0x5C67AB2B362E04BEE0BDDF45DDDADE29FF3BBF29E4F75A74AFD03CD8360222C1, 0x3514E223D890B6B9750C01F6CF7BB0D6),
(N'Phạm', N'Thị Dung', 'dung.pham@example.com', '0904455667', 'women/11.jpg', 'dungpham', 0x12345, 0x12345),
(N'Hoàng', N'Minh Đức', 'duc.hoang@example.com', '0905566778', 'men/12.jpg', 'duchoang', 0x12345, 0x12345),
(N'Võ', N'Thị Hạnh', 'hanh.vo@example.com', '0906677889', 'women/12.jpg', 'hanhvo', 0x12345, 0x12345),
(N'Đặng', N'Tuấn Hùng', 'hung.dang@example.com', '0907788990', 'men/13.jpg', 'hungdang', 0x12345, 0x12345),
(N'Bùi', N'Thị Lan', 'lan.bui@example.com', '0908899001', 'women/13.jpg', 'lanbui', 0x12345, 0x12345),
(N'Huỳnh', N'Gia Nam', 'nam.huynh@example.com', '0909900112', 'men/14.jpg', 'namhuynh', 0x12345, 0x12345),
(N'Dương', N'Thị Oanh', 'oanh.duong@example.com', '0910011223', 'women/14.jpg', 'oanhduong', 0x12345, 0x12345);

-- Phương thức thanh toán
INSERT INTO PaymentMethods (MethodName) VALUES
(N'Tiền mặt'),
(N'Thẻ tín dụng'),
(N'Thanh toán qua di động'),
(N'Chuyển khoản ngân hàng');

-- Doanh thu
INSERT INTO Sales (CustomerID, EmployeeID, PaymentMethodID, DeliveryAddress, DeliveryTime, IsPickup, OrderStatus) VALUES
(1, 1, 2, '789 Customer Ave', GETDATE(), 0, N'Hoàn thành'),
(2, 2, 1, '123 Delivery St', GETDATE(), 1, N'Đã xác nhận'),
(3, 3, 3, '555 Mobile Lane', GETDATE(), 0, N'Đang xử lý'),
(1, 2, 4, '333 Bank Rd', GETDATE(), 0, N'Đã hủy');

-- Chi tiết đơn hàng
INSERT INTO SaleDetails (SaleID, ProductTypeID, Quantity, ProductPriceAtPurchase) VALUES
(1, 1, 3.5, 1.99),
(1, 2, 2, 2.50),
(1, 5, 1, 1.50),
(2, 3, 2, 2.49),
(2, 4, 3, 1.20),
(3, 7, 1, 3.49),
(3, 9, 2, 5.99),
(4, 6, 5, 0.99);

-- OtypTypes
INSERT INTO dbo.OtpTypes (OtpTypeName, Description) VALUES
('PasswordReset', 'OTP for resetting account password'),
('OrderConfirmation', 'OTP for confirming an order'),
('AccountVerification', 'OTP for verifying new account'),
('CustomerAccountVerification', 'OTP sent to verify a new customer''s email address upon registration.'),
('CustomerPasswordReset', 'OTP sent to a customer''s email when they request to reset a forgotten password.'),
('EmployeePasswordReset', 'OTP sent to an employee''s email when they request to reset a forgotten password.'),
('EmployeeLoginTwoFactor', 'OTP required as a mandatory second factor after an employee enters correct credentials to complete login.'),
('CustomerChangeEmailVerification', 'OTP sent to the NEW email address to verify when a customer requests to change their account-linked email.'),
('EmployeeChangeEmailVerification', 'OTP sent to the NEW email address to verify when an employee requests an email change (may require admin approval or internal process).'),
('UserChangePasswordVerification', 'OTP sent to confirm a password change request when the user is already logged in (typically after re-authenticating with old password).'),
('UserChangePhoneNumberVerification', 'OTP sent to the NEW phone number (if SMS capable) or current email to verify a phone number change request.'),
('LoginTwoFactorVerification', 'Mandatory OTP for two-factor authentication during login for any user type (Customer/Employee).');


USE Minimart;
GO

DECLARE @TargetCustomerID INT = 1;
DECLARE @EmployeeForSales INT;
DECLARE @PaymentMethodForSales INT;

SELECT TOP 1 @EmployeeForSales = e.EmployeeID
FROM dbo.Employees e
JOIN dbo.EmployeeAccounts ea ON e.EmployeeID = ea.EmployeeID
WHERE ea.Username = 'alice';

SELECT TOP 1 @PaymentMethodForSales = PaymentMethodID
FROM dbo.PaymentMethods
WHERE PaymentMethodID = 1;

IF @EmployeeForSales IS NULL
BEGIN
    PRINT 'Error: No matching EmployeeID found. Please check Employee and EmployeeAccount data.';
    RETURN;
END

IF @PaymentMethodForSales IS NULL
BEGIN
    PRINT 'Error: No matching PaymentMethodID found. Please check PaymentMethods data.';
    RETURN;
END

PRINT 'Using EmployeeID: ' + CAST(@EmployeeForSales AS VARCHAR) + ' and PaymentMethodID: ' + CAST(@PaymentMethodForSales AS VARCHAR) + ' for new orders.';

PRINT 'Starting data insertion for 12 months of Sales and SaleDetails for CustomerID ' + CAST(@TargetCustomerID AS VARCHAR) + '...';

DECLARE @CurrentMonthOffset INT = 0;
DECLARE @BaseDate DATETIME = GETDATE();

WHILE @CurrentMonthOffset < 12
BEGIN
    DECLARE @TargetMonth DATETIME = DATEADD(month, -@CurrentMonthOffset, @BaseDate);
    DECLARE @FirstDayOfTargetMonth DATETIME = DATEFROMPARTS(YEAR(@TargetMonth), MONTH(@TargetMonth), 1);
    DECLARE @LastDayOfTargetMonth DATETIME = EOMONTH(@TargetMonth);
    DECLARE @MaxDayInMonth DATETIME = @TargetMonth;

    IF (YEAR(@TargetMonth) = YEAR(@BaseDate) AND MONTH(@TargetMonth) = MONTH(@BaseDate))
        SET @MaxDayInMonth = @BaseDate;
    ELSE
        SET @MaxDayInMonth = @LastDayOfTargetMonth;
    
    DECLARE @NumberOfSalesThisMonth INT = FLOOR(RAND() * 3) + 1;
    DECLARE @SaleCounter INT = 0;

    PRINT 'Processing month: ' + FORMAT(@TargetMonth, 'yyyy-MM') + '. Planned orders: ' + CAST(@NumberOfSalesThisMonth AS VARCHAR);

    WHILE @SaleCounter < @NumberOfSalesThisMonth
    BEGIN
        DECLARE @SaleDateThisOrder DATETIME;
        DECLARE @RandomDayOffset INT;

        IF (DATEDIFF(day, @FirstDayOfTargetMonth, @MaxDayInMonth) >= 0)
        BEGIN
            SET @RandomDayOffset = FLOOR(RAND() * (DATEDIFF(day, @FirstDayOfTargetMonth, @MaxDayInMonth) + 1));
            SET @SaleDateThisOrder = DATEADD(day, @RandomDayOffset, @FirstDayOfTargetMonth);
            SET @SaleDateThisOrder = DATEADD(second, FLOOR(RAND() * 86399), CAST(CAST(@SaleDateThisOrder AS DATE) AS DATETIME));
        END
        ELSE
        BEGIN
            SET @SaleDateThisOrder = @FirstDayOfTargetMonth;
            PRINT 'Warning: Failed to generate valid random date, using first day of month.';
        END

        INSERT INTO dbo.Sales (SaleDate, CustomerID, EmployeeID, PaymentMethodID, DeliveryAddress, DeliveryTime, IsPickup, OrderStatus)
        VALUES (
            @SaleDateThisOrder,
            @TargetCustomerID,
            @EmployeeForSales,
            @PaymentMethodForSales,
            '123 Random St, District ' + CAST(FLOOR(RAND()*10)+1 AS VARCHAR), 
            DATEADD(day, 1, @SaleDateThisOrder), 
            0, 
            'Hoàn thành'
        );

        DECLARE @NewSaleID INT = SCOPE_IDENTITY();
        PRINT '  Created SaleID: ' + CAST(@NewSaleID AS VARCHAR) + ' on ' + FORMAT(@SaleDateThisOrder, 'yyyy-MM-dd HH:mm:ss');

        DECLARE @NumberOfItemsInSale INT = FLOOR(RAND() * 3) + 1;
        DECLARE @ItemCounter INT = 0;
        DECLARE @UsedProductTypeIDsInOrder TABLE (ID INT);

        WHILE @ItemCounter < @NumberOfItemsInSale
        BEGIN
            DECLARE @RandomProductTypeID INT;
            DECLARE @ProductPrice DECIMAL(10,2);
            DECLARE @ProductStock DECIMAL(10,2);
            
            SELECT TOP 1 @RandomProductTypeID = pt.ProductTypeID, @ProductPrice = pt.Price, @ProductStock = pt.StockAmount
            FROM dbo.ProductTypes pt
            WHERE pt.IsActive = 1
                  AND pt.StockAmount > 0
                  AND pt.ProductTypeID BETWEEN 1 AND 10
                  AND NOT EXISTS (SELECT 1 FROM @UsedProductTypeIDsInOrder u WHERE u.ID = pt.ProductTypeID)
            ORDER BY NEWID();

            IF @RandomProductTypeID IS NOT NULL AND @ProductPrice IS NOT NULL
            BEGIN
                DECLARE @Quantity DECIMAL(10,2) = FLOOR(RAND() * 2) + 1;

                INSERT INTO dbo.SaleDetails (SaleID, ProductTypeID, Quantity, ProductPriceAtPurchase)
                VALUES (@NewSaleID, @RandomProductTypeID, @Quantity, @ProductPrice);
                
                INSERT INTO @UsedProductTypeIDsInOrder (ID) VALUES (@RandomProductTypeID);

                PRINT '    -> Added SaleDetail: ProductTypeID ' + CAST(@RandomProductTypeID AS VARCHAR) + ', Quantity ' + CAST(@Quantity AS VARCHAR) + ', Price ' + CAST(@ProductPrice AS VARCHAR);
            END
            ELSE
            BEGIN
                PRINT '    -> No suitable ProductTypeID found or all products used for this order.';
                IF (SELECT COUNT(*) FROM @UsedProductTypeIDsInOrder) >= (SELECT COUNT(*) FROM dbo.ProductTypes WHERE ProductTypeID BETWEEN 1 AND 10 AND IsActive=1)
                    BREAK;
            END
            
            SET @ItemCounter = @ItemCounter + 1;
        END
        DELETE FROM @UsedProductTypeIDsInOrder;
        SET @SaleCounter = @SaleCounter + 1;
    END
    SET @CurrentMonthOffset = @CurrentMonthOffset + 1;
END

PRINT 'Completed insertion of Sales and SaleDetails data for CustomerID ' + CAST(@TargetCustomerID AS VARCHAR) + '.';
GO

SELECT s.SaleID, s.SaleDate, s.CustomerID, sd.ProductTypeID, sd.Quantity, sd.ProductPriceAtPurchase
FROM Sales s
JOIN SaleDetails sd ON s.SaleID = sd.SaleID
WHERE s.CustomerID = 11 AND s.OrderStatus = 'Completed'
ORDER BY s.SaleDate DESC;

SELECT
    FORMAT(s.SaleDate, 'yyyy-MM') AS SaleMonthYear,
    SUM(sd.Quantity * sd.ProductPriceAtPurchase) AS MonthlyTotal
FROM Sales s
JOIN SaleDetails sd ON s.SaleID = sd.SaleID
WHERE s.CustomerID = 11 AND s.OrderStatus = 'Completed'
GROUP BY FORMAT(s.SaleDate, 'yyyy-MM')
ORDER BY SaleMonthYear DESC;
