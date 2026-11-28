import { computed, shallowRef } from 'vue'
import { me } from '../../modules/auth/api/authApi'
import type { UserResponse } from '../../modules/auth/types/auth'

export const sessionUser = shallowRef<UserResponse | null>(null)
let pending: Promise<UserResponse> | undefined
let generation = 0
export function clearSession() {
  generation++
  pending = undefined
  sessionUser.value = null
}
export function loadSession(): Promise<UserResponse> {
  if (!pending) {
    const version = generation
    const request = me().then(user => {
      if (version === generation) sessionUser.value = user
      return user
    }).finally(() => { if (pending === request) pending = undefined })
    pending = request
  }
  return pending
}
export function hasRole(...roles: string[]) {
  return roles.some(role => sessionUser.value?.roles.some(assigned => assigned === role))
}
export function canReadProcess(type: 'social_service' | 'internship') {
  return hasRole('department_head', type === 'social_service' ? 'social_service_officer' : 'internship_coordinator')
}
export const defaultProcess = computed<'social_service' | 'internship'>(() =>
  hasRole('internship_coordinator') && !hasRole('social_service_officer', 'department_head') ? 'internship' : 'social_service')
