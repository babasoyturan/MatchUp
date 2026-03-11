using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchUp.Migrations
{
    /// <inheritdoc />
    public partial class RefactorOpenToGameToSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpenToGameMemberApprovals_AspNetUsers_PlayerId",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_OpenToGameMemberApprovals_OpenToGameConfigs_OpenToGameConfigId",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.DropTable(
                name: "OpenToGameTimeWindows");

            migrationBuilder.DropTable(
                name: "OpenToGameConfigs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OpenToGameMemberApprovals",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.RenameColumn(
                name: "OpenToGameConfigId",
                table: "OpenToGameMemberApprovals",
                newName: "OpenToGameSubmissionId");

            migrationBuilder.RenameIndex(
                name: "IX_OpenToGameMemberApprovals_OpenToGameConfigId_PlayerId",
                table: "OpenToGameMemberApprovals",
                newName: "IX_OpenToGameMemberApprovals_OpenToGameSubmissionId_PlayerId");

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveOpenToGameSubmissionId",
                table: "Teams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_OpenToGameMemberApprovals",
                table: "OpenToGameMemberApprovals",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "OpenToGameSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Formats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenToGameSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenToGameSubmissions_AspNetUsers_SubmittedByPlayerId",
                        column: x => x.SubmittedByPlayerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpenToGameSubmissions_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenToGameSubmissionTimeWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenToGameSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    StartMinute = table.Column<int>(type: "int", nullable: false),
                    EndMinute = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenToGameSubmissionTimeWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenToGameSubmissionTimeWindows_OpenToGameSubmissions_OpenToGameSubmissionId",
                        column: x => x.OpenToGameSubmissionId,
                        principalTable: "OpenToGameSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ActiveOpenToGameSubmissionId",
                table: "Teams",
                column: "ActiveOpenToGameSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenToGameSubmissions_SubmittedByPlayerId",
                table: "OpenToGameSubmissions",
                column: "SubmittedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenToGameSubmissions_TeamId",
                table: "OpenToGameSubmissions",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenToGameSubmissionTimeWindows_OpenToGameSubmissionId_Day",
                table: "OpenToGameSubmissionTimeWindows",
                columns: new[] { "OpenToGameSubmissionId", "Day" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OpenToGameMemberApprovals_AspNetUsers_PlayerId",
                table: "OpenToGameMemberApprovals",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OpenToGameMemberApprovals_OpenToGameSubmissions_OpenToGameSubmissionId",
                table: "OpenToGameMemberApprovals",
                column: "OpenToGameSubmissionId",
                principalTable: "OpenToGameSubmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_OpenToGameSubmissions_ActiveOpenToGameSubmissionId",
                table: "Teams",
                column: "ActiveOpenToGameSubmissionId",
                principalTable: "OpenToGameSubmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpenToGameMemberApprovals_AspNetUsers_PlayerId",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_OpenToGameMemberApprovals_OpenToGameSubmissions_OpenToGameSubmissionId",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_OpenToGameSubmissions_ActiveOpenToGameSubmissionId",
                table: "Teams");

            migrationBuilder.DropTable(
                name: "OpenToGameSubmissionTimeWindows");

            migrationBuilder.DropTable(
                name: "OpenToGameSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_Teams_ActiveOpenToGameSubmissionId",
                table: "Teams");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OpenToGameMemberApprovals",
                table: "OpenToGameMemberApprovals");

            migrationBuilder.DropColumn(
                name: "ActiveOpenToGameSubmissionId",
                table: "Teams");

            migrationBuilder.RenameColumn(
                name: "OpenToGameSubmissionId",
                table: "OpenToGameMemberApprovals",
                newName: "OpenToGameConfigId");

            migrationBuilder.RenameIndex(
                name: "IX_OpenToGameMemberApprovals_OpenToGameSubmissionId_PlayerId",
                table: "OpenToGameMemberApprovals",
                newName: "IX_OpenToGameMemberApprovals_OpenToGameConfigId_PlayerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OpenToGameMemberApprovals",
                table: "OpenToGameMemberApprovals",
                columns: new[] { "OpenToGameConfigId", "PlayerId" });

            migrationBuilder.CreateTable(
                name: "OpenToGameConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Formats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenToGameConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenToGameConfigs_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenToGameTimeWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenToGameConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndMinute = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    StartMinute = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenToGameTimeWindows", x => x.Id);
                    table.CheckConstraint("CK_OpenToGameTimeWindow_Minutes", "[StartMinute] >= 0 AND [StartMinute] <= 1439 AND [EndMinute] >= 0 AND [EndMinute] <= 1439 AND [StartMinute] < [EndMinute]");
                    table.ForeignKey(
                        name: "FK_OpenToGameTimeWindows_OpenToGameConfigs_OpenToGameConfigId",
                        column: x => x.OpenToGameConfigId,
                        principalTable: "OpenToGameConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpenToGameConfigs_TeamId",
                table: "OpenToGameConfigs",
                column: "TeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenToGameTimeWindows_OpenToGameConfigId",
                table: "OpenToGameTimeWindows",
                column: "OpenToGameConfigId");

            migrationBuilder.AddForeignKey(
                name: "FK_OpenToGameMemberApprovals_AspNetUsers_PlayerId",
                table: "OpenToGameMemberApprovals",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OpenToGameMemberApprovals_OpenToGameConfigs_OpenToGameConfigId",
                table: "OpenToGameMemberApprovals",
                column: "OpenToGameConfigId",
                principalTable: "OpenToGameConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
