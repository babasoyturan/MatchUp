using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchUp.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotOpenToGameApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsApproved",
                table: "OpenToGameMemberApprovals",
                newName: "IsDeleted");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RespondedAtUtc",
                table: "OpenToGameMemberApprovals",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "OpenToGameMemberApprovals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "OpenToGameMemberApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "OpenToGameMemberApprovals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "OpenToGameMemberApprovals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OpenToGameMemberApprovals_OpenToGameConfigId_PlayerId",
                table: "OpenToGameMemberApprovals",
                columns: new[] { "OpenToGameConfigId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpenToGameMemberApprovals_OpenToGameConfigId_PlayerId",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "OpenToGameMemberApprovals",
                newName: "IsApproved");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RespondedAtUtc",
                table: "OpenToGameMemberApprovals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
