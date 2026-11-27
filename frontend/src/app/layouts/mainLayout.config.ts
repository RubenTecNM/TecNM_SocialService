import type { BrandOptions, NavigationItem, NavigationLink } from '../../shared/components/navigation'

export const mainLayoutConfig = {
  showHeader: false,
  showSidebar: true,
  header: {
    title: 'TecNM',
    subtitle: 'Campus Monclova',
    showTitle: true,
    showLogo: true,
  } satisfies BrandOptions,
  sidebar: {
    title: 'TecNM Monclova',
    subtitle: 'Vinculación · SSPP',
    showTitle: true,
    showLogo: true,
  } satisfies BrandOptions,
}

export const ROLE_LABELS: Record<string, string> = {
  admin: 'Administrador del Sistema',
  social_service_officer: 'Encargado(a) de Servicio Social',
  internship_coordinator: 'Coordinador(a) de Prácticas Prof.',
  department_head: 'Jefatura de Vinculación',
  student: 'Portal del Estudiante',
}

export function getRoleDisplayLabel(roles: string[]): string {
  for (const r of roles) {
    if (ROLE_LABELS[r]) return ROLE_LABELS[r]
  }
  return 'Usuario Institucional'
}

export function getSidebarNavigationForRoles(roles: string[]): NavigationItem[] {
  const isAdmin = roles.includes('admin')
  const isSSOfficer = roles.includes('social_service_officer')
  const isPPCoord = roles.includes('internship_coordinator')
  const isDeptHead = roles.includes('department_head')
  const isStudent = roles.includes('student')
  const isStaff = isAdmin || isSSOfficer || isPPCoord || isDeptHead

  // Si el usuario es únicamente estudiante, solo ve su Portal de Estudiante
  if (isStudent && !isStaff) {
    return [
      { label: 'Resumen de Mi Trámite', to: '/', icon: 'dashboard' },
      { label: 'Mi Perfil & Vigencia IMSS', to: '/students/me', icon: 'profile' },
      { label: 'Mi Servicio Social', to: '/social-service/my', icon: 'social' },
      { label: 'Mis Prácticas Profesionales', to: '/internships/my', icon: 'internship' },
    ]
  }

  if (!isStaff) return []

  const nav: NavigationItem[] = [
    { label: 'Panel Principal', to: '/', icon: 'dashboard' },
  ]

  // Grupo de Gestión Operativa según rol de Vinculación
  const tramitesChildren: NavigationLink[] = []
  if (isAdmin || isSSOfficer || isDeptHead) {
    tramitesChildren.push({ label: 'Servicio Social', to: '/social-service', icon: 'social' })
  }
  if (isAdmin || isPPCoord || isDeptHead) {
    tramitesChildren.push({ label: 'Prácticas Profesionales', to: '/internships', icon: 'internship' })
  }
  if (isAdmin || isSSOfficer || isPPCoord) {
    tramitesChildren.push({ label: 'Revisión Documental', to: '/expedients/review', icon: 'review' })
  }
  tramitesChildren.push({ label: 'Padrón de Alumnos', to: '/students', icon: 'students' })

  nav.push({
    label: 'Gestión de Trámites',
    open: true,
    children: tramitesChildren,
  })

  // Grupo de Estadísticos y Calidad (SEGUIMIENTO VINCULACIÓN1.xlsx y 911.10)
  nav.push({
    label: 'Estadísticos & Calidad',
    open: true,
    children: [
      { label: 'Matriz Seguimiento (Excel)', to: '/reports/tracking', icon: 'tracking' },
      { label: 'Estadístico & Reporte 911', to: '/reports/stats', icon: 'stats' },
      { label: 'Producto No Conforme (PNC)', to: '/quality/pnc', icon: 'pnc' },
    ],
  })

  // Catálogos Oficiales (para Administrador y Encargados)
  if (isStaff) {
    nav.push({
      label: 'Catálogos Oficiales',
      open: true,
      children: [
        { label: 'Empresas y dependencias', to: '/companies', icon: 'companies' },
        { label: 'Carreras Oficiales', to: '/catalogs/careers', icon: 'careers' },
        ...(isAdmin || isSSOfficer || isPPCoord
          ? [{ label: 'Plantillas documentales', to: '/catalogs/document-templates', icon: 'careers' as const }]
          : []),
      ],
    })
  }

  return nav
}

export const headerNavigation: NavigationItem[] = [
  { label: 'Panel Principal', to: '/', icon: 'dashboard' },
]

export const sidebarNavigation: NavigationItem[] = getSidebarNavigationForRoles(['admin'])
