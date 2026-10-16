/* ==========================================================================
   RaceDay - SQL Server database script (SSMS)
   Creates the full schema (6 tables) and seeds realistic sample data.
   Matches docs/ERD.png and the EF Core Code First model in Part2-API.
   WARNING: drops and recreates the RaceDayDb database if it already exists.
   Sample login password for ALL seeded users: Password123!
   ========================================================================== */
USE master;
GO
IF DB_ID(N'RaceDayDb') IS NOT NULL
BEGIN
    ALTER DATABASE RaceDayDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RaceDayDb;
END
GO
CREATE DATABASE RaceDayDb;
GO
USE RaceDayDb;
GO

/* ---------- 1. Roles ---------- */
CREATE TABLE Roles (
    RoleId   INT IDENTITY(1,1) NOT NULL,
    RoleName NVARCHAR(20)      NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);

/* ---------- 2. Users ---------- */
CREATE TABLE Users (
    UserId            INT IDENTITY(1,1) NOT NULL,
    RoleId            INT               NOT NULL,
    FirstName         NVARCHAR(50)      NOT NULL,
    LastName          NVARCHAR(50)      NOT NULL,
    Email             NVARCHAR(150)     NOT NULL,
    PasswordHash      NVARCHAR(500)     NOT NULL,
    PhoneNumber       NVARCHAR(20)      NULL,
    ProfilePictureUrl NVARCHAR(500)     NULL,   -- used in Part 3 (Azure Blob Storage)
    CreatedAt         DATETIME2         NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles (RoleId)
);

/* ---------- 3. Events ---------- */
CREATE TABLE Events (
    EventId        INT IDENTITY(1,1) NOT NULL,
    OrganiserId    INT               NOT NULL,
    Name           NVARCHAR(150)     NOT NULL,
    Description    NVARCHAR(1000)    NOT NULL,
    EventDate      DATETIME2         NOT NULL,
    Location       NVARCHAR(200)     NOT NULL,
    DistanceKm     DECIMAL(6,2)      NOT NULL,
    EventType      NVARCHAR(10)      NOT NULL,
    BannerImageUrl NVARCHAR(500)     NULL,      -- used in Part 3 (Azure Blob Storage)
    CreatedAt      DATETIME2         NOT NULL CONSTRAINT DF_Events_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Events PRIMARY KEY (EventId),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserId) REFERENCES Users (UserId),
    CONSTRAINT CK_Events_EventType CHECK ([EventType] IN ('Run','Walk','Cycle')),
    CONSTRAINT CK_Events_DistanceKm CHECK ([DistanceKm] > 0)
);

/* ---------- 4. Categories ---------- */
CREATE TABLE Categories (
    CategoryId   INT IDENTITY(1,1) NOT NULL,
    EventId      INT               NOT NULL,
    Name         NVARCHAR(100)     NOT NULL,
    CategoryType NVARCHAR(10)      NOT NULL,
    CONSTRAINT PK_Categories PRIMARY KEY (CategoryId),
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES Events (EventId) ON DELETE CASCADE,
    CONSTRAINT UQ_Categories_Event_Name UNIQUE (EventId, Name),
    CONSTRAINT CK_Categories_CategoryType CHECK ([CategoryType] IN ('Age','Distance'))
);

/* ---------- 5. Enrolments (links Participant + Event + Category) ---------- */
CREATE TABLE Enrolments (
    EnrolmentId INT IDENTITY(1,1) NOT NULL,
    UserId      INT               NOT NULL,
    EventId     INT               NOT NULL,
    CategoryId  INT               NOT NULL,
    EnrolledAt  DATETIME2         NOT NULL CONSTRAINT DF_Enrolments_EnrolledAt DEFAULT SYSUTCDATETIME(),
    Status      NVARCHAR(15)      NOT NULL CONSTRAINT DF_Enrolments_Status DEFAULT 'Confirmed',
    CONSTRAINT PK_Enrolments PRIMARY KEY (EnrolmentId),
    CONSTRAINT FK_Enrolments_Users      FOREIGN KEY (UserId)     REFERENCES Users (UserId),
    CONSTRAINT FK_Enrolments_Events     FOREIGN KEY (EventId)    REFERENCES Events (EventId) ON DELETE CASCADE,
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES Categories (CategoryId),
    CONSTRAINT UQ_Enrolments_User_Event UNIQUE (UserId, EventId),
    CONSTRAINT CK_Enrolments_Status CHECK ([Status] IN ('Confirmed','Pending'))
);

/* ---------- 6. Results (one result per enrolment) ---------- */
CREATE TABLE Results (
    ResultId       INT IDENTITY(1,1) NOT NULL,
    EnrolmentId    INT               NOT NULL,
    FinishTime     TIME              NOT NULL,
    FinishPosition INT               NOT NULL,
    RecordedAt     DATETIME2         NOT NULL CONSTRAINT DF_Results_RecordedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Results PRIMARY KEY (ResultId),
    CONSTRAINT UQ_Results_EnrolmentId UNIQUE (EnrolmentId),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES Enrolments (EnrolmentId) ON DELETE CASCADE,
    CONSTRAINT CK_Results_FinishPosition CHECK ([FinishPosition] > 0)
);
GO

