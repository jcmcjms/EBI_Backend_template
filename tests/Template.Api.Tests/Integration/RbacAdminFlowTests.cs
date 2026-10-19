using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Template.Api.Features.Auth;
using Template.Api.Features.Roles;
using Template.Api.Features.Users;
using Template.Api.Infrastructure.Authorization;
using Template.Api.Infrastructure.Paging;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Tests.Integration;

/// <summary>
/// End-to-end RBAC and user/role administration flows against real SQL Server:
/// permission enforcement, profile management, role lifecycle guards, pagination bounds, disable semantics.
/// </summary>
[Collection(SqlServerIntegrationCollectionDefinition.Name)]
public sealed class RbacAdminFlowTests(SqlServerIntegrationFixture fixture)
{
    private const string InitialPassword = "Initial-Password-123!";

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = fixture.CreateClient();
        var tokens = await IntegrationHttp.LoginAsync(
            client,
            SqlServerIntegrationFixture.SeedAdminEmail,
            SqlServerIntegrationFixture.SeedAdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    [Fact]
    public async Task PermissionCatalog_IsExposedToAuthenticatedCallers()
    {
        fixture.SkipIfUnavailable();
        using var admin = await CreateAdminClientAsync();

        var permissions = await admin.GetFromJsonAsync<List<string>>("/api/permissions", IntegrationHttp.Json);

        Assert.NotNull(permissions);
        Assert.Equal([.. Permissions.All.Order(StringComparer.Ordinal)], [.. permissions.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task RbacFlow_PermissionsAreEnforcedPerEndpoint_AndFollowRoleChanges()
    {
        fixture.SkipIfUnavailable();
        using var admin = await CreateAdminClientAsync();

        // Auditor role can read users but not write them.
        var auditorRole = await CreateRoleAsync(admin, "Auditor", [Permissions.UsersRead]);
        var email = IntegrationHttp.UniqueEmail("auditor");
        var auditor = await CreateUserAsync(admin, email, IntegrationHttp.UniqueUsername("auditor"), [auditorRole.Id]);

        using var auditorClient = fixture.CreateClient();
        var auditorTokens = await IntegrationHttp.LoginAsync(auditorClient, email, InitialPassword);
        auditorClient.WithBearer(auditorTokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await auditorClient.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditorClient.PostAsJsonAsync("/api/users", NewUserRequest("denied"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditorClient.GetAsync("/api/roles")).StatusCode);

        // Grant write access: a new role plus reassignment, effective with the next issued token.
        var writerRole = await CreateRoleAsync(admin, "Writer", [Permissions.UsersWrite]);
        var setRoles = await admin.PatchAsJsonAsync($"/api/users/{auditor.Id}/roles", new SetUserRolesRequest
        {
            RoleIds = [auditorRole.Id, writerRole.Id],
        });
        Assert.Equal(HttpStatusCode.OK, setRoles.StatusCode);

        var refreshedTokens = await IntegrationHttp.LoginAsync(fixture.CreateClient(), email, InitialPassword);
        using var promotedClient = fixture.CreateClient().WithBearer(refreshedTokens.AccessToken);
        var created = await promotedClient.PostAsJsonAsync("/api/users", NewUserRequest("created-by-auditor"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var me = await promotedClient.GetFromJsonAsync<CurrentUserResponse>("/api/users/me", IntegrationHttp.Json);
        Assert.NotNull(me);
        Assert.Equal(
            [Permissions.UsersRead, Permissions.UsersWrite],
            [.. me.Permissions.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task CreateUser_RejectsUnknownRoles_DuplicateEmails_DuplicateUsernames_AndWeakPasswords()
    {
        fixture.SkipIfUnavailable();
        using var admin = await CreateAdminClientAsync();

        var unknownRole = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Email = IntegrationHttp.UniqueEmail("unknown-role"),
            Username = IntegrationHttp.UniqueUsername("unknownrole"),
            FirstName = "Test",
            LastName = "User",
            Password = InitialPassword,
            RoleIds = [Guid.NewGuid()],
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknownRole.StatusCode);

        var email = IntegrationHttp.UniqueEmail("duplicate");
        var username = IntegrationHttp.UniqueUsername("duplicate");
        var first = await CreateUserAsync(admin, email, username, []);
        Assert.NotEqual(Guid.Empty, first.Id);

        var duplicateEmail = await admin.PostAsJsonAsync("/api/users", NewUserRequest("dup-email", email: email.ToUpperInvariant()));
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmail.StatusCode); // email uniqueness is case-insensitive by normalization

        var duplicateUsername = await admin.PostAsJsonAsync("/api/users", NewUserRequest("dup-username", username: username.ToUpperInvariant()));
        Assert.Equal(HttpStatusCode.Conflict, duplicateUsername.StatusCode);

        var invalidUsername = await admin.PostAsJsonAsync("/api/users", NewUserRequest("bad-username", username: "not valid!"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidUsername.StatusCode);
        var problem = JsonDocument.Parse(await invalidUsername.Content.ReadAsStringAsync());
        Assert.Equal("Invalid username", problem.RootElement.GetProperty("title").GetString());

        var weak = await admin.PostAsJsonAsync("/api/users", NewUserRequest("weak", password: "only-11-ch"));
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
    }

    [Fact]
    public async Task ProfileUpdate_ReplacesAllFields_AndTheNewUsernameWorksForLogin()
    {
        fixture.SkipIfUnavailable();
        using var admin = await CreateAdminClientAsync();
        var email = IntegrationHttp.UniqueEmail("profile");
        var user = await CreateUserAsync(admin, email, IntegrationHttp.UniqueUsername("profile"), []);

        var newUsername = IntegrationHttp.UniqueUsername("renamed");
        var update = await admin.PutAsJsonAsync($"/api/users/{user.Id}/profile", new UpdateUserProfileRequest
        {
            Username = newUsername,
            FirstName = "Grace",
            MiddleName = "Brewster",
            LastName = "Hopper",
            Suffix = "Jr.",
            JobTitle = "Rear Admiral",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<UserResponse>(IntegrationHttp.Json);
        Assert.NotNull(updated);
        Assert.Equal(newUsername, updated.Username);
        Assert.Equal("Grace", updated.FirstName);
        Assert.Equal("Brewster", updated.MiddleName);
        Assert.Equal("Hopper", updated.LastName);
        Assert.Equal("Jr.", updated.Suffix);
        Assert.Equal("Rear Admiral", updated.JobTitle);

        // Omitted optional fields are cleared by the full-replacement contract.
        var clear = await admin.PutAsJsonAsync($"/api/users/{user.Id}/profile", new UpdateUserProfileRequest
        {
            Username = newUsername,
            FirstName = "Grace",
            LastName = "Hopper",
        });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var cleared = await clear.Content.ReadFromJsonAsync<UserResponse>(IntegrationHttp.Json);
        Assert.NotNull(cleared);
        Assert.Null(cleared.MiddleName);
        Assert.Null(cleared.Suffix);
        Assert.Null(cleared.JobTitle);

        // The renamed handle works for login, and /me reflects the fresh database state.
        using var client = fixture.CreateClient();
        var tokens = await IntegrationHttp.LoginAsync(client, newUsername, InitialPassword);
        var me = await client.WithBearer(tokens.AccessToken)
            .GetFromJsonAsync<CurrentUserResponse>("/api/users/me", IntegrationHttp.Json);
        Assert.NotNull(me);
        Assert.Equal(newUsername, me.Username);
        Assert.Equal("Hopper", me.LastName);

        // Conflicts and invalid handles are rejected.
        var other = await CreateUserAsync(admin, IntegrationHttp.UniqueEmail("other"), IntegrationHttp.UniqueUsername("other"), []);
        var conflict = await admin.PutAsJsonAsync($"/api/users/{other.Id}/profile", new UpdateUserProfileRequest
        {
            Username = newUsername,
            FirstName = "Conflict",
            LastName = "Test",
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var invalid = await admin.PutAsJsonAsync($"/api/users/{other.Id}/profile", new UpdateUserProfileRequest
        {
            Username = "invalid handle",
            FirstName = "Conflict",
            LastName = "Test",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var missing = await admin.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}/profile", new UpdateUserProfileRequest
        {
            Username = newUsername,
            FirstName = "Nobody",
            LastName = "Here",
        });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_HandlesNotFound_EmailConflicts_AndSelfDisableGuard()
    {
        fixture.SkipIfUnavailable();
        using var admin = await CreateAdminClientAsync();

        var missing = await admin.PatchAsJsonAsync($"/api/users/{Guid.NewGuid()}", new UpdateUserRequest { IsDisabled = true });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var adminProfile = await admin.GetFromJsonAsync<CurrentUserResponse>("/api/users/me", IntegrationHttp.Json);
        Assert.NotNull(adminProfile);
        var selfDisable = await admin.PatchAsJsonAsync($"/api/users/{adminProfile.Id}", new UpdateUserRequest { IsDisabled = true });
        Assert.Equal(HttpStatusCode.Conflict, selfDisable.StatusCode);

        var first = await CreateUserAsync(admin, IntegrationHttp.UniqueEmail("conflict-a"), IntegrationHttp.UniqueUsername("conflicta"), []);
        var second = await CreateUserAsync(admin, IntegrationHttp.UniqueEmail("conflict-b"), IntegrationHttp.UniqueUsername("conflictb"), []);
        var conflict = await admin.PatchAsJsonAsync($"/api/users/{second.Id}", new UpdateUserRequest { Email = first.Email });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task DisableUser_BlocksLoginAndRevokesSessions_AndEnableRestoresAccess()
    {
        fixture.SkipIfUnavailable();
        using var admin = await CreateAdminClientAsync();
        var email = IntegrationHttp.UniqueEmail("disable");
        var username = IntegrationHttp.UniqueUsername("disable");
        var user = await CreateUserAsync(admin, email, username, []);

        using var userClient = fixture.CreateClient();
        var tokens = await IntegrationHttp.LoginAsync(userClient, username, InitialPassword); // login by username

        var disable = await admin.PatchAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest { IsDisabled = true });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        var disabledDetail = await disable.Content.ReadFromJsonAsync<UserResponse>(IntegrationHttp.Json);
        Assert.NotNull(disabledDetail);
        Assert.True(disabledDetail.IsDisabled);

        // The active refresh session dies with the disable.
        var refresh = await userClient.PostAsJsonAsync("/api/auth/refresh", new RefreshSessionRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        var login = await userClient.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = email, Password = InitialPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var enable = await admin.PatchAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest { IsDisabled = false });
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);

        var loginAgain = await userClient.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = email, Password = InitialPassword });
        Assert.Equal(HttpStatusCode.OK, loginAgain.StatusCode);
    }

    [Fact]
    public async Task Roles_GuardSystemRole_AssignedDeletion_AndUnknownPermissions()
    {
        fixture.SkipIfUnavailable();
        using var admin = await CreateAdminClientAsync();

        var rolesPage = await admin.GetFromJsonAsync<PagedResponse<RoleSummaryResponse>>(
            "/api/roles?page=1&pageSize=100", IntegrationHttp.Json);
        Assert.NotNull(rolesPage);
        var systemAdmin = rolesPage.Items.Single(role => role.IsSystem);
        Assert.Equal(DatabaseSeeder.AdminRoleName, systemAdmin.Name);

        var unknownPermission = await admin.PostAsJsonAsync("/api/roles", new SaveRoleRequest
        {
            Name = "Broken-" + Guid.NewGuid().ToString("N")[..8],
            Permissions = ["users.read", "billing.everything"],
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknownPermission.StatusCode);
        var problem = JsonDocument.Parse(await unknownPermission.Content.ReadAsStringAsync());
        Assert.Contains("billing.everything", problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        var modifySystem = await admin.PutAsJsonAsync($"/api/roles/{systemAdmin.Id}", new SaveRoleRequest
        {
            Name = "NotAdmin",
            Permissions = [Permissions.UsersRead],
        });
        Assert.Equal(HttpStatusCode.Conflict, modifySystem.StatusCode);

        var deleteSystem = await admin.DeleteAsync($"/api/roles/{systemAdmin.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteSystem.StatusCode);

        // Custom role lifecycle: delete blocked while assigned, allowed once unassigned.
        var role = await CreateRoleAsync(admin, "Temporary", [Permissions.RolesRead]);
        var email = IntegrationHttp.UniqueEmail("role-holder");
        var user = await CreateUserAsync(admin, email, IntegrationHttp.UniqueUsername("roleholder"), [role.Id]);

        var deleteAssigned = await admin.DeleteAsync($"/api/roles/{role.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteAssigned.StatusCode);

        var unassign = await admin.PatchAsJsonAsync($"/api/users/{user.Id}/roles", new SetUserRolesRequest { RoleIds = [] });
        Assert.Equal(HttpStatusCode.OK, unassign.StatusCode);

        var deleteUnassigned = await admin.DeleteAsync($"/api/roles/{role.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteUnassigned.StatusCode);

        var rolesAfterDelete = await admin.GetFromJsonAsync<PagedResponse<RoleSummaryResponse>>(
            "/api/roles?page=1&pageSize=100", IntegrationHttp.Json);
        Assert.NotNull(rolesAfterDelete);
        Assert.DoesNotContain(rolesAfterDelete.Items, candidate => candidate.Id == role.Id);
    }

    [Fact]
    public async Task DuplicateRoleName_IsRejected()
    {
        fixture.SkipIfUnavailable();
        using var admin = await CreateAdminClientAsync();

        var name = "Unique-" + Guid.NewGuid().ToString("N")[..8];
        var first = await CreateRoleAsync(admin, name, [Permissions.UsersRead]);

        var duplicate = await admin.PostAsJsonAsync("/api/roles", new SaveRoleRequest { Name = first.Name, Permissions = [Permissions.UsersRead] });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task UserList_Pagination_IsBoundedDeterministicAndValidated()
    {
        fixture.SkipIfUnavailable();
        using var admin = await CreateAdminClientAsync();

        for (var index = 0; index < 3; index++)
        {
            await CreateUserAsync(admin, IntegrationHttp.UniqueEmail("page"), IntegrationHttp.UniqueUsername("page"), []);
        }

        var oversizedPage = await admin.GetAsync("/api/users?page=1&pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, oversizedPage.StatusCode);

        var firstPage = await admin.GetFromJsonAsync<PagedResponse<UserResponse>>(
            "/api/users?page=1&pageSize=2", IntegrationHttp.Json);
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.True(firstPage.TotalCount >= 4); // seeded admin + created users

        // Deterministic ordering: two reads of the same page return the same ids in the same order.
        var firstPageAgain = await admin.GetFromJsonAsync<PagedResponse<UserResponse>>(
            "/api/users?page=1&pageSize=2", IntegrationHttp.Json);
        Assert.NotNull(firstPageAgain);
        Assert.Equal(
            [.. firstPage.Items.Select(user => user.Id)],
            [.. firstPageAgain.Items.Select(user => user.Id)]);

        // A page beyond the end is empty but still reports the total.
        var beyondEnd = await admin.GetFromJsonAsync<PagedResponse<UserResponse>>(
            "/api/users?page=100000&pageSize=100", IntegrationHttp.Json);
        Assert.NotNull(beyondEnd);
        Assert.Empty(beyondEnd.Items);
        Assert.Equal(firstPage.TotalCount, beyondEnd.TotalCount);

        // Default page size applies when nothing is supplied.
        var defaultPage = await admin.GetFromJsonAsync<PagedResponse<UserResponse>>("/api/users", IntegrationHttp.Json);
        Assert.NotNull(defaultPage);
        Assert.Equal(PagingLimits.DefaultPageSize, defaultPage.PageSize);
        Assert.True(defaultPage.Items.Count <= PagingLimits.DefaultPageSize);
    }

    private static CreateUserRequest NewUserRequest(
        string prefix,
        string? email = null,
        string? username = null,
        string? password = null) =>
        new()
        {
            Email = email ?? IntegrationHttp.UniqueEmail(prefix),
            Username = username ?? IntegrationHttp.UniqueUsername(prefix),
            FirstName = "Test",
            LastName = "User",
            Password = password ?? InitialPassword,
        };

    private static async Task<RoleSummaryResponse> CreateRoleAsync(HttpClient admin, string name, IReadOnlyList<string> permissions)
    {
        var response = await admin.PostAsJsonAsync("/api/roles", new SaveRoleRequest
        {
            Name = name + "-" + Guid.NewGuid().ToString("N")[..8],
            Permissions = permissions,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var role = await response.Content.ReadFromJsonAsync<RoleSummaryResponse>(IntegrationHttp.Json);
        Assert.NotNull(role);
        return role;
    }

    private static async Task<UserResponse> CreateUserAsync(HttpClient admin, string email, string username, IReadOnlyList<Guid> roleIds)
    {
        var response = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Email = email,
            Username = username,
            FirstName = "Test",
            MiddleName = "M",
            LastName = "User",
            JobTitle = "QA",
            Password = InitialPassword,
            RoleIds = roleIds,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>(IntegrationHttp.Json);
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
        Assert.Equal("M", user.MiddleName);
        Assert.Equal("QA", user.JobTitle);
        return user;
    }
}
