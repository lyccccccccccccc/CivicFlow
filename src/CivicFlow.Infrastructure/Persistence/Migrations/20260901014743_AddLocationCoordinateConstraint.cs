using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationCoordinateConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceRequests_CoordinatePairAndRange",
                table: "ServiceRequests",
                sql: "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceRequests_CoordinatePairAndRange",
                table: "ServiceRequests");
        }
    }
}
