BEGIN;

-- ============================================================================
-- 1. TRÁMITES DE SERVICIO SOCIAL (5 Escenarios Operativos Completos)
-- ============================================================================
INSERT INTO social_services (
    id, student_id, period_id, program_id, agreement_id, company_id, supervisor_id,
    semester_enrolled, modality, start_date, end_date, required_hours, accumulated_hours,
    status, presentation_letter_folio, presentation_letter_date, acceptance_letter_date,
    termination_letter_date, original_termination_letter_received, release_certificate_folio,
    released_at, final_grade_level, final_score, school_services_act_sent,
    school_services_act_sent_at, school_services_act_folio, beneficiaries_count,
    has_inclusion_activities, target_community, observations, created_by
)
OVERRIDING SYSTEM VALUE
VALUES
    -- SS #1: Alumna 2 ('alumno.ss.local') — Modalidad INTERNA en TecNM, estatus 'approved' (CP emitida, lista para generar CA local)
    (
        1,
        2,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        (SELECT id FROM social_service_programs WHERE code = 'SS-INS-TEC' AND deleted_at IS NULL LIMIT 1),
        NULL,
        1, -- TecNM Campus Monclova (Interno)
        1, -- Ing. Marco Antonio Salazar Silva
        8,
        'internal',
        '2026-02-16',
        '2026-08-16',
        480,
        0,
        'approved',
        'SS_CP0001/15/02/2026',
        '2026-02-15',
        NULL,
        NULL,
        false,
        NULL,
        NULL,
        NULL,
        NULL,
        false,
        NULL,
        NULL,
        120,
        true,
        'Comunidad Estudiantil ITSM',
        'Solicitud aprobada. Lista para descargar plantilla de Carta de Aceptación local.',
        6
    ),
    -- SS #2: Alumna 4 ('alumno.ss.activo') — Modalidad EXTERNA en Presidencia Municipal, estatus 'active' (avanzando reportes R1..R3)
    (
        2,
        4,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        (SELECT id FROM social_service_programs WHERE code = 'SS-DES-COM' AND deleted_at IS NULL LIMIT 1),
        1,
        2, -- Presidencia Municipal de Monclova
        2, -- Lic. Cynthia Elizabeth Ramos Vega
        6,
        'external',
        '2026-02-10',
        '2026-08-10',
        480,
        320,
        'active',
        'SS_CP0002/10/02/2026',
        '2026-02-10',
        '2026-02-14',
        NULL,
        false,
        NULL,
        NULL,
        NULL,
        NULL,
        false,
        NULL,
        NULL,
        250,
        true,
        'Colonias Zona Oriente Monclova',
        'Primer bimestre validado. Segundo reporte bimestral en revisión.',
        7
    ),
    -- SS #3: Alumna 6 ('alumno.liberado') — Servicio Social 100% LIBERADO ('released') con Constancia SSC y Acta DGTV
    (
        3,
        6,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        (SELECT id FROM social_service_programs WHERE code = 'SS-GOB-ESP' AND deleted_at IS NULL LIMIT 1),
        1,
        2, -- Presidencia Municipal de Monclova
        2,
        8,
        'external',
        '2026-02-09',
        '2026-08-09',
        480,
        480,
        'released',
        'SS_CP0003/09/02/2026',
        '2026-02-09',
        '2026-02-12',
        '2026-08-09',
        true,
        'SSC0001/10/08/2026',
        '2026-08-10 14:30:00+00',
        'excellent',
        98.00,
        true,
        '2026-08-12 10:00:00+00',
        'DGTV001/12-08-2026',
        340,
        true,
        'Municipio de Monclova',
        'Expediente 100% validado y liberado.',
        10
    ),
    -- SS #4: Alumno 7 — Solicitud recién enviada ('requested') pendiente de dictamen por Encargado de SS
    (
        4,
        7,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        (SELECT id FROM social_service_programs WHERE code = 'SS-MED-AMB' AND deleted_at IS NULL LIMIT 1),
        NULL,
        1, -- TecNM Campus Monclova (Interno)
        1,
        6,
        'internal',
        '2026-03-01',
        '2026-09-01',
        480,
        0,
        'requested',
        NULL,
        NULL,
        NULL,
        NULL,
        false,
        NULL,
        NULL,
        NULL,
        NULL,
        false,
        NULL,
        NULL,
        80,
        false,
        'Campus ITSM',
        'En espera de revisión de apertura Anexo XVIII y emisión de Carta de Presentación.',
        3
    ),
    -- SS #5: Alumna 8 — Todos los documentos aprobados ('completed_pending_review'), lista para emitir Constancia de Liberación
    (
        5,
        8,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        (SELECT id FROM social_service_programs WHERE code = 'SS-PRO-FES' AND deleted_at IS NULL LIMIT 1),
        2,
        3, -- Gunderson Gimsa
        3,
        9,
        'external',
        '2026-02-09',
        '2026-08-09',
        480,
        480,
        'completed_pending_review',
        'SS_CP0004/09/02/2026',
        '2026-02-09',
        '2026-02-13',
        '2026-08-09',
        true,
        NULL,
        NULL,
        'excellent',
        95.00,
        false,
        NULL,
        NULL,
        60,
        true,
        'Sector Industrial Región Centro',
        'Documentación completa validada. Lista para emisión de Constancia de Terminación SSC.',
        3
    )
