# Contrato: Reportes e indicadores

> **Estado:** Borrador v0.2 (alineado a las fórmulas de `SEGUIMIENTO VINCULACIÓN1.xlsx`). Convenciones en [api-conventions.spec.md](./api-conventions.spec.md). Solo lectura sobre todas las tablas. Política: `ReportViewer`.

Todos los endpoints aceptan `periodId` (default: periodo activo) y responden JSON en tiempo real conforme los alumnos suben documentos y Vinculación los valida. Exportación a Excel/PDF la hace frontend (SheetJS / jsPDF).

## 1. Tablero — `GET /api/reports/dashboard`

```ts
interface DashboardResponse {
  period: { id: number; code: string };
  socialService: Record<TramiteStatus, number>;
  internships: Record<TramiteStatus, number>;
  pendingDocumentReviews: number;
  agreementsExpiringSoon: number;   // validity = expiring
  openNonConformances: number;
}
```

## 2. Sábana de Seguimiento de Vinculación — `GET /api/reports/tracking`

Reemplaza las hojas `SERVICIO SOCIAL`, `PRÁCTICAS` y `PRINCIPAL CONSULTA` de `SEGUIMIENTO VINCULACIÓN1.xlsx`. Se actualiza automáticamente conforme se emiten cartas y se aprueban documentos (`SI`).
Query: `periodId`, `tramiteType`* (`social_service` | `internship`), `status`, `careerId`, `search` (matrícula o nombre).

```ts
interface TrackingRow {
  tramiteId: number;
  controlNumber: string;
  studentFullName: string;
  careerCode: string;
  careerName: string;
  semesterEnrolled: number;
  gender: "M" | "F" | null;
  shift: StudentShift;
  phoneNumber: string | null;
  companyName: string | null;
  companySector: string | null;
  supervisorName: string | null;
  agreementNumber: string | null;
  programName: string | null;        // solo SS
  startDate: string | null;
  endDate: string | null;
  status: TramiteStatus;
  progressPercentage: number;        // 0..100 (10% por doc en SS, 20% por doc en PP, igual a PRINCIPAL CONSULTA)
  // Checklist documental en vivo (true = "SI" en Excel):
  documents: {
    cp: boolean;                     // Carta de Presentación emitida/aprobada
    ca: boolean;                     // Carta / Oficio de Aceptación aprobado
    sol?: boolean;                   // Solicitud Anexo XVIII aprobada (SS)
    cc?: boolean;                    // Carta Compromiso Anexo XX aprobada (SS) / Expediente completo liberado (PP)
    r1?: boolean;                    // Reporte Bimestral 1 aprobado (SS)
    r2?: boolean;                    // Reporte Bimestral 2 aprobado (SS)
    r3?: boolean;                    // Reporte Bimestral 3 aprobado (SS)
    ev1?: boolean;                   // Evaluación Cualitativa 1 aprobada (SS)
    ev2?: boolean;                   // Evaluación Cualitativa 2 aprobada (SS)
    ev3?: boolean;                   // Evaluación Cualitativa 3 aprobada (SS)
    ev?: boolean;                    // Evaluación Anexo XVII aprobada (PP)
    ct: boolean;                     // Carta de Terminación aprobada
  };
  presentationLetterFolio: string | null; // ej. SS_CP0405/17/09/2026 o Oficio No. EJ26/PR013
  releaseCertificateFolio: string | null; // CONST.TERM (ej. SSC82)
  originalTerminationLetterReceived: boolean; // CT. ORIGINAL
  finalGradeLevel: QualitativeGrade | null;   // CALIF (ej. EXCELENTE)
  isPnc: boolean;                             // PNC
  observations: string | null;                // OBSERVACIONES
  schoolServicesActFolio: string | null;      // ACTA ENVIADA (ej. DGTV016/24-02-2026)
  beneficiariesCount: number | null;          // BENEFICIADOS (solo SS)
  hasInclusionActivities: boolean | null;     // ACTIVIDADES INCLUSIÓN (solo SS)
  targetCommunity: string | null;             // COMUNIDADES (solo SS)
}
```

Respuesta: `200 TrackingRow[]` sin paginar (volumen semestral acotado, exportable directo a `.xlsx`).

## 3. Estadístico Semestral y por Año Escolar — `GET /api/reports/statistics`

Reemplaza las hojas `ESTADÍSTICO` y `PNC` de `SEGUIMIENTO VINCULACIÓN1.xlsx`. Replica exactamente las fórmulas `COUNTIFS` del archivo oficial en tiempo real conforme se validan los documentos:
- **En condiciones (`eligibleWithPresentationLetter`):** Alumnos con Carta de Presentación (`CP = SI`).
- **Solicitantes (`applicants`):** Alumnos que entregaron Carta de Aceptación (`CA = SI`).
- **Liberados (`released`):** Alumnos con Carta de Terminación / Constancia de Liberación (`CTA. TERM. = SI` / `CONST.TERM` en SS; `CA = SI` y `CC = 1` en PP).
- **PNC estadístico (`pnc`):** `Solicitantes - Liberados`.

