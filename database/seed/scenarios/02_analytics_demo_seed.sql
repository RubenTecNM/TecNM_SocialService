-- ============================================================================
-- Seed Reproducible para Analítica de Servicio Social y Prácticas Profesionales
-- Archivo: database/seed/scenarios/02_analytics_demo_seed.sql
-- SOLO ENTORNO DE DESARROLLO.
-- Identificadores de limpieza:
--   - Alumnos: control_number LIKE 'SQA24%' / personal_email LIKE '%@seed-analytics.local'
--   - Empresas y supervisores: email LIKE '%@seed-analytics.local'
--   - Trámites SS/PP: observations LIKE '[SEED_ANALYTICS]%'
--   - Periodos históricos extra: code IN ('AD23','FJ24','AD24')
-- Para revertir sin afectar datos previos: ejecutar 02_analytics_demo_cleanup.sql
-- ============================================================================

BEGIN;

-- 1. Periodos históricos adicionales para completar 4 ciclos escolares (2023-2024 .. 2026-2027)
INSERT INTO academic_periods (code, name, school_year, start_date, end_date, is_active)
VALUES
    ('AD23', 'Agosto - Diciembre 2023', '2023-2024', '2023-08-21', '2023-12-15', false),
    ('FJ24', 'Febrero - Julio 2024',    '2023-2024', '2024-02-05', '2024-06-28', false),
    ('AD24', 'Agosto - Diciembre 2024', '2024-2025', '2024-08-19', '2024-12-20', false)
ON CONFLICT (code) WHERE deleted_at IS NULL DO NOTHING;

-- 2. Empresas, instituciones y dependencias receptoras adicionales (12 variadas en sector y tamaño)
INSERT INTO companies (name, commercial_name, company_type, sector, company_size, is_tecnm_internal, city, state, email, is_active)
SELECT v.name, v.commercial_name, v.company_type, v.sector, v.company_size, false, 'Monclova', 'Coahuila', v.email, true
FROM (VALUES
    ('Trinity Rail de México S.A. de C.V.',          'Trinity Rail',            'large',  'private', 'large',          'rh.trinity@seed-analytics.local'),
    ('Aptiv Componentes Coahuila S. de R.L.',        'Aptiv Monclova',          'large',  'private', 'large',          'rh.aptiv@seed-analytics.local'),
    ('Acerero Industrial del Norte S.A. de C.V.',    'Acerero del Norte',       'large',  'private', 'large',          'rh.acerero@seed-analytics.local'),
    ('DIF Municipal Monclova',                       'DIF Monclova',            'public', 'public',  'not_applicable', 'vinculacion.dif@seed-analytics.local'),
    ('Hospital General Amparo Pape de Benavides',    'Hospital Amparo Pape',    'public', 'public',  'not_applicable', 'ensenanza.hospital@seed-analytics.local'),
    ('Secretaría de Educación de Coahuila (Región Centro)', 'SEDU Coahuila',    'public', 'public',  'not_applicable', 'enlace.sedu@seed-analytics.local'),
    ('Comisión Federal de Electricidad Zona Monclova', 'CFE Monclova',          'public', 'public',  'not_applicable', 'rh.cfe@seed-analytics.local'),
    ('Cruz Roja Mexicana Delegación Monclova',       'Cruz Roja Monclova',      'social', 'social',  'not_applicable', 'coord.cruzroja@seed-analytics.local'),
    ('Asociación Ecológica Río Monclova A.C.',       'Ecológica Río Monclova',  'social', 'social',  'not_applicable', 'contacto.ecologia@seed-analytics.local'),
    ('Automatización y Control de Coahuila S.A.',    'AutoControl Coahuila',    'medium', 'private', 'medium',         'rh.autocontrol@seed-analytics.local'),
    ('Consultoría Empresarial y Sistemas del Centro','CES Centro',              'small',  'private', 'small',          'rh.ces@seed-analytics.local'),
    ('Taller Mecatrónico Frontera',                  'Mecatrónica Frontera',    'micro',  'private', 'micro',          'contacto.mecatronica@seed-analytics.local')
) AS v(name, commercial_name, company_type, sector, company_size, email)
WHERE NOT EXISTS (
    SELECT 1 FROM companies c WHERE c.email = v.email AND c.deleted_at IS NULL
);