ON CONFLICT (id) DO UPDATE SET
    status = EXCLUDED.status,
    accumulated_hours = EXCLUDED.accumulated_hours,
    presentation_letter_folio = EXCLUDED.presentation_letter_folio,
    release_certificate_folio = EXCLUDED.release_certificate_folio,
    school_services_act_folio = EXCLUDED.school_services_act_folio,
    observations = EXCLUDED.observations;

SELECT setval(pg_get_serial_sequence('social_services', 'id'), COALESCE(MAX(id), 1)) FROM social_services;

-- ============================================================================
-- 2. SOLICITUDES ANEXO XVIII (social_service_applications)
-- ============================================================================
INSERT INTO social_service_applications (
    id, social_service_id, student_address, student_phone, period_type, current_semester,
    dependency_name, dependency_head_name, dependency_head_position, activities_outline,
    is_accepted_by_office, office_observations, reviewed_by, reviewed_at
)
OVERRIDING SYSTEM VALUE
VALUES
    (
        1, 1,
        'Calle Hidalgo No. 312, Col. Guadalupe, Monclova, Coah.', '8662310944', 'february_july', 8,
        'Instituto Tecnológico Superior de Monclova Ejército Mexicano',
        'Ing. Marco Antonio Salazar Silva', 'Jefe de Departamento de Sistemas y Computación',
        'Apoyo en mantenimiento de laboratorios de cómputo, inventario de equipos y asesoría técnica a estudiantes.',
        true, 'Cumple con >70% de créditos y vigencia IMSS activa.', 3, '2026-02-15 10:00:00+00'
    ),
    (
        2, 2,
        'Av. Acereros No. 804, Col. Obrera, Monclova, Coah.', '8661127765', 'february_july', 6,
        'Presidencia Municipal de Monclova',
        'Lic. Cynthia Elizabeth Ramos Vega', 'Directora de Recursos Humanos',
        'Digitalización de padrones ciudadanos, atención en ventanilla social y logística de brigadas comunitarias.',
        true, 'Aprobada con convenio vigente CONV-ITSM-MUN-2025-01.', 3, '2026-02-10 09:30:00+00'
    ),
    (
        3, 3,
        'Blvd. Benito Juárez No. 519, Col. Picasso, Monclova, Coah.', '8661549087', 'february_july', 8,
        'Presidencia Municipal de Monclova',
        'Lic. Cynthia Elizabeth Ramos Vega', 'Directora de Recursos Humanos',
        'Diagnóstico energético en edificios públicos municipales y campañas de ahorro de energía.',
        true, 'Expediente ejemplar.', 3, '2026-02-09 11:00:00+00'
    ),
    (
        4, 4,
        'Calle De la Fuente No. 105, Zona Centro, Monclova, Coah.', '8661340921', 'february_july', 6,
        'Instituto Tecnológico Superior de Monclova Ejército Mexicano',
        'Ing. Marco Antonio Salazar Silva', 'Jefe de Departamento de Sistemas y Computación',
        'Implementación de campaña de reciclaje electrónico y monitoreo ambiental en el campus.',
        NULL, NULL, NULL, NULL
    ),
    (
        5, 5,
        'Calle Venustiano Carranza No. 410, Frontera, Coah.', '8661884412', 'february_july', 9,
        'Gunderson Gimsa S.A. de C.V.',
        'Ing. Roberto Garza Villarreal', 'Gerente de Aseguramiento de Calidad',
        'Sistematización de formatos de seguridad industrial e inclusión laboral en planta.',
        true, 'Solicitud validada.', 3, '2026-02-09 12:00:00+00'
    )
ON CONFLICT (social_service_id) WHERE deleted_at IS NULL DO UPDATE SET
    is_accepted_by_office = EXCLUDED.is_accepted_by_office,
    office_observations = EXCLUDED.office_observations;

SELECT setval(pg_get_serial_sequence('social_service_applications', 'id'), COALESCE(MAX(id), 1)) FROM social_service_applications;

-- ============================================================================
-- 3. CARTAS COMPROMISO ANEXO XX (social_service_commitments)
-- ============================================================================
INSERT INTO social_service_commitments (
    id, social_service_id, student_control_number, student_address, student_phone,
    dependency_address, commitment_date, city, is_signed_by_student, signed_at
)
OVERRIDING SYSTEM VALUE
VALUES
    (
        1, 2, 'I23050412',
        'Av. Acereros No. 804, Col. Obrera, Monclova, Coah.', '8661127765',
        'Zaragoza No. 402, Zona Centro, Monclova, Coah.', '2026-02-14', 'Monclova', true, '2026-02-14 12:00:00+00'
    ),
    (
        2, 3, 'I23050618',
        'Blvd. Benito Juárez No. 519, Col. Picasso, Monclova, Coah.', '8661549087',
        'Zaragoza No. 402, Zona Centro, Monclova, Coah.', '2026-02-12', 'Monclova', true, '2026-02-12 12:00:00+00'
    ),
    (
        3, 5, 'I21050890',
        'Calle Venustiano Carranza No. 410, Frontera, Coah.', '8661884412',
        'Carretera 57 Km 12.5, Frontera, Coah.', '2026-02-13', 'Monclova', true, '2026-02-13 12:00:00+00'
    )
