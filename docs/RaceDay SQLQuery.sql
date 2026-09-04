USE master;
IF DB_ID('RaceDayDB') IS NOT NULL
BEGIN
    ALTER DATABASE RaceDayDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RaceDayDB;
END
GO

CREATE DATABASE RaceDayDB;
GO

USE RaceDayDB;
GO


CREATE TABLE Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(100)   NOT NULL,
    Email           NVARCHAR(150)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(256)   NOT NULL,
    Role            NVARCHAR(20)    NOT NULL
                        CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant')),
    PhoneNumber     NVARCHAR(20)    NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);
GO


CREATE TABLE Events (
    EventId         INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserId     INT             NOT NULL,
    Name            NVARCHAR(150)   NOT NULL,
    Description     NVARCHAR(1000)  NULL,
    EventType       NVARCHAR(20)    NOT NULL
                        CONSTRAINT CK_Events_Type CHECK (EventType IN ('Run', 'Walk', 'Cycle')),
    EventDate       DATETIME2       NOT NULL,
    Location        NVARCHAR(150)   NOT NULL,
    Province        NVARCHAR(50)    NOT NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Upcoming'
                        CONSTRAINT CK_Events_Status CHECK (Status IN ('Upcoming', 'Completed', 'Cancelled')),
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserId) REFERENCES Users(UserId)
);
GO


CREATE TABLE Categories (
    CategoryId      INT IDENTITY(1,1) PRIMARY KEY,
    EventId         INT             NOT NULL,
    Name            NVARCHAR(100)   NOT NULL,
    DistanceKm      DECIMAL(6,2)    NOT NULL,
    EntryFee        DECIMAL(8,2)    NOT NULL DEFAULT 0,
    MaxParticipants INT             NOT NULL,
    MinAge          INT             NOT NULL DEFAULT 0,
    CONSTRAINT FK_Categories_Event FOREIGN KEY (EventId) REFERENCES Events(EventId) ON DELETE CASCADE
);
GO


CREATE TABLE RouteInfo (
    RouteId         INT IDENTITY(1,1) PRIMARY KEY,
    EventId         INT             NOT NULL UNIQUE,
    RouteMapUrl     NVARCHAR(300)   NULL,
    ElevationGainM  INT             NULL,
    StartLatitude   DECIMAL(9,6)    NULL,
    StartLongitude  DECIMAL(9,6)    NULL,
    WeatherNotes    NVARCHAR(500)   NULL,
    CONSTRAINT FK_RouteInfo_Event FOREIGN KEY (EventId) REFERENCES Events(EventId) ON DELETE CASCADE
);
GO


CREATE TABLE Enrolments (
    EnrolmentId     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantId   INT             NOT NULL,
    CategoryId      INT             NOT NULL,
    EnrolmentDate   DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Confirmed'
                        CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled')),
    RaceNumber      NVARCHAR(20)    NULL,
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantId) REFERENCES Users(UserId),
    CONSTRAINT FK_Enrolments_Category FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantId, CategoryId)
);
GO


CREATE TABLE Results (
    ResultId                INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId             INT             NOT NULL UNIQUE,
    CapturedByOrganiserId   INT             NOT NULL,
    FinishTime              TIME            NULL,
    Position                INT             NULL,
    Status                  NVARCHAR(20)    NOT NULL DEFAULT 'Finished'
                                CONSTRAINT CK_Results_Status CHECK (Status IN ('Finished', 'DNF', 'DSQ')),
    CapturedAt              DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Results_Enrolment FOREIGN KEY (EnrolmentId) REFERENCES Enrolments(EnrolmentId) ON DELETE CASCADE,
    CONSTRAINT FK_Results_Organiser FOREIGN KEY (CapturedByOrganiserId) REFERENCES Users(UserId)
);
GO

-- Organisers (2)
INSERT INTO Users (FullName, Email, PasswordHash, Role, PhoneNumber) VALUES
('Thandiwe Nkosi', 'thandiwe.nkosi@raceday.co.za', 'HASHED_PASSWORD_1', 'Organiser', '0821234567'),
('Johan van der Merwe', 'johan.vdm@raceday.co.za', 'HASHED_PASSWORD_2', 'Organiser', '0837654321');

-- Participants (2)
INSERT INTO Users (FullName, Email, PasswordHash, Role, PhoneNumber) VALUES
('Lindiwe Dube', 'lindiwe.dube@example.com', 'HASHED_PASSWORD_3', 'Participant', '0731112222'),
('Ryan Botha', 'ryan.botha@example.com', 'HASHED_PASSWORD_4', 'Participant', '0723334444');

-- Events (3)
INSERT INTO Events (OrganiserId, Name, Description, EventType, EventDate, Location, Province, Status) VALUES
(1, 'Benoni Park Run Challenge', 'A community 10km run around Benoni Lake.', 'Run', '2026-10-10 07:00', 'Benoni Lake', 'Gauteng', 'Upcoming'),
(1, 'Gauteng Charity Cycle Tour', 'Charity cycling event supporting local schools.', 'Cycle', '2026-11-14 06:30', 'Fourways', 'Gauteng', 'Upcoming'),
(2, 'Cape Peninsula Walkathon', 'Scenic community walk along the Cape coastline.', 'Walk', '2026-09-20 08:00', 'Sea Point', 'Western Cape', 'Upcoming');

-- Categories (multiple per event)
INSERT INTO Categories (EventId, Name, DistanceKm, EntryFee, MaxParticipants, MinAge) VALUES
(1, '10km Run', 10.00, 150.00, 500, 12),
(1, '5km Fun Run', 5.00, 80.00, 800, 6),
(2, '40km Road Cycle', 40.00, 250.00, 300, 16),
(2, '80km Road Cycle', 80.00, 350.00, 200, 18),
(3, '5km Walk', 5.00, 50.00, 1000, 5);

-- RouteInfo (one per event)
INSERT INTO RouteInfo (EventId, RouteMapUrl, ElevationGainM, StartLatitude, StartLongitude, WeatherNotes) VALUES
(1, 'https://maps.raceday.co.za/routes/benoni-park-run', 45, -26.1885, 28.3200, 'Mild morning temperatures expected, light wind.'),
(2, 'https://maps.raceday.co.za/routes/gauteng-charity-cycle', 210, -26.0167, 28.0100, 'Check for afternoon thunderstorms in summer.'),
(3, 'https://maps.raceday.co.za/routes/cape-peninsula-walk', 15, -33.9150, 18.3850, 'Coastal wind can be strong - bring a windbreaker.');

-- Enrolments (sample)
INSERT INTO Enrolments (ParticipantId, CategoryId, Status, RaceNumber) VALUES
(3, 1, 'Confirmed', 'B-1001'),
(3, 3, 'Confirmed', 'B-1002'),
(4, 2, 'Confirmed', 'B-1003'),
(4, 5, 'Confirmed', 'B-1004');

-- Results (sample, for a completed-style test)
INSERT INTO Results (EnrolmentId, CapturedByOrganiserId, FinishTime, Position, Status) VALUES
(1, 1, '00:48:32', 1, 'Finished'),
(3, 1, '00:26:15', 4, 'Finished');
GO

