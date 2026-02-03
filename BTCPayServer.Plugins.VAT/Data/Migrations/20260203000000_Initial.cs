using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable enable

namespace BTCPayServer.Plugins.VAT.Data.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "BTCPayServer.Plugins.VAT");

        migrationBuilder.CreateTable(
            name: "VATStoreSettings",
            schema: "BTCPayServer.Plugins.VAT",
            columns: table => new
            {
                StoreId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Mode = table.Column<int>(type: "integer", nullable: false),
                HomeCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                VATNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                ValidateVIES = table.Column<bool>(type: "boolean", nullable: false),
                BelowSmallBusinessThreshold = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VATStoreSettings", x => x.StoreId);
            });

        migrationBuilder.CreateTable(
            name: "VATInvoiceRecords",
            schema: "BTCPayServer.Plugins.VAT",
            columns: table => new
            {
                InvoiceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                StoreId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CustomerCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                VATRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                NetAmount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                VATAmount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                GrossAmount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                ReverseChargeApplied = table.Column<bool>(type: "boolean", nullable: false),
                CustomerVATNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                ModeApplied = table.Column<int>(type: "integer", nullable: false),
                EvidenceJson = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VATInvoiceRecords", x => x.InvoiceId);
            });

        migrationBuilder.CreateTable(
            name: "VATEvidence",
            schema: "BTCPayServer.Plugins.VAT",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                VATInvoiceRecordId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                Confidence = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                RawData = table.Column<string>(type: "text", nullable: true),
                CollectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VATEvidence", x => x.Id);
                table.ForeignKey(
                    name: "FK_VATEvidence_VATInvoiceRecords_VATInvoiceRecordId",
                    column: x => x.VATInvoiceRecordId,
                    principalSchema: "BTCPayServer.Plugins.VAT",
                    principalTable: "VATInvoiceRecords",
                    principalColumn: "InvoiceId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VATStoreSettings_StoreId",
            schema: "BTCPayServer.Plugins.VAT",
            table: "VATStoreSettings",
            column: "StoreId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VATInvoiceRecords_StoreId",
            schema: "BTCPayServer.Plugins.VAT",
            table: "VATInvoiceRecords",
            column: "StoreId");

        migrationBuilder.CreateIndex(
            name: "IX_VATInvoiceRecords_CustomerCountry",
            schema: "BTCPayServer.Plugins.VAT",
            table: "VATInvoiceRecords",
            column: "CustomerCountry");

        migrationBuilder.CreateIndex(
            name: "IX_VATInvoiceRecords_CreatedAt",
            schema: "BTCPayServer.Plugins.VAT",
            table: "VATInvoiceRecords",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_VATInvoiceRecords_StoreId_CreatedAt",
            schema: "BTCPayServer.Plugins.VAT",
            table: "VATInvoiceRecords",
            columns: new[] { "StoreId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_VATEvidence_VATInvoiceRecordId",
            schema: "BTCPayServer.Plugins.VAT",
            table: "VATEvidence",
            column: "VATInvoiceRecordId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "VATEvidence",
            schema: "BTCPayServer.Plugins.VAT");

        migrationBuilder.DropTable(
            name: "VATInvoiceRecords",
            schema: "BTCPayServer.Plugins.VAT");

        migrationBuilder.DropTable(
            name: "VATStoreSettings",
            schema: "BTCPayServer.Plugins.VAT");
    }
}
