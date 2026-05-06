using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyLeasing.API.Migrations.PropertyLeasingDb
{
    /// <inheritdoc />
    public partial class AddLeaseLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeaseApplicationLog')
BEGIN
    CREATE TABLE [LeaseApplicationLog] (
        [LogID]           INT           NOT NULL IDENTITY(1,1),
        [ApplicationID]   INT           NOT NULL,
        [Status]          NVARCHAR(50)  NOT NULL,
        [ChangedByUserID] INT           NOT NULL,
        [CreatedAt]       DATETIME      NOT NULL,
        CONSTRAINT [PK_LeaseApplicationLog] PRIMARY KEY ([LogID]),
        CONSTRAINT [FK_LeaseApplicationLog_Application]
            FOREIGN KEY ([ApplicationID]) REFERENCES [LeaseApplication] ([ApplicationID]),
        CONSTRAINT [FK_LeaseApplicationLog_User]
            FOREIGN KEY ([ChangedByUserID]) REFERENCES [User] ([UserID])
    );
    CREATE INDEX [IX_LeaseApplicationLog_ApplicationID]  ON [LeaseApplicationLog] ([ApplicationID]);
    CREATE INDEX [IX_LeaseApplicationLog_ChangedByUserID] ON [LeaseApplicationLog] ([ChangedByUserID]);
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeaseLog')
BEGIN
    CREATE TABLE [LeaseLog] (
        [LogID]           INT           NOT NULL IDENTITY(1,1),
        [LeaseID]         INT           NOT NULL,
        [Status]          NVARCHAR(50)  NOT NULL,
        [ChangedByUserID] INT           NOT NULL,
        [Notes]           NVARCHAR(500) NULL,
        [CreatedAt]       DATETIME      NOT NULL,
        CONSTRAINT [PK_LeaseLog] PRIMARY KEY ([LogID]),
        CONSTRAINT [FK_LeaseLog_Lease]
            FOREIGN KEY ([LeaseID]) REFERENCES [Lease] ([LeaseID]),
        CONSTRAINT [FK_LeaseLog_User]
            FOREIGN KEY ([ChangedByUserID]) REFERENCES [User] ([UserID])
    );
    CREATE INDEX [IX_LeaseLog_LeaseID]         ON [LeaseLog] ([LeaseID]);
    CREATE INDEX [IX_LeaseLog_ChangedByUserID] ON [LeaseLog] ([ChangedByUserID]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [LeaseApplicationLog]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [LeaseLog]");
        }
    }
}
