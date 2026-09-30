using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LearnCloud.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlatformSecondFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_second_factors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    secret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    step_up_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_verified_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    last_accepted_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_second_factors", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_platform_second_factors_user",
                table: "platform_second_factors",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_second_factors");
        }
    }
}
