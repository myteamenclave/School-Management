using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassSectionAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentSectionEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSectionEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentSectionEnrollments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentSectionEnrollments_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentSectionEnrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherSectionSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSectionSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherSectionSubjects_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherSectionSubjects_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherSectionSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherSectionSubjects_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentSectionEnrollments_AcademicYearId",
                table: "StudentSectionEnrollments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSectionEnrollments_SectionId",
                table: "StudentSectionEnrollments",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSectionEnrollments_StudentId_AcademicYearId",
                table: "StudentSectionEnrollments",
                columns: new[] { "StudentId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSectionSubjects_AcademicYearId",
                table: "TeacherSectionSubjects",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSectionSubjects_SectionId",
                table: "TeacherSectionSubjects",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSectionSubjects_SubjectId_SectionId_AcademicYearId",
                table: "TeacherSectionSubjects",
                columns: new[] { "SubjectId", "SectionId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSectionSubjects_TeacherId",
                table: "TeacherSectionSubjects",
                column: "TeacherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentSectionEnrollments");

            migrationBuilder.DropTable(
                name: "TeacherSectionSubjects");
        }
    }
}
