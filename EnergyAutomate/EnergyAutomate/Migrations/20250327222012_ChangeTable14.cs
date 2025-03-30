using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyAutomate.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTable14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommitedPowerValue",
                table: "Devices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastChangePowerValue",
                table: "Devices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedPowerValue",
                table: "Devices",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommitedPowerValue",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastChangePowerValue",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "RequestedPowerValue",
                table: "Devices");
        }
    }
}
