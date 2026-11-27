using TecNM.Api.Core.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;
using TecNM.Api.Modules.Expedients.Pdf;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Security;
using TecNM.Api.Modules.Expedients;
using TecNM.Api.Modules.Realtime;

static void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
}
DateExtractionChecks.TextChecks();
var today = new DateOnly(2026, 12, 31);
var doc = new ExpedientDocument { TramiteType = "general", DocumentCode = "IMSS", Status = "approved", DocumentDate = today };
Check(ImssEligibilityService.IsCurrent(doc, today), "Approved current-year IMSS");
Check(ImssEligibilityService.IsCurrent(doc, today.AddDays(1)), "Year boundary must preserve visual approval");
doc.Status = "pending";
Check(!ImssEligibilityService.IsCurrent(doc, today), "Pending cannot admit");
doc.Status = "rejected";
Check(!ImssEligibilityService.IsCurrent(doc, today), "Rejected cannot admit");
doc.Status = "approved"; doc.DocumentDate = today.AddDays(1);
doc.DocumentDate = null;
Check(ImssEligibilityService.IsCurrent(doc, today), "Visual approval requires no document date");
var clock = new FixedClock(new DateTimeOffset(2027, 1, 1, 5, 59, 59, TimeSpan.Zero));
Check(ImssEligibilityService.Today(clock) == today, "Mexico year differs from UTC at midnight");
Check(DocumentDefinitions.RequiredForSocialService(2).Contains("EV_ACT"), "V2 release requires activity assessment");
Check(DocumentDefinitions.RequiredForSocialService(2).Contains("SOL"), "V2 release requires approved application");
Check(!DocumentDefinitions.RequiredForSocialService(1).Contains("EV_ACT"), "Historic release remains compatible");
foreach (var controller in new[] { typeof(TecNM.Api.Modules.SocialService.SocialServicesController), typeof(TecNM.Api.Modules.Internships.InternshipsController) })
{
    foreach (var method in controller.GetMethods())
    {
        var routes = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPutAttribute), false).Cast<Microsoft.AspNetCore.Mvc.HttpPutAttribute>();
        if (!routes.Any(r => r.Template is "{id:long}/evaluation" or "{id:long}/evaluations/{evaluatorType}/{bimonthlyPeriod}" or "{id:long}/activity-assessment")) continue;
        var roles = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().SelectMany(a => (a.Roles ?? "").Split(','));
        Check(!roles.Contains("student"), "Students cannot digitally assign grades");
    }
}
var student = new TestUser(["student"]);
Check(!DocumentDefinitions.CanUpload("internship", "CA", 2, student), "Student cannot upload PP acceptance");
Check(DocumentDefinitions.CanUpload("social_service", "CP", 2, student), "Student can upload signed SS presentation during temporary workflow");
Check(!DocumentDefinitions.CanUpload("internship", "CP", 2, student), "PP presentation remains institutional");
Check(!DocumentDefinitions.CanUpload("social_service", "CA", 2, new TestUser(["internship_coordinator"])), "Staff cannot write opposite domain");
Check(DocumentDefinitions.CanUpload("internship", "CA", 2, new TestUser(["internship_coordinator"])), "PP staff can upload acceptance");
Check(DocumentDefinitions.CanUpload("social_service", "EV1", 1, student), "Keep historic bundles");
Check(!DocumentDefinitions.CanUpload("social_service", "EV1", 2, student), "New cases cannot submit old bundles");
foreach (var role in new[] { "admin", "social_service_officer", "internship_coordinator", "department_head" })
{
    var actor = new TestUser([role]);
    foreach (var version in new[] { 1, 2 })
    {
        Check(DocumentDefinitions.For("social_service", version, actor).All(d => !d.CanUpload), "Staff cannot upload SS student evidence");
        Check(DocumentDefinitions.For("internship", version, actor).Where(d => d.Code != "CA").All(d => !d.CanUpload), "Staff cannot upload PP student evidence");
    }
}
Check(!new TestUser(["social_service_officer"]).CanReadProcess("internship"), "SS cannot read PP individual process");
Check(!new TestUser(["internship_coordinator"]).CanReadProcess("social_service"), "PP cannot read SS individual process");
Check(new TestUser(["social_service_officer", "internship_coordinator"]).CanReadProcess("internship"), "Dual roles retain explicit access");
Check(CompanyTypes.IsConsistent("micro", "private", "micro"), "Private size mapping");
Check(!CompanyTypes.IsConsistent("public", "private", "large"), "Reject inconsistent type");
Check(!CompanyTypes.IsConsistent(null, "educational", "not_applicable"), "Educational needs explicit classification");
var approved = new HashSet<string> { "EV_SUP1", "EV_SELF1" };
Check(DocumentProgressCalculator.EvaluationDone(approved, 2, 1), "Both annexes complete period 1");
approved = ["EV_SUP3", "EV_SELF3"];
Check(!DocumentProgressCalculator.EvaluationDone(approved, 2, 3), "Final requires activity assessment");
approved.Add("EV_ACT");
Check(DocumentProgressCalculator.EvaluationDone(approved, 2, 3), "Complete final evaluations");
var historicalService = new SocialService { DocumentWorkflowVersion = 1 };
Check(DocumentProgressCalculator.SocialService(historicalService, new HashSet<string> { "EV1" }) == 10, "Historic evaluation bundle retains its statistical block");
var newService = new SocialService { DocumentWorkflowVersion = 2 };
Check(DocumentProgressCalculator.SocialService(newService, new HashSet<string> { "EV1", "EV2", "EV3" }) == 0, "Historic bundles cannot complete new evaluations");
var allEvaluations = DocumentDefinitions.SeparateEvaluations.ToHashSet();
Check(DocumentProgressCalculator.SocialService(newService, allEvaluations) == 10, "Seven new annexes complete one statistical block");
foreach (var missing in DocumentDefinitions.SeparateEvaluations)
    Check(DocumentProgressCalculator.SocialService(newService, allEvaluations.Where(code => code != missing).ToHashSet()) == 0, "Every separate evaluation requires its own approval");