-- Supervisores para cada empresa seed
INSERT INTO company_supervisors (company_id, full_name, position, department, email, is_signatory, is_active)
SELECT c.id,
       'Ing. Responsable ' || COALESCE(c.commercial_name, c.name),
       'Coordinador de Vinculación',
       'Recursos Humanos',
       'sup.' || c.id || '@seed-analytics.local',
       true,
       true
FROM companies c
WHERE c.email LIKE '%@seed-analytics.local'
  AND c.deleted_at IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM company_supervisors cs WHERE cs.company_id = c.id AND cs.deleted_at IS NULL
  );

-- 3. 180 estudiantes sintéticos con distribución NO uniforme (carreras, sexos, semestres, turnos y discapacidad)
DO $$
DECLARE
    i INT;
    v_career_code TEXT;
    v_career_id BIGINT;
    v_gender CHAR(1);
    v_sem INT;
    v_shift TEXT;
    v_dis BOOLEAN;
    v_ctrl TEXT;
    v_first_names_m TEXT[] := ARRAY['Carlos','Alejandro','Diego','Fernando','Ricardo','Eduardo','Jorge','Miguel','Luis','Andrés','Jesús','Humberto','Óscar','Raúl','Iván'];
    v_first_names_f TEXT[] := ARRAY['Mariana','Valeria','Daniela','Sofía','Fernanda','Camila','Paola','Andrea','Karla','Gabriela','Regina','Natalia','Ximena','Lucía','Alejandra'];
    v_surnames TEXT[] := ARRAY['Hernández','García','Martínez','López','González','Rodríguez','Pérez','Sánchez','Ramírez','Flores','Villarreal','Garza','Salazar','Castillo','Ramos','Valdés','Zapata','Morales','Torres','Cruz'];
    v_fname TEXT;
    v_sn1 TEXT;
    v_sn2 TEXT;
    v_bucket INT;
BEGIN
    FOR i IN 1..180 LOOP
        v_ctrl := 'SQA24' || LPAD(i::text, 3, '0');
        IF EXISTS (SELECT 1 FROM students WHERE control_number = v_ctrl AND deleted_at IS NULL) THEN
            CONTINUE;
        END IF;

        -- Distribución realista sesgada por carrera: IND 28%, INF 24%, GE 19%, MEC 14%, ELE 9%, ER 6%
        v_bucket := ((i * 37) % 100);
        IF v_bucket < 28 THEN v_career_code := 'IND';
        ELSIF v_bucket < 52 THEN v_career_code := 'INF';
        ELSIF v_bucket < 71 THEN v_career_code := 'GE';
        ELSIF v_bucket < 85 THEN v_career_code := 'MEC';
        ELSIF v_bucket < 94 THEN v_career_code := 'ELE';
        ELSE v_career_code := 'ER';
        END IF;

        SELECT id INTO v_career_id FROM careers WHERE code = v_career_code AND deleted_at IS NULL LIMIT 1;

        -- Sexo sesgado según perfil de carrera + ~4% NULL (No especificado)
        IF i % 25 = 0 THEN
            v_gender := NULL;
        ELSIF v_career_code = 'GE' THEN
            v_gender := CASE WHEN (i % 10) < 7 THEN 'F' ELSE 'M' END;
        ELSIF v_career_code = 'IND' THEN
            v_gender := CASE WHEN (i % 10) < 5 THEN 'F' ELSE 'M' END;
        ELSIF v_career_code IN ('MEC', 'ELE') THEN
            v_gender := CASE WHEN (i % 10) < 2 THEN 'F' ELSE 'M' END;
        ELSE
            v_gender := CASE WHEN (i % 10) < 3 THEN 'F' ELSE 'M' END;
        END IF;

        -- Semestre concentrado en 6..10 con pico en 7° y 8° y cola de rezago en 11°-12°
        v_bucket := ((i * 19) % 100);
        IF v_bucket < 16 THEN v_sem := 6;
        ELSIF v_bucket < 46 THEN v_sem := 7;
        ELSIF v_bucket < 76 THEN v_sem := 8;
        ELSIF v_bucket < 90 THEN v_sem := 9;
        ELSIF v_bucket < 96 THEN v_sem := 10;
        ELSIF v_bucket < 99 THEN v_sem := 11;
        ELSE v_sem := 12;
        END IF;

        v_shift := CASE
            WHEN (i % 10) < 7 THEN 'morning'
            WHEN (i % 10) < 9 THEN 'afternoon'
            ELSE 'night'
        END;

        v_dis := (i % 22 = 0) OR (i = 25); -- incluye 1 alumno con discapacidad y gender = NULL

        v_fname := CASE WHEN v_gender = 'F'
            THEN v_first_names_f[1 + (i % array_length(v_first_names_f, 1))]
            ELSE v_first_names_m[1 + (i % array_length(v_first_names_m, 1))]
        END;
        v_sn1 := v_surnames[1 + ((i * 3) % array_length(v_surnames, 1))];
        v_sn2 := v_surnames[1 + ((i * 7) % array_length(v_surnames, 1))];

        INSERT INTO students (
            career_id, control_number, first_surname, second_surname, first_name,
            gender, shift, has_disability, disability_description,
            institutional_email, personal_email, phone_number,
            current_semester, completed_credits, curriculum_progress_percentage,
            is_enrolled, has_valid_imss
        ) VALUES (
            v_career_id, v_ctrl, v_sn1, v_sn2, v_fname,
            v_gender, v_shift, v_dis, CASE WHEN v_dis THEN 'Condición motriz/sensorial registrada' ELSE NULL END,
            LOWER(v_ctrl) || '@monclova.tecnm.mx', LOWER(v_ctrl) || '@seed-analytics.local', '866' || LPAD((1000000 + i)::text, 7, '0'),
            v_sem, LEAST(260, v_sem * 28), LEAST(100.00, ROUND((v_sem * 28.0 / 260.0) * 100.0, 2)),
            true, true
        );
    END LOOP;
