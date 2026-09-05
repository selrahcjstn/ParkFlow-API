using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackReplyAndInvoiceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdminRepliedAt",
                table: "Feedbacks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminReplyMessage",
                table: "Feedbacks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceAmount",
                table: "Feedbacks",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceDescription",
                table: "Feedbacks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Feedbacks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceStatus",
                table: "Feedbacks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminRepliedAt",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "AdminReplyMessage",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "InvoiceAmount",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "InvoiceDescription",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "InvoiceStatus",
                table: "Feedbacks");
        }
    }
}
