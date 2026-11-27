import type { RouteRecordRaw } from 'vue-router'

export const expedientsRoutes: RouteRecordRaw[] = [
  {
    path: 'catalogs/document-templates',
    name: 'template-management',
    component: () => import('./views/TemplateManagementView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'social_service_officer', 'internship_coordinator'] },
  },
  {
    path: 'expedients/review',
    name: 'expedients-review',
    component: () => import('./views/ExpedientsReviewView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'social_service_officer', 'internship_coordinator'] },
  },
]

