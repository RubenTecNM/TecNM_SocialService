using TecNM.Api.Core.Security;

namespace TecNM.Api.Modules.Expedients;

public sealed record DocumentRequirement(string Code, string Title, string? PdfKey = null,
    string? TemplateKey = null, bool CanUpload = true, string? BlockedReason = null);

public static class DocumentDefinitions
{
    public static readonly string[] SeparateEvaluations =
        ["EV_SUP1", "EV_SELF1", "EV_SUP2", "EV_SELF2", "EV_SUP3", "EV_SELF3", "EV_ACT"];

    public static IReadOnlyList<DocumentRequirement> For(string type, int version, ICurrentUser user)
    {
        var staff = user.IsInRole("department_head") || user.IsInRole(type == "internship" ? "internship_coordinator" : "social_service_officer");
        if (type == "internship") return
        [
            new("CP", "Carta de Presentación", CanUpload: false),
            new("CA", "Carta de Aceptación", CanUpload: staff,
                BlockedReason: staff ? null : "La empresa la envía a arturo.martinez@monclova.tecnm.mx. Vinculación la incorpora al expediente."),
            new("EV", "Anexo XVII · Evaluación de desempeño", "evaluation", CanUpload: user.IsInRole("student")),
            new("CT", "Oficio de Terminación", CanUpload: user.IsInRole("student")),
            new("CC", "Constancia de Liberación", CanUpload: false)
        ];

        var docs = new List<DocumentRequirement>
        {
            new("SOL", SocialServiceDocumentCatalog.Title("SOL"), CanUpload: false),
            new("CP", SocialServiceDocumentCatalog.Title("CP"), BlockedReason: "Adjunta el PDF firmado. Vinculación debe revisarlo y aprobarlo."),
            new("CA", SocialServiceDocumentCatalog.Title("CA"), TemplateKey: "ss-acceptance-example"),
            new("CC", SocialServiceDocumentCatalog.Title("CC"), "commitment")
        };
        for (var period = 1; period <= 3; period++)
        {
            docs.Add(new($"R{period}", SocialServiceDocumentCatalog.Title($"R{period}"), $"report_{period}"));
            if (version >= 2)
            {
                docs.Add(new($"EV_SUP{period}", SocialServiceDocumentCatalog.Title($"EV_SUP{period}"), $"evaluation_{period}"));
                docs.Add(new($"EV_SELF{period}", SocialServiceDocumentCatalog.Title($"EV_SELF{period}"), $"self_evaluation_{period}"));
            }
            else docs.Add(new($"EV{period}", SocialServiceDocumentCatalog.Title($"EV{period}"), $"evaluation_{period}"));
        }
        if (version >= 2) docs.Add(new("EV_ACT", SocialServiceDocumentCatalog.Title("EV_ACT"), "activity_assessment"));
        docs.Add(new("CT", SocialServiceDocumentCatalog.Title("CT"), "termination_letter"));
        return docs.OrderBy(d => SocialServiceDocumentCatalog.Order(d.Code))
            .Select(d => d with { Title = SocialServiceDocumentCatalog.Title(d.Code), CanUpload = d.CanUpload && user.IsInRole("student") }).ToList();
    }

    public static bool CanUpload(string type, string code, int version, ICurrentUser user)
    {
        var isStaff = user.IsInRole("department_head") || user.IsInRole(type == "social_service" ? "social_service_officer" : "internship_coordinator");
        if (!isStaff && !user.IsInRole("student")) return false;
        return For(type, version, user).Any(d => d.Code == code && d.CanUpload);
    }

    public static string[] RequiredForSocialService(int version) => version >= 2
        ? ["SOL", "CP", "CA", "CC", "R1", "R2", "R3", .. SeparateEvaluations, "CT"]
        : ["CA", "CC", "R1", "R2", "R3", "CT"];
}
