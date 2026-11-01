import type { RouteRecordRaw } from 'vue-router'

export const socialServiceRoutes: RouteRecordRaw[] = [
  {
    path: 'social-service',
    name: 'social-service-list',
    component: () => import('./views/SocialServiceListView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: 'social-service/my',
    name: 'social-service-my',
    component: () => import('./views/StudentSocialServiceView.vue'),
    meta: { requiresAuth: true },
  },
]

