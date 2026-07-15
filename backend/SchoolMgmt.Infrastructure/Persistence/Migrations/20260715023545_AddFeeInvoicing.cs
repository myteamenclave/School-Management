using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "FeeTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FeeInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeInvoices_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeInvoices_FeeTemplates_FeeTemplateId",
                        column: x => x.FeeTemplateId,
                        principalTable: "FeeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeInvoices_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentDiscountAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDiscountAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentDiscountAssignments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentDiscountAssignments_DiscountRules_DiscountRuleId",
                        column: x => x.DiscountRuleId,
                        principalTable: "DiscountRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentDiscountAssignments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentFeeAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFeeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentFeeAssignments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentFeeAssignments_FeeTemplates_FeeTemplateId",
                        column: x => x.FeeTemplateId,
                        principalTable: "FeeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentFeeAssignments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeInvoiceInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceInstallmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeInvoiceInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeInvoiceInstallments_FeeInvoices_FeeInvoiceId",
                        column: x => x.FeeInvoiceId,
                        principalTable: "FeeInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeeInvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLineItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeInvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeInvoiceLineItems_FeeInvoices_FeeInvoiceId",
                        column: x => x.FeeInvoiceId,
                        principalTable: "FeeInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeInvoiceInstallments_FeeInvoiceId",
                table: "FeeInvoiceInstallments",
                column: "FeeInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeInvoiceLineItems_FeeInvoiceId",
                table: "FeeInvoiceLineItems",
                column: "FeeInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeInvoices_AcademicYearId",
                table: "FeeInvoices",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeInvoices_FeeTemplateId",
                table: "FeeInvoices",
                column: "FeeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeInvoices_SchoolId_StudentId_AcademicYearId",
                table: "FeeInvoices",
                columns: new[] { "SchoolId", "StudentId", "AcademicYearId" },
                unique: true,
                filter: "\"Status\" != 2");

            migrationBuilder.CreateIndex(
                name: "IX_FeeInvoices_StudentId",
                table: "FeeInvoices",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDiscountAssignments_AcademicYearId",
                table: "StudentDiscountAssignments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDiscountAssignments_DiscountRuleId",
                table: "StudentDiscountAssignments",
                column: "DiscountRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDiscountAssignments_SchoolId_StudentId_DiscountRuleI~",
                table: "StudentDiscountAssignments",
                columns: new[] { "SchoolId", "StudentId", "DiscountRuleId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentDiscountAssignments_StudentId",
                table: "StudentDiscountAssignments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeAssignments_AcademicYearId",
                table: "StudentFeeAssignments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeAssignments_FeeTemplateId",
                table: "StudentFeeAssignments",
                column: "FeeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeAssignments_SchoolId_StudentId_AcademicYearId",
                table: "StudentFeeAssignments",
                columns: new[] { "SchoolId", "StudentId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeAssignments_StudentId",
                table: "StudentFeeAssignments",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeeInvoiceInstallments");

            migrationBuilder.DropTable(
                name: "FeeInvoiceLineItems");

            migrationBuilder.DropTable(
                name: "StudentDiscountAssignments");

            migrationBuilder.DropTable(
                name: "StudentFeeAssignments");

            migrationBuilder.DropTable(
                name: "FeeInvoices");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "FeeTemplates");
        }
    }
}