/* ==========================  SEED DATA  ============================== */

INSERT INTO Roles (RoleName) VALUES ('Organiser'), ('Participant');   -- 1 = Organiser, 2 = Participant

-- 2 Organisers (UserId 1-2) and 2 Participants (UserId 3-4). Password for all: Password123!
INSERT INTO Users (RoleId, FirstName, LastName, Email, PasswordHash, PhoneNumber) VALUES
(1, 'Thandi', 'Mokoena',       'thandi.organiser@raceday.co.za',  'AQAAAAIAAYagAAAAEBvJ7gdrlp322Ah3Ye0iSgWQj9Gz61K8lnmzWIRPDc4u/SHMa+1BGzp6XGd6Ep9DFw==', '0821234567'),
(1, 'Pieter', 'van der Merwe', 'pieter.organiser@raceday.co.za', 'AQAAAAIAAYagAAAAELUBgjlCsEjBHHwc9UQeoZI4o7isToNxkHEPtAy3Wo9oKu1MYyKexUswVTwBynowxQ==', '0837654321'),
(2, 'Lerato', 'Dlamini',       'lerato.runner@raceday.co.za',     'AQAAAAIAAYagAAAAEPSXSFRS0aPOlXeDODtsbHfOrSFRCP06P5F2CXvtkLehmU5iwO1HhQEYWScR2U9tLg==', '0719876543'),
(2, 'Sipho',  'Naidoo',        'sipho.cyclist@raceday.co.za',     'AQAAAAIAAYagAAAAENlKZNh2UW0sRrWKcP6XBMlF8TqyvYQSIZw11yIziPH2c1jdIW35yYh9uxuI5INjHg==', NULL);

-- 3 Events (EventId 1-3). Event 1 is in the past so it can hold results.
INSERT INTO Events (OrganiserId, Name, Description, EventDate, Location, DistanceKm, EventType) VALUES
(1, 'Winelands Harvest Half Marathon',    'A scenic 21.1 km road run through the Paarl wine estates.',          '2026-09-12 06:30', 'Paarl, Western Cape',        21.10, 'Run'),
(1, 'Table Mountain Charity Walk',        'Family-friendly 5 km charity walk raising funds for local schools.', '2026-11-14 08:00', 'Cape Town, Western Cape',     5.00, 'Walk'),
(2, 'Stellenbosch Vineyard Cycle Classic','Rolling road cycle through the Stellenbosch winelands.',             '2026-12-05 07:00', 'Stellenbosch, Western Cape', 60.00, 'Cycle');

-- Categories for each event (CategoryId 1-9)
INSERT INTO Categories (EventId, Name, CategoryType) VALUES
(1, 'Under 20',     'Age'),       -- 1
(1, 'Senior 20-39', 'Age'),       -- 2
(1, 'Masters 40+',  'Age'),       -- 3
(2, 'Under 20',     'Age'),       -- 4
(2, 'Adult',        'Age'),       -- 5
(2, 'Senior 60+',   'Age'),       -- 6
(3, '30km Social',  'Distance'),  -- 7
(3, '60km Race',    'Distance'),  -- 8
(3, 'Masters 40+',  'Age');       -- 9

-- Sample enrolments (EnrolmentId 1-5)
INSERT INTO Enrolments (UserId, EventId, CategoryId, Status) VALUES
(3, 1, 2, 'Confirmed'),   -- 1 Lerato -> Half Marathon, Senior 20-39
(4, 1, 3, 'Confirmed'),   -- 2 Sipho  -> Half Marathon, Masters 40+
(3, 2, 5, 'Confirmed'),   -- 3 Lerato -> Charity Walk, Adult
(4, 2, 5, 'Pending'),     -- 4 Sipho  -> Charity Walk, Adult
(3, 3, 8, 'Pending');     -- 5 Lerato -> Cycle Classic, 60km Race

-- Results for the completed event (Event 1)
INSERT INTO Results (EnrolmentId, FinishTime, FinishPosition) VALUES
(1, '01:52:30', 1),
(2, '02:05:10', 2);
GO

/* Quick check */
SELECT 'Roles' AS TableName, COUNT(*) AS Total FROM Roles UNION ALL
SELECT 'Users', COUNT(*) FROM Users UNION ALL
SELECT 'Events', COUNT(*) FROM Events UNION ALL
SELECT 'Categories', COUNT(*) FROM Categories UNION ALL
SELECT 'Enrolments', COUNT(*) FROM Enrolments UNION ALL
SELECT 'Results', COUNT(*) FROM Results;
GO
