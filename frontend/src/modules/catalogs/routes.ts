import type { RouteRecordRaw } from 'vue-router'

export const catalogsRoutes: RouteRecordRaw[] = [
  {
    path: 'catalogs/institutional-identity',
    name: 'catalogs-institutional-identity',
    component: () => import('./views/InstitutionalIdentityView.vue'),
    meta: { requiresAuth: true, roles: ['department_head'] },
  },
  {
    path: 'catalogs/careers',
    name: 'catalogs-careers',
    component: () => import('./views/CareersView.vue'),
    meta: { requiresAuth: true, roles: ['department_head', 'social_service_officer', 'internship_coordinator'] },
  },
  {
    path: 'catalogs/activity-types',
    name: 'catalogs-activity-types',
    component: () => import('./views/ActivityTypesView.vue'),
    meta: { requiresAuth: true, roles: ['department_head', 'social_service_officer', 'internship_coordinator'] },
  },
]
