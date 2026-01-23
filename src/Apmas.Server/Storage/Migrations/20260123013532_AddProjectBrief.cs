using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apmas.Server.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBrief : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Checkpoints_AgentRole",
                table: "Checkpoints");

            migrationBuilder.DropIndex(
                name: "IX_Checkpoints_CreatedAt",
                table: "Checkpoints");

            migrationBuilder.DropIndex(
                name: "IX_AgentStates_Role",
                table: "AgentStates");

            migrationBuilder.DropIndex(
                name: "IX_AgentStates_Status",
                table: "AgentStates");

            migrationBuilder.DropColumn(
                name: "ActiveFiles",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "CompletedItems",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "PendingItems",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "Artifacts",
                table: "AgentStates");

            migrationBuilder.DropColumn(
                name: "Dependencies",
                table: "AgentStates");

            migrationBuilder.RenameColumn(
                name: "Metadata",
                table: "Messages",
                newName: "MetadataJson");

            migrationBuilder.RenameColumn(
                name: "Artifacts",
                table: "Messages",
                newName: "ArtifactsJson");

            migrationBuilder.AlterColumn<int>(
                name: "Phase",
                table: "ProjectStates",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "ProjectBrief",
                table: "ProjectStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Messages",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "ActiveFilesJson",
                table: "Checkpoints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedItemsJson",
                table: "Checkpoints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletedTaskCount",
                table: "Checkpoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PendingItemsJson",
                table: "Checkpoints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalTaskCount",
                table: "Checkpoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "AgentStates",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "ArtifactsJson",
                table: "AgentStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DependenciesJson",
                table: "AgentStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeat",
                table: "AgentStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryContext",
                table: "AgentStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_AgentRole_CreatedAt",
                table: "Checkpoints",
                columns: new[] { "AgentRole", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentStates_Role",
                table: "AgentStates",
                column: "Role",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Checkpoints_AgentRole_CreatedAt",
                table: "Checkpoints");

            migrationBuilder.DropIndex(
                name: "IX_AgentStates_Role",
                table: "AgentStates");

            migrationBuilder.DropColumn(
                name: "ProjectBrief",
                table: "ProjectStates");

            migrationBuilder.DropColumn(
                name: "ActiveFilesJson",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "CompletedItemsJson",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "CompletedTaskCount",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "PendingItemsJson",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "TotalTaskCount",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "ArtifactsJson",
                table: "AgentStates");

            migrationBuilder.DropColumn(
                name: "DependenciesJson",
                table: "AgentStates");

            migrationBuilder.DropColumn(
                name: "LastHeartbeat",
                table: "AgentStates");

            migrationBuilder.DropColumn(
                name: "RecoveryContext",
                table: "AgentStates");

            migrationBuilder.RenameColumn(
                name: "MetadataJson",
                table: "Messages",
                newName: "Metadata");

            migrationBuilder.RenameColumn(
                name: "ArtifactsJson",
                table: "Messages",
                newName: "Artifacts");

            migrationBuilder.AlterColumn<string>(
                name: "Phase",
                table: "ProjectStates",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Messages",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "ActiveFiles",
                table: "Checkpoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompletedItems",
                table: "Checkpoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PendingItems",
                table: "Checkpoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Progress",
                table: "Checkpoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AgentStates",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Artifacts",
                table: "AgentStates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Dependencies",
                table: "AgentStates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_AgentRole",
                table: "Checkpoints",
                column: "AgentRole");

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_CreatedAt",
                table: "Checkpoints",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentStates_Role",
                table: "AgentStates",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_AgentStates_Status",
                table: "AgentStates",
                column: "Status");
        }
    }
}
