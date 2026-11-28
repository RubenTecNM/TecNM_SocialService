<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBasePage, UiButton } from '../../../shared/components'
import { http } from '../../../shared/api/http'
import { problemMessage } from '../../../shared/api/problemDetails'

type Authority = { id: number; title: string | null; fullName: string; position: string; isActive: boolean }
type Logo = { id: number; name: string; width: number; height: number; createdAt: string }
const roles = {
  institute_director: 'Dirección', outreach_division_head: 'Jefatura de Vinculación',
  social_service_head: 'Oficina de Servicio Social', internship_coordinator: 'Coordinación de Prácticas',
}
const authorities = ref<Record<string, Authority | null>>({})
const logos = ref<Logo[]>([])
const roleKey = ref<keyof typeof roles>('institute_director')
const title = ref('')
const fullName = ref('')
const position = ref('')
const name = ref('')
const file = ref<File>()
const uploadInput = ref<HTMLInputElement>()
const history = ref<Authority[]>([])
const busy = ref(false)
const error = ref('')
const message = ref('')

async function run(work: () => Promise<void>) {
  busy.value = true
  error.value = ''
  message.value = ''
  try { await work() }
  catch (cause) { error.value = problemMessage(cause, 'La operación no pudo completarse.') }
  finally { busy.value = false }
}
async function csrf() {
  const { data } = await http.get<{ token: string }>('/template-management/csrf')
  return { headers: { 'X-CSRF-TOKEN': data.token } }
}
async function loadHistory() {
  history.value = (await http.get<{ items: Authority[] }>('/institutional-authorities', {
    params: { roleKey: roleKey.value, isActive: false, pageSize: 100 },
  })).data.items
}
async function activate() {
  const { data } = await http.post<Authority>('/institutional-authorities', {
    roleKey: roleKey.value, title: title.value, fullName: fullName.value, position: position.value,
  }, await csrf())
  authorities.value[roleKey.value] = data
  title.value = fullName.value = position.value = ''
  await loadHistory()
  message.value = 'Titular activado para futuras descargas.'
}
async function upload() {
  if (!file.value) return
  const body = new FormData()
  body.append('name', name.value)
  body.append('file', file.value)
  const { data } = await http.post<Logo>('/template-management/logos', body, await csrf())
  logos.value.unshift(data)
  name.value = ''
  file.value = undefined
  if (uploadInput.value) uploadInput.value.value = ''
  message.value = 'Revisión disponible en el editor de plantillas.'
}
onMounted(() => run(async () => {
  const [active, library] = await Promise.all([
    http.get<Record<string, Authority | null>>('/institutional-authorities/active'),
    http.get<Logo[]>('/template-management/logos'),
  ])
  authorities.value = active.data
  logos.value = library.data
  await loadHistory()
}))
</script>

<template>
  <UiBasePage title="Autoridades y logotipos" fluid>
    <UiAlert v-if="error" tone="danger">{{ error }}</UiAlert>
    <p v-if="message" role="status">{{ message }}</p>
    <div class="identity-grid">
      <section aria-labelledby="authority-heading">
        <h2 id="authority-heading">Autoridades institucionales</h2>
        <label>Cargo institucional
          <select v-model="roleKey" :disabled="busy" @change="run(loadHistory)">
            <option v-for="(label, key) in roles" :key="key" :value="key">{{ label }}</option>
          </select>
        </label>
        <p v-if="authorities[roleKey]">
          <strong>{{ authorities[roleKey]?.title }} {{ authorities[roleKey]?.fullName }}</strong><br />
          {{ authorities[roleKey]?.position }}
        </p>
        <p v-else>Titular pendiente de registrar.</p>
        <form @submit.prevent="run(activate)">
          <label>Título académico <input v-model="title" maxlength="32" /></label>
          <label>Nombre del nuevo titular <input v-model="fullName" maxlength="180" required /></label>
          <label>Cargo para el PDF <input v-model="position" maxlength="180" required /></label>
          <UiButton type="submit" :disabled="busy">Activar titular</UiButton>
        </form>
        <details v-if="history.length">
          <summary>Titulares anteriores</summary>
          <ul><li v-for="item in history" :key="item.id">{{ item.fullName }} · {{ item.position }}</li></ul>
        </details>
      </section>
      <section aria-labelledby="logos-heading">
        <h2 id="logos-heading">Biblioteca de logotipos</h2>
        <form @submit.prevent="run(upload)">
          <label>Nombre del logotipo <input v-model="name" maxlength="100" required /></label>
          <label>Archivo PNG <input ref="uploadInput" type="file" accept="image/png" required @change="file = ($event.target as HTMLInputElement).files?.[0]" /></label>
          <UiButton type="submit" :disabled="busy || !file">Cargar revisión</UiButton>
        </form>
        <ul class="logo-library">
          <li v-for="logo in logos" :key="logo.id"><strong>{{ logo.name }}</strong> · revisión {{ logo.id }} · {{ logo.width }} × {{ logo.height }} px</li>
        </ul>
      </section>
    </div>
  </UiBasePage>
</template>

<style scoped>
.identity-grid { display: grid; gap: 1.5rem; grid-template-columns: repeat(2, minmax(0, 1fr)); }
section { border-top: 1px solid var(--color-border-medium); padding: 1rem 0; }
section h2 { margin: 0 0 1rem; }
form, label { display: grid; gap: .5rem; }
form { gap: .85rem; margin-block: 1rem; max-width: 34rem; justify-items: start; }
label, input, select { width: 100%; }
.logo-library { max-height: 22rem; overflow: auto; padding-left: 1.1rem; }
.logo-library li { padding-block: .4rem; }
@media (max-width: 780px) { .identity-grid { grid-template-columns: minmax(0, 1fr); } }
</style>
