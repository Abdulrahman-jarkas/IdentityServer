using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDriverTenantType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AspNetUsers_UserId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Tenants_TenantId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_AppRoles_Tenants_TenantId",
                table: "AppRoles");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_AppRoles_TenantId_Name",
                table: "AppRoles");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_TenantId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_UserId_TenantId",
                table: "Accounts");

            migrationBuilder.AddColumn<string>(
                name: "Permissions",
                table: "AppRoles",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantType",
                table: "AppRoles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Accounts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Accounts",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantType",
                table: "Accounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Accounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRoles_TenantType_TenantId_Name",
                table: "AppRoles",
                columns: new[] { "TenantType", "TenantId", "Name" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ApplicationUserId",
                table: "Accounts",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId_TenantType_TenantId",
                table: "Accounts",
                columns: new[] { "UserId", "TenantType", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AspNetUsers_ApplicationUserId",
                table: "Accounts",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AspNetUsers_UserId",
                table: "Accounts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AspNetUsers_ApplicationUserId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AspNetUsers_UserId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_AppRoles_TenantType_TenantId_Name",
                table: "AppRoles");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_ApplicationUserId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_UserId_TenantType_TenantId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "AppRoles");

            migrationBuilder.DropColumn(
                name: "TenantType",
                table: "AppRoles");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TenantType",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Accounts");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Accounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_AppRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AppRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Accounts_CreatedByAccountId",
                        column: x => x.CreatedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tenants_Tenants_ParentTenantId",
                        column: x => x.ParentTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RoleIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_Accounts_CreatedAccountId",
                        column: x => x.CreatedAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invitations_Accounts_InvitedByAccountId",
                        column: x => x.InvitedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invitations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppRoles_TenantId_Name",
                table: "AppRoles",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId",
                table: "Accounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId_TenantId",
                table: "Accounts",
                columns: new[] { "UserId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_CreatedAccountId",
                table: "Invitations",
                column: "CreatedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email_TenantId_Status",
                table: "Invitations",
                columns: new[] { "Email", "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InvitedByAccountId",
                table: "Invitations",
                column: "InvitedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TenantId",
                table: "Invitations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Token",
                table: "Invitations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_Permission",
                table: "RolePermissions",
                columns: new[] { "RoleId", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedByAccountId",
                table: "Tenants",
                column: "CreatedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ParentTenantId",
                table: "Tenants",
                column: "ParentTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AspNetUsers_UserId",
                table: "Accounts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Tenants_TenantId",
                table: "Accounts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppRoles_Tenants_TenantId",
                table: "AppRoles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }
    }
}
