import { http } from '../../../shared/api/http'
import type {
  CreateStudentRequest,
  PagedResponse,
  Student,
  StudentFilterParams,
  StudentImportResult,
  UpdateImssRequest,
} from '../types/students'

export async function getStudents(params?: StudentFilterParams): Promise<PagedResponse<Student>> {
  return (await http.get<PagedResponse<Student>>('/students', { params })).data
}

export async function getStudentById(id: number): Promise<Student> {
  return (await http.get<Student>(`/students/${id}`)).data
}

export async function getMyStudentProfile(): Promise<Student> {
  return (await http.get<Student>('/students/me')).data
}

export async function createStudent(data: CreateStudentRequest): Promise<Student> {
  return (await http.post<Student>('/students', data)).data
}

export async function updateStudent(id: number, data: Partial<CreateStudentRequest>): Promise<Student> {
  return (await http.put<Student>(`/students/${id}`, data)).data
}

export async function updateMyImss(data: UpdateImssRequest): Promise<Student> {
  return (await http.put<Student>('/students/me/imss', data)).data
}

export async function downloadImportTemplate(): Promise<Blob> {
  const response = await http.post('/student-imports/template', {}, { responseType: 'blob' })
  return response.data
}

export async function uploadStudentsExcel(file: File): Promise<StudentImportResult> {
  const formData = new FormData()
  formData.append('file', file)
  return (await http.post<StudentImportResult>('/student-imports/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })).data
}

