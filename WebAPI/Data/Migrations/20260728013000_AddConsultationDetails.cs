using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyVetClinic.Api.Data.Migrations;

[DbContext(typeof(ClinicDbContext))]
[Migration("20260728013000_AddConsultationDetails")]
public partial class AddConsultationDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ChiefComplaint",
            table: "Consultations",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ClinicalNotes",
            table: "Consultations",
            type: "TEXT",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ChiefComplaint",
            table: "Consultations");

        migrationBuilder.DropColumn(
            name: "ClinicalNotes",
            table: "Consultations");
    }
}