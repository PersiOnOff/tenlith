using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenlith.Api.Migrations
{
    /// <inheritdoc />
    public partial class PendingCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckoutGeneration",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingCheckoutExpiresAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PendingCheckoutId",
                table: "Organizations",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PendingCheckoutUrl",
                table: "Organizations",
                type: "text",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_StripeCustomerId",
                table: "Organizations",
                column: "StripeCustomerId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Organizations_StripeCustomerId",
                table: "Organizations"
            );

            migrationBuilder.DropColumn(name: "CheckoutGeneration", table: "Organizations");

            migrationBuilder.DropColumn(name: "PendingCheckoutExpiresAt", table: "Organizations");

            migrationBuilder.DropColumn(name: "PendingCheckoutId", table: "Organizations");

            migrationBuilder.DropColumn(name: "PendingCheckoutUrl", table: "Organizations");
        }
    }
}
