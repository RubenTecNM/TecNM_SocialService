BEGIN;

-- ============================================================================
-- 1. CATÁLOGO OFICIAL DE ROLES INSTITUCIONALES
-- ============================================================================
INSERT INTO roles (name, description)
VALUES
    ('student', 'Estudiante / Prestador de SS / Practicante'),
    ('social_service_officer', 'Encargado(a) de Servicio Social'),
    ('internship_coordinator', 'Coordinador(a) de Prácticas Profesionales'),
    ('department_head', 'Jefe(a) de División de Gestión Tecnológica y Vinculación')
ON CONFLICT (name) WHERE deleted_at IS NULL DO UPDATE SET
    description = EXCLUDED.description;

-- ============================================================================
-- 2. CUENTAS DEMO POR ROL Y ETAPA DE TRÁMITE
-- Contraseña universal para todas las cuentas demo: TecNM-Demo-2026!
-- ============================================================================
INSERT INTO users (id, username, email, password_hash, first_name, first_surname, second_surname, is_active)
OVERRIDING SYSTEM VALUE
VALUES
    -- Personal Administrativo y Directivo (1 cuenta por cada rol de staff)
    (
        3,
        'encargado.ss',
        'servicio.social@monclova.tecnm.mx',
        'AQAAAAIAAYagAAAAEIr5sN4qIlQg9nbMvyG/Ber0Le6W9Gg8Bt2Ej4uha46Bx7kn/1vJ0MbMx84IKWUhTQ==',
        'Miguel Alejandro',
        'Sanson',
        'Olguin',
        true
    ),
    (
        4,
        'coord.practicas',
        'arturo.martinez@monclova.tecnm.mx',
        'AQAAAAIAAYagAAAAEIr5sN4qIlQg9nbMvyG/Ber0Le6W9Gg8Bt2Ej4uha46Bx7kn/1vJ0MbMx84IKWUhTQ==',
        'Arturo',
        'Martínez',
        'Coordinación',
        true
    ),
    (
        5,
        'jefa.vinculacion',
        'vinculacion@monclova.tecnm.mx',
        'AQAAAAIAAYagAAAAEIr5sN4qIlQg9nbMvyG/Ber0Le6W9Gg8Bt2Ej4uha46Bx7kn/1vJ0MbMx84IKWUhTQ==',
        'Rocío',
        'Mendoza',
        'Riojas',
        true
    ),

    -- Cuentas de Estudiantes en distintas etapas del flujo (SS y Prácticas)
    (
        2,
        'l23050363@monclova.tecnm.mx',
        'L23050363@monclova.tecnm.mx',
        'AQAAAAIAAYagAAAAEIr5sN4qIlQg9nbMvyG/Ber0Le6W9Gg8Bt2Ej4uha46Bx7kn/1vJ0MbMx84IKWUhTQ==',
        'Héctor Gabriel',
        'Torres',
        'Arzola',
        true
    ),
    (
        6,
        'l22050114@monclova.tecnm.mx',
        'L22050114@monclova.tecnm.mx',
        'AQAAAAIAAYagAAAAEIr5sN4qIlQg9nbMvyG/Ber0Le6W9Gg8Bt2Ej4uha46Bx7kn/1vJ0MbMx84IKWUhTQ==',
        'Ana Carolina',
        'Garza',
        'Alvarado',
        true
    ),
    (
        7,
        'l23050412@monclova.tecnm.mx',
        'L23050412@monclova.tecnm.mx',
        'AQAAAAIAAYagAAAAEIr5sN4qIlQg9nbMvyG/Ber0Le6W9Gg8Bt2Ej4uha46Bx7kn/1vJ0MbMx84IKWUhTQ==',
        'Valeria Sofía',
        'Hernández',
        'Castillo',
        true
    ),
    (
        8,
        'l22050289@monclova.tecnm.mx',
        'L22050289@monclova.tecnm.mx',
        'AQAAAAIAAYagAAAAEIr5sN4qIlQg9nbMvyG/Ber0Le6W9Gg8Bt2Ej4uha46Bx7kn/1vJ0MbMx84IKWUhTQ==',
        'Luis Fernando',
        'Rodríguez',
        'Martínez',
        true
    ),
    (
        9,
        'l22050501@monclova.tecnm.mx',
        'L22050501@monclova.tecnm.mx',
        'AQAAAAIAAYagAAAAEIr5sN4qIlQg9nbMvyG/Ber0Le6W9Gg8Bt2Ej4uha46Bx7kn/1vJ0MbMx84IKWUhTQ==',
        'Miguel Ángel',
        'Sánchez',
        'Gómez',
        true
    ),
    (
        10,
        'l23050618@monclova.tecnm.mx',
        'L23050618@monclova.tecnm.mx',
        'AQAAAAIAAYagAAAAEIr5sN4qIlQg9nbMvyG/Ber0Le6W9Gg8Bt2Ej4uha46Bx7kn/1vJ0MbMx84IKWUhTQ==',
        'Daniela Fernanda',
        'Pérez',
        'Cavazos',
        true
    )
ON CONFLICT (username) WHERE deleted_at IS NULL DO UPDATE SET
    email = EXCLUDED.email,
    password_hash = EXCLUDED.password_hash,
    first_name = EXCLUDED.first_name,
    first_surname = EXCLUDED.first_surname,
    second_surname = EXCLUDED.second_surname,
    is_active = EXCLUDED.is_active;

-- Sincronizar secuencia de users tras inserción con IDs explícitos
SELECT setval(pg_get_serial_sequence('users', 'id'), COALESCE(MAX(id), 1)) FROM users;

-- ============================================================================
-- 3. ASIGNACIÓN DE ROLES A USUARIOS DEMO
-- ============================================================================
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id
FROM users u
CROSS JOIN roles r
WHERE (u.username = 'encargado.ss' AND r.name = 'social_service_officer')
   OR (u.username = 'coord.practicas' AND r.name = 'internship_coordinator')
   OR (u.username = 'jefa.vinculacion' AND r.name = 'department_head')
   OR (u.username IN (
        'l23050363@monclova.tecnm.mx',
        'l22050114@monclova.tecnm.mx',
        'l23050412@monclova.tecnm.mx',
        'l22050289@monclova.tecnm.mx',
        'l22050501@monclova.tecnm.mx',
        'l23050618@monclova.tecnm.mx'
      ) AND r.name = 'student')
ON CONFLICT (user_id, role_id) WHERE deleted_at IS NULL DO NOTHING;

COMMIT;
