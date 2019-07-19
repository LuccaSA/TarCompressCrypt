using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TCC.Lib.Migrations.TccBackupDb
{
    public partial class Initial : Migration
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
                name: "BackupSources",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullSourcePath = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupBlockJobs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(nullable: false),
                    BackupMode = table.Column<int>(nullable: false),
                    BackupSourceId = table.Column<int>(nullable: false),
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
                        name: "FK_BackupBlockJobs_BackupSources_BackupSourceId",
                        column: x => x.BackupSourceId,
                        principalTable: "BackupSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BackupBlockJobs_BackupJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "BackupJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupBlockJobs_BackupSourceId",
                table: "BackupBlockJobs",
                column: "BackupSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupBlockJobs_JobId",
                table: "BackupBlockJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupBlockJobs_StartTime",
                table: "BackupBlockJobs",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_BackupSources_FullSourcePath",
                table: "BackupSources",
                column: "FullSourcePath",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupBlockJobs");

            migrationBuilder.DropTable(
                name: "BackupSources");

            migrationBuilder.DropTable(
                name: "BackupJobs");
        }
    }
}