ON CONFLICT (social_service_id) WHERE deleted_at IS NULL DO NOTHING;

SELECT setval(pg_get_serial_sequence('social_service_commitments', 'id'), COALESCE(MAX(id), 1)) FROM social_service_commitments;

-- ============================================================================
-- 4. REPORTES BIMESTRALES ANEXO XXII (social_service_bi_monthly_reports)
-- ============================================================================
INSERT INTO social_service_bi_monthly_reports (
    id, social_service_id, report_number, period_start_date, period_end_date,
    activities_summary, reported_hours, accumulated_hours, evaluator_supervisor_id,
    status, office_observations, approved_by, approved_at
)
OVERRIDING SYSTEM VALUE
VALUES
    -- SS #2 ('alumno.ss.activo'): R1 aprobado, R2 enviado en revisión
    (
        1, 2, 1, '2026-02-10', '2026-04-10',
        'Clasificación y captura de expedientes de beneficiarios del programa social municipal.',
        160, 160, 2, 'approved', 'Sello y firma validados.', 3, '2026-04-14 10:00:00+00'
    ),
    (
        2, 2, 2, '2026-04-11', '2026-06-10',
        'Seguimiento a entregas en campo y actualización de padrón digital de apoyos.',
        160, 320, 2, 'submitted', NULL, NULL, NULL
    ),
    -- SS #3 ('alumno.liberado'): R1, R2, R3 aprobados (480 hrs)
    (
        3, 3, 1, '2026-02-09', '2026-04-09',
        'Levantamiento de cargas eléctricas e iluminación en edificios municipales.',
        160, 160, 2, 'approved', 'Correcto.', 3, '2026-04-12 10:00:00+00'
    ),
    (
        4, 3, 2, '2026-04-10', '2026-06-09',
        'Elaboración de propuestas de sustitución por luminarias LED de alta eficiencia.',
        160, 320, 2, 'approved', 'Correcto.', 3, '2026-06-12 10:00:00+00'
    ),
    (
        5, 3, 3, '2026-06-10', '2026-08-09',
        'Presentación de informe final de ahorro energético y capacitación al personal.',
        160, 480, 2, 'approved', 'Completado satisfactoriamente.', 3, '2026-08-09 16:00:00+00'
    ),
    -- SS #5 (Mariana Villarreal): R1, R2, R3 aprobados (480 hrs)
    (
        6, 5, 1, '2026-02-09', '2026-04-09',
        'Diagnóstico de señalética y rutas de evacuación inclusivas en nave industrial.',
        160, 160, 3, 'approved', 'Validado.', 3, '2026-04-11 10:00:00+00'
    ),
    (
        7, 5, 2, '2026-04-10', '2026-06-09',
        'Actualización de manuales de inducción y checklists de seguridad ISO.',
        160, 320, 3, 'approved', 'Validado.', 3, '2026-06-11 10:00:00+00'
    ),
    (
        8, 5, 3, '2026-06-10', '2026-08-09',
        'Cierre de auditoría interna y entrega de compendio documental a Calidad.',
        160, 480, 3, 'approved', 'Validado.', 3, '2026-08-09 15:00:00+00'
    )
ON CONFLICT (social_service_id, report_number) WHERE deleted_at IS NULL DO UPDATE SET
    status = EXCLUDED.status,
    accumulated_hours = EXCLUDED.accumulated_hours;

SELECT setval(pg_get_serial_sequence('social_service_bi_monthly_reports', 'id'), COALESCE(MAX(id), 1)) FROM social_service_bi_monthly_reports;

-- ============================================================================
-- 5. EVALUACIONES CUALITATIVAS ANEXOS XXIII, XXIV Y XXV
-- ============================================================================
INSERT INTO social_service_evaluations (
    id, social_service_id, evaluator_type, bimonthly_period,
    criterion_1, criterion_2, criterion_3, criterion_4, criterion_5, criterion_6, criterion_7,
    evaluator_supervisor_id, observations
)
OVERRIDING SYSTEM VALUE
VALUES
    (1, 2, 'external_supervisor', '1', 'excellent', 'excellent', 'notable', 'excellent', 'excellent', 'excellent', 'excellent', 2, 'Excelente disposición en el primer bimestre.'),
    (2, 2, 'student_self', '1', 'excellent', 'excellent', 'excellent', 'notable', 'excellent', 'excellent', 'excellent', NULL, 'Aprendizaje práctico en atención ciudadana.'),
    (3, 3, 'external_supervisor', '1', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 2, 'Desempeño sobresaliente.'),
    (4, 3, 'external_supervisor', '2', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 2, 'Desempeño sobresaliente.'),
    (5, 3, 'external_supervisor', '3', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 2, 'Proyecto concluido con excelencia.'),
    (6, 5, 'external_supervisor', '1', 'excellent', 'notable', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 3, 'Muy buen avance.'),
    (7, 5, 'external_supervisor', '2', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 3, 'Excelente compromiso.'),
    (8, 5, 'external_supervisor', '3', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 3, 'Cumplió todos los objetivos.')
ON CONFLICT (social_service_id, evaluator_type, bimonthly_period) WHERE deleted_at IS NULL DO NOTHING;

