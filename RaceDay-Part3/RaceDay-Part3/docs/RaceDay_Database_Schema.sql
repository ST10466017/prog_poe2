/*
===============================================================================
 RaceDay System - Full Database Schema (SQL Server / SSMS)
 Matches ERD: RaceDay_ERD.png
 Entities: Users, Events, Categories, Enrolments, Results, Payments
===============================================================================
*/

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

-- =============================================================================
-- Drop tables if they exist (in FK-safe order) so the script re-runs cleanly
-- =============================================================================
IF OBJECT_ID('dbo.Payments', 'U') IS NOT NULL DROP TABLE dbo.Payments;
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- =============================================================================
-- Table: Users
-- Stores both Organisers and Participants, distinguished by Role.
-- =============================================================================
CREATE TABLE dbo.Users (
    user_id         INT             IDENTITY(1,1)   NOT NULL,
    full_name       NVARCHAR(100)   NOT NULL,
    email           NVARCHAR(150)   NOT NULL,
    password_hash   NVARCHAR(255)   NOT NULL,
    role            NVARCHAR(20)    NOT NULL DEFAULT ('Participant'),
    phone           NVARCHAR(20)    NULL,
    created_at      DATETIME        NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT PK_Users PRIMARY KEY (user_id),
    CONSTRAINT UQ_Users_Email UNIQUE (email),
    CONSTRAINT CK_Users_Role CHECK (role IN ('Organiser', 'Participant'))
);
GO

-- =============================================================================
-- Table: Events
-- One Organiser (Users) creates many Events. 1 -- * (Users -> Events)
-- =============================================================================
CREATE TABLE dbo.Events (
    event_id        INT             IDENTITY(1,1)   NOT NULL,
    organiser_id    INT             NOT NULL,
    event_name      NVARCHAR(150)   NOT NULL,
    event_date      DATE            NOT NULL,
    location        NVARCHAR(150)   NOT NULL,
    description     NVARCHAR(MAX)   NULL,
    distance_km     DECIMAL(6,2)    NOT NULL,
    event_type      NVARCHAR(10)    NOT NULL,
    status          NVARCHAR(20)    NOT NULL DEFAULT ('Upcoming'),
    created_at      DATETIME        NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT PK_Events PRIMARY KEY (event_id),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (organiser_id)
        REFERENCES dbo.Users (user_id),
    CONSTRAINT CK_Events_Status CHECK (status IN ('Upcoming', 'Completed', 'Cancelled')),
    CONSTRAINT CK_Events_Type CHECK (event_type IN ('Run', 'Walk', 'Cycle')),
    CONSTRAINT CK_Events_Distance CHECK (distance_km > 0)
);
GO

-- =============================================================================
-- Table: Categories
-- One Event has many Categories - age (Under 20, Senior) or distance (10km, 21km). 1 -- * (Events -> Categories)
-- =============================================================================
CREATE TABLE dbo.Categories (
    category_id      INT            IDENTITY(1,1)   NOT NULL,
    event_id         INT            NOT NULL,
    category_name    NVARCHAR(50)   NOT NULL,
    category_type    NVARCHAR(10)   NOT NULL DEFAULT ('Distance'),
    distance_km      DECIMAL(5,2)   NULL,
    entry_fee        DECIMAL(8,2)   NOT NULL DEFAULT (0),
    max_participants INT            NOT NULL DEFAULT (100),
    CONSTRAINT PK_Categories PRIMARY KEY (category_id),
    CONSTRAINT FK_Categories_Event FOREIGN KEY (event_id)
        REFERENCES dbo.Events (event_id),
    CONSTRAINT UQ_Categories_Event_Name UNIQUE (event_id, category_name),
    CONSTRAINT CK_Categories_Distance CHECK (distance_km IS NULL OR distance_km > 0),
    CONSTRAINT CK_Categories_Type CHECK (category_type IN ('Age', 'Distance')),
    CONSTRAINT CK_Categories_MaxParticipants CHECK (max_participants > 0)
);
GO

-- =============================================================================
-- Table: Enrolments
-- One Participant (Users) enrols in many Categories, one Category has many
-- Enrolments. 1 -- * (Users -> Enrolments) and 1 -- * (Categories -> Enrolments)
-- =============================================================================
CREATE TABLE dbo.Enrolments (
    enrolment_id     INT            IDENTITY(1,1)   NOT NULL,
    participant_id   INT            NOT NULL,
    category_id      INT            NOT NULL,
    enrolment_date   DATETIME       NOT NULL DEFAULT (GETDATE()),
    race_number      NVARCHAR(10)   NULL,
    status           NVARCHAR(20)   NOT NULL DEFAULT ('Confirmed'),
    CONSTRAINT PK_Enrolments PRIMARY KEY (enrolment_id),
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (participant_id)
        REFERENCES dbo.Users (user_id),
    CONSTRAINT FK_Enrolments_Category FOREIGN KEY (category_id)
        REFERENCES dbo.Categories (category_id),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (participant_id, category_id),
    CONSTRAINT CK_Enrolments_Status CHECK (status IN ('Pending', 'Confirmed', 'Cancelled'))
);
GO

-- =============================================================================
-- Table: Results
-- One Enrolment produces exactly one Result. 1 -- 1 (Enrolments -> Results)
-- =============================================================================
CREATE TABLE dbo.Results (
    result_id      INT            IDENTITY(1,1)   NOT NULL,
    enrolment_id   INT            NOT NULL,
    finish_time    TIME           NULL,
    position       INT            NULL,
    status         NVARCHAR(20)   NOT NULL DEFAULT ('Not Started'),
    recorded_at    DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT PK_Results PRIMARY KEY (result_id),
    CONSTRAINT FK_Results_Enrolment FOREIGN KEY (enrolment_id)
        REFERENCES dbo.Enrolments (enrolment_id),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (enrolment_id),
    CONSTRAINT CK_Results_Status CHECK (status IN ('Not Started', 'DNS', 'DNF', 'Finished'))
);
GO

