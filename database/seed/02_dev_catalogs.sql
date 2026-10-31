BEGIN;

-- ============================================================================
-- 1. CARRERAS OFICIALES (ITSM - Monclova)
-- Extraídas de SEGUIMIENTO VINCULACIÓN1.xlsx (Hoja SEMESTRES - CARRERAS)
-- ============================================================================
INSERT INTO careers (code, name, total_credits, is_active)
VALUES
    ('INF', 'Ingeniería Informática', 260, true),
    ('IND', 'Ingeniería Industrial', 260, true),
    ('MEC', 'Ingeniería Electromecánica', 260, true),
    ('ELE', 'Ingeniería Electrónica', 260, true),
    ('GE',  'Ingeniería en Gestión Empresarial', 260, true),
    ('ER',  'Ingeniería en Energías Renovables', 260, true)
ON CONFLICT (code) WHERE deleted_at IS NULL DO UPDATE SET
    name = EXCLUDED.name,
    total_credits = EXCLUDED.total_credits,
    is_active = EXCLUDED.is_active;

-- ============================================================================
-- 2. PERIODOS ACADÉMICOS
-- ============================================================================
INSERT INTO academic_periods (code, name, start_date, end_date, is_active)
VALUES
    ('AD25', 'Agosto - Diciembre 2025', '2025-08-18', '2025-12-19', false),
    ('FJ26', 'Febrero - Julio 2026',    '2026-02-09', '2026-07-03', true),
    ('AD26', 'Agosto - Diciembre 2026', '2026-08-17', '2026-12-18', false)
ON CONFLICT (code) WHERE deleted_at IS NULL DO UPDATE SET
    name = EXCLUDED.name,
    start_date = EXCLUDED.start_date,
    end_date = EXCLUDED.end_date,
    is_active = EXCLUDED.is_active;

-- ============================================================================
-- 3. PROGRAMAS OFICIALES DE SERVICIO SOCIAL (9 Categorías Normativas TecNM)
-- Extraídas de SEGUIMIENTO VINCULACIÓN1.xlsx (Hoja PROGRAMAS SERVICIO SOCIAL)
-- ============================================================================
INSERT INTO social_service_programs (code, name, category, is_internal, description, is_active)
VALUES
    (
        'SS-DES-COM',
        'Desarrollo Comunitario',
        'community_development',
        false,
        'Programas dirigidos al apoyo, diagnóstico y mejora en comunidades y zonas vulnerables.',
        true
    ),
    (
        'SS-EDU-ADU',
        'Educación para Adultos',
        'adult_education',
        false,
        'Alfabetización y asesoría en educación básica y media superior para personas adultas (ej. INEA).',
        true
    ),
    (
        'SS-PRO-CON',
        'Programas de Contingencia',
        'contingency_programs',
        false,
        'Acciones de prevención, auxilio y mitigación ante desastres naturales o emergencias sanitarias.',
        true
    ),
    (
        'SS-MED-AMB',
        'Cuidado al Medio Ambiente y Desarrollo Sustentable',
        'environmental_care',
        true,
        'Proyectos ecológicos, reciclaje, reforestación y uso eficiente de la energía.',
        true
    ),
    (
        'SS-APO-SAL',
        'Apoyo a la Salud',
        'health_support',
        false,
        'Brigadas asistenciales de salud, campañas de vacunación y difusión preventiva.',
        true
    ),
    (
        'SS-DEP-CIV',
        'Actividades Deportivas, Cívicas y Culturales',
        'civic_sports_cultural',
        true,
        'Fomento del deporte, instrucción cívica y difusión artística institucional y regional.',
        true
    ),
    (
        'SS-GOB-ESP',
        'Operación de Programas Especiales Gubernamentales',
        'special_government_programs',
        false,
        'Colaboración con dependencias de los tres órdenes de gobierno en censos o trámites públicos.',
        true
    ),
    (
        'SS-PRO-FES',
        'Servicio Social Profesionalizante',
        'professionalizing_service',
        false,
        'Aplicación directa del perfil de egreso en el desarrollo tecnológico de organizaciones sociales.',
        true
    ),
    (
        'SS-INS-TEC',
        'Programas Institucionales TecNM',
        'institutional_programs',
        true,
        'Apoyo a áreas académicas, laboratorios, centros de cómputo y tutorías internas del Instituto.',
        true
    )
