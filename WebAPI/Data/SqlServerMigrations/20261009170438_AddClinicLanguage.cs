using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyVetClinic.Api.Data.SqlServerMigrations
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
                type: "nvarchar(10)",
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
