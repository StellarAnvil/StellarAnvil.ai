using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StellarAnvil.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Settings = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromState = table.Column<int>(type: "integer", nullable: false),
                    ToState = table.Column<int>(type: "integer", nullable: false),
                    RequiredRole = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromState = table.Column<int>(type: "integer", nullable: false),
                    ToState = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CurrentState = table.Column<int>(type: "integer", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SystemPrompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CurrentTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Tasks_CurrentTaskId",
                        column: x => x.CurrentTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "ApiKeys",
                columns: new[] { "Id", "CreatedAt", "ExpiresAt", "IsActive", "Key", "LastUsedAt", "Name", "Type" },
                values: new object[,]
                {
                    { new Guid("44444444-4444-4444-4444-444444444444"), new DateTime(2025, 9, 23, 17, 54, 35, 677, DateTimeKind.Utc).AddTicks(4700), null, true, "sk-admin-5ff4ac7fb8fb46b68a8f3d105c4c1d3e", null, "Default Admin Key", 1 },
                    { new Guid("55555555-5555-5555-5555-555555555555"), new DateTime(2025, 9, 23, 17, 54, 35, 677, DateTimeKind.Utc).AddTicks(4850), null, true, "sk-openapi-42638e03f1be4473b374283048eebb7a", null, "Default OpenAPI Key", 2 }
                });

            migrationBuilder.InsertData(
                table: "Workflows",
                columns: new[] { "Id", "CreatedAt", "Description", "IsDefault", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(2260), "Complete software development lifecycle with all phases", true, "Full SDLC Workflow", new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(2400) },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7560), "Standard software development lifecycle without UX design", true, "Standard SDLC Workflow", new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7560) },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7720), "Simplified software development lifecycle for small changes", true, "Simple SDLC Workflow", new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7720) }
                });

            migrationBuilder.InsertData(
                table: "WorkflowTransitions",
                columns: new[] { "Id", "CreatedAt", "FromState", "Order", "RequiredRole", "ToState", "WorkflowId" },
                values: new object[,]
                {
                    { new Guid("07cee5a3-57c0-4f9c-9514-18cfbdcb28e0"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7460), 6, 6, 6, 8, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("170f122a-c079-4be0-b11c-3d4eab29940c"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7440), 3, 3, 3, 4, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("31487040-28f9-44e4-805d-8f77fd3ac064"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7640), 6, 5, 6, 8, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("370668fb-c143-4906-aa46-3e4552eeed98"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7770), 6, 4, 6, 8, new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("5605854f-b9bb-4a31-a220-6b739c723e0a"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7620), 2, 2, 2, 3, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("78b7310f-8c26-48fe-8f22-4be937b717d3"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7440), 4, 4, 4, 5, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("849c451d-a904-4cea-b950-b0f915e88f17"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7290), 1, 1, 1, 2, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("9024b3d0-c31b-4aa5-9e57-fb9217431b33"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7750), 2, 2, 2, 5, new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("95cf06f3-94fc-46f3-9939-984325d342f7"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7450), 5, 5, 5, 6, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("a6fba73c-6681-4464-94c5-2c3f156bbdb8"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7630), 3, 3, 3, 5, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("bf366279-3a28-41b0-a64c-d3c6d56e651a"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7740), 1, 1, 1, 2, new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("bf9ebbef-29a8-42aa-b010-3776637c9953"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7610), 1, 1, 1, 2, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("ca807a9e-613e-44d7-8931-f53becd82214"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7640), 5, 4, 5, 6, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("caa165d8-d5a6-4f3c-872b-7587d3362c6e"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7760), 5, 3, 5, 6, new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("cabc4b4e-e02f-4e19-8832-9632542864f4"), new DateTime(2025, 9, 23, 17, 54, 35, 676, DateTimeKind.Utc).AddTicks(7430), 2, 2, 2, 3, new Guid("11111111-1111-1111-1111-111111111111") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Key",
                table: "ApiKeys",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpConfigurations_Name_Type",
                table: "McpConfigurations",
                columns: new[] { "Name", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_TaskId",
                table: "TaskHistories",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_TeamMemberId",
                table: "TaskHistories",
                column: "TeamMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssigneeId",
                table: "Tasks",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkflowId",
                table: "Tasks",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_CurrentTaskId",
                table: "TeamMembers",
                column: "CurrentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_Email",
                table: "TeamMembers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_Name",
                table: "TeamMembers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Name",
                table: "Workflows",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_WorkflowId_FromState_ToState",
                table: "WorkflowTransitions",
                columns: new[] { "WorkflowId", "FromState", "ToState" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskHistories_Tasks_TaskId",
                table: "TaskHistories",
                column: "TaskId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskHistories_TeamMembers_TeamMemberId",
                table: "TaskHistories",
                column: "TeamMemberId",
                principalTable: "TeamMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_TeamMembers_AssigneeId",
                table: "Tasks",
                column: "AssigneeId",
                principalTable: "TeamMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamMembers_Tasks_CurrentTaskId",
                table: "TeamMembers");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "McpConfigurations");

            migrationBuilder.DropTable(
                name: "TaskHistories");

            migrationBuilder.DropTable(
                name: "WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "Workflows");
        }
    }
}