SELECT setval(pg_get_serial_sequence('social_service_evaluations', 'id'), COALESCE(MAX(id), 1)) FROM social_service_evaluations;

INSERT INTO social_service_activity_assessments (
    id, social_service_id,
    question_1, question_2, question_3, question_4, question_5, question_6, question_7, question_8,
    observations
)
OVERRIDING SYSTEM VALUE
VALUES
    (1, 3, 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'El programa permitió aplicar conocimientos de eficiencia energética en beneficio del municipio.'),
    (2, 5, 'excellent', 'excellent', 'notable', 'excellent', 'excellent', 'excellent', 'excellent', 'excellent', 'Experiencia enriquecedora en normatividad industrial.')
ON CONFLICT (social_service_id) WHERE deleted_at IS NULL DO NOTHING;

SELECT setval(pg_get_serial_sequence('social_service_activity_assessments', 'id'), COALESCE(MAX(id), 1)) FROM social_service_activity_assessments;

-- ============================================================================
-- 6. TRÁMITES DE PRÁCTICAS PROFESIONALES (4 Escenarios Operativos Completos)
-- ============================================================================
INSERT INTO internships (
    id, student_id, period_id, company_id, supervisor_id, agreement_id,
    semester_enrolled, start_date, end_date, schedule_description,
    required_hours, completed_hours, status, credits_awarded,
    presentation_letter_folio, presentation_letter_date, acceptance_letter_date,
    termination_letter_date, original_termination_letter_received,
    release_certificate_folio, release_date, released_at, is_expedient_complete,
    school_services_act_sent, school_services_act_sent_at, school_services_act_folio,
    observations, created_by
)
OVERRIDING SYSTEM VALUE
VALUES
    -- PP #1: Alumno 3 ('alumno.pp.solicitud') — Solicitud aprobada con Carta de Presentación emitida (EJ26/PR001), esperando subir Oficio de Aceptación membretado (CA)
    (
        1,
        3,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        NULL,
        NULL,
        NULL,
        7,
        NULL,
        NULL,
        NULL,
        240,
        0,
        'approved',
        2,
        'EJ26/PR001',
        '2026-02-18',
        NULL,
        NULL,
        false,
        NULL,
        NULL,
        NULL,
        false,
        false,
        NULL,
        NULL,
        'Carta de Presentación emitida. En espera de Oficio de Aceptación membretado enviado a arturo.martinez@monclova.tecnm.mx.',
        8
    ),
    -- PP #2: Alumno 5 ('alumno.pp.activo') — Prácticas activas en Gunderson Gimsa, con CP y CA aprobadas y EV en revisión
    (
        2,
        5,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        3, -- Gunderson Gimsa
        3, -- Ing. Roberto Garza Villarreal
        2,
        8,
        '2026-02-20',
        '2026-06-20',
        'Lunes a Viernes de 08:00 a 13:00 hrs',
        240,
        240,
        'active',
        2,
        'EJ26/PR002',
        '2026-02-16',
        '2026-02-20',
        NULL,
        false,
        NULL,
        NULL,
        NULL,
        false,
        false,
        NULL,
        NULL,
        'Oficio de Aceptación validado. Alumno subió Evaluación Anexo XVII para revisión.',
        9
    ),
    -- PP #3: Alumno 9 (Jorge Eduardo Ramos) — Solicitud recién enviada ('requested') desde el formulario tipo Forms, pendiente de aprobar por Coordinador de Prácticas
    (
        3,
        9,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        NULL,
        NULL,
        NULL,
        7,
        NULL,
        NULL,
        NULL,
        240,
        0,
        'requested',
        2,
        NULL,
        NULL,
        NULL,
        NULL,
        false,
        NULL,
        NULL,
        NULL,
        false,
        false,
        NULL,
        NULL,
        'Solicitud recibida con Constancia de Vigencia IMSS adjunta. Pendiente emitir Carta de Presentación.',
        4
    ),
    -- PP #4: Alumna 10 (Karla Berenice Zapata) — Prácticas 100% LIBERADAS ('released') en SIMSA Industrial
    (
        4,
        10,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        4, -- SIMSA Industrial
        4, -- Ing. Sofía Valdés Cantú
        NULL,
        9,
        '2026-02-10',
        '2026-06-15',
        'Lunes a Viernes de 09:00 a 14:00 hrs',
        240,
        240,
        'released',
        2,
        'EJ26/PR003',
        '2026-02-09',
        '2026-02-11',
        '2026-06-16',
        true,
        'PPC0001/20/06/2026',
        '2026-06-20',
        '2026-06-20 15:00:00+00',
        true,
        true,
        '2026-06-22 11:00:00+00',
        'DGTV002/22-06-2026',
        'Expediente de Prácticas Profesionales 100% liberado y acreditado.',
        4
    )
ON CONFLICT (id) DO UPDATE SET
    status = EXCLUDED.status,
    presentation_letter_folio = EXCLUDED.presentation_letter_folio,
    release_certificate_folio = EXCLUDED.release_certificate_folio,
    is_expedient_complete = EXCLUDED.is_expedient_complete,
    observations = EXCLUDED.observations;

SELECT setval(pg_get_serial_sequence('internships', 'id'), COALESCE(MAX(id), 1)) FROM internships;

