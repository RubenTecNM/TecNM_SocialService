-- Solo desarrollo: escenarios sobre el padrón demo. Ejecutar después de la migración de seguimiento.
BEGIN;
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM students WHERE control_number='I23050363') OR NOT EXISTS (SELECT 1 FROM academic_periods WHERE code='AD26') THEN
 RAISE EXCEPTION 'Se requiere el padrón y catálogo demo TecNM'; END IF;
END $$;
INSERT INTO academic_periods(code,name,start_date,end_date,is_active)
SELECT 'FJ25','Febrero - Julio 2025','2025-02-10','2025-07-04',false
WHERE NOT EXISTS(SELECT 1 FROM academic_periods WHERE code='FJ25' AND deleted_at IS NULL);
UPDATE academic_periods SET is_active=(code='AD26'),updated_at=now(),updated_by=5 WHERE deleted_at IS NULL;
UPDATE students s SET social_service_eligible_period_id=p.id,
 social_service_eligible_period_updated_at=now(),social_service_eligible_period_updated_by=5,updated_at=now(),updated_by=5
FROM (VALUES
 ('I23050363','AD26'), -- En proceso, constante; usa evidencias aprobadas existentes.
 ('I22050114','FJ26'), -- En proceso, pendiente de valoración, aunque aún no esté activo.
 ('I22050289','AD25'), -- Tercer aviso; PP no acredita avance SS.
 ('I23050412','FJ26'), -- En proceso, con poco avance.
 ('I22050501','FJ25'), -- Producto no conforme por antigüedad, sin SS.
 ('I23050618','FJ26'), -- Liberado.
 ('I23050741','AD26'), -- Primer aviso: solicitud con PDF pendiente.
 ('I21050890','FJ26'), -- En proceso, pendiente de valoración.
 ('I22050915','FJ26'), -- Segundo aviso: sin apertura.
 ('I23050348','AD26')  -- Primer aviso: solo IMSS no acredita avance SS.
) AS sample(control_number,period_code)
JOIN academic_periods p ON p.code=sample.period_code AND p.deleted_at IS NULL
WHERE s.control_number=sample.control_number AND s.deleted_at IS NULL;
UPDATE students SET social_service_eligible_period_id=NULL,social_service_eligible_period_updated_at=now(),social_service_eligible_period_updated_by=5,updated_at=now(),updated_by=5
WHERE control_number='I21050409' AND deleted_at IS NULL; -- Periodo pendiente: PP liberado no equivale a SS liberado.
DELETE FROM social_service_follow_up_reviews r USING social_services t,students s,academic_periods p
WHERE r.social_service_id=t.id AND t.student_id=s.id AND r.period_id=p.id AND p.code='AD26'
 AND s.control_number IN ('I22050114','I21050890');
INSERT INTO social_service_follow_up_reviews(social_service_id,period_id,assessment,notes,created_by,updated_by)
SELECT t.id,p.id,sample.assessment,sample.notes,3,3
FROM (VALUES ('I23050363','consistent','DEMO: entrega constante de documentos conforme a su etapa.'),
 ('I23050412','low_progress','DEMO: inició y documentó, pero requiere seguimiento por poco avance reciente.')) sample(control_number,assessment,notes)
JOIN students s ON s.control_number=sample.control_number AND s.deleted_at IS NULL
JOIN social_services t ON t.student_id=s.id AND t.deleted_at IS NULL AND t.status NOT IN ('cancelled','non_compliant','released')
JOIN academic_periods p ON p.code='AD26' AND p.deleted_at IS NULL
ON CONFLICT(social_service_id,period_id) DO UPDATE SET assessment=excluded.assessment,notes=excluded.notes,updated_by=excluded.updated_by,updated_at=now(),deleted_at=NULL,deleted_by=NULL;
COMMIT;
SELECT s.control_number,f.status,f.progress_assessment,f.eligible_period_code,f.tracking_period_code
FROM vw_student_social_service_follow_up f JOIN students s ON s.id=f.student_id ORDER BY s.id;
