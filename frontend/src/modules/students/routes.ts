import type { RouteRecordRaw } from 'vue-router'

export const studentsRoutes: RouteRecordRaw[] = [
  {
    path: 'students',
    name: 'students-list',
    component: () => import('./views/StudentsListView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: 'students/me',
    name: 'students-profile',
    component: () => import('./views/StudentProfileView.vue'),
    meta: { requiresAuth: true },
  },
]

