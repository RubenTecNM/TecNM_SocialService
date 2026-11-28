export type UserRole =
  | 'student'
  | 'social_service_officer'
  | 'internship_coordinator'
  | 'department_head'

export interface LoginRequest {
  username: string
  password: string
}

export interface UserResponse {
  id: number
  username: string
  email: string
  name: string
  roles: UserRole[]
}