END $$;

-- 4. Trámites de Servicio Social (136 trámites principales + 6 reinicios en mismo ciclo 2025-2026 = 142)
--    Incluye duraciones de 6 meses que cruzan periodos escolares y 6 casos de deduplicación en ciclo 2025-2026.
DO $$
DECLARE
    rec RECORD;
    idx INT := 0;
    v_period_code TEXT;
    v_period_id BIGINT;
    v_p_start DATE;
    v_start DATE;
    v_end DATE;
    v_status TEXT;
    v_comp_id BIGINT;
    v_sup_id BIGINT;
    v_modality TEXT;
    v_prog_id BIGINT;
    v_act_id BIGINT;
    v_ss_id BIGINT;
    v_cp_folio TEXT;
    v_rel_folio TEXT;
    v_b INT;
    v_ss_companies BIGINT[];
    v_ss_activities BIGINT[];
    v_prog_ids BIGINT[];
BEGIN
    -- Empresas ponderadas para SS (las primeras 4 con mayor peso)
    SELECT ARRAY(
        SELECT id FROM companies
        WHERE deleted_at IS NULL AND (id IN (1, 2) OR email IN (
            'vinculacion.dif@seed-analytics.local',
            'ensenanza.hospital@seed-analytics.local',
            'enlace.sedu@seed-analytics.local',
            'rh.cfe@seed-analytics.local',
            'coord.cruzroja@seed-analytics.local',
            'contacto.ecologia@seed-analytics.local',
            'rh.trinity@seed-analytics.local'
        ))
        ORDER BY CASE
            WHEN id = 1 THEN 1
            WHEN id = 2 THEN 2
            WHEN email = 'vinculacion.dif@seed-analytics.local' THEN 3
            WHEN email = 'ensenanza.hospital@seed-analytics.local' THEN 4
            WHEN email = 'enlace.sedu@seed-analytics.local' THEN 5
            ELSE 6
        END, id
    ) INTO v_ss_companies;

    SELECT ARRAY(
        SELECT id FROM activity_types
        WHERE deleted_at IS NULL AND applies_to IN ('social_service', 'both')
        ORDER BY CASE code
            WHEN 'ACT-TI'  THEN 1
            WHEN 'ACT-ADM' THEN 2
            WHEN 'ACT-DOC' THEN 3
            WHEN 'ACT-COM' THEN 4
            WHEN 'ACT-CAL' THEN 5
            WHEN 'ACT-MTO' THEN 6
            WHEN 'ACT-ENE' THEN 7
            WHEN 'ACT-ING' THEN 8
            ELSE 9
        END
    ) INTO v_ss_activities;

    SELECT ARRAY(SELECT id FROM social_service_programs WHERE deleted_at IS NULL ORDER BY id) INTO v_prog_ids;

    FOR rec IN
        SELECT s.id, s.control_number, s.current_semester, c.code AS career_code
        FROM students s
        JOIN careers c ON c.id = s.career_id
        WHERE s.control_number LIKE 'SQA24%' AND s.deleted_at IS NULL
        ORDER BY s.id
        LIMIT 136
    LOOP
        idx := idx + 1;
        IF EXISTS (SELECT 1 FROM social_services WHERE student_id = rec.id AND observations LIKE '[SEED_ANALYTICS]%' AND deleted_at IS NULL) THEN
            CONTINUE;
        END IF;

        -- Distribución con tendencia de crecimiento a lo largo de 4 ciclos escolares:
        -- 2023-2024 (AD23: 11, FJ24: 13) -> 2024-2025 (AD24: 17, FJ25: 19) -> 2025-2026 (AD25: 22, FJ26: 24) -> 2026-2027 (AD26: 30)
        IF idx <= 11 THEN v_period_code := 'AD23';
        ELSIF idx <= 24 THEN v_period_code := 'FJ24';
        ELSIF idx <= 41 THEN v_period_code := 'AD24';
        ELSIF idx <= 60 THEN v_period_code := 'FJ25';
        ELSIF idx <= 82 THEN v_period_code := 'AD25';
        ELSIF idx <= 106 THEN v_period_code := 'FJ26';
        ELSE v_period_code := 'AD26';
        END IF;

        SELECT id, start_date INTO v_period_id, v_p_start FROM academic_periods WHERE code = v_period_code AND deleted_at IS NULL LIMIT 1;
        -- Servicio Social de 6 meses (abarca naturalmente hacia el siguiente periodo escolar)
        v_start := v_p_start + ((idx % 12) * INTERVAL '1 day');
        v_end := v_start + INTERVAL '6 months';

        -- Estatus realista según antigüedad del periodo
        v_b := idx % 10;
        IF v_period_code IN ('AD23', 'FJ24', 'AD24', 'FJ25') THEN
            v_status := CASE WHEN v_b < 8 THEN 'released' WHEN v_b = 8 THEN 'non_compliant' ELSE 'cancelled' END;
        ELSIF v_period_code IN ('AD25', 'FJ26') THEN
            v_status := CASE
                WHEN v_b < 5 THEN 'released'
                WHEN v_b < 7 THEN 'active'
                WHEN v_b = 7 THEN 'completed_pending_review'
                WHEN v_b = 8 THEN 'non_compliant'
                ELSE 'cancelled'
            END;
        ELSE -- AD26 (periodo activo)
            v_status := CASE
                WHEN v_b < 4 THEN 'active'
                WHEN v_b < 6 THEN 'approved'
                WHEN v_b = 6 THEN 'accepted'
                WHEN v_b = 7 THEN 'completed_pending_review'
                WHEN v_b = 8 THEN 'requested'
                ELSE 'released'
            END;
        END IF;

        -- Selección ponderada de empresa receptora (top 3 absorben ~55%)
        v_b := (idx * 13) % 100;
        IF v_b < 24 THEN v_comp_id := v_ss_companies[1];
        ELSIF v_b < 44 THEN v_comp_id := v_ss_companies[2];
        ELSIF v_b < 58 THEN v_comp_id := v_ss_companies[LEAST(3, array_length(v_ss_companies, 1))];
        ELSIF v_b < 70 THEN v_comp_id := v_ss_companies[LEAST(4, array_length(v_ss_companies, 1))];
        ELSE v_comp_id := v_ss_companies[1 + (idx % array_length(v_ss_companies, 1))];
        END IF;

        SELECT id INTO v_sup_id FROM company_supervisors WHERE company_id = v_comp_id AND deleted_at IS NULL ORDER BY id LIMIT 1;
        v_modality := CASE WHEN v_comp_id = 1 THEN 'internal' ELSE 'external' END;
        v_prog_id := v_prog_ids[1 + (idx % array_length(v_prog_ids, 1))];

        -- Tipo de actividad sesgado por carrera + 5 históricos NULL ("Sin clasificar")
        IF idx IN (3, 15, 29, 44, 65) THEN
            v_act_id := NULL;
        ELSIF rec.career_code = 'INF' AND (idx % 3 <> 0) THEN
            v_act_id := v_ss_activities[1]; -- ACT-TI
        ELSIF rec.career_code = 'GE' AND (idx % 3 <> 0) THEN
            v_act_id := v_ss_activities[2]; -- ACT-ADM
        ELSIF v_comp_id = 1 THEN
            v_act_id := v_ss_activities[3]; -- ACT-DOC
        ELSIF (idx % 10) < 3 THEN
            v_act_id := v_ss_activities[4]; -- ACT-COM
        ELSE
            v_act_id := v_ss_activities[1 + ((idx * 5) % array_length(v_ss_activities, 1))];
        END IF;

        v_cp_folio := CASE WHEN v_status <> 'requested' THEN 'SS_CP_QA' || LPAD(idx::text, 4, '0') ELSE NULL END;
        v_rel_folio := CASE WHEN v_status = 'released' THEN 'SSC_QA' || LPAD(idx::text, 4, '0') ELSE NULL END;

        INSERT INTO social_services (
            student_id, period_id, program_id, activity_type_id, company_id, supervisor_id,
            semester_enrolled, modality, start_date, end_date, required_hours, accumulated_hours,
            status, presentation_letter_folio, presentation_letter_date, acceptance_letter_date,
            termination_letter_date, original_termination_letter_received, release_certificate_folio,
            released_at, final_grade_level, beneficiaries_count, has_inclusion_activities,
            target_community, observations
        ) VALUES (
            rec.id, v_period_id, v_prog_id, v_act_id, v_comp_id, v_sup_id,
            rec.current_semester, v_modality, v_start, v_end, 480,
            CASE WHEN v_status IN ('released','completed_pending_review') THEN 480 WHEN v_status = 'active' THEN 240 ELSE 0 END,
            v_status, v_cp_folio,
            CASE WHEN v_status <> 'requested' THEN v_start - 3 ELSE NULL END,
            CASE WHEN v_status IN ('accepted','active','completed_pending_review','released','non_compliant') THEN v_start + 2 ELSE NULL END,
            CASE WHEN v_status IN ('completed_pending_review','released') THEN v_end ELSE NULL END,
            v_status = 'released',
            v_rel_folio,
            CASE WHEN v_status = 'released' THEN (v_end + 5)::timestamptz ELSE NULL END,
            CASE WHEN v_status = 'released' THEN 'excellent' ELSE NULL END,
            CASE WHEN v_status = 'released' THEN 40 + ((idx * 17) % 220) ELSE 0 END,
            (idx % 3 = 0),
            'Región Centro de Coahuila',
            '[SEED_ANALYTICS] Trámite SS sintético (' || v_period_code || ')'
        ) RETURNING id INTO v_ss_id;

        INSERT INTO social_service_applications (
            social_service_id, student_address, student_phone, period_type, current_semester,
            dependency_name, dependency_head_name, dependency_head_position, activities_outline,
            is_accepted_by_office
        ) VALUES (
            v_ss_id, 'Domicilio Demo Monclova', '8661000000',
            CASE WHEN EXTRACT(MONTH FROM v_p_start) >= 8 THEN 'august_january' ELSE 'february_july' END,
            rec.current_semester, 'Dependencia Receptora', 'Responsable', 'Titular',
            'Actividades de servicio social generadas para pruebas analíticas.',
            CASE WHEN v_status <> 'requested' THEN true ELSE NULL END
        );

        -- Documentos aprobados mínimos para consistencia de Estadístico Oficial y 911.10
        IF v_status <> 'requested' THEN
            INSERT INTO expedient_documents (student_id, tramite_type, tramite_id, document_code, original_file_name, storage_path, file_size_bytes, status)
            VALUES (rec.id, 'social_service', v_ss_id, 'CP', 'seed_cp.pdf', 'seed-analytics/cp.pdf', 1024, 'approved')
            ON CONFLICT DO NOTHING;
        END IF;
        IF v_status IN ('accepted','active','completed_pending_review','released','non_compliant') THEN
            INSERT INTO expedient_documents (student_id, tramite_type, tramite_id, document_code, original_file_name, storage_path, file_size_bytes, status)
            VALUES (rec.id, 'social_service', v_ss_id, 'CA', 'seed_ca.pdf', 'seed-analytics/ca.pdf', 1024, 'approved')
            ON CONFLICT DO NOTHING;
        END IF;
        IF v_status = 'released' THEN
            INSERT INTO expedient_documents (student_id, tramite_type, tramite_id, document_code, original_file_name, storage_path, file_size_bytes, status)
            VALUES (rec.id, 'social_service', v_ss_id, 'CT', 'seed_ct.pdf', 'seed-analytics/ct.pdf', 1024, 'approved')
            ON CONFLICT DO NOTHING;
        END IF;
    END LOOP;

    -- 4B. Escenario explícito de DEDUPLICACIÓN en Ciclo Escolar 2025-2026:
    --     6 alumnos con trámite 'cancelled' en AD25 que reiniciaron un trámite 'released'/'active' en FJ26
    --     (totalTramites > uniqueStudents en ciclo 2025-2026, probando cero doble conteo)
    FOR rec IN
        SELECT s.id, s.current_semester, ss.program_id, ss.activity_type_id, ss.company_id, ss.supervisor_id, ss.modality
        FROM social_services ss
        JOIN students s ON s.id = ss.student_id
        JOIN academic_periods ap ON ap.id = ss.period_id
        WHERE ss.observations LIKE '[SEED_ANALYTICS]%'
          AND ap.code = 'FJ26'
          AND ss.status IN ('released', 'active')
          AND ss.deleted_at IS NULL
        ORDER BY ss.id
        LIMIT 6
    LOOP
        SELECT id, start_date INTO v_period_id, v_p_start FROM academic_periods WHERE code = 'AD25' AND deleted_at IS NULL LIMIT 1;
        IF NOT EXISTS (
            SELECT 1 FROM social_services
            WHERE student_id = rec.id AND period_id = v_period_id AND status = 'cancelled' AND deleted_at IS NULL
        ) THEN
            INSERT INTO social_services (
                student_id, period_id, program_id, activity_type_id, company_id, supervisor_id,
                semester_enrolled, modality, start_date, end_date, required_hours, accumulated_hours,
                status, observations, cancellation_reason
            ) VALUES (
                rec.id, v_period_id, rec.program_id, rec.activity_type_id, rec.company_id, rec.supervisor_id,
                GREATEST(6, rec.current_semester - 1), rec.modality, v_p_start, v_p_start + INTERVAL '6 months', 480, 0,
                'cancelled', '[SEED_ANALYTICS] Intento previo cancelado en AD25 (prueba deduplicación ciclo 2025-2026)',
                'Cambio de dependencia receptora'
            );
        END IF;
    END LOOP;
