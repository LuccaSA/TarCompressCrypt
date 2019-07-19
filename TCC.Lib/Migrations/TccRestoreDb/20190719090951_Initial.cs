using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TCC.Lib.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestoreDestinations",
                columns: table => new
                {
                    Id = table.Column<int>()
                        .Annotation("Sqlite:Autoincrement", true),
                    FullDestinationPath = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreDestinations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestoreJobs",
                columns: table => new
                {
                    Id = table.Column<int>()
                        .Annotation("Sqlite:Autoincrement", true),
                    StartTime = table.Column<DateTime>(),
                    Duration = table.Column<TimeSpan>()
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestoreBlockJobs",
                columns: table => new
                {
                    Id = table.Column<int>()
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(),
                    BackupMode = table.Column<int>(),
                    RestoreDestinationId = table.Column<int>(),
                    StartTime = table.Column<DateTime>(),
                    Duration = table.Column<TimeSpan>(),
                    Size = table.Column<long>(),
                    Success = table.Column<bool>(),
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
                        name: "FK_RestoreBlockJobs_RestoreDestinations_RestoreDestinationId",
                        column: x => x.RestoreDestinationId,
                        principalTable: "RestoreDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestoreBlockJobs_JobId",
                table: "RestoreBlockJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreBlockJobs_RestoreDestinationId",
                table: "RestoreBlockJobs",
                column: "RestoreDestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreBlockJobs_StartTime",
                table: "RestoreBlockJobs",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreDestinations_FullDestinationPath",
                table: "RestoreDestinations",
                column: "FullDestinationPath",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestoreBlockJobs");

            migrationBuilder.DropTable(
                name: "RestoreJobs");

            migrationBuilder.DropTable(
                name: "RestoreDestinations");
        }
    }
}
