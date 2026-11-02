import type { BrandOptions, NavigationItem } from '../../shared/components/navigation'

// Configuración de encabezado y barra lateral
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

export const headerNavigation: NavigationItem[] = [
  { label: 'Panel Principal', to: '/', icon: 'dashboard' },
  { label: 'Padrón Alumnos', to: '/students', icon: 'students' },
  { label: 'Servicio Social', to: '/social-service', icon: 'social', badge: 142, badgeTone: 'emerald' },
  { label: 'Prácticas', to: '/internships', icon: 'internship', badge: 58, badgeTone: 'navy' },
  { label: 'Reportes', to: '/reports/tracking', icon: 'tracking' },
]

export const sidebarNavigation: NavigationItem[] = [
  { label: 'Panel Principal', to: '/', icon: 'dashboard' },
  {
    label: 'Gestión de Trámites',
    open: true,
    children: [
      { label: 'Servicio Social', to: '/social-service', icon: 'social', badge: 142, badgeTone: 'emerald' },
      { label: 'Prácticas Prof.', to: '/internships', icon: 'internship', badge: 58, badgeTone: 'navy' },
      { label: 'Revisión Documental', to: '/expedients/review', icon: 'review', badge: 19, badgeTone: 'gold' },
      { label: 'Padrón de Alumnos', to: '/students', icon: 'students' },
    ],
  },
  {
    label: 'Calidad & Reportes',
    open: false,
    children: [
      { label: 'Dashboard General', to: '/reports/dashboard', icon: 'reports' },
      { label: 'Matriz Seguimiento', to: '/reports/tracking', icon: 'tracking' },
      { label: 'Estadísticas AD26', to: '/reports/stats', icon: 'stats' },
      { label: 'Producto No Conforme', to: '/quality/pnc', icon: 'pnc' },
    ],
  },
  {
    label: 'Portal Estudiante',
    open: false,
    children: [
      { label: 'Mi Perfil & IMSS', to: '/students/me', icon: 'profile' },
      { label: 'Mi Servicio Social', to: '/social-service/my', icon: 'social' },
      { label: 'Mis Prácticas', to: '/internships/my', icon: 'internship' },
    ],
  },
  {
    label: 'Catálogos Oficiales',
    open: false,
    children: [
      { label: 'Carreras', to: '/catalogs/careers', icon: 'careers', badge: 6, badgeTone: 'slate' },
    ],
  },
]
