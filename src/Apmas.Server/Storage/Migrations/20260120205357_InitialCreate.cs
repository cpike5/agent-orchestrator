using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apmas.Server.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    SubagentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SpawnedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TimeoutAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Artifacts = table.Column<string>(type: "TEXT", nullable: false),
                    Dependencies = table.Column<string>(type: "TEXT", nullable: false),
                    LastMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    EstimatedContextUsage = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Checkpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgentRole = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Progress = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedItems = table.Column<string>(type: "TEXT", nullable: false),
                    PendingItems = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveFiles = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    EstimatedContextUsage = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checkpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    From = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    To = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Artifacts = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentStates_Role",
                table: "AgentStates",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_AgentStates_Status",
                table: "AgentStates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_AgentRole",
                table: "Checkpoints",
                column: "AgentRole");

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_CreatedAt",
                table: "Checkpoints",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_From",
                table: "Messages",
                column: "From");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Timestamp",
                table: "Messages",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_To",
                table: "Messages",
                column: "To");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentStates");

            migrationBuilder.DropTable(
                name: "Checkpoints");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "ProjectStates");
        }
    }
}
