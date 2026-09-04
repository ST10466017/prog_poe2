
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

IF OBJECT_ID('dbo.Payments', 'U') IS NOT NULL DROP TABLE dbo.Payments;
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

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

CREATE TABLE dbo.Events (
    event_id        INT             IDENTITY(1,1)   NOT NULL,
    organiser_id    INT             NOT NULL,
    event_name      NVARCHAR(150)   NOT NULL,
    event_date      DATE            NOT NULL,
    location        NVARCHAR(150)   NOT NULL,
    description     NVARCHAR(MAX)   NULL,
    status          NVARCHAR(20)    NOT NULL DEFAULT ('Upcoming'),
    created_at      DATETIME        NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT PK_Events PRIMARY KEY (event_id),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (organiser_id)
        REFERENCES dbo.Users (user_id),
    CONSTRAINT CK_Events_Status CHECK (status IN ('Upcoming', 'Completed', 'Cancelled'))
);
GO

CREATE TABLE dbo.Categories (
    category_id      INT            IDENTITY(1,1)   NOT NULL,
    event_id         INT            NOT NULL,
    category_name    NVARCHAR(50)   NOT NULL,
    distance_km      DECIMAL(5,2)   NOT NULL,
    entry_fee        DECIMAL(8,2)   NOT NULL DEFAULT (0),
    max_participants INT            NOT NULL DEFAULT (100),
    CONSTRAINT PK_Categories PRIMARY KEY (category_id),
    CONSTRAINT FK_Categories_Event FOREIGN KEY (event_id)
        REFERENCES dbo.Events (event_id),
    CONSTRAINT UQ_Categories_Event_Name UNIQUE (event_id, category_name),
    CONSTRAINT CK_Categories_Distance CHECK (distance_km > 0),
    CONSTRAINT CK_Categories_MaxParticipants CHECK (max_participants > 0)
);
GO


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

INSERT INTO dbo.Users (full_name, email, password_hash, role, phone) VALUES
('Sarah Naidoo',   'sarah.naidoo@raceday.co.za',   'HASH_PLACEHOLDER_1', 'Organiser',   '0821234567'),
('Thabo Mokoena',  'thabo.mokoena@raceday.co.za',  'HASH_PLACEHOLDER_2', 'Organiser',   '0827654321'),
('Emma van Wyk',   'emma.vanwyk@example.com',      'HASH_PLACEHOLDER_3', 'Participant', '0731122334'),
('Liam Petersen',  'liam.petersen@example.com',    'HASH_PLACEHOLDER_4', 'Participant', '0739988776');
GO

INSERT INTO dbo.Events (organiser_id, event_name, event_date, location, description, status) VALUES
(1, 'Cape Town Coastal Marathon', '2026-11-15', 'Sea Point, Cape Town',    'A scenic coastal marathon along the Atlantic seaboard.', 'Upcoming'),
(1, 'Table Mountain Trail Run',   '2026-10-03', 'Table Mountain, Cape Town','A challenging trail run with mountain views.',           'Upcoming'),
(2, 'Johannesburg City Fun Run',  '2026-09-20', 'Sandton, Johannesburg',   'A family-friendly fun run through the city.',            'Upcoming');
GO

INSERT INTO dbo.Categories (event_id, category_name, distance_km, entry_fee, max_participants) VALUES
(1, '10km',  10.00, 150.00, 500),
(1, '21km',  21.10, 250.00, 300),
(1, '42km',  42.20, 350.00, 200),
(2, '15km Trail', 15.00, 200.00, 150),
(3, '5km Fun Run', 5.00,  80.00, 1000),
(3, '10km',  10.00, 120.00, 500);
GO

INSERT INTO dbo.Enrolments (participant_id, category_id, race_number, status) VALUES
(3, 1, 'CT-1001', 'Confirmed'),  -- Emma -> CT Marathon 10km
(3, 5, 'JHB-2001', 'Confirmed'), -- Emma -> JHB Fun Run 5km
(4, 2, 'CT-1002', 'Confirmed'),  -- Liam -> CT Marathon 21km
(4, 4, 'TM-3001', 'Pending');    -- Liam -> Table Mountain Trail 15km
GO

INSERT INTO dbo.Results (enrolment_id, finish_time, position, status) VALUES
(1, '00:52:14', 12, 'Finished');
GO

INSERT INTO dbo.Payments (enrolment_id, amount, payment_method, payment_status, paid_at) VALUES
(1, 150.00, 'Card', 'Paid', '2026-08-01 09:15:00'),
(2, 80.00,  'EFT',  'Paid', '2026-08-02 14:20:00'),
(3, 250.00, 'Card', 'Paid', '2026-08-03 10:05:00'),
(4, 200.00, 'Cash', 'Pending', NULL);
GO

SELECT * FROM dbo.Users;
SELECT * FROM dbo.Events;
SELECT * FROM dbo.Categories;
SELECT * FROM dbo.Enrolments;
SELECT * FROM dbo.Results;
SELECT * FROM dbo.Payments;
