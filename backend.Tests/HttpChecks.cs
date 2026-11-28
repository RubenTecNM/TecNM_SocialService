using TecNM.Api.Modules.Expedients.Pdf;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TecNM.Api.Core.Entities;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using TecNM.Api.Core.Storage;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Npgsql;
using TecNM.Api.Data;

internal static class HttpChecks
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("DOCUMENT_TEST_CONNECTION");
        if (string.IsNullOrEmpty(connection)) { Console.WriteLine("HTTP checks skipped: isolated PostgreSQL required."); return; }
        if (new NpgsqlConnectionStringBuilder(connection).Database != "document_checks") throw new Exception("Isolated database required.");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connection);
        Environment.SetEnvironmentVariable("Jwt__SecretKey", "document-tests-only-signing-key-at-least-32-bytes");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "document-tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "document-tests");
        Environment.SetEnvironmentVariable("Jwt__LifetimeHours", "8");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigin", "https://localhost");
        Environment.SetEnvironmentVariable("Storage__RootPath", Path.GetFullPath("tmp/pdf-qa/http-storage"));
        var resourceRoot = Path.GetFullPath("backend/Resources/Documents");
        var fixtureRoot = Path.GetFullPath("tmp/pdf-qa/http-resources/" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.EnumerateFiles(resourceRoot, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(fixtureRoot, Path.GetRelativePath(resourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        Environment.SetEnvironmentVariable("Documents__RootPath", fixtureRoot);
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_TECNM_API", Path.GetFullPath("backend"));
        var failureStorage = new FailingStorage(new LocalFileStorage(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:RootPath"] = Path.GetFullPath("tmp/pdf-qa/http-storage") }).Build()));
        var databaseFailure = new FailingDocumentSave();
        var testClock = new DocumentTestClock(TimeProvider.System.GetUtcNow());
        using var factory = new WebApplicationFactory<TecNMDbContext>().WithWebHostBuilder(builder =>
            builder.UseContentRoot(Path.GetFullPath("backend")).UseEnvironment("Testing").ConfigureLogging(logging => logging.ClearProviders()).ConfigureTestServices(services => { services.RemoveAll<IFileStorage>(); services.AddSingleton<IFileStorage>(failureStorage); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(testClock); services.AddDbContext<TecNMDbContext>(options => options.UseNpgsql(connection).AddInterceptors(databaseFailure)); }));
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await using (var importScope = factory.Services.CreateAsyncScope())
        {
            var catalog = importScope.ServiceProvider.GetRequiredService<TecNM.Api.Modules.Expedients.Templates.TemplateCatalog>();
            await catalog.ImportAsync();
            await catalog.ImportAsync();
        }
        Expect(await anonymous.GetAsync("/api/documents/review-queue"), HttpStatusCode.Unauthorized);
        Expect(await anonymous.PostAsync("/api/hubs/sspp/negotiate?negotiateVersion=1", null), HttpStatusCode.Unauthorized);
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var student = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await Login(admin, "admin");
        await Login(student, "l23050363@monclova.tecnm.mx");
        await CheckTemplateManagementAsync(factory, student);
        Expect(await admin.PostAsync("/api/hubs/sspp/negotiate?negotiateVersion=1", null), HttpStatusCode.OK);
        Expect(await student.PostAsync("/api/hubs/sspp/negotiate?negotiateVersion=1", null), HttpStatusCode.OK);
        Expect(await student.GetAsync("/api/documents/review-queue"), HttpStatusCode.Forbidden);
        Expect(await anonymous.GetAsync("/api/social-services/1/pdf/individual_act"), HttpStatusCode.Unauthorized);
        Expect(await student.GetAsync("/api/social-services/1/pdf/individual_act"), HttpStatusCode.Forbidden);
        Expect(await admin.GetAsync("/api/social-services/1/pdf/individual_act"), HttpStatusCode.Conflict);

        Expect(await student.GetAsync("/api/students/2/documents"), HttpStatusCode.NotFound);
        Expect(await student.PutAsJsonAsync("/api/internships/1/evaluation", new { }), HttpStatusCode.Forbidden);
        Expect(await student.PutAsJsonAsync("/api/social-services/1/evaluations/external_supervisor/1", new { }), HttpStatusCode.Forbidden);
        var template = await student.GetAsync("/api/document-templates/ss-acceptance-example/file");
        Expect(template, HttpStatusCode.OK);
        if (template.Content.Headers.ContentType?.MediaType != "application/pdf") throw new Exception("Template MIME type");
        Expect(await student.GetAsync("/api/document-templates/private/file"), HttpStatusCode.NotFound);
        var bytes = File.ReadAllBytes("backend/Resources/Documents/templates/pp-evaluation.pdf");
        var today = TecNM.Api.Modules.Expedients.ImssEligibilityService.Today(TimeProvider.System);
        MultipartFormDataContent ImssForm(DateOnly date, bool general = false)
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(bytes), "file", "imss.pdf");
            form.Add(new StringContent(date.ToString("yyyy-MM-dd")), "documentDate");
            if (general) { form.Add(new StringContent("general"), "tramiteType"); form.Add(new StringContent("IMSS"), "documentCode"); }
            return form;
        }
        using (var future = ImssForm(today.AddDays(1)))
            Expect(await student.PutAsync("/api/students/me/imss", future), HttpStatusCode.BadRequest);
        using (var future = ImssForm(today.AddDays(1), true))
            Expect(await student.PostAsync("/api/students/me/documents", future), HttpStatusCode.BadRequest);
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(bytes), "file", "imss.pdf");
        upload.Add(new StringContent(today.AddDays(-1).ToString("yyyy-MM-dd")), "documentDate");
        var response = await student.PutAsync("/api/students/me/imss", upload);
        Expect(response, HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (profile.GetProperty("hasValidImss").GetBoolean()) throw new Exception("Upload incorrectly approved IMSS");
        if (profile.GetProperty("imssDocumentDate").GetString() != today.AddDays(-1).ToString("yyyy-MM-dd")) throw new Exception("Declared IMSS date was discarded.");
        Expect(await admin.PostAsJsonAsync("/api/students/1/imss/validation", new { isValid = false }), HttpStatusCode.BadRequest);
        Expect(await admin.PostAsJsonAsync("/api/students/1/imss/validation", new { isValid = true }), HttpStatusCode.OK);
        Expect(await admin.PostAsJsonAsync("/api/students/1/imss/validation", new { isValid = true, documentDate = today }), HttpStatusCode.OK);
        var approvedProfile = await student.GetFromJsonAsync<JsonElement>("/api/students/me");
        if (!approvedProfile.GetProperty("hasValidImss").GetBoolean()) throw new Exception("Approval projection missing");
        using (var sameFile = ImssForm(today.AddDays(-2)))
        {
            var repeated = await student.PutAsync("/api/students/me/imss", sameFile);
            Expect(repeated, HttpStatusCode.OK);
            var unchanged = await repeated.Content.ReadFromJsonAsync<JsonElement>();
            if (unchanged.GetProperty("imssDocumentDate").GetString() != today.AddDays(-1).ToString("yyyy-MM-dd") || !unchanged.GetProperty("hasValidImss").GetBoolean())
                throw new Exception("Identical upload changed confirmed date or approval.");
        }
        Expect(await student.GetAsync("/api/students/me/imss"), HttpStatusCode.OK);
        var queue = await admin.GetFromJsonAsync<JsonElement>("/api/documents/review-queue?tramiteType=general&documentCode=IMSS&status=approved&pageSize=1");
        if (queue.GetProperty("items").GetArrayLength() != 1) throw new Exception("Review filters/paging");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TecNMDbContext>();
            var periodId = await db.AcademicPeriods.Where(p => p.IsActive).Select(p => p.Id).FirstAsync();
            var payload = new { periodId, currentSemester = 7, shift = "morning", phoneNumber = "8661234567", institutionalEmail = "test@monclova.tecnm.mx" };
            var openings = await Task.WhenAll(student.PostAsJsonAsync("/api/internships", payload), student.PostAsJsonAsync("/api/internships", payload));
            if (openings.Count(r => r.StatusCode == HttpStatusCode.Created) != 1 || openings.Any(r => r.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)))
                throw new Exception("Concurrent opening did not produce exactly one internship.");
            var created = await openings.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();
            var internshipId = created.GetProperty("id").GetInt64();
            var year = DateTime.UtcNow.Year;
            var before = await db.DocumentSeries.Where(d => d.DocumentType == "internship_presentation_letter" && d.CurrentYear == year).Select(d => (int?)d.CurrentSequence).SingleOrDefaultAsync() ?? 0;
            var approvals = await Task.WhenAll(admin.PostAsJsonAsync($"/api/internships/{internshipId}/approve", new { }), admin.PostAsJsonAsync($"/api/internships/{internshipId}/approve", new { }));
            if (!approvals.Any(r => r.StatusCode == HttpStatusCode.OK) || approvals.Any(r => r.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Conflict)))
                throw new Exception("Concurrent approval failed unexpectedly.");
            var after = await db.DocumentSeries.Where(d => d.DocumentType == "internship_presentation_letter" && d.CurrentYear == year).Select(d => d.CurrentSequence).SingleAsync();
            if (after - before != 1) throw new Exception("Concurrent approval consumed extra folios.");
            await CheckEvidenceDeletion(student, admin, db, internshipId, bytes, factory);
            Directory.CreateDirectory("tmp/pdf-qa/ui-fixtures");
            File.WriteAllText("tmp/pdf-qa/ui-fixtures/internship.json", await student.GetStringAsync($"/api/internships/{internshipId}"));
            File.WriteAllText("tmp/pdf-qa/ui-fixtures/internship-admin.json", await admin.GetStringAsync($"/api/internships/{internshipId}"));
            Console.WriteLine("HTTP concurrent opening and atomic folio checks passed.");
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TecNMDbContext>();
            var reference = await db.SocialServices.AsNoTracking().FirstAsync();
            var activityTypeId = reference.ActivityTypeId ?? await db.ActivityTypes.Where(a => a.IsActive && (a.AppliesTo == "social_service" || a.AppliesTo == "both")).Select(a => a.Id).FirstAsync();
            var submissionPayload = new { periodId = reference.PeriodId, programId = reference.ProgramId, activityTypeId, companyId = reference.CompanyId, supervisorId = reference.SupervisorId,
                modality = await db.Companies.Where(c => c.Id == reference.CompanyId).Select(c => c.IsTecnmInternal).SingleAsync() ? "internal" : "external", startDate = today, endDate = today.AddMonths(6),
                application = new { studentAddress = "Calle de prueba", studentPhone = "8661234567", periodType = "february_july", dependencyName = "Dependencia", dependencyHeadName = "Responsable", dependencyHeadPosition = "Director", activitiesOutline = "Actividades de prueba" } };
            async Task<HttpResponseMessage> Submit(byte[] content)
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(JsonSerializer.Serialize(submissionPayload)), "application");
                form.Add(new ByteArrayContent(content), "imssFile", "imss.pdf");
                return await student.PostAsync("/api/social-services/submissions", form);
            }
            var submissionsBefore = await db.SocialServices.CountAsync();
            var invalidSubmission = await Submit([1, 2, 3]);
            if (invalidSubmission.IsSuccessStatusCode || await db.SocialServices.CountAsync() != submissionsBefore) throw new Exception("Invalid IMSS left a submission.");
            var submitted = await Submit([.. bytes, 10, 37, 88]);
            if (!submitted.IsSuccessStatusCode) throw new Exception(await submitted.Content.ReadAsStringAsync());
            var submittedId = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
            var ss = await db.SocialServices.SingleAsync(t => t.Id == submittedId);
            var admission = await db.ExpedientDocuments.SingleAsync(d => d.Id == ss.AdmissionImssDocumentId);
            if (admission.Status != "pending" || ss.Status != "requested") throw new Exception("Joint submission must await review.");
            Expect(await admin.PostAsJsonAsync($"/api/social-services/{ss.Id}/application/review", new { isAccepted = true }), HttpStatusCode.UnprocessableEntity);
            Expect(await admin.PostAsJsonAsync($"/api/documents/{admission.Id}/review", new { status = "approved" }), HttpStatusCode.OK);
            using var applicationPdf = await student.GetAsync($"/api/social-services/{ss.Id}/pdf/application");
            Expect(applicationPdf, HttpStatusCode.OK);
            if (applicationPdf.Content.Headers.ContentType?.MediaType != "application/pdf")
                throw new Exception("Application must download as PDF.");
            var commitmentCount = await db.SocialServiceCommitments.CountAsync();
            var clockBefore = testClock.UtcNow;
            try
            {
                foreach (var instant in new[] { new DateTimeOffset(2027, 1, 1, 5, 59, 59, TimeSpan.Zero), new DateTimeOffset(2027, 1, 1, 6, 0, 0, TimeSpan.Zero) })
                {
                    testClock.UtcNow = instant;
                    var printed = await student.GetFromJsonAsync<JsonElement>($"/api/social-services/{ss.Id}/print-data/commitment");
                    if (printed.GetProperty("commitmentDate").GetString() != TecNM.Api.Modules.Expedients.ImssEligibilityService.Today(testClock).ToString("yyyy-MM-dd")
                        || printed.GetProperty("studentAddress").GetString() != "Calle de prueba")
                        throw new Exception("Commitment must reuse student data and the current Mexico download date.");
                    Expect(await student.GetAsync($"/api/social-services/{ss.Id}/pdf/commitment"), HttpStatusCode.OK);
                }
                if (commitmentCount != await db.SocialServiceCommitments.CountAsync()) throw new Exception("Commitment download persisted a draft.");
            }
            finally { testClock.UtcNow = clockBefore; }
            var alternate = new CompanySupervisor { CompanyId = ss.CompanyId, FullName = "Evaluador específico Ñ", Position = "Coordinador bimestral" };
            db.CompanySupervisors.Add(alternate); await db.SaveChangesAsync();
            var supervisor = await db.CompanySupervisors.SingleAsync(s => s.Id == ss.SupervisorId);
            for (var period = 1; period <= 3; period++)
                db.SocialServiceBiMonthlyReports.Add(new SocialServiceBiMonthlyReport { SocialServiceId = ss.Id, ReportNumber = period, PeriodStartDate = today, PeriodEndDate = today.AddMonths(2), ActivitiesSummary = "Apoyo comunitario", ReportedHours = 160, AccumulatedHours = period * 160 });
            await db.SaveChangesAsync();
            var evaluationsBefore = await db.SocialServiceEvaluations.CountAsync();
            var documentsBefore = await db.ExpedientDocuments.CountAsync();
            for (var period = 1; period <= 3; period++)
            {
                var report = await db.SocialServiceBiMonthlyReports.SingleAsync(r => r.SocialServiceId == ss.Id && r.ReportNumber == period);
                foreach (var evaluator in new long?[] { null, alternate.Id })
                {
                    report.EvaluatorSupervisorId = evaluator; await db.SaveChangesAsync();
                    var data = await student.GetFromJsonAsync<JsonElement>($"/api/social-services/{ss.Id}/print-data/report_{period}");
                    if (data.GetProperty("supervisorFullName").GetString() != (evaluator is null ? supervisor.FullName : alternate.FullName)
                        || data.GetProperty("supervisorPosition").GetString() != (evaluator is null ? supervisor.Position : alternate.Position))
                        throw new Exception("Report evaluator precedence failed.");
                    Expect(await student.GetAsync($"/api/social-services/{ss.Id}/pdf/report_{period}"), HttpStatusCode.OK);
                }
                foreach (var key in new[] { $"evaluation_{period}", $"self_evaluation_{period}" })
                {
                    var data = await student.GetFromJsonAsync<JsonElement>($"/api/social-services/{ss.Id}/print-data/{key}");
                    if (data.GetProperty("supervisorFullName").GetString() != supervisor.FullName) throw new Exception("Evaluation changed responsible supervisor.");
                    Expect(await student.GetAsync($"/api/social-services/{ss.Id}/pdf/{key}"), HttpStatusCode.OK);
                }
            }
            Expect(await student.GetAsync($"/api/social-services/{ss.Id}/pdf/activity_assessment"), HttpStatusCode.OK);
            var terminationBefore = await db.SocialServices.AsNoTracking().SingleAsync(t => t.Id == ss.Id);
            Expect(await student.GetAsync($"/api/social-services/{ss.Id}/pdf/termination_letter"), HttpStatusCode.OK);
            var terminationData = await student.GetFromJsonAsync<JsonElement>($"/api/social-services/{ss.Id}/print-data/termination_letter");
            if (terminationData.GetProperty("accumulatedHours").GetInt32() != 480)
                throw new Exception("Termination must always print 480 hours.");
            if (terminationData.GetProperty("activitiesOutline").GetString() != "Apoyo comunitario"
                || terminationData.GetProperty("supervisorFullName").GetString() != supervisor.FullName)
                throw new Exception("Termination did not reuse report activities and service supervisor.");
            var terminationAfter = await db.SocialServices.AsNoTracking().SingleAsync(t => t.Id == ss.Id);
            if (terminationAfter.TerminationLetterDate != terminationBefore.TerminationLetterDate || terminationAfter.Status != terminationBefore.Status)
                throw new Exception("Termination prefill changed service state.");
            if (evaluationsBefore != await db.SocialServiceEvaluations.CountAsync() || documentsBefore != await db.ExpedientDocuments.CountAsync())
                throw new Exception("PDF download wrote evaluations or evidence.");
            db.SocialServiceBiMonthlyReports.RemoveRange(await db.SocialServiceBiMonthlyReports.Where(r => r.SocialServiceId == ss.Id).ToListAsync());
            await db.SaveChangesAsync();
            var tracking = await admin.GetFromJsonAsync<JsonElement>($"/api/reports/tracking?tramiteType=social_service&periodId={ss.PeriodId}");
            var initialRow = tracking.EnumerateArray().Single(row => row.GetProperty("tramiteId").GetInt64() == ss.Id);
            if (initialRow.GetProperty("documents").GetProperty("cp").GetBoolean() || initialRow.GetProperty("progressPercentage").GetInt32() != 0)
                throw new Exception("V2 tracking credited presentation date without approved PDF.");
            Expect(await admin.PostAsJsonAsync($"/api/social-services/{ss.Id}/application/review",new {isAccepted=true}),HttpStatusCode.OK);
            if (await db.SocialServices.AsNoTracking().Where(t=>t.Id==ss.Id).Select(t=>t.PresentationLetterFolio).SingleAsync() is not null) throw new Exception("Temporary approval consumed a presentation folio");
            Expect(await admin.PostAsJsonAsync($"/api/social-services/{ss.Id}/presentation-letter",new {presentationLetterDate=today}),HttpStatusCode.Conflict);
            async Task<long> UploadCp(byte suffix)
            {
                using var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent([..bytes,10,37,suffix]),"file","presentacion-firmada.pdf");
                form.Add(new StringContent("social_service"),"tramiteType"); form.Add(new StringContent(ss.Id.ToString()),"tramiteId"); form.Add(new StringContent("CP"),"documentCode");
                var uploaded = await student.PostAsync("/api/students/me/documents",form); Expect(uploaded,HttpStatusCode.Created);
                return (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
            }
            var rejectedCp = await UploadCp(65);
            Expect(await student.GetAsync($"/api/social-services/{ss.Id}/pdf/presentation_letter"),HttpStatusCode.Conflict);
            Expect(await admin.PostAsJsonAsync($"/api/documents/{rejectedCp}/review",new {status="rejected",rejectionReason="Falta firma"}),HttpStatusCode.OK);
            var approvedCp = await UploadCp(66);
            Expect(await admin.PostAsJsonAsync($"/api/documents/{approvedCp}/review",new {status="approved"}),HttpStatusCode.OK);
            var first = await student.GetByteArrayAsync($"/api/social-services/{ss.Id}/pdf/presentation_letter");
            byte[] expectedCp = [..bytes,10,37,66];
            if (!first.SequenceEqual(expectedCp)) throw new Exception("Uploaded signed CP was modified");
            Expect(await admin.PostAsJsonAsync($"/api/social-services/{ss.Id}/application/review",new {isAccepted=true}),HttpStatusCode.OK);
            var again = await student.GetByteArrayAsync($"/api/social-services/{ss.Id}/pdf/presentation_letter");
            if (!first.SequenceEqual(again)) throw new Exception("Issued CP changed on repeated download");
            var emitted = await db.ExpedientDocuments.AsNoTracking().SingleAsync(d=>d.Id==approvedCp);
            if(emitted.Origin!="uploaded" || emitted.ContentSha256 != Convert.ToHexString(SHA256.HashData(first))) throw new Exception("Uploaded PDF provenance/hash missing");
            Directory.CreateDirectory("tmp/pdf-qa/ui-fixtures");
            File.WriteAllText("tmp/pdf-qa/ui-fixtures/social-service.json",await student.GetStringAsync($"/api/social-services/{ss.Id}"));
            File.WriteAllText("tmp/pdf-qa/ui-fixtures/profile.json",await student.GetStringAsync("/api/students/me"));
            File.WriteAllText("tmp/pdf-qa/ui-fixtures/documents.json",await student.GetStringAsync($"/api/students/me/documents?tramiteType=social_service&tramiteId={ss.Id}"));
            Console.WriteLine("SS approval without template, signed CP upload/rejection/correction and immutable download checks passed.");
            await CheckSocialDocumentCycle(student, admin, db, ss.Id, bytes, today);
            var admissionId = await db.SocialServices.AsNoTracking().Where(t => t.Id == ss.Id).Select(t => t.AdmissionImssDocumentId).SingleAsync();
            if (admissionId is null) throw new Exception("SS admission IMSS reference missing.");
            var originalTime = testClock.UtcNow;
            try
            {
                testClock.UtcNow = new DateTimeOffset(today.Year + 1, 1, 1, 5, 59, 59, TimeSpan.Zero);
                var beforeMidnight = await student.GetFromJsonAsync<JsonElement>("/api/students/me");
                if (!beforeMidnight.GetProperty("hasValidImss").GetBoolean()) throw new Exception("IMSS expired before Mexican midnight.");
                testClock.UtcNow = testClock.UtcNow.AddSeconds(1);
                var afterMidnight = await student.GetFromJsonAsync<JsonElement>("/api/students/me");
                if (!afterMidnight.GetProperty("hasValidImss").GetBoolean()) throw new Exception("Visual IMSS approval expired at the year boundary.");
                Expect(await student.GetAsync($"/api/social-services/{ss.Id}/pdf/commitment"), HttpStatusCode.OK);
                var admittedPp = await db.Internships.AsNoTracking().SingleAsync(t => t.StudentId == 1);
                if (admittedPp.AdmissionImssDocumentId is null) throw new Exception("PP admission evidence missing.");
                Expect(await student.PutAsJsonAsync($"/api/internships/{admittedPp.Id}/evaluation-format", new { evaluationStartDate = today, evaluationEndDate = today.AddMonths(2) }), HttpStatusCode.NoContent);
                Expect(await student.GetAsync($"/api/internships/{admittedPp.Id}/pdf/evaluation"), HttpStatusCode.OK);
                if (await db.InternshipEvaluations.AnyAsync(e => e.InternshipId == admittedPp.Id)) throw new Exception("PP format prefill created grades.");
                if (await db.SocialServices.AsNoTracking().Where(t => t.Id == ss.Id).Select(t => t.AdmissionImssDocumentId).SingleAsync() != admissionId)
                    throw new Exception("Year rollover changed historical admission evidence.");
                var newPp = new { studentId = 1, periodId = ss.PeriodId, currentSemester = 7, shift = "morning", phoneNumber = "8661234567", institutionalEmail = "test@monclova.tecnm.mx" };
                foreach (var client in new[] { student, admin })
                {
                    var blocked = await client.PostAsJsonAsync("/api/internships", newPp);
                    Expect(blocked, HttpStatusCode.UnprocessableEntity);
                    if ((await blocked.Content.ReadAsStringAsync()).Contains("students.imss_approval_required")) throw new Exception("Year boundary incorrectly blocked PP due to IMSS.");
                    var blockedSs = await client.PostAsJsonAsync("/api/social-services", new {
                        studentId = 1, periodId = ss.PeriodId, programId = ss.ProgramId, companyId = ss.CompanyId, supervisorId = ss.SupervisorId,
                        modality = await db.Companies.Where(c => c.Id == reference.CompanyId).Select(c => c.IsTecnmInternal).SingleAsync() ? "internal" : "external", startDate = today, endDate = today.AddMonths(6),
                        application = new { studentAddress = "Calle de prueba", studentPhone = "8661234567", periodType = "normal",
                            dependencyName = "Dependencia", dependencyHeadName = "Responsable", dependencyHeadPosition = "Director", activitiesOutline = "Actividades" }
                    });
                    Expect(blockedSs, HttpStatusCode.UnprocessableEntity);
                    if ((await blockedSs.Content.ReadAsStringAsync()).Contains("students.imss_approval_required")) throw new Exception("Year boundary incorrectly blocked SS due to IMSS.");
                }
                Console.WriteLine("HTTP year rollover preserves visual IMSS approval; duplicate SS/PP remain blocked.");
                await ClosingChecks.RunAsync(student, admin, db, ss.Id, admittedPp.Id, bytes, today);
            }
            finally { testClock.UtcNow = originalTime; }
        }
        await FollowUpChecks.RunAsync(factory);
        await AccessChecks.RunAsync(factory);
        await AccountImportChecks.RunAsync(factory, bytes);
        await ValidationChecks.RunAsync(factory);
        async Task<HttpResponseMessage> Replace(byte suffix)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent([.. bytes, 10, 37, suffix]), "file", "replacement.pdf");
            return await student.PutAsync("/api/students/me/imss", form);
        }
        var concurrent = await Task.WhenAll(Replace(65), Replace(66));
        if (!concurrent.Any(r => r.StatusCode == HttpStatusCode.OK) || concurrent.Any(r => r.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Conflict)))
            throw new Exception("Concurrent replacements must succeed or conflict, never lose data/return 500.");
        var currentDocs = await student.GetFromJsonAsync<JsonElement>("/api/students/me/documents?tramiteType=general");
        var imssDocs = currentDocs.EnumerateArray().Where(d => d.GetProperty("documentCode").GetString() == "IMSS").ToArray();
        if (imssDocs.Length != 1) throw new Exception("Concurrent replacement left multiple current versions.");
        var currentId = imssDocs[0].GetProperty("id").GetInt64();
        var beforeBytes = await student.GetByteArrayAsync($"/api/documents/{currentId}/file");
        failureStorage.FailWrites = true;
        Expect(await Replace(67), HttpStatusCode.InternalServerError);
        failureStorage.FailWrites = false;
        var afterBytes = await student.GetByteArrayAsync($"/api/documents/{currentId}/file");
        if (!beforeBytes.SequenceEqual(afterBytes)) throw new Exception("Storage failure changed previous bytes.");
        var afterProfile = await student.GetFromJsonAsync<JsonElement>("/api/students/me");
        if (afterProfile.GetProperty("imssDocumentId").GetInt64() != currentId) throw new Exception("Storage failure replaced previous document.");
        var storageRoot = Path.GetFullPath("tmp/pdf-qa/http-storage");
        var countBefore = Directory.GetFiles(storageRoot, "*.pdf", SearchOption.AllDirectories).Length;
        databaseFailure.FailInsert = true;
        Expect(await Replace(68), HttpStatusCode.InternalServerError);
        databaseFailure.FailInsert = false;
        var restored = await student.GetFromJsonAsync<JsonElement>("/api/students/me");
        if (restored.GetProperty("imssDocumentId").GetInt64() != currentId || Directory.GetFiles(storageRoot, "*.pdf", SearchOption.AllDirectories).Length != countBefore)
            throw new Exception("Database failure did not restore previous version/clean new file.");
        Console.WriteLine("Post-file database failure rollback and cleanup checks passed.");
        Console.WriteLine("HTTP concurrent replacement and failed-storage preservation checks passed.");
        Console.WriteLine("HTTP authentication, ownership, role restrictions, IMSS lifecycle and PDF download checks passed.");
    }

    private static async Task CheckEvidenceDeletion(HttpClient student, HttpClient admin, TecNMDbContext db, long id, byte[] bytes, WebApplicationFactory<TecNMDbContext> factory)
    {
        using var ssOfficer = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var ppCoordinator = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var viewer = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await Login(ssOfficer, "encargado.ss");
        await Login(ppCoordinator, "coord.practicas");
        await Login(viewer, "jefa.vinculacion");
        var foreignId = await db.Internships.Where(t => t.StudentId != 1).Select(t => t.Id).FirstAsync();
        Expect(await student.GetAsync($"/api/internships/{foreignId}/pdf/evaluation"), HttpStatusCode.NotFound);
        Expect(await student.PutAsJsonAsync($"/api/internships/{foreignId}/evaluation-format", new { evaluationStartDate = new DateOnly(2026, 1, 1), evaluationEndDate = new DateOnly(2026, 2, 1) }), HttpStatusCode.NotFound);
        async Task<HttpResponseMessage> Upload(HttpClient client, string code)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(bytes), "file", code + ".pdf");
            form.Add(new StringContent("internship"), "tramiteType");
            form.Add(new StringContent(id.ToString()), "tramiteId");
            form.Add(new StringContent(code), "documentCode");
            return await client.PostAsync($"/api/students/1/documents", form);
        }
        var acceptance = await Upload(admin, "CA");
        Expect(acceptance, HttpStatusCode.Created);
        var caId = (await acceptance.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Expect(await Upload(student, "CA"), HttpStatusCode.Forbidden);
        Expect(await Upload(ssOfficer, "CA"), HttpStatusCode.NotFound);
        Expect(await ssOfficer.PostAsJsonAsync($"/api/documents/{caId}/review", new { status = "approved" }), HttpStatusCode.NotFound);
        Expect(await ssOfficer.DeleteAsync($"/api/documents/{caId}"), HttpStatusCode.NotFound);
        Expect(await viewer.DeleteAsync($"/api/documents/{caId}"), HttpStatusCode.Forbidden);
        Expect(await student.DeleteAsync($"/api/documents/{caId}"), HttpStatusCode.Forbidden);
        if (!await db.ExpedientDocuments.AsNoTracking().AnyAsync(d => d.Id == caId)) throw new Exception("Forbidden delete changed evidence.");
        Expect(await ppCoordinator.DeleteAsync($"/api/documents/{caId}"), HttpStatusCode.NoContent);

        var evaluation = await Upload(student, "EV");
        Expect(evaluation, HttpStatusCode.Created);
        var evId = (await evaluation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        var tramite = await db.Internships.SingleAsync(t => t.Id == id);
        var original = tramite.Status;
        tramite.Status = "cancelled"; await db.SaveChangesAsync();
        Expect(await student.DeleteAsync($"/api/documents/{evId}"), HttpStatusCode.Conflict);
        Expect(await admin.DeleteAsync($"/api/documents/{evId}"), HttpStatusCode.Forbidden);
        Expect(await Upload(student, "EV"), HttpStatusCode.Conflict);
        tramite.Status = original; await db.SaveChangesAsync();
        Expect(await student.DeleteAsync($"/api/documents/{evId}"), HttpStatusCode.NoContent);
        Expect(await student.GetAsync($"/api/documents/{evId}/file"), HttpStatusCode.NotFound);
        Console.WriteLine("HTTP evidence deletion role and terminal-state protection checks passed.");
    }

    private static async Task CheckSocialDocumentCycle(HttpClient student, HttpClient admin, TecNMDbContext db, long id, byte[] bytes, DateOnly today)
    {
        async Task<long> Upload(string code, byte version)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent([.. bytes, 10, 37, version]), "file", code + ".pdf");
            form.Add(new StringContent("social_service"), "tramiteType");
            form.Add(new StringContent(id.ToString()), "tramiteId");
            form.Add(new StringContent(code), "documentCode");
            var response = await student.PostAsync("/api/students/me/documents", form);
            Expect(response, HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        }
        var commitment = new { studentAddress = "Calle de prueba", studentPhone = "8661234567", dependencyAddress = "Dependencia de prueba", commitmentDate = today, city = "Monclova" };
        Expect(await student.PutAsJsonAsync($"/api/social-services/{id}/commitment", commitment), HttpStatusCode.OK);
        var draft = await db.SocialServiceCommitments.AsNoTracking().SingleAsync(c => c.SocialServiceId == id);
        if (draft.IsSignedByStudent || draft.SignedAt is not null) throw new Exception("Commitment draft falsely credited signature.");
        Expect(await student.GetAsync($"/api/social-services/{id}/pdf/commitment"), HttpStatusCode.OK);
        var firstCc = await Upload("CC", 65);
        Expect(await admin.PostAsJsonAsync($"/api/documents/{firstCc}/review", new { status = "rejected", rejectionReason = "Falta firma del alumno" }), HttpStatusCode.OK);
        var rejected = await db.SocialServiceCommitments.AsNoTracking().SingleAsync(c => c.SocialServiceId == id);
        if (rejected.IsSignedByStudent || rejected.SignedAt is not null) throw new Exception("Rejected commitment falsely credited signature.");
        var correctedCc = await Upload("CC", 66);
        Expect(await admin.PostAsJsonAsync($"/api/documents/{correctedCc}/review", new { status = "approved" }), HttpStatusCode.OK);
        var signed = await db.SocialServiceCommitments.AsNoTracking().SingleAsync(c => c.SocialServiceId == id);
        if (!signed.IsSignedByStudent || signed.SignedAt is null) throw new Exception("Approved commitment did not credit signature.");
        Expect(await student.PutAsJsonAsync($"/api/social-services/{id}/commitment", commitment), HttpStatusCode.Conflict);
        var oldCc = await db.ExpedientDocuments.AsNoTracking().SingleAsync(d => d.Id == firstCc);
        if (oldCc.Status != "superseded" || oldCc.RejectionReason != "Falta firma del alumno") throw new Exception("Commitment correction lost history.");

        var report = new { periodStartDate = today, periodEndDate = today.AddMonths(2), activitiesSummary = "Actividades del reporte de prueba", reportedHours = 160 };
        var savedReport = await student.PutAsJsonAsync($"/api/social-services/{id}/reports/1", report);
        Expect(savedReport, HttpStatusCode.OK);
        var reportId = (await savedReport.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Expect(await admin.PostAsJsonAsync($"/api/social-services/{id}/reports/{reportId}/review", new { status = "approved" }), HttpStatusCode.UnprocessableEntity);
        var firstR1 = await Upload("R1", 65);
        Expect(await admin.PostAsJsonAsync($"/api/social-services/{id}/reports/{reportId}/review", new { status = "rejected", officeObservations = "Falta sello" }), HttpStatusCode.OK);
        var rejectedReport = await db.SocialServiceBiMonthlyReports.AsNoTracking().SingleAsync(r => r.Id == reportId);
        var rejectedPdf = await db.ExpedientDocuments.AsNoTracking().SingleAsync(d => d.Id == firstR1);
        if (rejectedReport.Status != "rejected" || rejectedPdf.Status != "rejected" || rejectedReport.OfficeObservations != rejectedPdf.RejectionReason)
            throw new Exception("Report review did not reject the same PDF.");
        var correctedR1 = await Upload("R1", 66);
        Expect(await admin.PostAsJsonAsync($"/api/documents/{correctedR1}/review", new { status = "approved" }), HttpStatusCode.OK);
        var approvedReport = await db.SocialServiceBiMonthlyReports.AsNoTracking().SingleAsync(r => r.Id == reportId);
        if (approvedReport.Status != "approved" || approvedReport.OfficeObservations is not null || approvedReport.ApprovedAt is null)
            throw new Exception("PDF review did not approve report data.");
        Expect(await student.PutAsJsonAsync($"/api/social-services/{id}/reports/1", report), HttpStatusCode.Conflict);
        var history = await student.GetFromJsonAsync<JsonElement>($"/api/students/me/documents?tramiteType=social_service&tramiteId={id}&includeSuperseded=true");
        if (history.EnumerateArray().Count(d => d.GetProperty("documentCode").GetString() == "R1") != 2)
            throw new Exception("Report replacement history missing.");
        Console.WriteLine("HTTP commitment signature and report/PDF rejection-correction-approval consistency checks passed.");
    }

    private static async Task CheckTemplateManagementAsync(WebApplicationFactory<TecNMDbContext> factory, HttpClient student)
    {
        const string root = "/api/template-management";
        const string key = "ss-acceptance-example";
        using var ss = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var pp = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await Login(ss, "encargado.ss");
        await Login(pp, "coord.practicas");
        Expect(await student.GetAsync(root), HttpStatusCode.Forbidden);
        Expect(await ss.PostAsync($"{root}/{key}/draft", null), HttpStatusCode.BadRequest);
        foreach (var client in new[] { ss, pp })
        {
            var token = await client.GetFromJsonAsync<JsonElement>(root + "/csrf");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        }
        Expect(await pp.PostAsync($"{root}/{key}/draft", null), HttpStatusCode.Forbidden);
        Expect(await ss.PostAsync($"{root}/pp-evaluation/draft", null), HttpStatusCode.Forbidden);
        async Task<JsonElement> Summary(HttpClient client, string template)
        {
            var list = await client.GetFromJsonAsync<JsonElement>(root);
            return list.EnumerateArray().Single(item => item.GetProperty("key").GetString() == template);
        }
        async Task<JsonElement> Draft(HttpClient client, string template)
        {
            var result = await client.PostAsync($"{root}/{template}/draft", null);
            Expect(result, HttpStatusCode.OK);
            return await result.Content.ReadFromJsonAsync<JsonElement>();
        }

        foreach (var (client, area) in new[] { (ss, "social_service"), (pp, "internship") })
        {
            var available = await client.GetFromJsonAsync<JsonElement>(root);
            foreach (var item in available.EnumerateArray())
            {
                if (item.GetProperty("area").GetString() != area) throw new Exception("Template catalog leaked the other area.");
                var template = item.GetProperty("key").GetString()!;
                var check = await Draft(client, template);
                if (check.GetProperty("slots").GetArrayLength() != check.GetProperty("layout").GetProperty("fields").GetArrayLength())
                    throw new Exception("The draft must use the field contract from the version it was copied from.");
                var tag = check.GetProperty("revision").GetUInt32();
                foreach (var longSample in new[] { "false", "true" })
                {
                    var preview = await client.PostAsync($"{root}/{template}/draft/preview?revision={tag}&longSample={longSample}", null);
                    if (!preview.IsSuccessStatusCode) throw new Exception($"Preview {template}: {await preview.Content.ReadAsStringAsync()}");
                }
                if (template == "pp-evaluation")
                {
                    var layout = check.GetProperty("layout");
                    var placements = layout.GetProperty("fields").Deserialize<TecNM.Api.Modules.Expedients.Templates.TemplatePlacement[]>(
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                    var originalPlacement = placements[0];
                    placements[0] = originalPlacement with { X = -2 };
                    async Task<HttpResponseMessage> SaveLayout() =>
                        await client.PutAsJsonAsync($"{root}/{template}/draft/layout", new
                        {
                            revision = tag, font = layout.GetProperty("font").GetString(),
                            fontSize = layout.GetProperty("fontSize").GetDouble(), minFontSize = layout.GetProperty("minFontSize").GetDouble(), fields = placements
                        });
                    Expect(await SaveLayout(), HttpStatusCode.UnprocessableEntity);
                    placements[0] = originalPlacement with { X = originalPlacement.X + 1 };
                    var updatedLayout = await SaveLayout();
                    Expect(updatedLayout, HttpStatusCode.OK);
                    tag = (await updatedLayout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetUInt32();
                    Expect(await client.PostAsJsonAsync($"{root}/{template}/draft/publish", new
                    {
                        templateRevision = item.GetProperty("revision").GetUInt32(), draftRevision = tag, reason = "Distribución de prueba PP"
                    }), HttpStatusCode.NoContent);
                    continue;
                }
                Expect(await client.DeleteAsync($"{root}/{template}/draft?revision={tag}"), HttpStatusCode.NoContent);
            }
        }

        var path = $"{root}/{key}";
        var original = await Summary(ss, key);
        var originalId = original.GetProperty("activeVersionId").GetInt64();
        var draft = await Draft(ss, key);
        var versionId = draft.GetProperty("id").GetInt64();
        var revision = draft.GetProperty("revision").GetUInt32();
        var global = $"/api/document-templates/{key}/file";
        var pinned = global + "?tramiteId=1";
        var originalBytes = await ss.GetByteArrayAsync(pinned);
        using (var malformed = new MultipartFormDataContent())
        {
            malformed.Add(new ByteArrayContent("%PDF-1.7 invalid document"u8.ToArray()), "file", "danado.pdf");
            Expect(await ss.PutAsync($"{path}/draft/file?revision={revision}", malformed), HttpStatusCode.UnsupportedMediaType);
        }
        using var form = new MultipartFormDataContent();
        using var staticPdf = new PdfSharp.Pdf.PdfDocument();
        staticPdf.AddPage();
        using var stream = new MemoryStream();
        staticPdf.Save(stream, false);
        var replacement = stream.ToArray();
        form.Add(new ByteArrayContent(replacement), "file", "sustituto.pdf");
        var updated = await ss.PutAsync($"{path}/draft/file?revision={revision}", form);
        if (!updated.IsSuccessStatusCode) throw new Exception(await updated.Content.ReadAsStringAsync());
        var publishedRevision = (await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetUInt32();
        Expect(await ss.DeleteAsync($"{path}/draft?revision={revision}"), HttpStatusCode.Conflict);
        var publish = new { templateRevision = original.GetProperty("revision").GetUInt32(), draftRevision = publishedRevision, reason = "Formato redistribuido" };
        var attempts = await Task.WhenAll(ss.PostAsJsonAsync(path + "/draft/publish", publish), ss.PostAsJsonAsync(path + "/draft/publish", publish));
        if (attempts.Count(response => response.StatusCode == HttpStatusCode.NoContent) != 1 ||
            attempts.Count(response => response.StatusCode == HttpStatusCode.Conflict) != 1)
            throw new Exception(string.Join(", ", attempts.Select(r => r.StatusCode)));
        if (!(await ss.GetByteArrayAsync(global)).SequenceEqual(replacement) ||
            !(await ss.GetByteArrayAsync(pinned)).SequenceEqual(originalBytes))
            throw new Exception("Publication changed the assigned version or failed to activate the replacement.");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TecNMDbContext>();
            var catalog = scope.ServiceProvider.GetRequiredService<TecNM.Api.Modules.Expedients.Templates.TemplateCatalog>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var reference = await db.SocialServices.AsNoTracking().FirstAsync();
            var additional = new TecNM.Api.Core.Entities.SocialService
            {
                StudentId = reference.StudentId, PeriodId = reference.PeriodId, ProgramId = reference.ProgramId,
                CompanyId = reference.CompanyId, SupervisorId = reference.SupervisorId, StartDate = reference.StartDate,
                EndDate = reference.EndDate, Status = "cancelled", Modality = reference.Modality, RequiredHours = 480,
                SemesterEnrolled = reference.SemesterEnrolled
            };
            // The isolated transaction rolls back this synthetic opening after checking its assignment.
            db.Add(additional);
            await db.SaveChangesAsync();
            await catalog.AssignAsync("social_service", additional.Id, CancellationToken.None);
            await db.SaveChangesAsync();
            var assignedBytes = await catalog.ResolveAsync(key, additional.Id, null, CancellationToken.None);
            if (!assignedBytes.SequenceEqual(replacement)) throw new Exception("New opening did not pin the active version.");
            await transaction.RollbackAsync();
        }
        var active = await Summary(ss, key);
        if (active.GetProperty("activeVersionId").GetInt64() != versionId) throw new Exception("Replacement version was not recorded.");
        Expect(await ss.PostAsJsonAsync($"{path}/versions/{originalId}/restore", new
        {
            templateRevision = active.GetProperty("revision").GetUInt32(), reason = "Restauración de prueba"
        }), HttpStatusCode.NoContent);
        if (!(await ss.GetByteArrayAsync(global)).SequenceEqual(originalBytes)) throw new Exception("Restore did not reactivate the original bytes.");
        var events = await ss.GetFromJsonAsync<JsonElement>(path + "/activations");
        if (events.GetArrayLength() != 3) throw new Exception("Missing activation history.");
        Console.WriteLine("Template management permissions, previews, immutable pins, concurrency and restore passed.");
    }

    private static async Task Login(HttpClient client, string username)
    {
        Expect(await client.PostAsJsonAsync("/api/auth/login", new { username, password = "TecNM-Demo-2026!" }), HttpStatusCode.OK);
    }
    private static void Expect(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected) throw new Exception($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath}: expected {(int)expected}, got {(int)response.StatusCode}");
    }
}

internal sealed class DocumentTestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal sealed class FailingStorage(IFileStorage inner) : IFileStorage
{
    public bool FailWrites { get; set; }
    public Task<string> SavePdfAsync(Microsoft.AspNetCore.Http.IFormFile file, string relativeDirectory, string? customFileNameWithoutExtension = null, long maxSizeBytes = 5 * 1024 * 1024, CancellationToken cancellationToken = default)
    {
        if (FailWrites) throw new IOException("Simulated storage failure");
        return inner.SavePdfAsync(file, relativeDirectory, customFileNameWithoutExtension, maxSizeBytes, cancellationToken);
    }
    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) => inner.OpenReadAsync(path, cancellationToken);
    public void TryDelete(string? path) => inner.TryDelete(path);
}

internal sealed class FailingDocumentSave : SaveChangesInterceptor
{
    public bool FailInsert { get; set; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (FailInsert && eventData.Context!.ChangeTracker.Entries<ExpedientDocument>().Any(e => e.State == EntityState.Added))
            throw new InvalidOperationException("Simulated database failure after file write");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
