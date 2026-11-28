BEGIN;

LOCK TABLE roles, user_roles, users IN SHARE ROW EXCLUSIVE MODE;

DO $$
DECLARE
    v_company_sup_users text;
    v_dept_head_role_id bigint;
    v_migrated_user_ids bigint[];
BEGIN
    -- 1. Preflight defensivo para company_supervisor: bloquear si existen usuarios activos con ese rol.
    SELECT string_agg(u.username, ', ' ORDER BY u.id)
    INTO v_company_sup_users
    FROM user_roles ur
    JOIN roles r ON r.id = ur.role_id
    JOIN users u ON u.id = ur.user_id
    WHERE ur.deleted_at IS NULL
      AND r.deleted_at IS NULL
      AND u.deleted_at IS NULL
      AND r.name = 'company_supervisor';

    IF v_company_sup_users IS NOT NULL THEN
        RAISE EXCEPTION 'Existen usuarios activos con el rol company_supervisor (%). Reasignar o desactivar antes de migrar.', v_company_sup_users;
    END IF;

    -- Retirar por borrado lógico cualquier registro residual de company_supervisor sin usuarios activos.
    UPDATE user_roles ur
    SET deleted_at = now(), updated_at = now()
    FROM roles r
    WHERE ur.role_id = r.id
      AND r.name = 'company_supervisor'
      AND ur.deleted_at IS NULL;

    UPDATE roles
    SET deleted_at = now(), updated_at = now()
    WHERE name = 'company_supervisor'
      AND deleted_at IS NULL;

    -- 2. Garantizar que el rol activo department_head exista.
    INSERT INTO roles (name, description)
    VALUES ('department_head', 'Jefe(a) de División de Gestión Tecnológica y Vinculación')
    ON CONFLICT (name) WHERE deleted_at IS NULL DO NOTHING;

    SELECT id INTO v_dept_head_role_id
    FROM roles
    WHERE name = 'department_head' AND deleted_at IS NULL;

    IF v_dept_head_role_id IS NULL THEN
        RAISE EXCEPTION 'No se pudo resolver el rol activo department_head.';
    END IF;

    -- 3. Capturar usuarios con asignación activa de admin.
    SELECT COALESCE(array_agg(DISTINCT ur.user_id), ARRAY[]::bigint[])
    INTO v_migrated_user_ids
    FROM user_roles ur
    JOIN roles r ON r.id = ur.role_id
    WHERE ur.deleted_at IS NULL
      AND r.name = 'admin';

    -- Reasignar a department_head evitando duplicados por uq_user_roles__user_role.
    INSERT INTO user_roles (user_id, role_id)
    SELECT DISTINCT uid, v_dept_head_role_id
    FROM unnest(v_migrated_user_ids) AS uid
    ON CONFLICT (user_id, role_id) WHERE deleted_at IS NULL DO NOTHING;

    -- Invalidar sesiones activas de los usuarios migrados para forzar refresco de JWT/UI.
    UPDATE users
    SET session_version = session_version + 1,
        updated_at = now()
    WHERE id = ANY(v_migrated_user_ids)
      AND deleted_at IS NULL;

    -- Retirar por borrado lógico asignaciones de admin y el rol admin del catálogo.
    UPDATE user_roles ur
    SET deleted_at = now(), updated_at = now()
    FROM roles r
    WHERE ur.role_id = r.id
      AND r.name = 'admin'
      AND ur.deleted_at IS NULL;

    UPDATE roles
    SET deleted_at = now(), updated_at = now()
    WHERE name = 'admin'
      AND deleted_at IS NULL;

    -- 4. Post-condiciones de integridad.
    IF EXISTS (
        SELECT 1 FROM roles
        WHERE name IN ('admin', 'company_supervisor') AND deleted_at IS NULL
    ) THEN
        RAISE EXCEPTION 'Post-condición fallida: permanecen roles activos admin o company_supervisor.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM unnest(v_migrated_user_ids) AS uid
        JOIN users u ON u.id = uid AND u.deleted_at IS NULL
        WHERE NOT EXISTS (
            SELECT 1 FROM user_roles ur
            WHERE ur.user_id = uid
              AND ur.role_id = v_dept_head_role_id
              AND ur.deleted_at IS NULL
        )
    ) THEN
        RAISE EXCEPTION 'Post-condición fallida: al menos un usuario migrado quedó sin rol department_head.';
    END IF;
END $$;

COMMIT;
