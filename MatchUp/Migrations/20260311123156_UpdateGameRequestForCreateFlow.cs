using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchUp.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGameRequestForCreateFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "GameRequests",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByPlayerId",
                table: "GameRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAtUtc",
                table: "GameRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameRequests_RequestedByPlayerId",
                table: "GameRequests",
                column: "RequestedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_GameRequests_StartAtUtc",
                table: "GameRequests",
                column: "StartAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_GameRequests_AspNetUsers_RequestedByPlayerId",
                table: "GameRequests",
                column: "RequestedByPlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameRequests_AspNetUsers_RequestedByPlayerId",
                table: "GameRequests");

            migrationBuilder.DropIndex(
                name: "IX_GameRequests_RequestedByPlayerId",
                table: "GameRequests");

            migrationBuilder.DropIndex(
                name: "IX_GameRequests_StartAtUtc",
                table: "GameRequests");

            migrationBuilder.DropColumn(
                name: "RequestedByPlayerId",
                table: "GameRequests");

            migrationBuilder.DropColumn(
                name: "RespondedAtUtc",
                table: "GameRequests");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "GameRequests",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }
    }
}