ON CONFLICT (code) WHERE deleted_at IS NULL DO UPDATE SET
    name = EXCLUDED.name,
    category = EXCLUDED.category,
    is_internal = EXCLUDED.is_internal,
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active;

-- ============================================================================
-- 4. AUTORIDADES INSTITUCIONALES (Firmantes oficiales del Instituto)
-- ============================================================================
INSERT INTO institutional_authorities (role_key, title, full_name, position, email, phone_number, signature_initials, is_active)
VALUES
    (
        'institute_director',
        'Mtro.',
        'Juan Darío Hernández Guerrero',
        'Director del Instituto Tecnológico Superior de Monclova',
        'direccion@monclova.tecnm.mx',
        '8666490370',
        'JDHG',
        true
    ),
    (
        'outreach_division_head',
        'M.I.',
        'Rocío Mendoza Riojas',
        'Jefa de División de Gestión Tecnológica y Vinculación',
        'vinculacion@monclova.tecnm.mx',
        '8666490372',
        'RMR',
        true
    ),
    (
        'social_service_head',
        'M.A.F.H.',
        'Miguel Alejandro Sanson Olguin',
        'Encargado de la Oficina de Servicio Social',
        'vinculacion@monclova.tecnm.mx',
        '8666490375',
        'MSO',
        true
    ),
    (
        'internship_coordinator',
        'Ing.',
        'Arturo Martínez',
        'Coordinador de Prácticas Profesionales',
        'arturo.martinez@monclova.tecnm.mx',
        '8666490372',
        'AM',
        true
    )
ON CONFLICT (id) DO NOTHING;

-- Sincronizar secuencias
SELECT setval(pg_get_serial_sequence('careers', 'id'), COALESCE(MAX(id), 1)) FROM careers;
SELECT setval(pg_get_serial_sequence('academic_periods', 'id'), COALESCE(MAX(id), 1)) FROM academic_periods;
SELECT setval(pg_get_serial_sequence('social_service_programs', 'id'), COALESCE(MAX(id), 1)) FROM social_service_programs;
SELECT setval(pg_get_serial_sequence('institutional_authorities', 'id'), COALESCE(MAX(id), 1)) FROM institutional_authorities;

-- ============================================================================
-- 5. SERIES DE FOLIO INSTITUCIONALES (Año 2026)
-- Formato patrón: {PREFIX}{SEQ:4}/{DD}/{MM}/{YYYY} o {PREFIX}{SEQ:3}
-- ============================================================================
INSERT INTO document_series (document_type, prefix, current_year, current_sequence, format_pattern, description)
VALUES
    (
        'social_service_presentation_letter',
        'SS_CP',
        2026,
        0,
        '{PREFIX}{SEQ:4}/{DD}/{MM}/{YYYY}',
        'Cartas de Presentación de Servicio Social'
    ),
    (
        'internship_presentation_letter',
        'PR',
        2026,
        0,
        'EJ26/{PREFIX}{SEQ:3}',
        'Cartas de Presentación de Prácticas Profesionales'
    ),
    (
        'social_service_release_certificate',
        'SSC',
        2026,
        0,
        '{PREFIX}{SEQ:4}/{DD}/{MM}/{YYYY}',
        'Constancias de Terminación de Servicio Social'
    ),
    (
        'internship_release_certificate',
        'PPC',
        2026,
        0,
        '{PREFIX}{SEQ:4}/{DD}/{MM}/{YYYY}',
        'Constancias de Acreditación de Prácticas Profesionales'
    ),
    (
        'school_services_act',
        'DGTV',
        2026,
        0,
        '{PREFIX}{SEQ:3}/{DD}-{MM}-{YYYY}',
        'Actas turnadas a Servicios Escolares'
    )
ON CONFLICT (document_type, current_year) WHERE deleted_at IS NULL DO UPDATE SET
    prefix = EXCLUDED.prefix,
    format_pattern = EXCLUDED.format_pattern,
    description = EXCLUDED.description;

SELECT setval(pg_get_serial_sequence('document_series', 'id'), COALESCE(MAX(id), 1)) FROM document_series;

COMMIT;
