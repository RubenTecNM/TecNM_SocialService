import type { RouteRecordRaw } from 'vue-router'

export const internshipsRoutes: RouteRecordRaw[] = [
  {
    path: 'internships',
    name: 'internships-list',
    component: () => import('./views/InternshipsListView.vue'),
    meta: { requiresAuth: true, roles: ['department_head', 'internship_coordinator'] },
  },
  {
    path: 'internships/my',
    name: 'internships-my',
    component: () => import('./views/StudentInternshipsView.vue'),
    meta: { requiresAuth: true, roles: ['student'] },
  },
]

