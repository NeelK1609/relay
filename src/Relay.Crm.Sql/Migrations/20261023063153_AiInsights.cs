using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relay.Crm.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AiInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Insights_AnalyzedAt",
                table: "Cases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Insights_Applied",
                table: "Cases",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Insights_Confidence",
                table: "Cases",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insights_Reason",
                table: "Cases",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Insights_SafetyRisk",
                table: "Cases",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insights_Sentiment",
                table: "Cases",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insights_SuggestedCategory",
                table: "Cases",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insights_SuggestedPriority",
                table: "Cases",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insights_Summary",
                table: "Cases",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Cases",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Insights_AnalyzedAt",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Insights_Applied",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Insights_Confidence",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Insights_Reason",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Insights_SafetyRisk",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Insights_Sentiment",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Insights_SuggestedCategory",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Insights_SuggestedPriority",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Insights_Summary",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Cases");
        }
    }
}
