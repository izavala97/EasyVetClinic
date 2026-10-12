using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyVetClinic.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Clinics",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "Clinics");
        }
    }
}