-- ============================================================================
-- 7. EVALUACIONES DE PRÁCTICAS ANEXO XVII (internship_evaluations)
-- ============================================================================
INSERT INTO internship_evaluations (
    id, internship_id, activity_name, evaluation_start_date, evaluation_end_date,
    criterion_1, criterion_2, criterion_3, criterion_4, criterion_5, criterion_6, criterion_7,
    numerical_average, performance_level, is_competency_achieved,
    evaluator_supervisor_id, evaluator_signature_date, observations
)
OVERRIDING SYSTEM VALUE
VALUES
    (
        1, 2, 'PRODUCTIVIDAD LABORAL', '2026-02-20', '2026-06-20',
        4, 4, 4, 3, 4, 4, 4,
        3.86, 'excellent', true,
        3, '2026-06-20', 'Excelente desempeño en pruebas de control e instrumentación en planta.'
    ),
    (
        2, 4, 'PRODUCTIVIDAD LABORAL', '2026-02-10', '2026-06-15',
        4, 4, 4, 4, 4, 4, 4,
        4.00, 'excellent', true,
        4, '2026-06-16', 'Cumplió de manera sobresaliente con las metas del área de ingeniería.'
    )
ON CONFLICT (internship_id) WHERE deleted_at IS NULL DO NOTHING;

SELECT setval(pg_get_serial_sequence('internship_evaluations', 'id'), COALESCE(MAX(id), 1)) FROM internship_evaluations;