foreach (var service in new[] { historicalService, newService })
{
    service.PresentationLetterDate = today;
    service.AcceptanceLetterDate = today;
    service.TerminationLetterDate = today;
    service.Application = new SocialServiceApplication { IsAcceptedByOffice = true };
    foreach (var code in new[] { "CP", "CA", "SOL", "CT" })
    {
        Check(DocumentProgressCalculator.DocumentDone(service, new HashSet<string>(), code) == (code == "SOL" || service.DocumentWorkflowVersion == 1), "Application approval closes SOL without PDF");
        Check(DocumentProgressCalculator.DocumentDone(service, new HashSet<string> { code }, code), "Approved evidence credits either workflow version");
    }
}
Check(new SocialServiceCommitment().SignedAt is null && !new SocialServiceCommitment().IsSignedByStudent, "Draft is not a signature");
{
    var presence = new DocumentReviewPresenceRegistry();
    var baseNow = new DateTimeOffset(2026, 11, 25, 18, 0, 0, TimeSpan.Zero);
    presence.AcquireOrRefresh(101, "social_service", 10, "Revisor SS", "conn-a", baseNow);
    Check(presence.GetLocksForTramiteType("social_service", baseNow).Count == 1, "Reviewer acquires SS lock");
    Check(presence.GetLocksForTramiteType("internship", baseNow).Count == 0, "SS lock isolated from PP group");
    presence.AcquireOrRefresh(101, "social_service", 20, "Segundo Revisor", "conn-b", baseNow.AddMinutes(2));
    Check(presence.GetLocksForTramiteType("social_service", baseNow.AddMinutes(2))[0].ReviewerUserId == 10, "Active reviewer lock is preserved against concurrent tab");
    var movedTypes = presence.AcquireOrRefresh(202, "internship", 10, "Revisor SS", "conn-a", baseNow.AddMinutes(3));
    Check(movedTypes.Contains("social_service") && movedTypes.Contains("internship"), "Switching modal releases previous document on same connection");
    Check(presence.GetLocksForTramiteType("social_service", baseNow.AddMinutes(3)).Count == 0, "Previous document lock released on tab switch");
    Check(presence.GetLocksForTramiteType("internship", baseNow.AddMinutes(3)).Single().DocumentId == 202, "New document lock active after switch");
    Check(presence.ReleaseDocument(202) == "internship", "Document review completion releases presence lock");
    presence.AcquireOrRefresh(303, "general", 30, "Admin", "conn-c", baseNow);
    Check(presence.GetLocksForTramiteType("general", baseNow.AddMinutes(31)).Count == 0, "Stale lock expires after 30 minutes");
    presence.AcquireOrRefresh(304, "general", 30, "Admin", "conn-c", baseNow.AddMinutes(32));
    Check(presence.ReleaseAllByConnection("conn-c").Contains("general"), "SignalR disconnect cleans up connection locks");
    Check(RealtimeGroups.Staff("social_service") == "staff:social_service" && RealtimeGroups.Staff("internship") == "staff:internship" && RealtimeGroups.Staff("general") == "staff:general", "SignalR group names isolate SS and PP domains");
}
var fonts = Environment.GetEnvironmentVariable("DOCUMENT_TEST_FONTS") ?? "C:/Windows/Fonts";
if (Directory.Exists(fonts))
{
    GlobalFontSettings.FontResolver = new OfficialFontResolver(fonts);
    DateExtractionChecks.PdfChecks();
    var resources = Path.GetFullPath("backend/Resources/Documents");
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Documents:RootPath"] = resources }).Build();
    var generator = new OfficialPdfService(new TestEnvironment(), config);
    var sample = new Dictionary<string, object?>
    {
        ["studentFullName"] = "María José Muñoz Peña", ["evaluationStartDate"] = new DateOnly(2026, 1, 12),
        ["evaluationEndDate"] = new DateOnly(2026, 6, 12)
    };
    var actData = new Dictionary<string, object?>
    {
        ["actBody"] = "Por medio de la presente se hace constar que María José Muñoz Peña con matrícula I23050001 de Ingeniería Informática realizó su Servicio Social en Instituto Tecnológico Superior de Monclova desempeñando actividades de Apoyo tecnológico cubriendo un total de 480 horas, del 01/01/2026 al 30/06/2026.",
        ["actDate"] = "Se extiende la presente para los fines legales que al interesado convengan, en Monclova, Coahuila a los 10 días del mes de noviembre del año 2026.",
        ["outreachAuthority"] = "M.I. Ana Gómez Díaz\nJefa de División de Gestión Tecnológica y Vinculación",
        ["directorAuthority"] = "Juan Pérez Muñoz\nDirector General"
    };
    Directory.CreateDirectory("tmp/pdf-qa");
    File.WriteAllBytes("tmp/pdf-qa/ss-individual-act.pdf", generator.Generate("ss-individual-act", actData));
    actData["actBody"] = new string('W', 10000);
    try { generator.Generate("ss-individual-act", actData); throw new Exception("Act overflow accepted"); }
    catch (BusinessRuleException ex) { Check(ex.Code == "documents.text_overflow", "Act rejects overflow"); }
    actData["actBody"] = null;
    try { generator.Generate("ss-individual-act", actData); throw new Exception("Missing act data accepted"); }
    catch (BusinessRuleException ex) { Check(ex.Code == "documents.data_missing", "Act rejects missing data"); }
    var bytes = generator.Generate("pp-evaluation", sample);
    using var input = new MemoryStream(bytes);
    using var result = PdfReader.Open(input, PdfDocumentOpenMode.Import);
    Check(result.PageCount == 2, "Evaluation preserves instruction page");
    Directory.CreateDirectory("tmp/pdf-qa");
    File.WriteAllBytes("tmp/pdf-qa/pp-evaluation.pdf", bytes);
    var ssData = new Dictionary<string, object?>
    {
        ["studentFullName"] = "María José Muñoz Peña", ["studentFirstName"] = "María José",
        ["studentFirstSurname"] = "Muñoz", ["studentSecondSurname"] = "Peña", ["studentControlNumber"] = "I23050001",
        ["studentPhone"] = "8661234567", ["studentAddress"] = "Calle Hidalgo 120, Monclova",
        ["studentSemester"] = 7, ["dependencyName"] = "Instituto Tecnológico Superior de Monclova",
        ["dependencyHeadName"] = "Juan Pérez López", ["dependencyHeadPosition"] = "Director",
        ["supervisorPosition"] = "Coordinadora de Servicio Social", ["supervisorFullName"] = "Ana Gómez Díaz", ["dependencyAddress"] = "Carretera 57 km 4.5, Monclova",
        ["careerName"] = "Ingeniería Informática", ["careerCode"] = "INF", ["programName"] = "Apoyo tecnológico",
        ["programCategory"] = "institutional_programs", ["periodCode"] = "Agosto - Enero", ["studentGender"] = "Mujer",
        ["modality"] = "Interno", ["generatedAt"] = clock.GetUtcNow(), ["startDate"] = new DateOnly(2026, 8, 1),
        ["endDate"] = new DateOnly(2027, 1, 31), ["commitmentDate"] = new DateOnly(2026, 7, 20), ["city"] = "Monclova",
        ["activitiesOutline"] = "Soporte a equipos y capacitación de usuarios.", ["activitiesSummary"] = "Mantenimiento de equipos. Apoyo a usuarios y revisión de inventarios.",
        ["issueDate"] = new DateOnly(2026, 11, 9), ["accumulatedHours"] = 480, ["reportNumber"] = 1, ["reportedHours"] = 160, ["reportAccumulatedHours"] = 160,
        ["periodStartDate"] = new DateOnly(2026, 8, 1), ["periodEndDate"] = new DateOnly(2026, 9, 30),
        ["companyName"] = "Instituto Tecnológico Superior de Monclova", ["realizationPeriod"] = "01/08/2026 - 31/01/2027", ["finalMark"] = "X"
    };
    foreach (var template in new[] { "ss-application", "ss-commitment", "ss-report", "ss-activity", "ss-supervisor-evaluation", "ss-self-evaluation", "ss-termination" })
        File.WriteAllBytes($"tmp/pdf-qa/{template}.pdf", generator.Generate(template, ssData));
    var longApplication = new Dictionary<string, object?>(ssData)
    {
        ["studentFullName"] = new string('W', 42), // Fits the calibrated name row only at 7 pt.
        ["studentAddress"] = "Avenida de la Constitución 425, colonia Ampliación Tecnológica",
        ["activitiesOutline"] = "Primera línea con acentos y ñ.\r\nSegunda línea de actividades.\nTercera línea de actividades."
    };
    File.WriteAllBytes("tmp/pdf-qa/ss-application-long.pdf", generator.Generate("ss-application", longApplication));
    var longReport = new Dictionary<string, object?>(ssData)
    {
        ["studentSecondSurname"] = null, ["periodEndDate"] = new DateOnly(2026, 9, 30),
        ["activitiesSummary"] = "Primera línea con acentos y ñ.\r\nSegunda línea.\nTercera línea.\nCuarta línea.",
        ["supervisorFullName"] = "María José Muñoz Peña\nCoordinación regional",
        ["supervisorPosition"] = "Responsable de servicio\nApoyo comunitario"
    };
    File.WriteAllBytes("tmp/pdf-qa/ss-report-long.pdf", generator.Generate("ss-report", longReport));
    foreach (var (template, key, value, source) in new[]
    {
        ("ss-application", "studentFullName", new string('W', 43), longApplication),
        ("ss-application", "activitiesOutline", "Uno\nDos\nTres\nCuatro", longApplication),
        ("ss-report", "supervisorPosition", "Uno\nDos\nTres", longReport)
    })
    {
        var overflowing = new Dictionary<string, object?>(source) { [key] = value };
        try { generator.Generate(template, overflowing); throw new Exception($"Overflow accepted for {key}"); }
        catch (BusinessRuleException ex) { Check(ex.Code == "documents.text_overflow" && ex.Errors!.ContainsKey(key), $"Overflow identifies {key}"); }
    }
    foreach (var key in new[] { "ss-report", "ss-supervisor-evaluation", "ss-self-evaluation", "ss-termination" })
    {
        var missing = new Dictionary<string, object?>(ssData);
        missing.Remove("studentFirstName"); missing.Remove("studentFullName");
        try { generator.Generate(key, missing); throw new Exception("Missing student accepted"); }
        catch (BusinessRuleException ex) { Check(ex.Code == "documents.data_missing", "Missing data error"); }
        var overflow = new Dictionary<string, object?>(ssData) { ["programName"] = new string('W', 2000) };
        try { generator.Generate(key, overflow); throw new Exception("Overflow accepted"); }
        catch (BusinessRuleException ex) { Check(ex.Code == "documents.text_overflow", "Overflow error"); }
    }
    ssData["careerCode"] = "UNKNOWN";
    try { generator.Generate("ss-report", ssData); throw new Exception("Unmapped career accepted"); }
    catch (BusinessRuleException) { }
    var stampRoot = Path.GetFullPath("tmp/pdf-qa/stamps");
    Directory.CreateDirectory(stampRoot);
    File.Copy(Path.Combine(resources, "templates/pp-evaluation.pdf"), Path.Combine(stampRoot, "base.pdf"), true);
    var stampBytes = File.ReadAllBytes(Path.Combine(resources, "stamps/dummy/placeholder.png"));
    File.WriteAllBytes(Path.Combine(stampRoot, "stamp.png"), stampBytes);
    var stampDefinition = new OfficialPdfService.Stamp("stamp.png", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stampBytes)), "social_service_head", 420, 600, 100, 100, 0.4);
    var templateDefinition = new OfficialPdfService.TemplateDefinition("base.pdf", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(stampRoot, "base.pdf")))) , "test", "Arial", [new("studentFullName",200,167,330,13)], false, [stampDefinition], 2, [new(612, 792), new(612, 792)]);
    void WriteStampManifest(OfficialPdfService.TemplateDefinition value) => File.WriteAllText(Path.Combine(stampRoot,"manifest.json"), System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, OfficialPdfService.TemplateDefinition> { ["test"] = value }));
    var stampedGenerator = new OfficialPdfService(new TestEnvironment(), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Documents:RootPath"] = stampRoot }).Build());
    foreach (var invalid in new[] {
        templateDefinition with { Pages = 0 },
        templateDefinition with { Pages = 1 },
        templateDefinition with { PageSizes = null },
        templateDefinition with { PageSizes = [new(612, 792)] },
        templateDefinition with { PageSizes = [new(1, 1), new(612, 792)] },
        templateDefinition with { Fields = [templateDefinition.Fields[0] with { Page = -1 }] },
        templateDefinition with { Fields = [templateDefinition.Fields[0] with { Page = 2 }] },
        templateDefinition with { Fields = [templateDefinition.Fields[0] with { X = -1 }] },
        templateDefinition with { Fields = [templateDefinition.Fields[0] with { Width = 900 }] },
        templateDefinition with { Fields = [templateDefinition.Fields[0] with { MinFontSize = 0 }] },
        templateDefinition with { Fields = [templateDefinition.Fields[0] with { FontSize = 6 }] },
        templateDefinition with { Fields = [templateDefinition.Fields[0] with { Rows = [new(200, 330, 178), new(200, 330, 175)] }] },
        templateDefinition with { Fields = [templateDefinition.Fields[0] with { Rows = [new(200, 331, 178)] }] },
        templateDefinition with { Fields = null! }
    })
    {
        WriteStampManifest(invalid);
        try { stampedGenerator.Template("test"); throw new Exception("Invalid template geometry accepted"); }
        catch (ConflictException) { }
    }
    foreach (var malformed in new[] { "null", "{" })
    {
        File.WriteAllText(Path.Combine(stampRoot, "manifest.json"), malformed);
        try { stampedGenerator.Template("test"); throw new Exception("Invalid manifest accepted"); }
        catch (ConflictException) { }
    }
    WriteStampManifest(templateDefinition);
    try { stampedGenerator.Generate("test", ssData); throw new Exception("Physical-signature annex stamped"); }
    catch (ConflictException) { }
    WriteStampManifest(templateDefinition with { Institutional = true });
    try { stampedGenerator.Generate("test", ssData); throw new Exception("Missing authority accepted"); }
    catch (ConflictException) { }
    ssData["authorities"] = new Dictionary<string, object> { ["social_service_head"] = new { FullName = "Autoridad de prueba" } };
    File.WriteAllBytes("tmp/pdf-qa/stamp-example.pdf", stampedGenerator.Generate("test", ssData));
    WriteStampManifest(templateDefinition with { Institutional = true, Stamps = [stampDefinition with { Opacity = 2 }] });
    try { stampedGenerator.Generate("test", ssData); throw new Exception("Invalid opacity accepted"); }
    catch (ConflictException) { }
    var storageRoot = Path.GetFullPath("tmp/pdf-qa/storage");
    var storageConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:RootPath"] = storageRoot }).Build();
    var storage = new LocalFileStorage(storageConfig);
    await using var validInput = new MemoryStream(bytes);
    var stored = await storage.SavePdfAsync(new FormFile(validInput, 0, bytes.Length, "file", "evaluation.pdf"), "student/general");
    await using (var saved = await storage.OpenReadAsync(stored)) Check(saved.Length == bytes.Length, "Storage preserves exact uploaded PDF bytes");
    storage.TryDelete(stored);
    var invalidBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7 invalid document");
    await using var invalidInput = new MemoryStream(invalidBytes);
    try
    {
        await storage.SavePdfAsync(new FormFile(invalidInput, 0, invalidBytes.Length, "file", "fake.pdf"), "student/general");
        throw new Exception("Malformed PDF accepted");
    }
    catch (AppException ex) { Check(ex.StatusCode == 415, "Malformed PDF returns 415"); }
    var sibling = storageRoot + "-outside";
    Directory.CreateDirectory(sibling);
    var probe = Path.Combine(sibling, Guid.NewGuid().ToString("N") + ".pdf");
    File.WriteAllBytes(probe, bytes);
    try { await storage.OpenReadAsync("../" + Path.GetFileName(sibling) + "/" + Path.GetFileName(probe)); throw new Exception("Sibling root escape accepted"); }
    catch (NotFoundException) { }
    storage.TryDelete("../" + Path.GetFileName(sibling) + "/" + Path.GetFileName(probe));
    Check(File.Exists(probe), "TryDelete cannot delete sibling-root files");
    File.Delete(probe);
    sample["studentFullName"] = new string('X', 1000);
    try { generator.Generate("pp-evaluation", sample); throw new Exception("Overflow accepted"); }
    catch (BusinessRuleException ex) { Check(ex.Code == "documents.text_overflow", "Explicit overflow error"); }
    try { generator.Template("../../invalid"); throw new Exception("Invalid template accepted"); }
    catch (ConflictException) { }
}
else Console.WriteLine("PDF rendering checks skipped: set DOCUMENT_TEST_FONTS to authorized Arial/Century Gothic fonts.");
await AccountMigrationChecks.RunAsync();
await LinkageDatabaseChecks.RunAsync();
await DatabaseChecks.RunAsync();
await HttpChecks.RunAsync();
Console.WriteLine("Document policy checks passed.");

sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
sealed class TestUser(string[] roles, long id = 1) : ICurrentUser
{
    public long? Id => id;
    public string? Username => "test";
    public bool IsAuthenticated => true;
    public IReadOnlyList<string> Roles => roles;
    public bool IsInRole(string role) => roles.Contains(role);
}

sealed class TestEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Tests";
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
