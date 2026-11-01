import { createRouter, createWebHistory } from 'vue-router'

import { authRoutes, authenticatedAuthRoutes } from '../../modules/auth'
import { catalogsRoutes } from '../../modules/catalogs'
import { expedientsRoutes } from '../../modules/expedients'
import { internshipsRoutes } from '../../modules/internships'
import { qualityRoutes } from '../../modules/quality'
import { reportsRoutes } from '../../modules/reports'
import { socialServiceRoutes } from '../../modules/social-service'
import { studentsRoutes } from '../../modules/students'
import {
  clearSessionMarker,
  hasActiveSession,
} from '../../shared/utils/sessionCookie'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    ...authRoutes,
    {
      path: '/',
      component: () => import('../layouts/MainLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        ...authenticatedAuthRoutes,
        ...catalogsRoutes,
        ...studentsRoutes,
        ...expedientsRoutes,
        ...socialServiceRoutes,
        ...internshipsRoutes,
        ...qualityRoutes,
        ...reportsRoutes,
      ],
    },
  ],

  scrollBehavior(to, _from, savedPosition) {
    return savedPosition ?? (to.hash ? { el: to.hash, top: 24 } : { top: 0 })
  },
})

router.beforeEach((to) => {
  if (!to.meta.requiresAuth) return true

  if (!hasActiveSession()) {
    clearSessionMarker()
    return {
      name: 'login',
      query: { redirect: to.fullPath },
    }
  }

  return true
})

export default router
