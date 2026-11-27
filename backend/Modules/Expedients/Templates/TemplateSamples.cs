namespace TecNM.Api.Modules.Expedients.Templates;

internal static class TemplateSamples
{
    internal static Dictionary<string, object?> Create(bool longSample)
    {
        var start = new DateOnly(2026, 8, 1);
        var end = new DateOnly(2026, 9, 30);
        return new()
        {
            ["studentFullName"] = longSample ? "María Guadalupe Muñoz de la Peña" : "María José Muñoz Peña",
            ["studentFirstName"] = longSample ? "María Guadalupe" : "María José",
            ["studentFirstSurname"] = "Muñoz", ["studentSecondSurname"] = "Peña",
            ["studentControlNumber"] = "I23050001", ["studentPhone"] = "8661234567",
            ["studentAddress"] = longSample ? "Avenida de la Constitución 425, Monclova" : "Calle Hidalgo 120, Monclova",
            ["studentSemester"] = 7, ["dependencyName"] = "Instituto Tecnológico Superior de Monclova",
            ["dependencyHeadName"] = "Juan Pérez López", ["dependencyHeadPosition"] = "Director",
            ["supervisorPosition"] = longSample ? "Coordinadora de los programas de vinculación" : "Coordinadora de Servicio Social",
            ["supervisorFullName"] = "Ana Gómez Díaz", ["dependencyAddress"] = "Carretera 57 km 4.5, Monclova",
            ["careerName"] = "Ingeniería Informática", ["careerCode"] = "INF",
            ["programName"] = longSample ? "Apoyo tecnológico y comunitario" : "Apoyo tecnológico",
            ["programCategory"] = "institutional_programs", ["periodCode"] = "Agosto - Enero",
            ["studentGender"] = "Mujer", ["modality"] = "Interno", ["generatedAt"] = start,
            ["startDate"] = start, ["endDate"] = end, ["commitmentDate"] = start, ["city"] = "Monclova",
            ["activitiesOutline"] = longSample ? "Soporte técnico y capacitación de usuarios del programa." : "Apoyo tecnológico",
            ["activitiesSummary"] = longSample
                ? "Mantenimiento de equipos para personas usuarias del programa.\nApoyo a la coordinación en inventarios y capacitación."
                : "Mantenimiento de equipos y apoyo a usuarios.",
            ["issueDate"] = end, ["accumulatedHours"] = 480, ["reportNumber"] = 1, ["reportedHours"] = 160,
            ["reportAccumulatedHours"] = 160, ["periodStartDate"] = start, ["periodEndDate"] = end,
            ["companyName"] = "Instituto Tecnológico Superior de Monclova", ["realizationPeriod"] = "01/08/2026 - 30/09/2026",
            ["finalMark"] = "X", ["evaluationStartDate"] = start, ["evaluationEndDate"] = end,
            ["actBody"] = longSample
                ? "Se hace constar que María Guadalupe Muñoz de la Peña realizó su Servicio Social en el Instituto Tecnológico Superior de Monclova, donde brindó soporte técnico, capacitación y atención a usuarios. El servicio cubrió un total de 480 horas."
                : "Se hace constar que María José Muñoz Peña realizó su Servicio Social y cubrió 480 horas.",
            ["actDate"] = "Se expide en Monclova, Coahuila, a 30 de septiembre de 2026.",
            ["outreachAuthority"] = "M.I. Ana Gómez Díaz\nJefa de División de Gestión Tecnológica y Vinculación",
            ["directorAuthority"] = "Juan Pérez Muñoz\nDirector General"
        };
    }
}
