using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0005a's four endpoints against the approved contract delta: the
/// `identity` group round trip, optimistic concurrency (spec 6.2.11 — both attempts on the audit
/// trail, only the winner gets a version row), the append-only <c>config_version</c> ledger, cursor
/// pagination (spec 9.5), and the idempotency substrate's replay path on a real product route for the
/// first time.
/// </summary>
public sealed class SettingsEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SettingsUrl = "/api/v1/settings";
    private const string IdentityUrl = "/api/v1/settings/identity";
    private const string RegNumberUrl = "/api/v1/settings/reg-number";
    private const string RegNumberPreviewUrl = "/api/v1/settings/reg-number/preview";
    private const string AbbreviationUrl = "/api/v1/settings/abbreviation";
    private const string ConfigVersionsUrl = "/api/v1/config-versions";

    [Fact]
    public async Task GetSettings_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        var response = await Client.GetAsync(new Uri(SettingsUrl, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSettings_AsSuperAdmin_ReturnsTheSeededIdentityGroup()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await GetAsync(SettingsUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = await ReadAsync<SettingsDto>(response);
        settings.Identity.Timezone.ShouldBe("Africa/Lagos");
        settings.Identity.VersionNumber.ShouldBe(0);
    }

    [Fact]
    public async Task UpdateIdentity_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        using var request = new HttpRequestMessage(HttpMethod.Patch, IdentityUrl)
        {
            Content = JsonContent.Create(ValidIdentityCommand(expectedVersion: 0)),
        };

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateIdentity_WithoutACsrfToken_Returns403CsrfMissing()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, IdentityUrl)
        {
            Content = JsonContent.Create(ValidIdentityCommand(expectedVersion: 0)),
        };
        jar.Apply(request); // Cookies, but deliberately no X-CSRF-Token.

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("csrf.missing");
    }

    [Fact]
    public async Task UpdateIdentity_WithAMalformedPhone_Returns422FieldKeyedToPhone()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await PatchAsync(
            IdentityUrl,
            jar,
            ValidIdentityCommand(expectedVersion: 0) with { Phone = "not-a-phone-number" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("request.validation_failed");
        document.RootElement.GetProperty("errors").TryGetProperty("Phone", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateIdentity_HappyPath_SavesAndReturnsTheIncrementedVersion()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await PatchAsync(IdentityUrl, jar, ValidIdentityCommand(expectedVersion: 0));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var identity = await ReadAsync<SettingsIdentityGroupDto>(response);
        identity.SchoolName.ShouldBe("Golden Royal Ark School");
        identity.Phone.ShouldBe("+2348012345678"); // Normalised from the national form submitted.
        identity.VersionNumber.ShouldBe(1);
        identity.Timezone.ShouldBe("Africa/Lagos"); // Fixed — echoed, never accepted from the body.

        // GET reflects the same saved state.
        var getResponse = await GetAsync(SettingsUrl, jar);
        var settings = await ReadAsync<SettingsDto>(getResponse);
        settings.Identity.VersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateIdentity_AttemptingToSmuggleTimezoneIntoTheBody_Returns400UnknownField()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, IdentityUrl)
        {
            Content = JsonContent.Create(new
            {
                schoolName = "Golden Royal Ark School",
                shortName = "GRAS",
                address = "12 Ark Crescent",
                phone = "08012345678",
                email = "info@example.com",
                motto = (string?)null,
                headTeacherName = "Chisom Maxwell",
                expectedVersion = 0,
                timezone = "UTC", // Not a bindable property — must be rejected, not silently ignored.
            }),
        };
        jar.ApplyWithCsrf(request);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateIdentity_TwoSavesAgainstTheSameVersion_TheFirstWinsAndTheSecondIsRejected()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var firstResponse = await PatchAsync(
            IdentityUrl, jar, ValidIdentityCommand(expectedVersion: 0) with { SchoolName = "First Save" });
        var secondResponse = await PatchAsync(
            IdentityUrl, jar, ValidIdentityCommand(expectedVersion: 0) with { SchoolName = "Second Save" });

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var document = await ReadJsonAsync(secondResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.identity.stale_version");

        // The winner's value stands; the loser never touched it.
        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        settings.Identity.SchoolName.ShouldBe("First Save");
        settings.Identity.VersionNumber.ShouldBe(1);

        // Append-only, under a real two-attempt race: exactly ONE new version row, not two.
        var versions = await ReadAsync<CursorPage<ConfigVersionSummaryDto>>(await GetAsync(ConfigVersionsUrl, jar));
        versions.Items.Count.ShouldBe(1);
        versions.Items[0].ChangedGroup.ShouldBe("Identity");
    }

    [Fact]
    public async Task ConfigVersion_ASecondSuccessfulSave_LeavesTheFirstRowByteIdentical()
    {
        // The central acceptance criterion, proven directly rather than inferred: config_version is
        // append-only in FACT, not merely by omitting an update path. Two genuinely successful,
        // correctly-versioned saves (no race, no rejection) must still leave row #1 untouched.
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var firstSaveResponse = await PatchAsync(
            IdentityUrl, jar, ValidIdentityCommand(expectedVersion: 0) with { SchoolName = "First Save" });
        firstSaveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstVersionId = (await ReadAsync<CursorPage<ConfigVersionSummaryDto>>(
            await GetAsync(ConfigVersionsUrl, jar))).Items[0].Id;
        var firstRowBeforeSecondSave = await ReadAsync<ConfigVersionDetailDto>(
            await GetAsync($"{ConfigVersionsUrl}/{firstVersionId}", jar));

        var secondSaveResponse = await PatchAsync(
            IdentityUrl, jar, ValidIdentityCommand(expectedVersion: 1) with { SchoolName = "Second Save" });
        secondSaveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstRowAfterSecondSave = await ReadAsync<ConfigVersionDetailDto>(
            await GetAsync($"{ConfigVersionsUrl}/{firstVersionId}", jar));

        firstRowAfterSecondSave.Id.ShouldBe(firstRowBeforeSecondSave.Id);
        firstRowAfterSecondSave.VersionNumber.ShouldBe(firstRowBeforeSecondSave.VersionNumber);
        firstRowAfterSecondSave.CreatedAtUtc.ShouldBe(firstRowBeforeSecondSave.CreatedAtUtc);
        firstRowAfterSecondSave.Snapshot.GetRawText().ShouldBe(firstRowBeforeSecondSave.Snapshot.GetRawText());

        // A second, distinct row exists for the second save — nothing was skipped either.
        var versions = await ReadAsync<CursorPage<ConfigVersionSummaryDto>>(await GetAsync(ConfigVersionsUrl, jar));
        versions.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateIdentity_RetriedWithTheSameIdempotencyKey_ReplaysAndWritesNoSecondVersionRow()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var idempotencyKey = $"key-{Guid.NewGuid():N}";
        var command = ValidIdentityCommand(expectedVersion: 0);

        var first = await PatchAsync(IdentityUrl, jar, command, idempotencyKey);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        first.Headers.Contains("Idempotency-Replay").ShouldBeFalse();
        var firstBody = await ReadAsync<SettingsIdentityGroupDto>(first);

        var replay = await PatchAsync(IdentityUrl, jar, command, idempotencyKey);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        replay.Headers.TryGetValues("Idempotency-Replay", out var values).ShouldBeTrue();
        values!.ShouldContain("true");
        var replayBody = await ReadAsync<SettingsIdentityGroupDto>(replay);
        replayBody.VersionNumber.ShouldBe(firstBody.VersionNumber);

        // Exactly one version row — the retry did not duplicate it.
        var versions = await ReadAsync<CursorPage<ConfigVersionSummaryDto>>(await GetAsync(ConfigVersionsUrl, jar));
        versions.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ListConfigVersions_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        var response = await Client.GetAsync(new Uri(ConfigVersionsUrl, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListConfigVersions_PaginatesByCursor_NeverByOffset()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        // Three saves, three version rows, newest first once listed.
        await PatchAsync(IdentityUrl, jar, ValidIdentityCommand(expectedVersion: 0) with { SchoolName = "Save 1" });
        await PatchAsync(IdentityUrl, jar, ValidIdentityCommand(expectedVersion: 1) with { SchoolName = "Save 2" });
        await PatchAsync(IdentityUrl, jar, ValidIdentityCommand(expectedVersion: 2) with { SchoolName = "Save 3" });

        var firstPageResponse = await GetAsync($"{ConfigVersionsUrl}?pageSize=1", jar);
        firstPageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPage = await ReadAsync<CursorPage<ConfigVersionSummaryDto>>(firstPageResponse);
        firstPage.Items.Count.ShouldBe(1);
        firstPage.Items[0].VersionNumber.ShouldBe(3);
        firstPage.NextCursor.ShouldNotBeNull();

        var secondPageResponse = await GetAsync(
            $"{ConfigVersionsUrl}?pageSize=1&cursor={Uri.EscapeDataString(firstPage.NextCursor!)}", jar);
        var secondPage = await ReadAsync<CursorPage<ConfigVersionSummaryDto>>(secondPageResponse);
        secondPage.Items.Count.ShouldBe(1);
        secondPage.Items[0].VersionNumber.ShouldBe(2);

        // Never the same row twice across pages — the defect a broken offset-style implementation
        // would produce if rows kept arriving between page reads.
        secondPage.Items[0].Id.ShouldNotBe(firstPage.Items[0].Id);
    }

    [Fact]
    public async Task ListConfigVersions_WithAMalformedCursor_Returns422()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await GetAsync($"{ConfigVersionsUrl}?cursor=not-a-valid-cursor!!", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("config_versions.invalid_cursor");
    }

    [Fact]
    public async Task GetConfigVersion_WhenItExists_ReturnsTheFullSnapshot()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        await PatchAsync(IdentityUrl, jar, ValidIdentityCommand(expectedVersion: 0));

        var list = await ReadAsync<CursorPage<ConfigVersionSummaryDto>>(await GetAsync(ConfigVersionsUrl, jar));
        var id = list.Items[0].Id;

        var response = await GetAsync($"{ConfigVersionsUrl}/{id}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await ReadAsync<ConfigVersionDetailDto>(response);
        detail.Id.ShouldBe(id);
        detail.ChangedGroup.ShouldBe("Identity");
        detail.Reason.ShouldBeNull();
        detail.Snapshot.GetProperty("schoolProfile").GetProperty("schoolName").GetString()
            .ShouldBe("Golden Royal Ark School");
    }

    [Fact]
    public async Task GetConfigVersion_WhenItDoesNotExist_Returns404()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await GetAsync($"{ConfigVersionsUrl}/{Guid.CreateVersion7()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("config_version.not_found");
    }

    [Fact]
    public async Task UpdateIdentity_WithoutThePrivilege_Returns403WithAGenericBody()
    {
        RequireDatabase();

        // Proves the ROUTE checks settings.identity.update specifically (not, say, settings.view by
        // mistake) — a real gap a happy-path super-admin test cannot catch, since the super-admin
        // flag bypass grants every privilege regardless of which one a route actually names. Uses the
        // same test-only auth substrate PrivilegeAuthorizationTests builds, since no real
        // non-super-admin account can be created without TASK-0027 (held for sign-off).
        var grants = new FakeEffectivePrivilegeProvider();
        var auditSink = new FakeAuthorizationAuditSink();

        await using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    configureOptions: null);

            services.RemoveAll<IEffectivePrivilegeProvider>();
            services.AddSingleton<IEffectivePrivilegeProvider>(grants);

            services.RemoveAll<IAuthorizationAuditSink>();
            services.AddSingleton<IAuthorizationAuditSink>(auditSink);
        }));

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch, IdentityUrl)
        {
            Content = JsonContent.Create(ValidIdentityCommand(expectedVersion: 0)),
        };
        request.Headers.Add("X-Test-User-Id", "user-without-settings-privilege");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("authorization.forbidden");
        auditSink.Rejections.ShouldContain(rejection =>
            rejection.UserId == "user-without-settings-privilege" &&
            rejection.Privilege == Privileges.Settings.IdentityUpdate);
    }

    [Fact]
    public async Task GetSettings_ReturnsTheSeededAbbreviationAndRegNumberGroupsWithTheirDefaults()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));

        settings.Abbreviation.Abbreviation.ShouldBe("GRAS");
        settings.Abbreviation.IssuedCount.ShouldBeNull(); // Amendment 2 — never 0.
        settings.Abbreviation.VersionNumber.ShouldBe(0);
        settings.RegNumber.Separator.ShouldBe("/");
        settings.RegNumber.SerialWidth.ShouldBe(4);
        settings.RegNumber.SerialReset.ShouldBe(RegNumberSerialReset.PerYear);
        settings.RegNumber.YearSource.ShouldBe("AdmissionYear");
        settings.RegNumber.VersionNumber.ShouldBe(0);
    }

    [Fact]
    public async Task UpdateRegNumber_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        using var request = new HttpRequestMessage(HttpMethod.Patch, RegNumberUrl)
        {
            Content = JsonContent.Create(ValidRegNumberCommand(expectedVersion: 0)),
        };

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateRegNumber_WithoutACsrfToken_Returns403CsrfMissing()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, RegNumberUrl)
        {
            Content = JsonContent.Create(ValidRegNumberCommand(expectedVersion: 0)),
        };
        jar.Apply(request); // Cookies, but deliberately no X-CSRF-Token.

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("csrf.missing");
    }

    [Fact]
    public async Task UpdateRegNumber_HappyPath_SavesAndReturnsTheIncrementedVersion()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await PatchAsync(
            RegNumberUrl,
            jar,
            ValidRegNumberCommand(expectedVersion: 0) with { Separator = "-", SerialWidth = 5 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var regNumber = await ReadAsync<SettingsRegNumberGroupDto>(response);
        regNumber.Separator.ShouldBe("-");
        regNumber.SerialWidth.ShouldBe(5);
        regNumber.VersionNumber.ShouldBe(1);

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        settings.RegNumber.Separator.ShouldBe("-");
        settings.RegNumber.VersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateRegNumber_TwoSavesAgainstTheSameVersion_TheFirstWinsAndTheSecondIsRejected()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var firstResponse = await PatchAsync(
            RegNumberUrl, jar, ValidRegNumberCommand(expectedVersion: 0) with { Separator = "-" });
        var secondResponse = await PatchAsync(
            RegNumberUrl, jar, ValidRegNumberCommand(expectedVersion: 0) with { Separator = "." });

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var document = await ReadJsonAsync(secondResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.regnumber.stale_version");

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        settings.RegNumber.Separator.ShouldBe("-"); // The winner's value stands.
        settings.RegNumber.VersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateRegNumber_ReducingWidthBelowAnIssuedSerial_Returns409NamingTheRealSerialAndTheMinimumWidth()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var currentYear = DateTimeOffset.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        await SeedRegistrationCounterAsync(currentYear, lastSerial: 1043);

        var response = await PatchAsync(
            RegNumberUrl, jar, ValidRegNumberCommand(expectedVersion: 0) with { SerialWidth = 3 });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.regnumber.width_too_small");
        document.RootElement.GetProperty("detail").GetString()
            .ShouldBe("Serial 1043 will not fit in a width of 3. Choose 4 or more.");

        // Nothing was written — the group's version pointer never moved.
        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        settings.RegNumber.VersionNumber.ShouldBe(0);
    }

    [Fact]
    public async Task GetRegNumberPreview_WithAnEmptyRegister_TheFirstSerialIsOne()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await GetAsync($"{RegNumberPreviewUrl}?separator=%2F&serialWidth=4", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await ReadAsync<RegNumberPreviewDto>(response);
        var currentYear = DateTimeOffset.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        preview.Preview.ShouldBe($"GRAS/{currentYear}/0001");
    }

    [Fact]
    public async Task GetRegNumberPreview_UsesTheUnsavedQueryParametersAndTheSavedAbbreviation()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var currentYear = DateTimeOffset.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        await SeedRegistrationCounterAsync(currentYear, lastSerial: 39);

        var response = await GetAsync($"{RegNumberPreviewUrl}?separator=-&serialWidth=5", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await ReadAsync<RegNumberPreviewDto>(response);
        preview.Preview.ShouldBe($"GRAS-{currentYear}-00040"); // "-" and width 5 — neither is saved.
    }

    [Fact]
    public async Task GetRegNumberPreview_UnderContinuous_ReadsTheAllPartitionInsteadOfTheYearPartition()
    {
        // Amendment 1, proven end to end: the SAME real request, only the SAVED serialReset differs,
        // reads a completely different counter row.
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var currentYear = DateTimeOffset.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        await SeedRegistrationCounterAsync(currentYear, lastSerial: 39);
        await SeedRegistrationCounterAsync(RegistrationCounterPartition.ContinuousKey, lastSerial: 999);

        var underPerYear = await ReadAsync<RegNumberPreviewDto>(
            await GetAsync($"{RegNumberPreviewUrl}?separator=%2F&serialWidth=4", jar));
        underPerYear.Preview.ShouldBe($"GRAS/{currentYear}/0040");

        var switchResponse = await PatchAsync(
            RegNumberUrl,
            jar,
            ValidRegNumberCommand(expectedVersion: 0) with { SerialReset = RegNumberSerialReset.Continuous });
        switchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var underContinuous = await ReadAsync<RegNumberPreviewDto>(
            await GetAsync($"{RegNumberPreviewUrl}?separator=%2F&serialWidth=4", jar));
        underContinuous.Preview.ShouldBe($"GRAS/{currentYear}/1000"); // From the "ALL" row, not "2026".
    }

    [Fact]
    public async Task UpdateAbbreviation_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        using var request = new HttpRequestMessage(HttpMethod.Patch, AbbreviationUrl)
        {
            Content = JsonContent.Create(ValidAbbreviationCommand(expectedVersion: 0)),
        };

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAbbreviation_WithoutTheLiteralConfirmationToken_Returns422()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await PatchAsync(
            AbbreviationUrl,
            jar,
            ValidAbbreviationCommand(expectedVersion: 0) with { ConfirmationToken = "change" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").TryGetProperty("ConfirmationToken", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAbbreviation_WithAnEmptyReason_Returns422()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await PatchAsync(
            AbbreviationUrl,
            jar,
            ValidAbbreviationCommand(expectedVersion: 0) with { Reason = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").TryGetProperty("Reason", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAbbreviation_HappyPath_SavesAndRewritesNothingElse()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await PatchAsync(AbbreviationUrl, jar, ValidAbbreviationCommand(expectedVersion: 0));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var abbreviation = await ReadAsync<SettingsAbbreviationGroupDto>(response);
        abbreviation.Abbreviation.ShouldBe("GRA");
        abbreviation.IssuedCount.ShouldBeNull();
        abbreviation.VersionNumber.ShouldBe(1);

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        settings.Abbreviation.Abbreviation.ShouldBe("GRA");
        settings.Identity.VersionNumber.ShouldBe(0); // Untouched — independent version pointer.
    }

    [Fact]
    public async Task UpdateAbbreviation_ToAValueAlreadyUsedHistorically_IsAllowed()
    {
        // Spec 6.2.11: "Allowed. Abbreviations are not unique over time..."
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var firstChange = await PatchAsync(AbbreviationUrl, jar, ValidAbbreviationCommand(expectedVersion: 0));
        firstChange.StatusCode.ShouldBe(HttpStatusCode.OK);

        var backToOriginal = await PatchAsync(
            AbbreviationUrl,
            jar,
            ValidAbbreviationCommand(expectedVersion: 1) with { Abbreviation = "GRAS" });

        backToOriginal.StatusCode.ShouldBe(HttpStatusCode.OK);
        var abbreviation = await ReadAsync<SettingsAbbreviationGroupDto>(backToOriginal);
        abbreviation.Abbreviation.ShouldBe("GRAS");
    }

    [Fact]
    public async Task UpdateAbbreviation_DoesNotTouchTheRegistrationCounter()
    {
        // Approved delta: "the serial counter is keyed on the admission year alone and not on the
        // abbreviation" — proven directly by seeding a counter row and asserting the preview built
        // from the NEW abbreviation still reflects that exact seeded serial, unrestarted.
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var currentYear = DateTimeOffset.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        await SeedRegistrationCounterAsync(currentYear, lastSerial: 39);

        var changeResponse = await PatchAsync(AbbreviationUrl, jar, ValidAbbreviationCommand(expectedVersion: 0));
        changeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var preview = await ReadAsync<RegNumberPreviewDto>(
            await GetAsync($"{RegNumberPreviewUrl}?separator=%2F&serialWidth=4", jar));

        preview.Preview.ShouldBe($"GRA/{currentYear}/0040"); // New abbreviation, SAME counter position.
    }

    [Fact]
    public async Task UpdateAbbreviation_TwoSavesAgainstTheSameVersion_TheFirstWinsAndTheSecondIsRejected()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var firstResponse = await PatchAsync(AbbreviationUrl, jar, ValidAbbreviationCommand(expectedVersion: 0));
        var secondResponse = await PatchAsync(
            AbbreviationUrl,
            jar,
            ValidAbbreviationCommand(expectedVersion: 0) with { Abbreviation = "GOLD" });

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var document = await ReadJsonAsync(secondResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.abbreviation.stale_version");

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        settings.Abbreviation.Abbreviation.ShouldBe("GRA"); // The winner's value stands.
    }

    /// <summary>Inserts a <c>registration_counter</c> row directly, bypassing the application layer —
    /// this card exposes no write path, so seeding real counter state for a test has no other route.</summary>
    private async Task SeedRegistrationCounterAsync(string counterKey, int lastSerial)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO registration_counter (counter_key, last_serial) VALUES ({counterKey}, {lastSerial})",
            TestContext.Current.CancellationToken);
    }

    private static UpdateRegNumberCommand ValidRegNumberCommand(int expectedVersion) => new(
        "/",
        4,
        RegNumberSerialReset.PerYear,
        expectedVersion);

    private static UpdateAbbreviationCommand ValidAbbreviationCommand(int expectedVersion) => new(
        "GRA",
        UpdateAbbreviationCommandValidator.RequiredConfirmationToken,
        "The school shortened its registered trading name.",
        expectedVersion);

    private static UpdateSchoolIdentityCommand ValidIdentityCommand(int expectedVersion) => new(
        "Golden Royal Ark School",
        "GRAS",
        "12 Ark Crescent, Lekki, Lagos",
        "08012345678",
        "info@example.com",
        "Excellence Through Character",
        "Chisom Maxwell",
        expectedVersion);

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> GetAsync(string url, CookieJar jar) => GetAsync(Client, url, jar);

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload) =>
        PostAsync(Client, url, jar, payload);

    private static async Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PatchAsync<T>(string url, CookieJar jar, T payload, string? idempotencyKey = null) =>
        PatchAsync(Client, url, jar, payload, idempotencyKey);

    private static async Task<HttpResponseMessage> PatchAsync<T>(
        HttpClient client,
        string url,
        CookieJar jar,
        T payload,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(payload),
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
