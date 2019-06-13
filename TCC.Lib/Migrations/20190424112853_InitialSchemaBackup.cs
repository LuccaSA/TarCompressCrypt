using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TCC.Lib.Migrations
{
    public partial class InitialSchemaBackup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupJobs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartTime = table.Column<DateTime>(nullable: false),
                    Duration = table.Column<TimeSpan>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupBlockJobs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(nullable: false),
                    JobId1 = table.Column<int>(nullable: true),
                    BackupMode = table.Column<int>(nullable: false),
                    FullSourcePath = table.Column<string>(nullable: true),
                    StartTime = table.Column<DateTime>(nullable: false),
                    Duration = table.Column<TimeSpan>(nullable: false),
                    Size = table.Column<long>(nullable: false),
                    Success = table.Column<bool>(nullable: false),
                    Exception = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupBlockJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupBlockJobs_BackupJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "BackupJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BackupBlockJobs_BackupJobs_JobId1",
                        column: x => x.JobId1,
                        principalTable: "BackupJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupBlockJobs_JobId",
                table: "BackupBlockJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupBlockJobs_JobId1",
                table: "BackupBlockJobs",
                column: "JobId1");

            migrationBuilder.CreateIndex(
                name: "IX_BackupBlockJobs_StartTime",
                table: "BackupBlockJobs",
                column: "StartTime");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupBlockJobs");

            migrationBuilder.DropTable(
                name: "BackupJobs");
        }
    }
}
