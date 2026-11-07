
CREATE TABLE dbo.Admin (
    Admin_ID      INT IDENTITY(1,1) NOT NULL,
    First_Name    NVARCHAR(50)      NOT NULL,
    Last_Name     NVARCHAR(50)      NOT NULL,
    Phone_Number  VARCHAR(12)       NOT NULL,
    Password      VARCHAR(255)      NOT NULL,                      -- bcrypt hash
    Email         VARCHAR(150)      NOT NULL,
    CONSTRAINT PK_Admin PRIMARY KEY (Admin_ID),
    CONSTRAINT UQ_Admin_Email UNIQUE (Email),
    CONSTRAINT CK_Admin_Phone CHECK (Phone_Number LIKE '05[034-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]')
);
GO


CREATE TABLE dbo.Home_Owner (
    Home_owner_ID      INT IDENTITY(1,1) NOT NULL,
    First_Name         NVARCHAR(50)      NOT NULL,
    Last_Name          NVARCHAR(50)      NOT NULL,
    Email              VARCHAR(150)      NOT NULL,
    Phone_Number       VARCHAR(12)       NOT NULL,
    Password           VARCHAR(255)      NOT NULL,
    Address            NVARCHAR(200)     NOT NULL,                 -- 'King Fahd Rd, Bldg 14, Apt 6'
    City               VARCHAR(20)       NOT NULL,                 -- Riyadh / Jeddah / Dammam
    Credit_Card        NVARCHAR(40)      NULL,                     -- 'Mada •••• 4417 · 08/28' — never a full number
    Loyalty_Points     INT               NOT NULL CONSTRAINT DF_HO_Points DEFAULT (0),   -- current balance
    District           NVARCHAR(60)      NOT NULL,                 -- [UI] 'Al Olaya' — coverage matching ("Coverage: Al Olaya")
    Latitude           DECIMAL(9,6)      NOT NULL,                 -- [UI] distance / ETA ("2.1 km away", "Arrives ~25 min")
    Longitude          DECIMAL(9,6)      NOT NULL,                 -- [UI]
    Phone_Verified_At  DATETIME2(0)      NULL,                     -- [UI] Verify Phone step (SH-07)
    Created_At         DATETIME2(0)      NOT NULL CONSTRAINT DF_HO_Created DEFAULT (SYSDATETIME()),  -- [UI]
    CONSTRAINT PK_Home_Owner PRIMARY KEY (Home_owner_ID),
    CONSTRAINT UQ_HO_Email UNIQUE (Email),
    CONSTRAINT UQ_HO_Phone UNIQUE (Phone_Number),
    CONSTRAINT CK_HO_Phone CHECK (Phone_Number LIKE '05[034-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
    CONSTRAINT CK_HO_City CHECK (City IN ('Riyadh', 'Jeddah', 'Dammam')),
    CONSTRAINT CK_HO_Points CHECK (Loyalty_Points >= 0)
);
GO

CREATE TABLE dbo.Technician (
    Technician_ID        INT IDENTITY(1,1) NOT NULL,
    First_Name           NVARCHAR(50)      NOT NULL,
    Last_Name            NVARCHAR(50)      NOT NULL,
    Phone_Number         VARCHAR(12)       NOT NULL,
    Password             VARCHAR(255)      NOT NULL,
    Email                VARCHAR(150)      NOT NULL,
    National_ID          CHAR(10)          NOT NULL,               -- national ID (1…) or Iqama (2…) number
    Vocational_License   NVARCHAR(300)     NOT NULL,               -- 'uploads/technicians/{id}/license-….pdf'
    Verification_Status  VARCHAR(10)       NOT NULL CONSTRAINT DF_Tech_Verif DEFAULT ('Pending'),
    Hourly_Rate          DECIMAL(6,2)      NOT NULL,               -- extra work beyond fixed prices
    Coverage_Zone        NVARCHAR(300)     NOT NULL,               -- 'Al Olaya, Al Sulimaniyah, Al Wurud'
    Average_Rating       DECIMAL(2,1)      NULL,                   -- kept = AVG(Review.Rating_Score)
    Is_Available         BIT               NOT NULL CONSTRAINT DF_Tech_Avail DEFAULT (0),
    City                 VARCHAR(20)       NOT NULL,               -- [UI] city of the coverage zones
    National_ID_File     NVARCHAR(300)     NOT NULL,               -- [UI] 'National ID · front & back' upload (SH-08)
    Latitude             DECIMAL(9,6)      NOT NULL,               -- [UI] base location for distance / ETA
    Longitude            DECIMAL(9,6)      NOT NULL,               -- [UI]
    Verified_At          DATETIME2(0)      NULL,                   -- [UI] "Approved by Fixora admin · 12 Mar 2025"
    Verification_Note    NVARCHAR(300)     NULL,                   -- [UI] reason shown when the admin rejects the documents
    Phone_Verified_At    DATETIME2(0)      NULL,                   -- [UI]
    Created_At           DATETIME2(0)      NOT NULL CONSTRAINT DF_Tech_Created DEFAULT (SYSDATETIME()),  -- [UI] "Member since Mar 2025"
    CONSTRAINT PK_Technician PRIMARY KEY (Technician_ID),
    CONSTRAINT UQ_Tech_Email UNIQUE (Email),
    CONSTRAINT UQ_Tech_Phone UNIQUE (Phone_Number),
    CONSTRAINT UQ_Tech_NationalID UNIQUE (National_ID),
    CONSTRAINT CK_Tech_Phone CHECK (Phone_Number LIKE '05[034-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
    CONSTRAINT CK_Tech_NationalID CHECK (National_ID LIKE '[12][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
    CONSTRAINT CK_Tech_Verif CHECK (Verification_Status IN ('Pending', 'Verified', 'Rejected')),
    CONSTRAINT CK_Tech_Rate CHECK (Hourly_Rate BETWEEN 30 AND 500),
    CONSTRAINT CK_Tech_Rating CHECK (Average_Rating BETWEEN 1 AND 5),
    CONSTRAINT CK_Tech_City CHECK (City IN ('Riyadh', 'Jeddah', 'Dammam')),
    CONSTRAINT CK_Tech_Avail CHECK (Is_Available = 0 OR Verification_Status = 'Verified'),
    CONSTRAINT CK_Tech_Verified CHECK ((Verification_Status = 'Verified' AND Verified_At IS NOT NULL) OR Verification_Status <> 'Verified')
);
GO


CREATE TABLE dbo.Service_Type (
    Service_Type_ID  INT IDENTITY(1,1) NOT NULL,
    Type_Name        NVARCHAR(40)      NOT NULL,
    Description      NVARCHAR(200)     NOT NULL,                   -- 'Leaks, drains, water heaters'
    Icon_Class       VARCHAR(40)       NOT NULL,                   -- [UI] 'bi-droplet-half'
    Image_URL        NVARCHAR(400)     NOT NULL,                   -- [UI] category photo (.cat img)
    CONSTRAINT PK_Service_Type PRIMARY KEY (Service_Type_ID),
    CONSTRAINT UQ_Service_Type UNIQUE (Type_Name)
);
GO


CREATE TABLE dbo.Service (
    Service_ID              INT IDENTITY(1,1) NOT NULL,
    Service_Type_ID         INT               NOT NULL,
    Service_Name            NVARCHAR(80)      NOT NULL,
    Service_Category        VARCHAR(15)       NOT NULL,            -- Repair / Installation / Inspection / Delivery / Assembly / Painting
    Price                   DECIMAL(8,2)      NOT NULL,
    Flexible_Rate           BIT               NOT NULL CONSTRAINT DF_Service_Flex DEFAULT (0),
    Service_Commission_Fee  DECIMAL(6,2)      NOT NULL CONSTRAINT DF_Service_Fee DEFAULT (15),
    Description             NVARCHAR(250)     NOT NULL,            -- [UI] 'Fix dripping or leaking pipes, joints …'
    Unit                    VARCHAR(10)       NOT NULL CONSTRAINT DF_Service_Unit DEFAULT ('job'),  -- [UI] job / hr / cylinder / room
    Is_Active               BIT               NOT NULL CONSTRAINT DF_Service_Active DEFAULT (1),    -- [UI] "Delete service" hides a service already used in bookings
    CONSTRAINT PK_Service PRIMARY KEY (Service_ID),
    CONSTRAINT UQ_Service UNIQUE (Service_Type_ID, Service_Name),
    CONSTRAINT CK_Service_Category CHECK (Service_Category IN ('Repair', 'Installation', 'Inspection', 'Delivery', 'Assembly', 'Painting')),
    CONSTRAINT CK_Service_Price CHECK (Price BETWEEN 10 AND 20000),
    CONSTRAINT CK_Service_Fee CHECK (Service_Commission_Fee BETWEEN 0 AND 500),
    CONSTRAINT CK_Service_Unit CHECK (Unit IN ('job', 'hr', 'cylinder', 'room')),
    CONSTRAINT CK_Service_FlexUnit CHECK ((Flexible_Rate = 1 AND Unit = 'hr') OR (Flexible_Rate = 0 AND Unit <> 'hr')),
    CONSTRAINT FK_Service_Type FOREIGN KEY (Service_Type_ID) REFERENCES dbo.Service_Type (Service_Type_ID)
);
GO


CREATE TABLE dbo.Technician_Service (
    Technician_Service_ID  INT IDENTITY(1,1) NOT NULL,
    Technician_ID          INT               NOT NULL,
    Service_ID             INT               NOT NULL,
    CONSTRAINT PK_Technician_Service PRIMARY KEY (Technician_Service_ID),
    CONSTRAINT UQ_Technician_Service UNIQUE (Technician_ID, Service_ID),
    CONSTRAINT FK_TS_Technician FOREIGN KEY (Technician_ID) REFERENCES dbo.Technician (Technician_ID),
    CONSTRAINT FK_TS_Service FOREIGN KEY (Service_ID) REFERENCES dbo.Service (Service_ID)
);
GO


CREATE TABLE dbo.Booking (
    Booking_ID              INT IDENTITY(1,1) NOT NULL,
    Home_owner_ID           INT               NOT NULL,
    Technician_ID           INT               NOT NULL,            -- re-sending after a decline replaces it (same booking)
    Booking_Type            VARCHAR(10)       NOT NULL,            -- Express / Scheduled
    Scheduled_Date_Time     DATETIME2(0)      NOT NULL,            -- express: the request time · scheduled: start of the 2-hour slot
    Issue_Attachment_Photo  NVARCHAR(1000)    NULL,                -- up to 3 paths separated by '|' ('uploads/bookings/{id}/…jpg')
    Status                  VARCHAR(10)       NOT NULL CONSTRAINT DF_Booking_Status DEFAULT ('Pending'),
    Created_At              DATETIME2(0)      NOT NULL CONSTRAINT DF_Booking_Created DEFAULT (SYSDATETIME()),
    Job_Progress_Status     VARCHAR(12)       NOT NULL CONSTRAINT DF_Booking_Progress DEFAULT ('Requested'),
    Service_Address         NVARCHAR(250)     NOT NULL,
    Service_District        NVARCHAR(60)      NOT NULL,            -- [UI] coverage zone of the address
    Accepted_At             DATETIME2(0)      NULL,                -- [UI] job timeline (UC2-01 "Accepted 23:12" …)
    On_The_Way_At           DATETIME2(0)      NULL,                -- [UI]
    Arrived_At              DATETIME2(0)      NULL,                -- [UI] "arrived 23:36"
    Started_At              DATETIME2(0)      NULL,                -- [UI] "In progress 23:41 · materials approved"
    Completed_At            DATETIME2(0)      NULL,                -- [UI]
    Decline_Reason          NVARCHAR(200)     NULL,                -- [UI] UC1-15 'Currently on another emergency job.'
    Cancelled_At            DATETIME2(0)      NULL,                -- [UI]
    Cancel_Reason           NVARCHAR(200)     NULL,                -- [UI]
    CONSTRAINT PK_Booking PRIMARY KEY (Booking_ID),
    CONSTRAINT CK_Booking_Type CHECK (Booking_Type IN ('Express', 'Scheduled')),
    CONSTRAINT CK_Booking_Status CHECK (Status IN ('Pending', 'Accepted', 'Rejected', 'Cancelled', 'Completed')),
    CONSTRAINT CK_Booking_Progress CHECK (Job_Progress_Status IN ('Requested', 'Accepted', 'On the way', 'In progress', 'Completed')),
    CONSTRAINT CK_Booking_Consistency CHECK (
           (Status IN ('Pending', 'Rejected', 'Cancelled') AND Job_Progress_Status IN ('Requested', 'Accepted', 'On the way') )
        OR (Status = 'Accepted' AND Job_Progress_Status IN ('Accepted', 'On the way', 'In progress'))
        OR (Status = 'Completed' AND Job_Progress_Status = 'Completed' AND Completed_At IS NOT NULL)),
    CONSTRAINT CK_Booking_Slot CHECK (Booking_Type = 'Express' OR (DATEPART(MINUTE, Scheduled_Date_Time) = 0 AND DATEPART(HOUR, Scheduled_Date_Time) IN (8, 10, 12, 14, 17, 19))),
    CONSTRAINT CK_Booking_Times CHECK (Accepted_At IS NULL OR Accepted_At >= Created_At),
    CONSTRAINT FK_Booking_HO FOREIGN KEY (Home_owner_ID) REFERENCES dbo.Home_Owner (Home_owner_ID),
    CONSTRAINT FK_Booking_Tech FOREIGN KEY (Technician_ID) REFERENCES dbo.Technician (Technician_ID)
);
CREATE INDEX IX_Booking_HO ON dbo.Booking (Home_owner_ID, Created_At DESC);
CREATE INDEX IX_Booking_Tech ON dbo.Booking (Technician_ID, Scheduled_Date_Time);
GO


CREATE TABLE dbo.Booking_Service (
    Booking_Service_ID  INT IDENTITY(1,1) NOT NULL,
    Booking_ID          INT               NOT NULL,
    Service_ID          INT               NOT NULL,
    Quantity            INT               NOT NULL,
    Agreed_Price        DECIMAL(8,2)      NOT NULL,                -- unit price agreed at booking (fixed price, or hourly rate × hours)
    CONSTRAINT PK_Booking_Service PRIMARY KEY (Booking_Service_ID),
    CONSTRAINT UQ_Booking_Service UNIQUE (Booking_ID, Service_ID),
    CONSTRAINT CK_BS_Qty CHECK (Quantity BETWEEN 1 AND 10),
    CONSTRAINT CK_BS_Price CHECK (Agreed_Price > 0),
    CONSTRAINT FK_BS_Booking FOREIGN KEY (Booking_ID) REFERENCES dbo.Booking (Booking_ID) ON DELETE CASCADE,
    CONSTRAINT FK_BS_Service FOREIGN KEY (Service_ID) REFERENCES dbo.Service (Service_ID)
);
GO

CREATE TABLE dbo.Invoice (
    Invoice_ID              INT IDENTITY(1,1) NOT NULL,
    Booking_ID              INT               NOT NULL,
    Labor_Cost              DECIMAL(9,2)      NOT NULL,
    Material_Cost           DECIMAL(9,2)      NOT NULL CONSTRAINT DF_Inv_Mat DEFAULT (0),
    Service_Commission_Fee  DECIMAL(7,2)      NOT NULL,
    Express_Fee             DECIMAL(7,2)      NOT NULL CONSTRAINT DF_Inv_Exp DEFAULT (0),
    Total_Amount            DECIMAL(9,2)      NOT NULL,
    Payment_Method          VARCHAR(10)       NULL,                -- Mada / STC Pay / Card / Cash (NULL until chosen)
    Payment_Status          VARCHAR(20)       NOT NULL CONSTRAINT DF_Inv_PayStatus DEFAULT ('Unpaid'),
    Cash_OTP_Code           CHAR(6)           NULL,                -- one-time code for cash (UC2-09)
    Job_Title               NVARCHAR(120)     NOT NULL,            -- [UI] 'Leak repair under kitchen sink'
    Work_Notes              NVARCHAR(250)     NULL,                -- [UI] 'Agreed fixed price · replaced joint seal and valve'
    Material_Items          NVARCHAR(MAX)     NULL,                -- [UI] JSON [{"name":"Brass compression valve ½\"","qty":1,"amount":24.00}, …]
    Points_Discount         DECIMAL(7,2)      NOT NULL CONSTRAINT DF_Inv_PtsDisc DEFAULT (0),   -- [UI] redeemed loyalty points
    Issued_At               DATETIME2(0)      NOT NULL CONSTRAINT DF_Inv_Issued DEFAULT (SYSDATETIME()),  -- [UI]
    Paid_At                 DATETIME2(0)      NULL,                -- [UI]
    Card_Label              NVARCHAR(40)      NULL,                -- [UI] 'Mada •••• 4417' shown on the receipt
    Transaction_Ref         VARCHAR(40)       NULL,                -- [UI] gateway reference (online payments)
    OTP_Expires_At          DATETIME2(0)      NULL,                -- [UI] code valid 10 minutes
    OTP_Attempts            INT               NOT NULL CONSTRAINT DF_Inv_OtpAtt DEFAULT (0),    -- [UI] 3 wrong entries lock the code
    CONSTRAINT PK_Invoice PRIMARY KEY (Invoice_ID),
    CONSTRAINT UQ_Invoice_Booking UNIQUE (Booking_ID),
    CONSTRAINT CK_Inv_Amounts CHECK (Labor_Cost >= 0 AND Material_Cost >= 0 AND Service_Commission_Fee >= 0 AND Express_Fee IN (0, 10) AND Points_Discount >= 0),
    CONSTRAINT CK_Inv_Total CHECK (Total_Amount = Labor_Cost + Material_Cost + Service_Commission_Fee + Express_Fee - Points_Discount AND Total_Amount > 0),
    CONSTRAINT CK_Inv_Method CHECK (Payment_Method IN ('Mada', 'STC Pay', 'Card', 'Cash')),
    CONSTRAINT CK_Inv_Status CHECK (Payment_Status IN ('Unpaid', 'Paid', 'Refunded', 'Partially Refunded')),
    CONSTRAINT CK_Inv_Paid CHECK ((Payment_Status = 'Unpaid' AND Paid_At IS NULL) OR (Payment_Status <> 'Unpaid' AND Paid_At IS NOT NULL AND Payment_Method IS NOT NULL)),
    CONSTRAINT CK_Inv_Otp CHECK (Cash_OTP_Code IS NULL OR Cash_OTP_Code LIKE '[0-9][0-9][0-9][0-9][0-9][0-9]'),
    CONSTRAINT CK_Inv_OtpAtt CHECK (OTP_Attempts BETWEEN 0 AND 3),
    CONSTRAINT CK_Inv_Ref CHECK (Payment_Status = 'Unpaid' OR Payment_Method = 'Cash' OR Transaction_Ref IS NOT NULL),
    CONSTRAINT FK_Invoice_Booking FOREIGN KEY (Booking_ID) REFERENCES dbo.Booking (Booking_ID)
);
CREATE UNIQUE INDEX UX_Invoice_Ref ON dbo.Invoice (Transaction_Ref) WHERE Transaction_Ref IS NOT NULL;
GO


CREATE TABLE dbo.Review (
    Review_ID      INT IDENTITY(1,1) NOT NULL,
    Booking_ID     INT               NOT NULL,
    Home_owner_ID  INT               NOT NULL,
    Technician_ID  INT               NOT NULL,
    Rating_Score   INT               NOT NULL,
    Comment        NVARCHAR(600)     NULL,
    Created_At     DATETIME2(0)      NOT NULL CONSTRAINT DF_Review_Created DEFAULT (SYSDATETIME()),  -- [UI] review lists, edit history
    CONSTRAINT PK_Review PRIMARY KEY (Review_ID),
    CONSTRAINT UQ_Review_Booking UNIQUE (Booking_ID),
    CONSTRAINT CK_Review_Score CHECK (Rating_Score BETWEEN 1 AND 5),
    CONSTRAINT FK_Review_Booking FOREIGN KEY (Booking_ID) REFERENCES dbo.Booking (Booking_ID),
    CONSTRAINT FK_Review_HO FOREIGN KEY (Home_owner_ID) REFERENCES dbo.Home_Owner (Home_owner_ID),
    CONSTRAINT FK_Review_Tech FOREIGN KEY (Technician_ID) REFERENCES dbo.Technician (Technician_ID)
);
CREATE INDEX IX_Review_Tech ON dbo.Review (Technician_ID, Created_At DESC);
GO


CREATE TABLE dbo.Dispute_Ticket (
    Ticket_ID          INT IDENTITY(1,1) NOT NULL,
    Booking_ID         INT               NOT NULL,
    Admin_ID           INT               NULL,
    Issue_Description  NVARCHAR(500)     NOT NULL,
    Ticket_Status      VARCHAR(12)       NOT NULL CONSTRAINT DF_Ticket_Status DEFAULT ('Open'),
    Refund_Amount      DECIMAL(9,2)      NULL,                     -- requested by the homeowner, then the approved amount
    Created_At         DATETIME2(0)      NOT NULL CONSTRAINT DF_Ticket_Created DEFAULT (SYSDATETIME()),
    Problem_Type       VARCHAR(30)       NOT NULL,                 -- [UI] 'What went wrong?' chips
    Photo_Path         NVARCHAR(300)     NULL,                     -- [UI] 'Add photos' (one photo)
    Resolution_Note    NVARCHAR(500)     NULL,                     -- [UI] admin decision text
    Resolved_At        DATETIME2(0)      NULL,                     -- [UI]
    CONSTRAINT PK_Dispute_Ticket PRIMARY KEY (Ticket_ID),
    CONSTRAINT UQ_Ticket_Booking UNIQUE (Booking_ID),
    CONSTRAINT CK_Ticket_Status CHECK (Ticket_Status IN ('Open', 'Under Review', 'Resolved', 'Rejected')),
    CONSTRAINT CK_Ticket_Type CHECK (Problem_Type IN ('Problem came back', 'Charged more than agreed', 'Damage to my home', 'Technician behavior', 'Other')),
    CONSTRAINT CK_Ticket_Refund CHECK (Refund_Amount IS NULL OR Refund_Amount >= 0),
    CONSTRAINT CK_Ticket_Admin CHECK (Ticket_Status = 'Open' OR Admin_ID IS NOT NULL),
    CONSTRAINT CK_Ticket_Resolved CHECK ((Ticket_Status IN ('Resolved', 'Rejected') AND Resolved_At IS NOT NULL) OR (Ticket_Status IN ('Open', 'Under Review') AND Resolved_At IS NULL)),
    CONSTRAINT FK_Ticket_Booking FOREIGN KEY (Booking_ID) REFERENCES dbo.Booking (Booking_ID),
    CONSTRAINT FK_Ticket_Admin FOREIGN KEY (Admin_ID) REFERENCES dbo.Admin (Admin_ID)
);
GO


CREATE TABLE dbo.Loyalty_Transaction (
    Transaction_ID    INT IDENTITY(1,1) NOT NULL,
    Home_owner_ID     INT               NOT NULL,
    Points_Earned     INT               NOT NULL CONSTRAINT DF_LT_Earned DEFAULT (0),
    Points_Redeemed   INT               NOT NULL CONSTRAINT DF_LT_Redeemed DEFAULT (0),
    Transaction_Type  VARCHAR(10)       NOT NULL,                  -- Earned / Redeemed / Reversed
    Transaction_Date  DATETIME2(0)      NOT NULL CONSTRAINT DF_LT_Date DEFAULT (SYSDATETIME()),
    Booking_ID        INT               NULL,                      -- [UI] "Leak repair completed · FX-26-01873"
    CONSTRAINT PK_Loyalty_Transaction PRIMARY KEY (Transaction_ID),
    CONSTRAINT CK_LT_Type CHECK (Transaction_Type IN ('Earned', 'Redeemed', 'Reversed')),
    CONSTRAINT CK_LT_Points CHECK (
           (Transaction_Type = 'Earned'   AND Points_Earned > 0 AND Points_Redeemed = 0)
        OR (Transaction_Type IN ('Redeemed', 'Reversed') AND Points_Redeemed > 0 AND Points_Earned = 0)),
    CONSTRAINT FK_LT_HO FOREIGN KEY (Home_owner_ID) REFERENCES dbo.Home_Owner (Home_owner_ID),
    CONSTRAINT FK_LT_Booking FOREIGN KEY (Booking_ID) REFERENCES dbo.Booking (Booking_ID)
);
CREATE INDEX IX_LT_HO ON dbo.Loyalty_Transaction (Home_owner_ID, Transaction_Date DESC);
GO

PRINT 'fixora_db: 12 tables created.';
GO
