using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StellarAnvil.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ApiKeys",
                columns: new[] { "Id", "CreatedAt", "ExpiresAt", "IsActive", "Key", "LastUsedAt", "Name", "Type" },
                values: new object[,]
                {
                    { new Guid("44444444-4444-4444-4444-444444444444"), new DateTime(2025, 9, 20, 10, 47, 45, 992, DateTimeKind.Utc).AddTicks(440), null, true, "sk-admin-d7d994a8004e4fc9bae8fe6e57508f21", null, "Default Admin Key", 1 },
                    { new Guid("55555555-5555-5555-5555-555555555555"), new DateTime(2025, 9, 20, 10, 47, 45, 992, DateTimeKind.Utc).AddTicks(650), null, true, "sk-openapi-a1fc7b6261d24c6cb383904db8d4561d", null, "Default OpenAPI Key", 2 }
                });

            migrationBuilder.InsertData(
                table: "Workflows",
                columns: new[] { "Id", "CreatedAt", "Description", "IsDefault", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2025, 9, 20, 10, 47, 45, 990, DateTimeKind.Utc).AddTicks(6240), "Complete software development lifecycle with all phases", true, "Full SDLC Workflow", new DateTime(2025, 9, 20, 10, 47, 45, 990, DateTimeKind.Utc).AddTicks(6390) },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2370), "Standard software development lifecycle without UX design", true, "Standard SDLC Workflow", new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2370) },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2480), "Simplified software development lifecycle for small changes", true, "Simple SDLC Workflow", new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2480) }
                });

            migrationBuilder.InsertData(
                table: "WorkflowTransitions",
                columns: new[] { "Id", "CreatedAt", "FromState", "Order", "RequiredRole", "ToState", "WorkflowId" },
                values: new object[,]
                {
                    { new Guid("08d562ca-6018-4fd9-a3a6-8aa4976f9bcd"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2450), 5, 4, 5, 6, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("1034ad6a-09f3-4793-9905-c6f4c48e25a0"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2500), 1, 1, 1, 2, new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("18cbd108-a2c0-4290-8d5d-4769fdfd6155"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2430), 1, 1, 1, 2, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("25037b20-3ed2-4256-808d-54a6955ec8bd"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2240), 3, 3, 3, 4, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("2cc41108-121e-4e1f-83dc-cbc079e1870f"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2270), 6, 6, 6, 8, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("59ea646d-1663-4516-8a26-761112b0eaf9"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2440), 3, 3, 3, 5, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("62ab0e6c-5a9c-4f07-a19d-f52adb81337f"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2430), 2, 2, 2, 3, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("921140f8-ef12-42c1-a4a5-9fa9ae4782cb"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2250), 4, 4, 4, 5, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("9e5094e1-8f85-46a2-ad74-b620c2c793a7"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2520), 5, 3, 5, 6, new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("a52c9bfb-c662-4820-87f9-3dcf2d38a842"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2520), 6, 4, 6, 8, new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("aa84c04a-4aad-4bfa-a4dc-6ba6e8a3dc4a"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2240), 2, 2, 2, 3, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("b96d5df0-7789-46ec-930d-48bfd024eca0"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2260), 5, 5, 5, 6, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("d787537d-d9f1-4af9-93a2-df6fc030311b"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2020), 1, 1, 1, 2, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("df52e2a5-158a-4484-af8d-b344deb7aa77"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2450), 6, 5, 6, 8, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("f6a960f0-00f0-4d94-871e-06b672f430c4"), new DateTime(2025, 9, 20, 10, 47, 45, 991, DateTimeKind.Utc).AddTicks(2510), 2, 2, 2, 5, new Guid("33333333-3333-3333-3333-333333333333") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ApiKeys",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "ApiKeys",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("08d562ca-6018-4fd9-a3a6-8aa4976f9bcd"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("1034ad6a-09f3-4793-9905-c6f4c48e25a0"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("18cbd108-a2c0-4290-8d5d-4769fdfd6155"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("25037b20-3ed2-4256-808d-54a6955ec8bd"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("2cc41108-121e-4e1f-83dc-cbc079e1870f"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("59ea646d-1663-4516-8a26-761112b0eaf9"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("62ab0e6c-5a9c-4f07-a19d-f52adb81337f"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("921140f8-ef12-42c1-a4a5-9fa9ae4782cb"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("9e5094e1-8f85-46a2-ad74-b620c2c793a7"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("a52c9bfb-c662-4820-87f9-3dcf2d38a842"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("aa84c04a-4aad-4bfa-a4dc-6ba6e8a3dc4a"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("b96d5df0-7789-46ec-930d-48bfd024eca0"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("d787537d-d9f1-4af9-93a2-df6fc030311b"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("df52e2a5-158a-4484-af8d-b344deb7aa77"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("f6a960f0-00f0-4d94-871e-06b672f430c4"));

            migrationBuilder.DeleteData(
                table: "Workflows",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "Workflows",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "Workflows",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));
        }
    }
}