-- ============================================================================
-- 8. EXPEDIENTE DOCUMENTAL (expedient_documents)
-- Incluye documentos aprobados (que alimentan SEGUIMIENTO VINCULACIÓN1.xlsx),
-- documentos pendientes en la Bandeja de Revisión y un documento observado.
-- ============================================================================
INSERT INTO expedient_documents (
    id, student_id, tramite_type, tramite_id, document_code,
    original_file_name, storage_path, file_mime_type, file_size_bytes,
    status, rejection_reason, reviewed_by, reviewed_at, created_by
)
OVERRIDING SYSTEM VALUE
VALUES
    -- Documentos de SS #1 (Alumna 2 - 'alumno.ss.local'): SOL y CP aprobados (20% avance)
    (1,  2, 'social_service', 1, 'SOL', 'I22050114_SOL_AnexoXVIII.pdf', 'seed/I22050114_SOL.pdf', 'application/pdf', 320400, 'approved', NULL, 3, '2026-02-15 10:00:00+00', 6),
    (2,  2, 'social_service', 1, 'CP',  'I22050114_CP_Interna_TecNM.pdf', 'seed/I22050114_CP.pdf', 'application/pdf', 280100, 'approved', NULL, 3, '2026-02-15 10:05:00+00', 3),

    -- Documentos de SS #2 (Alumna 4 - 'alumno.ss.activo'): SOL, CP, CA, CC, R1, EV1 aprobados (60% avance) + R2 y EV2 EN REVISIÓN ('pending')
    (3,  4, 'social_service', 2, 'SOL', 'I23050412_SOL_AnexoXVIII.pdf', 'seed/I23050412_SOL.pdf', 'application/pdf', 340120, 'approved', NULL, 3, '2026-02-10 09:30:00+00', 7),
    (4,  4, 'social_service', 2, 'CP',  'I23050412_CP_Presidencia.pdf', 'seed/I23050412_CP.pdf', 'application/pdf', 295000, 'approved', NULL, 3, '2026-02-10 09:35:00+00', 3),
    (5,  4, 'social_service', 2, 'CA',  'I23050412_CA_Presidencia_Firmada.pdf', 'seed/I23050412_CA.pdf', 'application/pdf', 512000, 'approved', NULL, 3, '2026-02-14 11:00:00+00', 7),
    (6,  4, 'social_service', 2, 'CC',  'I23050412_CC_AnexoXX_Firmada.pdf', 'seed/I23050412_CC.pdf', 'application/pdf', 410000, 'approved', NULL, 3, '2026-02-14 11:10:00+00', 7),
    (7,  4, 'social_service', 2, 'R1',  'I23050412_R1_AnexoXXII_Bim1.pdf', 'seed/I23050412_R1.pdf', 'application/pdf', 480000, 'approved', NULL, 3, '2026-04-14 10:00:00+00', 7),
    (8,  4, 'social_service', 2, 'EV1', 'I23050412_EV1_AnexosXXIII_XXIV.pdf', 'seed/I23050412_EV1.pdf', 'application/pdf', 495000, 'approved', NULL, 3, '2026-04-14 10:05:00+00', 7),
    (9,  4, 'social_service', 2, 'R2',  'I23050412_R2_AnexoXXII_Bim2.pdf', 'seed/I23050412_R2.pdf', 'application/pdf', 502000, 'pending',  NULL, NULL, NULL, 7),
    (10, 4, 'social_service', 2, 'EV2', 'I23050412_EV2_AnexosXXIII_XXIV.pdf', 'seed/I23050412_EV2.pdf', 'application/pdf', 468000, 'pending',  NULL, NULL, NULL, 7),

    -- Documentos de SS #3 (Alumna 6 - 'alumno.liberado'): Expediente 100% aprobado (SOL, CP, CA, CC, R1, R2, R3, EV1, EV2, EV3, CT, COT)
    (11, 6, 'social_service', 3, 'SOL', 'I23050618_SOL_AnexoXVIII.pdf', 'seed/I23050618_SOL.pdf', 'application/pdf', 310000, 'approved', NULL, 3, '2026-02-09 11:00:00+00', 10),
    (12, 6, 'social_service', 3, 'CP',  'I23050618_CP_Presidencia.pdf', 'seed/I23050618_CP.pdf', 'application/pdf', 290000, 'approved', NULL, 3, '2026-02-09 11:05:00+00', 3),
    (13, 6, 'social_service', 3, 'CA',  'I23050618_CA_Presidencia.pdf', 'seed/I23050618_CA.pdf', 'application/pdf', 530000, 'approved', NULL, 3, '2026-02-12 10:00:00+00', 10),
    (14, 6, 'social_service', 3, 'CC',  'I23050618_CC_AnexoXX.pdf', 'seed/I23050618_CC.pdf', 'application/pdf', 390000, 'approved', NULL, 3, '2026-02-12 10:05:00+00', 10),
    (15, 6, 'social_service', 3, 'R1',  'I23050618_R1_Bim1.pdf', 'seed/I23050618_R1.pdf', 'application/pdf', 440000, 'approved', NULL, 3, '2026-04-12 10:00:00+00', 10),
    (16, 6, 'social_service', 3, 'EV1', 'I23050618_EV1_Bim1.pdf', 'seed/I23050618_EV1.pdf', 'application/pdf', 420000, 'approved', NULL, 3, '2026-04-12 10:05:00+00', 10),
    (17, 6, 'social_service', 3, 'R2',  'I23050618_R2_Bim2.pdf', 'seed/I23050618_R2.pdf', 'application/pdf', 450000, 'approved', NULL, 3, '2026-06-12 10:00:00+00', 10),
    (18, 6, 'social_service', 3, 'EV2', 'I23050618_EV2_Bim2.pdf', 'seed/I23050618_EV2.pdf', 'application/pdf', 430000, 'approved', NULL, 3, '2026-06-12 10:05:00+00', 10),
    (19, 6, 'social_service', 3, 'R3',  'I23050618_R3_Bim3.pdf', 'seed/I23050618_R3.pdf', 'application/pdf', 470000, 'approved', NULL, 3, '2026-08-09 16:00:00+00', 10),
    (20, 6, 'social_service', 3, 'EV3', 'I23050618_EV3_Bim3_AnexoXXV.pdf', 'seed/I23050618_EV3.pdf', 'application/pdf', 490000, 'approved', NULL, 3, '2026-08-09 16:05:00+00', 10),
    (21, 6, 'social_service', 3, 'CT',  'I23050618_CT_CartaTerminacion.pdf', 'seed/I23050618_CT.pdf', 'application/pdf', 610000, 'approved', NULL, 3, '2026-08-09 16:10:00+00', 10),
    (22, 6, 'social_service', 3, 'COT', 'I23050618_COT_ConstanciaLiberacion_SSC0001.pdf', 'seed/I23050618_COT.pdf', 'application/pdf', 350000, 'approved', NULL, 3, '2026-08-10 14:30:00+00', 3),

    -- Documentos de SS #4 (Alumno 7): SOL en revisión ('pending')
    (23, 7, 'social_service', 4, 'SOL', 'I23050741_SOL_AnexoXVIII_Firmada.pdf', 'seed/I23050741_SOL.pdf', 'application/pdf', 330000, 'pending', NULL, NULL, NULL, 3),

    -- Documentos de SS #5 (Alumna 8 - lista para liberar): SOL, CP, CA, CC, R1..R3, EV1..EV3, CT aprobados (90% avance, solo falta emitir COT)
    (24, 8, 'social_service', 5, 'SOL', 'I21050890_SOL_AnexoXVIII.pdf', 'seed/I21050890_SOL.pdf', 'application/pdf', 315000, 'approved', NULL, 3, '2026-02-09 12:00:00+00', 3),
    (25, 8, 'social_service', 5, 'CP',  'I21050890_CP_Gunderson.pdf', 'seed/I21050890_CP.pdf', 'application/pdf', 295000, 'approved', NULL, 3, '2026-02-09 12:05:00+00', 3),
    (26, 8, 'social_service', 5, 'CA',  'I21050890_CA_Gunderson.pdf', 'seed/I21050890_CA.pdf', 'application/pdf', 540000, 'approved', NULL, 3, '2026-02-13 10:00:00+00', 3),
    (27, 8, 'social_service', 5, 'CC',  'I21050890_CC_AnexoXX.pdf', 'seed/I21050890_CC.pdf', 'application/pdf', 405000, 'approved', NULL, 3, '2026-02-13 10:05:00+00', 3),
    (28, 8, 'social_service', 5, 'R1',  'I21050890_R1_Bim1.pdf', 'seed/I21050890_R1.pdf', 'application/pdf', 445000, 'approved', NULL, 3, '2026-04-11 10:00:00+00', 3),
    (29, 8, 'social_service', 5, 'EV1', 'I21050890_EV1_Bim1.pdf', 'seed/I21050890_EV1.pdf', 'application/pdf', 425000, 'approved', NULL, 3, '2026-04-11 10:05:00+00', 3),
    (30, 8, 'social_service', 5, 'R2',  'I21050890_R2_Bim2.pdf', 'seed/I21050890_R2.pdf', 'application/pdf', 455000, 'approved', NULL, 3, '2026-06-11 10:00:00+00', 3),
    (31, 8, 'social_service', 5, 'EV2', 'I21050890_EV2_Bim2.pdf', 'seed/I21050890_EV2.pdf', 'application/pdf', 435000, 'approved', NULL, 3, '2026-06-11 10:05:00+00', 3),
    (32, 8, 'social_service', 5, 'R3',  'I21050890_R3_Bim3.pdf', 'seed/I21050890_R3.pdf', 'application/pdf', 475000, 'approved', NULL, 3, '2026-08-09 15:00:00+00', 3),
    (33, 8, 'social_service', 5, 'EV3', 'I21050890_EV3_Bim3.pdf', 'seed/I21050890_EV3.pdf', 'application/pdf', 495000, 'approved', NULL, 3, '2026-08-09 15:05:00+00', 3),
    (34, 8, 'social_service', 5, 'CT',  'I21050890_CT_CartaTerminacion_Gunderson.pdf', 'seed/I21050890_CT.pdf', 'application/pdf', 620000, 'approved', NULL, 3, '2026-08-09 15:10:00+00', 3),

    -- Documentos de PP #1 (Alumno 3 - 'alumno.pp.solicitud'): CP aprobada (20% avance)
    (35, 3, 'internship', 1, 'CP', 'I22050289_CP_EJ26_PR001.pdf', 'seed/I22050289_CP.pdf', 'application/pdf', 275000, 'approved', NULL, 4, '2026-02-18 11:00:00+00', 4),

    -- Documentos de PP #2 (Alumno 5 - 'alumno.pp.activo'): CP y CA aprobadas (40% avance) + EV EN REVISIÓN ('pending') + CT RECHAZADA ('rejected') para mostrar motivo de observación
    (36, 5, 'internship', 2, 'CP', 'I22050501_CP_EJ26_PR002.pdf', 'seed/I22050501_CP.pdf', 'application/pdf', 280000, 'approved', NULL, 4, '2026-02-16 09:00:00+00', 4),
    (37, 5, 'internship', 2, 'CA', 'I22050501_CA_OficioAceptacion_Gunderson.pdf', 'seed/I22050501_CA.pdf', 'application/pdf', 590000, 'approved', NULL, 4, '2026-02-20 13:00:00+00', 9),
    (38, 5, 'internship', 2, 'EV', 'I22050501_EV_AnexoXVII_Gunderson.pdf', 'seed/I22050501_EV.pdf', 'application/pdf', 515000, 'pending', NULL, NULL, NULL, 9),
    (39, 5, 'internship', 2, 'CT', 'I22050501_CT_OficioTerminacion_SinSello.pdf', 'seed/I22050501_CT.pdf', 'application/pdf', 490000, 'rejected', 'El oficio de terminación debe venir en hoja membretada con firma y sello húmedo de la empresa.', 4, '2026-06-21 10:30:00+00', 9),

    -- Documentos de PP #4 (Alumna 10 - 'released'): CP, CA, EV, CT, CC aprobados (100% avance)
    (40, 10, 'internship', 4, 'CP', 'I21050409_CP_EJ26_PR003.pdf', 'seed/I21050409_CP.pdf', 'application/pdf', 282000, 'approved', NULL, 4, '2026-02-09 10:00:00+00', 4),
    (41, 10, 'internship', 4, 'CA', 'I21050409_CA_OficioAceptacion_SIMSA.pdf', 'seed/I21050409_CA.pdf', 'application/pdf', 605000, 'approved', NULL, 4, '2026-02-11 12:00:00+00', 4),
    (42, 10, 'internship', 4, 'EV', 'I21050409_EV_AnexoXVII_SIMSA.pdf', 'seed/I21050409_EV.pdf', 'application/pdf', 530000, 'approved', NULL, 4, '2026-06-16 14:00:00+00', 4),
    (43, 10, 'internship', 4, 'CT', 'I21050409_CT_OficioTerminacion_SIMSA.pdf', 'seed/I21050409_CT.pdf', 'application/pdf', 640000, 'approved', NULL, 4, '2026-06-16 14:10:00+00', 4),
    (44, 10, 'internship', 4, 'CC', 'I21050409_CC_ConstanciaLiberacion_PPC0001.pdf', 'seed/I21050409_CC.pdf', 'application/pdf', 360000, 'approved', NULL, 4, '2026-06-20 15:00:00+00', 4)
