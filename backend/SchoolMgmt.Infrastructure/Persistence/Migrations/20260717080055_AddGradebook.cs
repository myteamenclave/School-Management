using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGradebook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GradeScaleBands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Letter = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    MinScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeScaleBands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubjectTermGrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    SemesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    MidtermScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    FinalScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CourseworkScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    TermScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    LetterGrade = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EnteredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectTermGrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubjectTermGrades_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectTermGrades_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectTermGrades_Semesters_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectTermGrades_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectTermGrades_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectTermGrades_Users_EnteredByUserId",
                        column: x => x.EnteredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "GradeScaleBands",
                columns: new[] { "Id", "CreatedAt", "Letter", "MaxScore", "MinScore", "SchoolId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-0000000000a1"), new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "A", 100m, 90m, new Guid("00000000-0000-0000-0000-000000000001"), null },
                    { new Guid("00000000-0000-0000-0000-0000000000a2"), new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "B", 89.99m, 80m, new Guid("00000000-0000-0000-0000-000000000001"), null },
                    { new Guid("00000000-0000-0000-0000-0000000000a3"), new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "C", 79.99m, 70m, new Guid("00000000-0000-0000-0000-000000000001"), null },
                    { new Guid("00000000-0000-0000-0000-0000000000a4"), new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "D", 69.99m, 60m, new Guid("00000000-0000-0000-0000-000000000001"), null },
                    { new Guid("00000000-0000-0000-0000-0000000000a5"), new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "F", 59.99m, 0m, new Guid("00000000-0000-0000-0000-000000000001"), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradeScaleBands_SchoolId_Letter",
                table: "GradeScaleBands",
                columns: new[] { "SchoolId", "Letter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTermGrades_AcademicYearId",
                table: "SubjectTermGrades",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTermGrades_EnteredByUserId",
                table: "SubjectTermGrades",
                column: "EnteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTermGrades_SchoolId_StudentId_SubjectId_SemesterId",
                table: "SubjectTermGrades",
                columns: new[] { "SchoolId", "StudentId", "SubjectId", "SemesterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTermGrades_SectionId",
                table: "SubjectTermGrades",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTermGrades_SemesterId",
                table: "SubjectTermGrades",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTermGrades_StudentId",
                table: "SubjectTermGrades",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTermGrades_SubjectId",
                table: "SubjectTermGrades",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeScaleBands");

            migrationBuilder.DropTable(
                name: "SubjectTermGrades");
        }
    }
}
