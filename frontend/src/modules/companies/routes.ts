import type { RouteRecordRaw } from 'vue-router'
export const companiesRoutes: RouteRecordRaw[] = [{
  path: 'companies', name: 'companies', component: () => import('./CompaniesView.vue'),
  meta: { requiresAuth: true, roles: ['department_head', 'social_service_officer', 'internship_coordinator'] },
}]
