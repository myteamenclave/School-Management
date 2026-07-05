using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeStructureTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeeTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    GradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeTemplates_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeTemplates_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeInstallments_FeeTemplates_FeeTemplateId",
                        column: x => x.FeeTemplateId,
                        principalTable: "FeeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeeLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeLineItems_FeeTemplates_FeeTemplateId",
                        column: x => x.FeeTemplateId,
                        principalTable: "FeeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscountRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuleType = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FeeLineItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscountRules_FeeLineItems_FeeLineItemId",
                        column: x => x.FeeLineItemId,
                        principalTable: "FeeLineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiscountRules_FeeTemplates_FeeTemplateId",
                        column: x => x.FeeTemplateId,
                        principalTable: "FeeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_FeeLineItemId",
                table: "DiscountRules",
                column: "FeeLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_FeeTemplateId",
                table: "DiscountRules",
                column: "FeeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeInstallments_FeeTemplateId",
                table: "FeeInstallments",
                column: "FeeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeLineItems_FeeTemplateId",
                table: "FeeLineItems",
                column: "FeeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeTemplates_AcademicYearId",
                table: "FeeTemplates",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeTemplates_GradeId",
                table: "FeeTemplates",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeTemplates_SchoolId_AcademicYearId_GradeId_Name",
                table: "FeeTemplates",
                columns: new[] { "SchoolId", "AcademicYearId", "GradeId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscountRules");

            migrationBuilder.DropTable(
                name: "FeeInstallments");

            migrationBuilder.DropTable(
                name: "FeeLineItems");

            migrationBuilder.DropTable(
                name: "FeeTemplates");
        }
    }
}