ON CONFLICT (id) DO UPDATE SET
    status = EXCLUDED.status,
    rejection_reason = EXCLUDED.rejection_reason;

SELECT setval(pg_get_serial_sequence('expedient_documents', 'id'), COALESCE(MAX(id), 1)) FROM expedient_documents;

-- ============================================================================
-- 9. PRODUCTO NO CONFORME ISO 9001 (non_conformances) Y BITÁCORA DE ESTADOS
-- ============================================================================
INSERT INTO non_conformances (
    id, tramite_type, tramite_id, student_id, period_id,
    incident_code, category, description, status, action_plan,
    resolved_at, resolved_by, created_by
)
OVERRIDING SYSTEM VALUE
VALUES
    (
        1,
        'internship',
        2,
        5,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        'PNC-PP-2026-0001',
        'invalid_documents',
        'Oficio de terminación presentado sin sello oficial de la empresa receptora.',
        'open',
        'Solicitar al practicante recabar sello de Recursos Humanos de Gunderson Gimsa y resubir documento CT.',
        NULL,
        NULL,
        4
    ),
    (
        2,
        'social_service',
        2,
        4,
        (SELECT id FROM academic_periods WHERE code = 'FJ26' AND deleted_at IS NULL LIMIT 1),
        'PNC-SS-2026-0001',
        'delayed_reports',
        'Entrega extemporánea de 1er Reporte Bimestral (3 días hábiles posteriores a la fecha límite).',
        'resolved',
        'Se apercibió al prestador y se validó el reporte con firma de la dependencia.',
        '2026-04-14 10:00:00+00',
        3,
        3
    )
