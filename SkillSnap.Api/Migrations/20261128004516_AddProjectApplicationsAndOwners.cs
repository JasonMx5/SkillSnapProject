using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillSnap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectApplicationsAndOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExperienceSummary",
                table: "ProjectEnrollments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsExperience",
                table: "ProjectEnrollments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "PortfolioUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectApplicationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: false),
                    StorageFileName = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectApplicationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectApplicationDocuments_ProjectEnrollments_UserId_ProjectId",
                        columns: x => new { x.UserId, x.ProjectId },
                        principalTable: "ProjectEnrollments",
                        principalColumns: new[] { "UserId", "ProjectId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioUsers_OwnerUserId",
                table: "PortfolioUsers",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectApplicationDocuments_ProjectId_UserId",
                table: "ProjectApplicationDocuments",
                columns: new[] { "ProjectId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectApplicationDocuments_UserId_ProjectId",
                table: "ProjectApplicationDocuments",
                columns: new[] { "UserId", "ProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioUsers_AspNetUsers_OwnerUserId",
                table: "PortfolioUsers",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioUsers_AspNetUsers_OwnerUserId",
                table: "PortfolioUsers");

            migrationBuilder.DropTable(
                name: "ProjectApplicationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioUsers_OwnerUserId",
                table: "PortfolioUsers");

            migrationBuilder.DropColumn(
                name: "ExperienceSummary",
                table: "ProjectEnrollments");

            migrationBuilder.DropColumn(
                name: "YearsExperience",
                table: "ProjectEnrollments");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "PortfolioUsers");
        }
    }
}
