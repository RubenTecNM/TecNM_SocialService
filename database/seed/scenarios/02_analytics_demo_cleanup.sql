-- ============================================================================
-- Limpieza Segura de Datos Sintéticos de Analítica
-- Archivo: database/seed/scenarios/02_analytics_demo_cleanup.sql
-- Elimina ÚNICAMENTE los registros creados por 02_analytics_demo_seed.sql
-- sin tocar alumnos, empresas, periodos o trámites reales/previos.
-- ============================================================================

BEGIN;

DELETE FROM expedient_documents
WHERE storage_path LIKE 'seed-analytics/%'
   OR student_id IN (SELECT id FROM students WHERE control_number LIKE 'SQA24%');

DELETE FROM social_service_applications
WHERE social_service_id IN (
    SELECT id FROM social_services WHERE observations LIKE '[SEED_ANALYTICS]%'
);

DELETE FROM social_services
WHERE observations LIKE '[SEED_ANALYTICS]%';

DELETE FROM internships
WHERE observations LIKE '[SEED_ANALYTICS]%';

DELETE FROM students
WHERE control_number LIKE 'SQA24%'
  AND personal_email LIKE '%@seed-analytics.local';

DELETE FROM company_supervisors
WHERE email LIKE '%@seed-analytics.local';

DELETE FROM companies
WHERE email LIKE '%@seed-analytics.local'
  AND NOT EXISTS (SELECT 1 FROM social_services ss WHERE ss.company_id = companies.id)
  AND NOT EXISTS (SELECT 1 FROM internships i WHERE i.company_id = companies.id);

DELETE FROM academic_periods
WHERE code IN ('AD23', 'FJ24', 'AD24')
  AND NOT EXISTS (SELECT 1 FROM social_services ss WHERE ss.period_id = academic_periods.id)
  AND NOT EXISTS (SELECT 1 FROM internships i WHERE i.period_id = academic_periods.id)
  AND NOT EXISTS (SELECT 1 FROM student_import_batches b WHERE b.period_id = academic_periods.id)
  AND NOT EXISTS (SELECT 1 FROM non_conformances n WHERE n.period_id = academic_periods.id);

COMMIT;