ON CONFLICT (incident_code) WHERE deleted_at IS NULL DO UPDATE SET
    status = EXCLUDED.status,
    action_plan = EXCLUDED.action_plan;

SELECT setval(pg_get_serial_sequence('non_conformances', 'id'), COALESCE(MAX(id), 1)) FROM non_conformances;

INSERT INTO tramite_status_logs (
    id, tramite_type, tramite_id, previous_status, new_status, reason, changed_by
)
OVERRIDING SYSTEM VALUE
VALUES
    (1, 'social_service', 1, NULL, 'requested', 'Apertura de solicitud Anexo XVIII enviada por la alumna.', 6),
    (2, 'social_service', 1, 'requested', 'approved', 'Solicitud aprobada y Carta de Presentación SS_CP0001 emitida.', 3),
    (3, 'social_service', 2, NULL, 'requested', 'Apertura de solicitud Anexo XVIII.', 7),
    (4, 'social_service', 2, 'requested', 'approved', 'Carta de Presentación SS_CP0002 emitida.', 3),
    (5, 'social_service', 2, 'approved', 'accepted', 'Carta de Aceptación validada.', 3),
    (6, 'social_service', 2, 'accepted', 'active', 'Carta Compromiso Anexo XX aprobada; servicio social activo.', 3),
    (7, 'social_service', 3, 'completed_pending_review', 'released', 'Expediente 100% aprobado. Constancia SSC0001 emitida.', 3),
    (8, 'internship', 1, NULL, 'requested', 'Solicitud de Carta de Presentación enviada con vigencia IMSS.', 8),
    (9, 'internship', 1, 'requested', 'approved', 'Carta de Presentación EJ26/PR001 emitida por Coordinación de Prácticas.', 4),
    (10, 'internship', 2, 'approved', 'active', 'Oficio de Aceptación membretado validado.', 4),
    (11, 'internship', 4, 'completed_pending_review', 'released', 'Acreditación de Prácticas Profesionales PPC0001 emitida.', 4)
ON CONFLICT (id) DO NOTHING;

SELECT setval(pg_get_serial_sequence('tramite_status_logs', 'id'), COALESCE(MAX(id), 1)) FROM tramite_status_logs;

-- ============================================================================
-- 10. SINCRONIZAR SECUENCIAS DE FOLIOS (document_series)
-- ============================================================================
UPDATE document_series SET current_sequence = 4 WHERE document_type = 'social_service_presentation_letter' AND current_year = 2026;
UPDATE document_series SET current_sequence = 3 WHERE document_type = 'internship_presentation_letter' AND current_year = 2026;
UPDATE document_series SET current_sequence = 1 WHERE document_type = 'social_service_release_certificate' AND current_year = 2026;
UPDATE document_series SET current_sequence = 1 WHERE document_type = 'internship_release_certificate' AND current_year = 2026;
UPDATE document_series SET current_sequence = 2 WHERE document_type = 'school_services_act' AND current_year = 2026;


-- Asignar tipos de actividad a trámites demo (dejando PP #1 y PP #3 en NULL por estar en solicitud/aprobado sin CA)
UPDATE social_services SET activity_type_id = (SELECT id FROM activity_types WHERE code = 'ACT-DOC' AND deleted_at IS NULL LIMIT 1) WHERE id = 1;
UPDATE social_services SET activity_type_id = (SELECT id FROM activity_types WHERE code = 'ACT-COM' AND deleted_at IS NULL LIMIT 1) WHERE id = 2;
UPDATE social_services SET activity_type_id = (SELECT id FROM activity_types WHERE code = 'ACT-ENE' AND deleted_at IS NULL LIMIT 1) WHERE id = 3;
UPDATE social_services SET activity_type_id = (SELECT id FROM activity_types WHERE code = 'ACT-ENE' AND deleted_at IS NULL LIMIT 1) WHERE id = 4;
UPDATE social_services SET activity_type_id = (SELECT id FROM activity_types WHERE code = 'ACT-CAL' AND deleted_at IS NULL LIMIT 1) WHERE id = 5;
UPDATE internships SET activity_type_id = (SELECT id FROM activity_types WHERE code = 'ACT-ING' AND deleted_at IS NULL LIMIT 1) WHERE id = 2;
UPDATE internships SET activity_type_id = (SELECT id FROM activity_types WHERE code = 'ACT-PRO' AND deleted_at IS NULL LIMIT 1) WHERE id = 4;

COMMIT;