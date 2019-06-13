using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TCC.Lib.Migrations.TccRestoreDb
{
    public partial class InitialSchemaRestore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestoreJobs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartTime = table.Column<DateTime>(nullable: false),
                    Duration = table.Column<TimeSpan>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestoreBlockJobs",
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
                    table.PrimaryKey("PK_RestoreBlockJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoreBlockJobs_RestoreJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "RestoreJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RestoreBlockJobs_RestoreJobs_JobId1",
                        column: x => x.JobId1,
                        principalTable: "RestoreJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestoreBlockJobs_JobId",
                table: "RestoreBlockJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreBlockJobs_JobId1",
                table: "RestoreBlockJobs",
                column: "JobId1");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreBlockJobs_StartTime",
                table: "RestoreBlockJobs",
                column: "StartTime");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestoreBlockJobs");

            migrationBuilder.DropTable(
                name: "RestoreJobs");
        }
    }
}
