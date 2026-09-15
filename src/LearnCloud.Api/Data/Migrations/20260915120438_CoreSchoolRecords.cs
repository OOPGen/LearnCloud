using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoreSchoolRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "terms",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "student_number",
                table: "students",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "photo_url",
                table: "students",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "national_id",
                table: "students",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                table: "students",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                table: "students",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "enrolment_type",
                table: "student_enrolments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "enrolment_status",
                table: "student_enrolments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "guardians",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "national_id",
                table: "guardians",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                table: "guardians",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                table: "guardians",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "guardians",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                table: "guardians",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "relationship_type",
                table: "guardian_student_links",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "grades",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "grades",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "class_streams",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "academic_years",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_term_tenant_id_id",
                table: "terms",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_student_tenant_id_id",
                table: "students",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_guardian_tenant_id_id",
                table: "guardians",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_grade_tenant_id_id",
                table: "grades",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_class_stream_tenant_id_id",
                table: "class_streams",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_academic_year_tenant_id_id",
                table: "academic_years",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateIndex(
                name: "uq_terms_one_current",
                table: "terms",
                column: "tenant_id",
                unique: true,
                filter: "is_current = true AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_terms_year_number",
                table: "terms",
                columns: new[] { "tenant_id", "academic_year_id", "term_number" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_students_name",
                table: "students",
                columns: new[] { "tenant_id", "last_name", "first_name" });

            migrationBuilder.CreateIndex(
                name: "idx_students_status",
                table: "students",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_students_stream",
                table: "students",
                columns: new[] { "tenant_id", "stream_id" });

            migrationBuilder.CreateIndex(
                name: "ix_students_tenant_id_academic_year_id",
                table: "students",
                columns: new[] { "tenant_id", "academic_year_id" });

            migrationBuilder.CreateIndex(
                name: "ix_students_tenant_id_grade_id",
                table: "students",
                columns: new[] { "tenant_id", "grade_id" });

            migrationBuilder.CreateIndex(
                name: "uq_students_tenant_number",
                table: "students",
                columns: new[] { "tenant_id", "student_number" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_student_enrolments_class",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "academic_year_id", "grade_id", "stream_id" });

            migrationBuilder.CreateIndex(
                name: "idx_student_enrolments_student",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_student_enrolments_tenant_id_grade_id",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "grade_id" });

            migrationBuilder.CreateIndex(
                name: "ix_student_enrolments_tenant_id_stream_id",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "stream_id" });

            migrationBuilder.CreateIndex(
                name: "ix_student_enrolments_tenant_id_term_id",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "term_id" });

            migrationBuilder.CreateIndex(
                name: "uq_student_enrolments_one_current",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "student_id" },
                unique: true,
                filter: "is_current = true AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_guardians_name",
                table: "guardians",
                columns: new[] { "tenant_id", "last_name", "first_name" });

            migrationBuilder.CreateIndex(
                name: "idx_guardians_phone",
                table: "guardians",
                columns: new[] { "tenant_id", "phone" });

            migrationBuilder.CreateIndex(
                name: "idx_guardian_links_student",
                table: "guardian_student_links",
                columns: new[] { "tenant_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_guardian_student_links_tenant_id_academic_year_id",
                table: "guardian_student_links",
                columns: new[] { "tenant_id", "academic_year_id" });

            migrationBuilder.CreateIndex(
                name: "uq_guardian_links_one_primary",
                table: "guardian_student_links",
                columns: new[] { "tenant_id", "student_id" },
                unique: true,
                filter: "is_primary_contact = true AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_guardian_links_pair",
                table: "guardian_student_links",
                columns: new[] { "tenant_id", "guardian_id", "student_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_grades_year_level",
                table: "grades",
                columns: new[] { "tenant_id", "academic_year_id", "level_order" });

            migrationBuilder.CreateIndex(
                name: "uq_grades_year_code",
                table: "grades",
                columns: new[] { "tenant_id", "academic_year_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_class_streams_tenant_id_academic_year_id",
                table: "class_streams",
                columns: new[] { "tenant_id", "academic_year_id" });

            migrationBuilder.CreateIndex(
                name: "uq_class_streams_grade_name",
                table: "class_streams",
                columns: new[] { "tenant_id", "grade_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_academic_years_one_current",
                table: "academic_years",
                column: "tenant_id",
                unique: true,
                filter: "is_current = true AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_academic_years_tenant_name",
                table: "academic_years",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.AddForeignKey(
                name: "fk_class_streams_academic_years_tenant_id_academic_year_id",
                table: "class_streams",
                columns: new[] { "tenant_id", "academic_year_id" },
                principalTable: "academic_years",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_class_streams_grade_tenant_id_grade_id",
                table: "class_streams",
                columns: new[] { "tenant_id", "grade_id" },
                principalTable: "grades",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_grades_academic_years_tenant_id_academic_year_id",
                table: "grades",
                columns: new[] { "tenant_id", "academic_year_id" },
                principalTable: "academic_years",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_guardian_student_links_academic_years_tenant_id_academic_ye",
                table: "guardian_student_links",
                columns: new[] { "tenant_id", "academic_year_id" },
                principalTable: "academic_years",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_guardian_student_links_guardians_tenant_id_guardian_id",
                table: "guardian_student_links",
                columns: new[] { "tenant_id", "guardian_id" },
                principalTable: "guardians",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_guardian_student_links_student_tenant_id_student_id",
                table: "guardian_student_links",
                columns: new[] { "tenant_id", "student_id" },
                principalTable: "students",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_student_enrolments_academic_years_tenant_id_academic_year_id",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "academic_year_id" },
                principalTable: "academic_years",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_student_enrolments_class_streams_tenant_id_stream_id",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "stream_id" },
                principalTable: "class_streams",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_student_enrolments_grades_tenant_id_grade_id",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "grade_id" },
                principalTable: "grades",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_student_enrolments_students_tenant_id_student_id",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "student_id" },
                principalTable: "students",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_student_enrolments_term_tenant_id_term_id",
                table: "student_enrolments",
                columns: new[] { "tenant_id", "term_id" },
                principalTable: "terms",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_students_academic_years_tenant_id_academic_year_id",
                table: "students",
                columns: new[] { "tenant_id", "academic_year_id" },
                principalTable: "academic_years",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_students_class_streams_tenant_id_stream_id",
                table: "students",
                columns: new[] { "tenant_id", "stream_id" },
                principalTable: "class_streams",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_students_grades_tenant_id_grade_id",
                table: "students",
                columns: new[] { "tenant_id", "grade_id" },
                principalTable: "grades",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_terms_academic_years_tenant_id_academic_year_id",
                table: "terms",
                columns: new[] { "tenant_id", "academic_year_id" },
                principalTable: "academic_years",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_class_streams_academic_years_tenant_id_academic_year_id",
                table: "class_streams");

            migrationBuilder.DropForeignKey(
                name: "fk_class_streams_grade_tenant_id_grade_id",
                table: "class_streams");

            migrationBuilder.DropForeignKey(
                name: "fk_grades_academic_years_tenant_id_academic_year_id",
                table: "grades");

            migrationBuilder.DropForeignKey(
                name: "fk_guardian_student_links_academic_years_tenant_id_academic_ye",
                table: "guardian_student_links");

            migrationBuilder.DropForeignKey(
                name: "fk_guardian_student_links_guardians_tenant_id_guardian_id",
                table: "guardian_student_links");

            migrationBuilder.DropForeignKey(
                name: "fk_guardian_student_links_student_tenant_id_student_id",
                table: "guardian_student_links");

            migrationBuilder.DropForeignKey(
                name: "fk_student_enrolments_academic_years_tenant_id_academic_year_id",
                table: "student_enrolments");

            migrationBuilder.DropForeignKey(
                name: "fk_student_enrolments_class_streams_tenant_id_stream_id",
                table: "student_enrolments");

            migrationBuilder.DropForeignKey(
                name: "fk_student_enrolments_grades_tenant_id_grade_id",
                table: "student_enrolments");

            migrationBuilder.DropForeignKey(
                name: "fk_student_enrolments_students_tenant_id_student_id",
                table: "student_enrolments");

            migrationBuilder.DropForeignKey(
                name: "fk_student_enrolments_term_tenant_id_term_id",
                table: "student_enrolments");

            migrationBuilder.DropForeignKey(
                name: "fk_students_academic_years_tenant_id_academic_year_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_students_class_streams_tenant_id_stream_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_students_grades_tenant_id_grade_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_terms_academic_years_tenant_id_academic_year_id",
                table: "terms");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_term_tenant_id_id",
                table: "terms");

            migrationBuilder.DropIndex(
                name: "uq_terms_one_current",
                table: "terms");

            migrationBuilder.DropIndex(
                name: "uq_terms_year_number",
                table: "terms");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_student_tenant_id_id",
                table: "students");

            migrationBuilder.DropIndex(
                name: "idx_students_name",
                table: "students");

            migrationBuilder.DropIndex(
                name: "idx_students_status",
                table: "students");

            migrationBuilder.DropIndex(
                name: "idx_students_stream",
                table: "students");

            migrationBuilder.DropIndex(
                name: "ix_students_tenant_id_academic_year_id",
                table: "students");

            migrationBuilder.DropIndex(
                name: "ix_students_tenant_id_grade_id",
                table: "students");

            migrationBuilder.DropIndex(
                name: "uq_students_tenant_number",
                table: "students");

            migrationBuilder.DropIndex(
                name: "idx_student_enrolments_class",
                table: "student_enrolments");

            migrationBuilder.DropIndex(
                name: "idx_student_enrolments_student",
                table: "student_enrolments");

            migrationBuilder.DropIndex(
                name: "ix_student_enrolments_tenant_id_grade_id",
                table: "student_enrolments");

            migrationBuilder.DropIndex(
                name: "ix_student_enrolments_tenant_id_stream_id",
                table: "student_enrolments");

            migrationBuilder.DropIndex(
                name: "ix_student_enrolments_tenant_id_term_id",
                table: "student_enrolments");

            migrationBuilder.DropIndex(
                name: "uq_student_enrolments_one_current",
                table: "student_enrolments");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_guardian_tenant_id_id",
                table: "guardians");

            migrationBuilder.DropIndex(
                name: "idx_guardians_name",
                table: "guardians");

            migrationBuilder.DropIndex(
                name: "idx_guardians_phone",
                table: "guardians");

            migrationBuilder.DropIndex(
                name: "idx_guardian_links_student",
                table: "guardian_student_links");

            migrationBuilder.DropIndex(
                name: "ix_guardian_student_links_tenant_id_academic_year_id",
                table: "guardian_student_links");

            migrationBuilder.DropIndex(
                name: "uq_guardian_links_one_primary",
                table: "guardian_student_links");

            migrationBuilder.DropIndex(
                name: "uq_guardian_links_pair",
                table: "guardian_student_links");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_grade_tenant_id_id",
                table: "grades");

            migrationBuilder.DropIndex(
                name: "idx_grades_year_level",
                table: "grades");

            migrationBuilder.DropIndex(
                name: "uq_grades_year_code",
                table: "grades");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_class_stream_tenant_id_id",
                table: "class_streams");

            migrationBuilder.DropIndex(
                name: "ix_class_streams_tenant_id_academic_year_id",
                table: "class_streams");

            migrationBuilder.DropIndex(
                name: "uq_class_streams_grade_name",
                table: "class_streams");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_academic_year_tenant_id_id",
                table: "academic_years");

            migrationBuilder.DropIndex(
                name: "uq_academic_years_one_current",
                table: "academic_years");

            migrationBuilder.DropIndex(
                name: "uq_academic_years_tenant_name",
                table: "academic_years");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "terms",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "student_number",
                table: "students",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "students",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "photo_url",
                table: "students",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "national_id",
                table: "students",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                table: "students",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "students",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                table: "students",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "enrolment_type",
                table: "student_enrolments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "enrolment_status",
                table: "student_enrolments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "guardians",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "national_id",
                table: "guardians",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                table: "guardians",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                table: "guardians",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "guardians",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                table: "guardians",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "relationship_type",
                table: "guardian_student_links",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "grades",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "grades",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "class_streams",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "academic_years",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