END $$;

-- 5. Trámites de Prácticas Profesionales (110 trámites principales + 4 reinicios en ciclo 2025-2026 = 114)
--    Incluye solapamiento de alumnos que hicieron SS y también hacen PP (~66 alumnos) y ~12 PP sin empresa (pendiente CA).
DO $$
DECLARE
    rec RECORD;
    idx INT := 0;
    v_period_code TEXT;
    v_period_id BIGINT;
    v_p_start DATE;
    v_start DATE;
    v_end DATE;
    v_status TEXT;
    v_comp_id BIGINT;
    v_sup_id BIGINT;
    v_act_id BIGINT;
    v_pp_id BIGINT;
    v_b INT;
    v_pp_companies BIGINT[];
    v_pp_activities BIGINT[];
BEGIN
    SELECT ARRAY(
        SELECT id FROM companies
        WHERE deleted_at IS NULL AND (id IN (3, 4) OR email IN (
            'rh.trinity@seed-analytics.local',
            'rh.aptiv@seed-analytics.local',
            'rh.acerero@seed-analytics.local',
            'rh.autocontrol@seed-analytics.local',
            'rh.ces@seed-analytics.local',
            'contacto.mecatronica@seed-analytics.local',
            'rh.cfe@seed-analytics.local'
        ))
        ORDER BY CASE
            WHEN id = 3 THEN 1
            WHEN email = 'rh.trinity@seed-analytics.local' THEN 2
            WHEN email = 'rh.aptiv@seed-analytics.local' THEN 3
            WHEN id = 4 THEN 4
            WHEN email = 'rh.acerero@seed-analytics.local' THEN 5
            ELSE 6
        END, id
    ) INTO v_pp_companies;

    SELECT ARRAY(
        SELECT id FROM activity_types
        WHERE deleted_at IS NULL AND applies_to IN ('internship', 'both')
        ORDER BY CASE code
            WHEN 'ACT-PRO' THEN 1
            WHEN 'ACT-CAL' THEN 2
            WHEN 'ACT-MTO' THEN 3
            WHEN 'ACT-ING' THEN 4
            WHEN 'ACT-TI'  THEN 5
            WHEN 'ACT-ADM' THEN 6
            WHEN 'ACT-ENE' THEN 7
            ELSE 8
        END
    ) INTO v_pp_activities;

    FOR rec IN
        SELECT s.id, s.control_number, s.current_semester, c.code AS career_code
        FROM students s
        JOIN careers c ON c.id = s.career_id
        WHERE s.control_number LIKE 'SQA24%' AND s.deleted_at IS NULL
        ORDER BY s.id DESC
        LIMIT 110
    LOOP
        idx := idx + 1;
        IF EXISTS (SELECT 1 FROM internships WHERE student_id = rec.id AND observations LIKE '[SEED_ANALYTICS]%' AND deleted_at IS NULL) THEN
            CONTINUE;
        END IF;

        -- Crecimiento histórico a lo largo de 4 ciclos escolares
        IF idx <= 8 THEN v_period_code := 'AD23';
        ELSIF idx <= 18 THEN v_period_code := 'FJ24';
        ELSIF idx <= 32 THEN v_period_code := 'AD24';
        ELSIF idx <= 48 THEN v_period_code := 'FJ25';
        ELSIF idx <= 66 THEN v_period_code := 'AD25';
        ELSIF idx <= 86 THEN v_period_code := 'FJ26';
        ELSE v_period_code := 'AD26';
        END IF;

        SELECT id, start_date INTO v_period_id, v_p_start FROM academic_periods WHERE code = v_period_code AND deleted_at IS NULL LIMIT 1;
        v_start := v_p_start + ((idx % 10) * INTERVAL '1 day');
        v_end := v_start + INTERVAL '4 months';

        v_b := idx % 10;
        IF v_period_code IN ('AD23', 'FJ24', 'AD24', 'FJ25') THEN
            v_status := CASE WHEN v_b < 8 THEN 'released' WHEN v_b = 8 THEN 'non_compliant' ELSE 'cancelled' END;
        ELSIF v_period_code IN ('AD25', 'FJ26') THEN
            v_status := CASE
                WHEN v_b < 5 THEN 'released'
                WHEN v_b < 8 THEN 'active'
                WHEN v_b = 8 THEN 'non_compliant'
                ELSE 'cancelled'
            END;
        ELSE -- AD26 (activo)
            v_status := CASE
                WHEN v_b < 4 THEN 'active'
                WHEN v_b < 6 THEN 'approved'
                WHEN v_b < 8 THEN 'requested'
                WHEN v_b = 8 THEN 'completed_pending_review'
                ELSE 'released'
            END;
        END IF;

        -- En PP, solicitudes en 'requested'/'approved' pueden no tener empresa ni actividad aún (pendiente de CA)
        IF v_status IN ('requested', 'approved') AND (idx % 2 = 0) THEN
            v_comp_id := NULL;
            v_sup_id := NULL;
            v_act_id := NULL;
        ELSE
            v_b := (idx * 17) % 100;
            IF v_b < 26 THEN v_comp_id := v_pp_companies[1];
            ELSIF v_b < 48 THEN v_comp_id := v_pp_companies[2];
            ELSIF v_b < 66 THEN v_comp_id := v_pp_companies[LEAST(3, array_length(v_pp_companies, 1))];
            ELSIF v_b < 80 THEN v_comp_id := v_pp_companies[LEAST(4, array_length(v_pp_companies, 1))];
            ELSE v_comp_id := v_pp_companies[1 + (idx % array_length(v_pp_companies, 1))];
            END IF;

            SELECT id INTO v_sup_id FROM company_supervisors WHERE company_id = v_comp_id AND deleted_at IS NULL ORDER BY id LIMIT 1;

            IF idx IN (5, 21, 53) THEN
                v_act_id := NULL;
            ELSIF rec.career_code = 'IND' AND (idx % 3 <> 0) THEN
                v_act_id := v_pp_activities[1]; -- ACT-PRO
            ELSIF rec.career_code = 'INF' AND (idx % 3 <> 0) THEN
                v_act_id := v_pp_activities[LEAST(5, array_length(v_pp_activities, 1))]; -- ACT-TI
            ELSIF rec.career_code IN ('MEC', 'ELE') AND (idx % 2 = 0) THEN
                v_act_id := v_pp_activities[3]; -- ACT-MTO
            ELSE
                v_act_id := v_pp_activities[1 + ((idx * 3) % array_length(v_pp_activities, 1))];
            END IF;
        END IF;

        INSERT INTO internships (
            student_id, period_id, company_id, supervisor_id, activity_type_id,
            semester_enrolled, start_date, end_date, required_hours, completed_hours,
            status, credits_awarded, presentation_letter_folio, presentation_letter_date,
            acceptance_letter_date, termination_letter_date, original_termination_letter_received,
            release_certificate_folio, release_date, released_at, is_expedient_complete, observations
        ) VALUES (
            rec.id, v_period_id, v_comp_id, v_sup_id, v_act_id,
            LEAST(15, GREATEST(6, rec.current_semester)),
            CASE WHEN v_comp_id IS NOT NULL THEN v_start ELSE NULL END,
            CASE WHEN v_comp_id IS NOT NULL THEN v_end ELSE NULL END,
            240,
            CASE WHEN v_status IN ('released','completed_pending_review') THEN 240 WHEN v_status = 'active' THEN 120 ELSE 0 END,
            v_status, 2,
            CASE WHEN v_status <> 'requested' THEN 'QA/PR' || LPAD(idx::text, 4, '0') ELSE NULL END,
            CASE WHEN v_status <> 'requested' THEN v_start - 3 ELSE NULL END,
            CASE WHEN v_comp_id IS NOT NULL AND v_status IN ('accepted','active','completed_pending_review','released','non_compliant') THEN v_start + 1 ELSE NULL END,
            CASE WHEN v_status IN ('completed_pending_review','released') THEN v_end ELSE NULL END,
            v_status = 'released',
            CASE WHEN v_status = 'released' THEN 'PPC_QA' || LPAD(idx::text, 4, '0') ELSE NULL END,
            CASE WHEN v_status = 'released' THEN v_end + 4 ELSE NULL END,
            CASE WHEN v_status = 'released' THEN (v_end + 4)::timestamptz ELSE NULL END,
            v_status = 'released',
            '[SEED_ANALYTICS] Trámite PP sintético (' || v_period_code || ')'
        ) RETURNING id INTO v_pp_id;

        IF v_status <> 'requested' THEN
            INSERT INTO expedient_documents (student_id, tramite_type, tramite_id, document_code, original_file_name, storage_path, file_size_bytes, status)
            VALUES (rec.id, 'internship', v_pp_id, 'CP', 'seed_cp.pdf', 'seed-analytics/cp.pdf', 1024, 'approved')
            ON CONFLICT DO NOTHING;
        END IF;
        IF v_comp_id IS NOT NULL AND v_status IN ('accepted','active','completed_pending_review','released','non_compliant') THEN
            INSERT INTO expedient_documents (student_id, tramite_type, tramite_id, document_code, original_file_name, storage_path, file_size_bytes, status)
            VALUES (rec.id, 'internship', v_pp_id, 'CA', 'seed_ca.pdf', 'seed-analytics/ca.pdf', 1024, 'approved')
            ON CONFLICT DO NOTHING;
        END IF;
        IF v_status = 'released' THEN
            INSERT INTO expedient_documents (student_id, tramite_type, tramite_id, document_code, original_file_name, storage_path, file_size_bytes, status)
            VALUES (rec.id, 'internship', v_pp_id, 'CC', 'seed_cc.pdf', 'seed-analytics/cc.pdf', 1024, 'approved')
            ON CONFLICT DO NOTHING;
        END IF;
    END LOOP;

    -- 5B. 4 casos de deduplicación PP en Ciclo Escolar 2025-2026 (cancelled en AD25 + active/released en FJ26)
    FOR rec IN
        SELECT s.id, s.current_semester, i.company_id, i.supervisor_id, i.activity_type_id
        FROM internships i
        JOIN students s ON s.id = i.student_id
        JOIN academic_periods ap ON ap.id = i.period_id
        WHERE i.observations LIKE '[SEED_ANALYTICS]%'
          AND ap.code = 'FJ26'
          AND i.status IN ('released', 'active')
          AND i.deleted_at IS NULL
        ORDER BY i.id
        LIMIT 4
    LOOP
        SELECT id INTO v_period_id FROM academic_periods WHERE code = 'AD25' AND deleted_at IS NULL LIMIT 1;
        IF NOT EXISTS (
            SELECT 1 FROM internships
            WHERE student_id = rec.id AND period_id = v_period_id AND status = 'cancelled' AND deleted_at IS NULL
        ) THEN
            INSERT INTO internships (
                student_id, period_id, company_id, supervisor_id, activity_type_id,
                semester_enrolled, required_hours, completed_hours, status, credits_awarded,
                observations, cancellation_reason
            ) VALUES (
                rec.id, v_period_id, rec.company_id, rec.supervisor_id, rec.activity_type_id,
                GREATEST(6, rec.current_semester - 1), 240, 0, 'cancelled', 2,
                '[SEED_ANALYTICS] Intento PP cancelado en AD25 (prueba deduplicación ciclo 2025-2026)',
                'Ajuste de horario laboral'
            );
        END IF;
    END LOOP;
END $$;

COMMIT;
