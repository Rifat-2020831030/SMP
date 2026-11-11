using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolAdmission.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "classes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false, computedColumnSql: "lower(name)", stored: true),
                    total_seats = table.Column<int>(type: "integer", nullable: false),
                    held_seats = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    confirmed_seats = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classes", x => x.id);
                    table.CheckConstraint("ck_classes_capacity", "held_seats + confirmed_seats <= total_seats");
                    table.CheckConstraint("ck_classes_name_length", "char_length(name) BETWEEN 1 AND 200");
                    table.CheckConstraint("ck_classes_name_trimmed", "name = btrim(name)");
                    table.CheckConstraint("ck_classes_nonnegative", "held_seats >= 0 AND confirmed_seats >= 0");
                    table.CheckConstraint("ck_classes_total_seats", "total_seats > 0");
                });

            migrationBuilder.CreateIndex(
                name: "uq_class_name",
                table: "classes",
                column: "normalized_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "classes");
        }
    }
}
