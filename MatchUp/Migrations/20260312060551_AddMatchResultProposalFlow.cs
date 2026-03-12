using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchUp.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchResultProposalFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchResultProposals_MatchId",
                table: "MatchResultProposals");

            migrationBuilder.RenameColumn(
                name: "HomeTeamScore",
                table: "MatchResultProposals",
                newName: "ProposedHomeTeamScore");

            migrationBuilder.RenameColumn(
                name: "AwayTeamScore",
                table: "MatchResultProposals",
                newName: "ProposedAwayTeamScore");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResultProposals_MatchId_Status",
                table: "MatchResultProposals",
                columns: new[] { "MatchId", "Status" },
                unique: true,
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchResultProposals_MatchId_Status",
                table: "MatchResultProposals");

            migrationBuilder.RenameColumn(
                name: "ProposedHomeTeamScore",
                table: "MatchResultProposals",
                newName: "HomeTeamScore");

            migrationBuilder.RenameColumn(
                name: "ProposedAwayTeamScore",
                table: "MatchResultProposals",
                newName: "AwayTeamScore");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResultProposals_MatchId",
                table: "MatchResultProposals",
                column: "MatchId");
        }
    }
}
