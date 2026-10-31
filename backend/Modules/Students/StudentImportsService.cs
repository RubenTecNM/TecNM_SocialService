using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Students;

public partial class StudentImportsService(
    TecNMDbContext dbContext,
    IConfiguration configuration)
{
    private static readonly byte[] ZipMagicBytes = [0x50, 0x4B, 0x03, 0x04]; // PK.. (.xlsx)

    private static readonly string[] TemplateHeaders =
    [
        "control_number",
        "first_surname",
        "second_surname",
        "first_name",
        "career_code",
        "curp",
        "gender",
        "shift",
        "current_semester",
        "completed_credits",
        "institutional_email",
        "personal_email",
        "phone_number",
        "has_disability",
        "disability_description"
    ];

    private static readonly string[] TemplateExampleRow =
    [
        "I23050363",
        "TORRES",
        "ARZOLA",
        "HECTOR GABRIEL",
        "INF",
        "",
        "M",
        "morning",
        "7",
        "156",
        "i23050363@monclova.tecnm.mx",
        "",
        "8661234567",
        "false",
        ""
    ];

    [GeneratedRegex(@"^([A-Z]+)(\d+)$")]
    private static partial Regex CellReferenceRegex();

    private string StorageRootPath =>
        configuration["Storage:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");

    public byte[] GenerateTemplateXlsx()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(archive, "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);

            WriteZipEntry(archive, "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);

            WriteZipEntry(archive, "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Estudiantes" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);

            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);

            var sb = new StringBuilder();
            sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sb.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
            sb.AppendLine("""  <sheetData>""");
            AppendSheetRow(sb, 1, TemplateHeaders);
            AppendSheetRow(sb, 2, TemplateExampleRow);
            sb.AppendLine("""  </sheetData>""");
            sb.AppendLine("""</worksheet>""");

            WriteZipEntry(archive, "xl/worksheets/sheet1.xml", sb.ToString());
        }

        return ms.ToArray();
    }

    public async Task<PagedResponse<StudentImportBatchDto>> GetPagedAsync(
        int page,
        int pageSize,
        long? periodId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.StudentImportBatches
            .Include(b => b.Period)
            .AsNoTracking();

        if (periodId.HasValue)
        {
            query = query.Where(b => b.PeriodId == periodId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normStatus = status.Trim().ToLowerInvariant();
            query = query.Where(b => b.Status == normStatus);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var currentPage = Math.Max(1, page);
        var validPageSize = Math.Clamp(pageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalItems / (double)validPageSize);

        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((currentPage - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync(cancellationToken);

        var creatorIds = items
            .Where(b => b.CreatedBy.HasValue)
            .Select(b => b.CreatedBy!.Value)
            .Distinct()
            .ToList();

        var creators = await dbContext.Users
            .AsNoTracking()
            .Where(u => creatorIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => string.Join(" ", new[] { u.FirstName, u.FirstSurname, u.SecondSurname }.Where(p => !string.IsNullOrWhiteSpace(p))),
                cancellationToken);

        var dtos = items
            .Select(b => ToBatchDto(b, b.CreatedBy.HasValue && creators.TryGetValue(b.CreatedBy.Value, out var name) ? name : null))
            .ToList();

        return new PagedResponse<StudentImportBatchDto>(dtos, currentPage, validPageSize, totalItems, totalPages);
    }

    public async Task<StudentImportResult> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var batch = await dbContext.StudentImportBatches
            .Include(b => b.Period)
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (batch is null)
        {
            throw new NotFoundException("student_imports.not_found", $"Lote de importación con id {id} no encontrado.");
        }

        string? creatorName = null;
        if (batch.CreatedBy.HasValue)
        {
            var creator = await dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == batch.CreatedBy.Value, cancellationToken);

            if (creator is not null)
            {
                creatorName = string.Join(" ", new[] { creator.FirstName, creator.FirstSurname, creator.SecondSurname }
                    .Where(p => !string.IsNullOrWhiteSpace(p)));
            }
        }

        var rowErrors = DeserializeRowErrors(batch.Notes);
        return ToResult(batch, creatorName, rowErrors);
    }

    public async Task<StudentImportResult> ImportExcelAsync(
        long periodId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var period = await dbContext.AcademicPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["periodId"] = ["El periodo académico especificado no existe."]
            });
        }

        if (file is null || file.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["El archivo Excel proporcionado está vacío."]
            });
        }

        const long maxSizeBytes = 5 * 1024 * 1024;
        if (file.Length > maxSizeBytes)
        {
            throw new AppException(413, "files.too_large", "El archivo Excel supera el tamaño máximo permitido de 5 MB.", "Archivo demasiado grande");
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException(415, "files.unsupported_type", "El archivo debe estar en formato Excel (.xlsx).", "Formato no compatible");
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        if (fileBytes.Length < 4 || !fileBytes.Take(4).SequenceEqual(ZipMagicBytes))
        {
            throw new AppException(415, "files.unsupported_type", "El archivo no es un libro OpenXML (.xlsx) válido.", "Formato no compatible");
        }

        List<Dictionary<int, string>> rawRows;
        try
        {
            rawRows = ReadFirstWorksheetRows(fileBytes);
        }
        catch (Exception)
        {
            throw new BusinessRuleException("student_imports.invalid_layout", "No fue posible leer las hojas del archivo .xlsx proporcionado.");
        }

        if (rawRows.Count == 0)
        {
            throw new BusinessRuleException("student_imports.invalid_layout", "El archivo Excel no contiene encabezados ni registros.");
        }

        var headerMap = MapColumns(rawRows[0]);
        if (!headerMap.ContainsKey("control_number") ||
            !headerMap.ContainsKey("career_code") ||
            (!headerMap.ContainsKey("first_name") && !headerMap.ContainsKey("full_name")))
        {
            throw new BusinessRuleException(
                "student_imports.invalid_layout",
                "El archivo no contiene las columnas obligatorias (control_number/matrícula, nombre y career_code/carrera). Descargue la plantilla oficial.");
        }

        // Guardar copia física del archivo importado
        var importsDir = Path.Combine(StorageRootPath, "imports");
        Directory.CreateDirectory(importsDir);
        var storedFileName = $"import_{period.Code}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.xlsx";
        var fullFilePath = Path.Combine(importsDir, storedFileName);
        await File.WriteAllBytesAsync(fullFilePath, fileBytes, cancellationToken);

        var batch = new StudentImportBatch
        {
            PeriodId = period.Id,
            FileName = Path.GetFileName(file.FileName),
            FileUrl = $"imports/{storedFileName}",
            Status = "processing"
        };

        dbContext.StudentImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        var careers = await dbContext.Careers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        var existingStudents = await dbContext.Students
            .ToDictionaryAsync(s => s.ControlNumber.ToUpperInvariant(), s => s, cancellationToken);

        var rowErrors = new List<StudentImportRowError>();
        var totalRecords = 0;
        var importedRecords = 0;
        var failedRecords = 0;

        for (var i = 1; i < rawRows.Count; i++)
        {
            var rowNumber = i + 1;
            var rowCells = rawRows[i];

            if (rowCells.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            totalRecords++;

            var rawControl = GetCell(rowCells, headerMap, "control_number")?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(rawControl) || rawControl.Length > 32)
            {
                failedRecords++;
                rowErrors.Add(new StudentImportRowError(rowNumber, rawControl, "control_number", "La matrícula es obligatoria (máximo 32 caracteres)."));
                continue;
            }

            var rawCareer = GetCell(rowCells, headerMap, "career_code")?.Trim();
            var career = MatchCareer(careers, rawCareer);
            if (career is null)
            {
                failedRecords++;
                rowErrors.Add(new StudentImportRowError(rowNumber, rawControl, "career_code", $"Carrera '{rawCareer}' no reconocida en el catálogo activo."));
                continue;
            }

            var firstSurname = GetCell(rowCells, headerMap, "first_surname")?.Trim();
            var secondSurname = GetCell(rowCells, headerMap, "second_surname")?.Trim();
            var firstName = GetCell(rowCells, headerMap, "first_name")?.Trim();

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(firstSurname))
            {
                var fullName = GetCell(rowCells, headerMap, "full_name")?.Trim();
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    (firstSurname, secondSurname, firstName) = SplitFullName(fullName);
                }
            }

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(firstSurname))
            {
                failedRecords++;
                rowErrors.Add(new StudentImportRowError(rowNumber, rawControl, "first_name", "Nombre y primer apellido son obligatorios."));
                continue;
            }

            var curp = GetCell(rowCells, headerMap, "curp")?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(curp) && curp.Length != 18)
            {
                failedRecords++;
                rowErrors.Add(new StudentImportRowError(rowNumber, rawControl, "curp", "La CURP debe tener exactamente 18 caracteres."));
                continue;
            }

            var gender = NormalizeGender(GetCell(rowCells, headerMap, "gender"));
            var shift = NormalizeShift(GetCell(rowCells, headerMap, "shift"));

            var semester = 7;
            var rawSemester = GetCell(rowCells, headerMap, "current_semester")?.Trim();
            if (!string.IsNullOrWhiteSpace(rawSemester))
            {
                if (!int.TryParse(rawSemester, NumberStyles.Integer, CultureInfo.InvariantCulture, out semester) ||
                    semester < 1 || semester > 15)
                {
                    failedRecords++;
                    rowErrors.Add(new StudentImportRowError(rowNumber, rawControl, "current_semester", "El semestre debe ser un entero entre 1 y 15."));
                    continue;
                }
            }

            var credits = 156;
            var rawCredits = GetCell(rowCells, headerMap, "completed_credits")?.Trim();
            if (!string.IsNullOrWhiteSpace(rawCredits))
            {
                if (!int.TryParse(rawCredits, NumberStyles.Integer, CultureInfo.InvariantCulture, out credits) ||
                    credits < 0 || credits > career.TotalCredits)
                {
                    failedRecords++;
                    rowErrors.Add(new StudentImportRowError(rowNumber, rawControl, "completed_credits", $"Los créditos deben estar entre 0 y {career.TotalCredits}."));
                    continue;
                }
            }

            var institutionalEmail = GetCell(rowCells, headerMap, "institutional_email")?.Trim().ToLowerInvariant();
            var personalEmail = GetCell(rowCells, headerMap, "personal_email")?.Trim().ToLowerInvariant();
            var phone = GetCell(rowCells, headerMap, "phone_number")?.Trim();
            var hasDisability = ParseBool(GetCell(rowCells, headerMap, "has_disability"));
            var disabilityDesc = hasDisability ? (GetCell(rowCells, headerMap, "disability_description")?.Trim() ?? "Reportada en padrón") : null;

            var progress = StudentsService.CalculateProgress(credits, career.TotalCredits);

            if (existingStudents.TryGetValue(rawControl, out var student))
            {
                student.CareerId = career.Id;
                student.ImportBatchId = batch.Id;
                student.FirstSurname = firstSurname;
                student.SecondSurname = string.IsNullOrWhiteSpace(secondSurname) ? null : secondSurname;
                student.FirstName = firstName;
                student.Curp = string.IsNullOrWhiteSpace(curp) ? student.Curp : curp;
                student.Gender = gender ?? student.Gender;
                student.Shift = shift;
                student.CurrentSemester = semester;
                student.CompletedCredits = credits;
                student.CurriculumProgressPercentage = progress;
                student.InstitutionalEmail = string.IsNullOrWhiteSpace(institutionalEmail) ? student.InstitutionalEmail : institutionalEmail;
                student.PersonalEmail = string.IsNullOrWhiteSpace(personalEmail) ? student.PersonalEmail : personalEmail;
                student.PhoneNumber = string.IsNullOrWhiteSpace(phone) ? student.PhoneNumber : phone;
                student.HasDisability = hasDisability;
                student.DisabilityDescription = disabilityDesc;
                student.IsEnrolled = true;
            }
            else
            {
                student = new Student
                {
                    CareerId = career.Id,
                    ImportBatchId = batch.Id,
                    ControlNumber = rawControl,
                    Curp = string.IsNullOrWhiteSpace(curp) ? null : curp,
                    FirstSurname = firstSurname,
                    SecondSurname = string.IsNullOrWhiteSpace(secondSurname) ? null : secondSurname,
                    FirstName = firstName,
                    Gender = gender,
                    Shift = shift,
                    HasDisability = hasDisability,
                    DisabilityDescription = disabilityDesc,
                    InstitutionalEmail = string.IsNullOrWhiteSpace(institutionalEmail) ? null : institutionalEmail,
                    PersonalEmail = string.IsNullOrWhiteSpace(personalEmail) ? null : personalEmail,
                    PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    CurrentSemester = semester,
                    CompletedCredits = credits,
                    CurriculumProgressPercentage = progress,
                    IsEnrolled = true
                };

                dbContext.Students.Add(student);
                existingStudents[rawControl] = student;
            }

            importedRecords++;
        }

        batch.TotalRecords = totalRecords;
        batch.ImportedRecords = importedRecords;
        batch.FailedRecords = failedRecords;
        batch.Status = importedRecords > 0 ? "completed" : "failed";
        batch.Notes = rowErrors.Count > 0 ? JsonSerializer.Serialize(rowErrors) : null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(batch.Id, cancellationToken);
    }

    private static List<Dictionary<int, string>> ReadFirstWorksheetRows(byte[] xlsxBytes)
    {
        using var stream = new MemoryStream(xlsxBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var sharedStrings = new List<string>();
        var sharedStringsEntry = archive.Entries
            .FirstOrDefault(e => e.FullName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));

        if (sharedStringsEntry is not null)
        {
            using var ssStream = sharedStringsEntry.Open();
            var ssDoc = XDocument.Load(ssStream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var si in ssDoc.Descendants(ns + "si"))
            {
                var text = string.Concat(si.Descendants(ns + "t").Select(t => t.Value));
                sharedStrings.Add(text);
            }
        }

        var sheetEntry = archive.Entries
            .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No worksheet found.");

        using var sheetStream = sheetEntry.Open();
        var sheetDoc = XDocument.Load(sheetStream);
        XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var rows = new List<Dictionary<int, string>>();
        var colIndexFallback = 0;

        foreach (var rowElem in sheetDoc.Descendants(mainNs + "row"))
        {
            var dict = new Dictionary<int, string>();
            colIndexFallback = 0;

            foreach (var cell in rowElem.Elements(mainNs + "c"))
            {
                var cellRef = cell.Attribute("r")?.Value;
                var colIdx = !string.IsNullOrWhiteSpace(cellRef) ? ParseColumnIndex(cellRef) : colIndexFallback;
                colIndexFallback = colIdx + 1;

                var cellType = cell.Attribute("t")?.Value;
                string value = string.Empty;

                if (cellType == "s")
                {
                    var vText = cell.Element(mainNs + "v")?.Value;
                    if (int.TryParse(vText, out var ssIdx) && ssIdx >= 0 && ssIdx < sharedStrings.Count)
                    {
                        value = sharedStrings[ssIdx];
                    }
                }
                else if (cellType == "inlineStr")
                {
                    value = string.Concat(cell.Descendants(mainNs + "t").Select(t => t.Value));
                }
                else
                {
                    value = cell.Element(mainNs + "v")?.Value ?? string.Empty;
                }

                dict[colIdx] = value;
            }

            rows.Add(dict);
        }

        return rows;
    }

    private static int ParseColumnIndex(string cellReference)
    {
        var match = CellReferenceRegex().Match(cellReference.Trim().ToUpperInvariant());
        if (!match.Success)
        {
            return 0;
        }

        var letters = match.Groups[1].Value;
        var index = 0;
        foreach (var ch in letters)
        {
            index = (index * 26) + (ch - 'A' + 1);
        }

        return index - 1;
    }

    private static Dictionary<string, int> MapColumns(Dictionary<int, string> headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (colIdx, rawHeader) in headerRow)
        {
            var h = NormalizeHeader(rawHeader);
            var key = h switch
            {
                "control_number" or "matricula" or "no_de_control" or "numero_de_control" or "nocontrol" => "control_number",
                "first_surname" or "primer_apellido" or "apellido_paterno" => "first_surname",
                "second_surname" or "segundo_apellido" or "apellido_materno" => "second_surname",
                "first_name" or "nombres" => "first_name",
                "nombre" or "nombre_completo" or "alumno" => headerRow.Values.Any(v => NormalizeHeader(v) is "first_surname" or "primer_apellido")
                    ? "first_name"
                    : "full_name",
                "career_code" or "carrera" or "clave_carrera" => "career_code",
                "curp" => "curp",
                "gender" or "sexo" or "genero" => "gender",
                "shift" or "turno" => "shift",
                "current_semester" or "semestre" or "sem" => "current_semester",
                "completed_credits" or "creditos" or "creditos_acumulados" => "completed_credits",
                "institutional_email" or "correo_institucional" or "correo" or "email" => "institutional_email",
                "personal_email" or "correo_personal" => "personal_email",
                "phone_number" or "telefono" or "tel" or "celular" => "phone_number",
                "has_disability" or "discapacidad" => "has_disability",
                "disability_description" or "descripcion_discapacidad" => "disability_description",
                _ => null
            };

            if (key is not null && !map.ContainsKey(key))
            {
                map[key] = colIdx;
            }
        }

        return map;
    }

    private static string NormalizeHeader(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Trim().ToLowerInvariant()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
            .Replace(".", "").Replace(" ", "_").Replace("-", "_");

        return normalized;
    }

    private static string? GetCell(Dictionary<int, string> row, Dictionary<string, int> map, string key) =>
        map.TryGetValue(key, out var idx) && row.TryGetValue(idx, out var val) ? val : null;

    private static Career? MatchCareer(IReadOnlyList<Career> careers, string? rawCareer)
    {
        if (string.IsNullOrWhiteSpace(rawCareer))
        {
            return null;
        }

        var clean = NormalizeHeader(rawCareer).ToUpperInvariant();
        var byCode = careers.FirstOrDefault(c => c.Code.Equals(rawCareer.Trim(), StringComparison.OrdinalIgnoreCase));
        if (byCode is not null)
        {
            return byCode;
        }

        if (clean.Contains("MECANICA") || clean.Contains("ELECTROMECANICA") || clean == "MEC")
            return careers.FirstOrDefault(c => c.Code == "MEC");
        if (clean.Contains("INDUSTRIAL") || clean == "IND")
            return careers.FirstOrDefault(c => c.Code == "IND");
        if (clean.Contains("ELECTRONICA") || clean == "ELE" || clean == "ELEC")
            return careers.FirstOrDefault(c => c.Code == "ELE");
        if (clean.Contains("INFORMATICA") || clean == "INF")
            return careers.FirstOrDefault(c => c.Code == "INF");
        if (clean.Contains("GESTION") || clean == "GE")
            return careers.FirstOrDefault(c => c.Code == "GE");
        if (clean.Contains("ENERGIA") || clean == "ER")
            return careers.FirstOrDefault(c => c.Code == "ER");

        return careers.FirstOrDefault(c => NormalizeHeader(c.Name).ToUpperInvariant().Contains(clean));
    }

    private static (string firstSurname, string? secondSurname, string firstName) SplitFullName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => ("", null, ""),
            1 => (parts[0], null, parts[0]),
            2 => (parts[0], null, parts[1]),
            3 => (parts[0], parts[1], parts[2]),
            _ => (parts[0], parts[1], string.Join(" ", parts.Skip(2)))
        };
    }

    private static string? NormalizeGender(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var u = raw.Trim().ToUpperInvariant();
        return u switch
        {
            "M" or "H" or "MASCULINO" or "HOMBRE" => "M",
            "F" or "MUJER" or "FEMENINO" => "F",
            _ => null
        };
    }

    private static string NormalizeShift(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "morning";
        }

        var u = raw.Trim().ToUpperInvariant();
        return u switch
        {
            "NIGHT" or "N" or "NOCTURNO" => "night",
            "AFTERNOON" or "V" or "VESPERTINO" => "afternoon",
            _ => "morning"
        };
    }

    private static bool ParseBool(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var u = raw.Trim().ToUpperInvariant();
        return u is "TRUE" or "1" or "SI" or "SÍ" or "YES";
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content.Trim());
    }

    private static void AppendSheetRow(StringBuilder sb, int rowNumber, IReadOnlyList<string> values)
    {
        sb.AppendLine($"    <row r=\"{rowNumber}\">");
        for (var c = 0; c < values.Count; c++)
        {
            var colRef = $"{(char)('A' + c)}{rowNumber}";
            var escaped = System.Security.SecurityElement.Escape(values[c]) ?? "";
            sb.AppendLine($"      <c r=\"{colRef}\" t=\"inlineStr\"><is><t>{escaped}</t></is></c>");
        }
        sb.AppendLine("    </row>");
    }

    private static IReadOnlyList<StudentImportRowError> DeserializeRowErrors(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StudentImportRowError>>(notes) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static StudentImportBatchDto ToBatchDto(StudentImportBatch b, string? createdByName) =>
        new(
            b.Id,
            b.PeriodId,
            b.Period?.Code ?? "",
            b.FileName,
            b.TotalRecords,
            b.ImportedRecords,
            b.FailedRecords,
            b.Status,
            b.Notes is not null && b.Notes.StartsWith('[') ? $"{b.FailedRecords} fila(s) con observaciones." : b.Notes,
            b.CreatedAt,
            createdByName);

    private static StudentImportResult ToResult(
        StudentImportBatch b,
        string? createdByName,
        IReadOnlyList<StudentImportRowError> rowErrors) =>
        new(
            b.Id,
            b.PeriodId,
            b.Period?.Code ?? "",
            b.FileName,
            b.TotalRecords,
            b.ImportedRecords,
            b.FailedRecords,
            b.Status,
            rowErrors.Count > 0 ? $"{rowErrors.Count} fila(s) con error de validación." : b.Notes,
            b.CreatedAt,
            createdByName,
            rowErrors);
}