-- =============================================================================
-- Table: Payments
-- One Enrolment is covered by exactly one Payment. 1 -- 1 (Enrolments -> Payments)
-- =============================================================================
CREATE TABLE dbo.Payments (
    payment_id       INT            IDENTITY(1,1)   NOT NULL,
    enrolment_id     INT            NOT NULL,
    amount           DECIMAL(8,2)   NOT NULL,
    payment_method   NVARCHAR(30)   NOT NULL,
    payment_status   NVARCHAR(20)   NOT NULL DEFAULT ('Pending'),
    paid_at          DATETIME       NULL,
    CONSTRAINT PK_Payments PRIMARY KEY (payment_id),
    CONSTRAINT FK_Payments_Enrolment FOREIGN KEY (enrolment_id)
        REFERENCES dbo.Enrolments (enrolment_id),
    CONSTRAINT UQ_Payments_Enrolment UNIQUE (enrolment_id),
    CONSTRAINT CK_Payments_Method CHECK (payment_method IN ('Card', 'EFT', 'Cash')),
    CONSTRAINT CK_Payments_Status CHECK (payment_status IN ('Pending', 'Paid', 'Refunded')),
    CONSTRAINT CK_Payments_Amount CHECK (amount >= 0)
);
GO

/*
===============================================================================
 SEED DATA
===============================================================================
*/

-- Users: 2 Organisers, 2 Participants (hashes are placeholders - real accounts
-- are created through POST /api/auth/register so passwords are hashed by the API)
INSERT INTO dbo.Users (full_name, email, password_hash, role, phone) VALUES
('Sarah Naidoo',   'sarah.naidoo@raceday.co.za',   'HASH_PLACEHOLDER_1', 'Organiser',   '0821234567'),
('Thabo Mokoena',  'thabo.mokoena@raceday.co.za',  'HASH_PLACEHOLDER_2', 'Organiser',   '0827654321'),
('Emma van Wyk',   'emma.vanwyk@example.com',      'HASH_PLACEHOLDER_3', 'Participant', '0731122334'),
('Liam Petersen',  'liam.petersen@example.com',    'HASH_PLACEHOLDER_4', 'Participant', '0739988776');
GO

-- Events: 3 events (run, run, cycle) owned by the two Organisers
INSERT INTO dbo.Events (organiser_id, event_name, event_date, location, description, distance_km, event_type, status) VALUES
(1, 'Cape Town Coastal Marathon', '2026-11-15', 'Sea Point, Cape Town',      'A scenic coastal marathon along the Atlantic seaboard.', 42.20, 'Run',   'Upcoming'),
(1, 'Table Mountain Trail Run',   '2026-10-03', 'Table Mountain, Cape Town', 'A challenging trail run with mountain views.',           15.00, 'Run',   'Completed'),
(2, 'Sandton Cycle Classic',      '2026-12-20', 'Sandton, Johannesburg',     'A fast road cycle through the northern suburbs.',       40.00, 'Cycle', 'Upcoming');
GO

-- Categories: distance and age categories for each event
INSERT INTO dbo.Categories (event_id, category_name, category_type, distance_km, entry_fee, max_participants) VALUES
(1, '10km',       'Distance', 10.00, 150.00, 500),   -- category_id 1
(1, '21km',       'Distance', 21.10, 250.00, 300),   -- category_id 2
(1, '42km',       'Distance', 42.20, 350.00, 200),   -- category_id 3
(2, '15km Trail', 'Distance', 15.00, 200.00, 150),   -- category_id 4
(3, 'Under 20',   'Age',      NULL,  120.00, 300),   -- category_id 5
(3, 'Senior',     'Age',      NULL,  180.00, 300),   -- category_id 6
(3, '40km',       'Distance', 40.00, 220.00, 400);   -- category_id 7
GO

-- Enrolments (one category per participant per event)
INSERT INTO dbo.Enrolments (participant_id, category_id, race_number, status) VALUES
(3, 1, 'E001-0001', 'Confirmed'),  -- Emma  -> Coastal Marathon 10km
(3, 7, 'E003-0002', 'Confirmed'),  -- Emma  -> Cycle Classic 40km
(4, 2, 'E001-0003', 'Confirmed'),  -- Liam  -> Coastal Marathon 21km
(4, 4, 'E002-0004', 'Confirmed');  -- Liam  -> Table Mountain 15km (completed event)
GO

-- Results: only for the completed event
INSERT INTO dbo.Results (enrolment_id, finish_time, position, status) VALUES
(4, '01:32:45', 3, 'Finished');
GO

-- Payments: one per enrolment
INSERT INTO dbo.Payments (enrolment_id, amount, payment_method, payment_status, paid_at) VALUES
(1, 150.00, 'Card', 'Paid', '2026-08-01 09:15:00'),
(2, 220.00, 'EFT',  'Paid', '2026-08-02 14:20:00'),
(3, 250.00, 'Card', 'Paid', '2026-08-03 10:05:00'),
(4, 200.00, 'EFT',  'Paid', '2026-08-04 11:30:00');
GO

-- SELECT * FROM dbo.Users;      SELECT * FROM dbo.Events;   SELECT * FROM dbo.Categories;
-- SELECT * FROM dbo.Enrolments; SELECT * FROM dbo.Results;  SELECT * FROM dbo.Payments;
