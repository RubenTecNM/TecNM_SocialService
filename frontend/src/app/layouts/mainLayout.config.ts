import type { BrandOptions, NavigationItem } from '../../shared/components/navigation'

// Configuración de encabezado y barra lateral
export const mainLayoutConfig = {
  showHeader: false,
  showSidebar: true,
  header: { title: 'TecNM', subtitle: 'Campus Monclova', showTitle: true, showLogo: false } satisfies BrandOptions,
  sidebar: { title: 'TecNM SSPP', subtitle: 'Vinculación y Trámites', showTitle: true, showLogo: false } satisfies BrandOptions,
}

export const headerNavigation: NavigationItem[] = [
  { label: 'Panel Principal', to: '/' },
  { label: 'Padrón Alumnos', to: '/students' },
  { label: 'Servicio Social', to: '/social-service' },
  { label: 'Prácticas', to: '/internships' },
  { label: 'Reportes', to: '/reports/tracking' },
]

export const sidebarNavigation: NavigationItem[] = [
  { label: 'Panel Principal', to: '/' },
  {
    label: 'Portal Estudiante',
    children: [
      { label: 'Mi Perfil & IMSS', to: '/students/me' },
      { label: 'Mi Servicio Social', to: '/social-service/my' },
      { label: 'Mis Prácticas', to: '/internships/my' },
    ],
  },
  {
    label: 'Gestión de Trámites',
    children: [
      { label: 'Padrón de Alumnos', to: '/students' },
      { label: 'Revisión Documental', to: '/expedients/review' },
      { label: 'Servicio Social', to: '/social-service' },
      { label: 'Prácticas Profesionales', to: '/internships' },
    ],
  },
  {
    label: 'Calidad & Reportes',
    children: [
      { label: 'Dashboard General', to: '/reports/dashboard' },
      { label: 'Matriz de Seguimiento', to: '/reports/tracking' },
      { label: 'Estadísticas Semestrales', to: '/reports/stats' },
      { label: 'Producto No Conforme (PNC)', to: '/quality/pnc' },
    ],
  },
  {
    label: 'Catálogos Institucionales',
    children: [
      { label: 'Carreras', to: '/catalogs/careers' },
    ],
  },
]
