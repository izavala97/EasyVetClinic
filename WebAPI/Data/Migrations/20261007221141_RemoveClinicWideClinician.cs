using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyVetClinic.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClinicWideClinician : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VeterinarianLicenseNumber",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "VeterinarianName",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "VeterinarianTitles",
                table: "Clinics");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VeterinarianLicenseNumber",
                table: "Clinics",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VeterinarianName",
                table: "Clinics",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VeterinarianTitles",
                table: "Clinics",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }
    }
}
