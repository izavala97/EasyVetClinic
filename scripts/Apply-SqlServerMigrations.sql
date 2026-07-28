IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Clinics] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [LogoUrl] nvarchar(max) NOT NULL,
        [VeterinarianName] nvarchar(200) NOT NULL,
        [VeterinarianTitles] nvarchar(300) NOT NULL,
        [VeterinarianLicenseNumber] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Clinics] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [ClinicUsers] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(450) NOT NULL,
        [EntraObjectId] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Role] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ClinicUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClinicUsers_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Guardians] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(450) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Phone] nvarchar(50) NOT NULL,
        [AlternatePhone] nvarchar(50) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [IdentityType] nvarchar(100) NOT NULL,
        [IdentityNumber] nvarchar(100) NOT NULL,
        [IdentityDocumentUrl] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Guardians] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Guardians_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(450) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [StockOnHand] int NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Patients] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(450) NOT NULL,
        [GuardianId] nvarchar(450) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Species] nvarchar(max) NOT NULL,
        [Breed] nvarchar(max) NOT NULL,
        [Sex] nvarchar(max) NOT NULL,
        [Weight] nvarchar(max) NOT NULL,
        [Color] nvarchar(max) NOT NULL,
        [Allergies] nvarchar(max) NOT NULL,
        [DistinguishingFeatures] nvarchar(max) NOT NULL,
        [DateOfBirth] date NULL,
        [PhotoUrl] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [LastVisit] date NULL,
        CONSTRAINT [PK_Patients] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Patients_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Patients_Guardians_GuardianId] FOREIGN KEY ([GuardianId]) REFERENCES [Guardians] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Appointments] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(450) NOT NULL,
        [PatientId] nvarchar(450) NOT NULL,
        [StartsAt] datetimeoffset NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [ClinicianName] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Appointments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Appointments_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([Id]),
        CONSTRAINT [FK_Appointments_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Consultations] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(450) NOT NULL,
        [PatientId] nvarchar(450) NOT NULL,
        [ClinicianName] nvarchar(max) NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [ChiefComplaint] nvarchar(max) NOT NULL,
        [ClinicalNotes] nvarchar(max) NOT NULL,
        [Diagnosis] nvarchar(max) NOT NULL,
        [Instructions] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Consultations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Consultations_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Sales] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(450) NOT NULL,
        [PatientId] nvarchar(450) NULL,
        [CompletedAt] datetimeoffset NOT NULL,
        [PaymentMethod] nvarchar(50) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_Sales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sales_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([Id]),
        CONSTRAINT [FK_Sales_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [VaccinationRecords] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(450) NOT NULL,
        [PatientId] nvarchar(450) NOT NULL,
        [VaccineName] nvarchar(200) NOT NULL,
        [AdministeredOn] date NOT NULL,
        [NextDueOn] date NULL,
        [LotNumber] nvarchar(100) NOT NULL,
        [VeterinarianName] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_VaccinationRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VaccinationRecords_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [WeightRecords] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(450) NOT NULL,
        [PatientId] nvarchar(450) NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [MeasuredOn] date NOT NULL,
        [RecordedBy] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_WeightRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WeightRecords_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Prescriptions] (
        [Id] nvarchar(450) NOT NULL,
        [ClinicId] nvarchar(max) NOT NULL,
        [ConsultationId] nvarchar(450) NOT NULL,
        [DiagnosisSnapshot] nvarchar(max) NOT NULL,
        [Instructions] nvarchar(max) NOT NULL,
        [IsFinalized] bit NOT NULL,
        [FinalizedAt] datetimeoffset NULL,
        [LastUpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Prescriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Prescriptions_Consultations_ConsultationId] FOREIGN KEY ([ConsultationId]) REFERENCES [Consultations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [SaleLines] (
        [Id] nvarchar(450) NOT NULL,
        [SaleId] nvarchar(450) NOT NULL,
        [ProductId] nvarchar(max) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Quantity] int NOT NULL,
        CONSTRAINT [PK_SaleLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SaleLines_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE TABLE [PrescriptionItems] (
        [Id] nvarchar(450) NOT NULL,
        [PrescriptionId] nvarchar(450) NOT NULL,
        [MedicationName] nvarchar(200) NOT NULL,
        [Presentation] nvarchar(100) NOT NULL,
        [Concentration] nvarchar(100) NOT NULL,
        [DosageDirections] nvarchar(max) NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_PrescriptionItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PrescriptionItems_Prescriptions_PrescriptionId] FOREIGN KEY ([PrescriptionId]) REFERENCES [Prescriptions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Appointments_ClinicId_StartsAt] ON [Appointments] ([ClinicId], [StartsAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Appointments_PatientId] ON [Appointments] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClinicUsers_ClinicId_EntraObjectId] ON [ClinicUsers] ([ClinicId], [EntraObjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Consultations_ClinicId_PatientId] ON [Consultations] ([ClinicId], [PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Consultations_PatientId] ON [Consultations] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Guardians_ClinicId_Phone] ON [Guardians] ([ClinicId], [Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Patients_ClinicId_Name] ON [Patients] ([ClinicId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Patients_GuardianId] ON [Patients] ([GuardianId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_PrescriptionItems_PrescriptionId_SortOrder] ON [PrescriptionItems] ([PrescriptionId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Prescriptions_ConsultationId] ON [Prescriptions] ([ConsultationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_ClinicId_Name] ON [Products] ([ClinicId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_SaleLines_SaleId] ON [SaleLines] ([SaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Sales_ClinicId_CompletedAt] ON [Sales] ([ClinicId], [CompletedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Sales_PatientId] ON [Sales] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_VaccinationRecords_ClinicId_PatientId_AdministeredOn] ON [VaccinationRecords] ([ClinicId], [PatientId], [AdministeredOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_VaccinationRecords_PatientId] ON [VaccinationRecords] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_WeightRecords_ClinicId_PatientId_MeasuredOn] ON [WeightRecords] ([ClinicId], [PatientId], [MeasuredOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_WeightRecords_PatientId] ON [WeightRecords] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728231115_InitialSqlServer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728231115_InitialSqlServer', N'10.0.0');
END;

COMMIT;
GO

