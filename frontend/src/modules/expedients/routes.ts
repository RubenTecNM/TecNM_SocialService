import type { RouteRecordRaw } from 'vue-router'

export const expedientsRoutes: RouteRecordRaw[] = [
  {
    path: 'expedients/review',
    name: 'expedients-review',
    component: () => import('./views/ExpedientsReviewView.vue'),
    meta: { requiresAuth: true },
  },
]