Query: `periodId` (semestre principal, ej. `FJ26`), `comparePeriodId?` (semestre anterior para consolidado de **Año Escolar**, ej. `AD25`), `tramiteType`* (`social_service` | `internship`).

```ts
interface CareerStatisticalBreakdown {
  careerCode: string;                // MEC, IND, ELE, INF, GE, ER
  careerName: string;
  eligibleWithPresentationLetter: number; // En condiciones (CP = SI)
  applicantsMale: number;            // Solicitantes Hombres (CA = SI, gender = M)
  applicantsFemale: number;          // Solicitantes Mujeres (CA = SI, gender = F)
  applicantsTotal: number;           // Total Solicitantes (CA = SI)
  releasedMale: number;              // Liberados Hombres
  releasedFemale: number;            // Liberados Mujeres
  releasedTotal: number;             // Total Liberados
  pncTotal: number;                  // Solicitantes - Liberados
  releaseRatePercentage: number;     // round(releasedTotal / applicantsTotal * 100, 2)
}

interface StatisticalReportResponse {
  period: { id: number; code: string; name: string };
  tramiteType: "social_service" | "internship";
  byCareer: CareerStatisticalBreakdown[];
  totals: Omit<CareerStatisticalBreakdown, "careerCode" | "careerName">;
  schoolYearComparison: {
    firstPeriodCode: string;         // ej. AD25
    secondPeriodCode: string;        // ej. FJ26
    totalEligible: number;
    totalApplicants: number;
    totalReleased: number;
    totalPnc: number;
    schoolYearReleaseRatePercentage: number;
  } | null;
}
```

## 4. Estadística SEP/TecNM 911.10 (XII y XIII) — `GET /api/reports/911`

Reemplaza las hojas `911.10 XII. SS,PRÁCTICAS E INTE` y `911.10 XIII. PROYECTOS DE VINCU`.
Query: `periodId`, `comparePeriodId?` (para columna `TOTAL POR CICLO ESCOLAR`), `tramiteType`*.

```ts
interface Report911Response {
  period: { id: number; code: string };
  tramiteType: "social_service" | "internship";
  byCareerAndGender: {
    careerCode: string;
    sector: "educational" | "public" | "private" | "social";
    male: number;
    female: number;
    unspecified: number;
    total: number;
    withDisability: number;
  }[];
  byCompanySector: {
    sector: "educational" | "public" | "private" | "social";
    male: number;
    female: number;
    total: number;
    withDisability: number;
  }[];
  byCompanySize: { companySize: string; total: number }[];
  withDisability: { male: number; female: number; total: number };
  byShift: { shift: string; total: number }[];
  withInclusionActivities: number;   // solo SS
  totalBeneficiaries: number;        // solo SS
}
```

Universo estadístico (alineado con `SEGUIMIENTO VINCULACIÓN1.xlsx`):
- **Reporte 911.10 XII (Servicio Social y Prácticas Profesionales Liberados):** Contabiliza trámites liberados (`CTA. TERM. = SI` / `released` en SS; `CA = SI` y `CC = 1` / `released` en PP), desglosados por sector (`Educativo (propia Institución)`, `Público`, `Privado`, `Social`), carrera, sexo y discapacidad.
- **Reporte 911.10 XIII (Proyectos de Vinculación con Empresas / PIID):** Contabiliza proyectos y convenios con estudiantes colocados (`CA = SI`) por sector y tamaño de empresa (`Micro`, `Pequeña`, `Mediana`, `Grande`).

## 5. Indicador PNC (ISO 9001) — `GET /api/reports/non-conformance-indicator`

```ts
interface NonConformanceIndicator {
  period: { id: number; code: string };
  tramiteType: "social_service" | "internship";
  applicants: number;        // trámites con CA = SI en el periodo
  released: number;
  nonCompliant: number;
  openNonConformances: number;
  releaseRate: number;       // released / applicants, 0–1
  byCategory: { category: string; total: number }[];
}
```

## 6. Tiempos de ciclo — `GET /api/reports/cycle-times` (fase posterior)

Promedio de días entre transiciones a partir de `tramite_status_logs`.

```ts
interface CycleTimesResponse {
  tramiteType: "social_service" | "internship";
  transitions: { from: string; to: string; averageDays: number; samples: number }[];
}
```

## Flujos UI esperados

- Dashboard inicial de staff con tarjetas y gráficas en vivo.
- Vista **"Seguimiento Vinculación"** con buscador tipo `PRINCIPAL CONSULTA` (barra de 0% a 100% por alumno) y tabla general con columnas `CP, CA, SOL, CC, R1, R2, R3, EV1, EV2, EV3, CT`.
- Vista **"Estadístico y 911.10"** con selector de semestre / ciclo escolar y botón **"Exportar a Excel"**.
