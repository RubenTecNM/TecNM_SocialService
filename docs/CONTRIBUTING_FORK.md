# Guía de Contribución por Fork y Pull Request — TecNM SSPP

Esta guía detalla el flujo de trabajo para colaborar en el proyecto mediante un **Fork** personal en GitHub y envío de **Pull Requests (PR)**.

---

## 📌 Resumen del Flujo
1. **Fork** del repositorio central a tu cuenta personal de GitHub.
2. **Clonar** tu fork en tu computadora.
3. **Vincular `upstream`** para recibir actualizaciones del repositorio central.
4. **Crear una rama** específica para cada tarea (`feature/...`).
5. **Comitear y pushear** los cambios a tu fork.
6. **Abrir el Pull Request** contra el repositorio central.

---

## Paso 1. Hacer Fork en GitHub

1. Entra al repositorio principal en tu navegador:
   👉 [https://github.com/RubenTecNM/TecNM_SocialService](https://github.com/RubenTecNM/TecNM_SocialService)
2. En la esquina superior derecha, haz clic en el botón **Fork**.
3. Selecciona tu cuenta de GitHub como destino y haz clic en **Create fork**.
4. Ahora tendrás una copia en tu cuenta:
   `https://github.com/<TU_USUARIO>/TecNM_SocialService`

---

## Paso 2. Clonar tu Fork localmente

Abre tu terminal o PowerShell en la carpeta donde guardas tus proyectos:

```bash
# Reemplaza <TU_USUARIO> por tu usuario de GitHub
git clone https://github.com/<TU_USUARIO>/TecNM_SocialService.git

# Entra al proyecto
cd TecNM_SocialService
```

---

## Paso 3. Configurar el repositorio principal como `upstream`

Para mantener tu repositorio actualizado con los cambios que se aprueben en el proyecto principal:

```bash
# Agregar el repo original con el nombre 'upstream'
git remote add upstream https://github.com/RubenTecNM/TecNM_SocialService.git

# Verificar remotos configurados
git remote -v
```

Deberías ver una salida similar a esta:
```text
origin    https://github.com/<TU_USUARIO>/TecNM_SocialService.git (fetch)
origin    https://github.com/<TU_USUARIO>/TecNM_SocialService.git (push)
upstream  https://github.com/RubenTecNM/TecNM_SocialService.git (fetch)
upstream  https://github.com/RubenTecNM/TecNM_SocialService.git (push)
```

---

## Paso 4. Crear una rama para tu tarea

**Nunca trabajes directamente en la rama `main`**. Crea siempre una rama con nombre descriptivo:

```bash
# 1. Asegúrate de estar en main y actualizado
git checkout main
git pull upstream main

# 2. Crear y cambiarte a tu rama de trabajo
git checkout -b feature/portal-estudiante
```

---

## Paso 5. Desarrollar, probar y guardar cambios

1. Realiza tus modificaciones en el código.
2. **Regla de oro antes de comitear:** verifica que el frontend compile sin errores de tipos:
   ```bash
   pnpm --prefix frontend run build
   ```
3. Guarda tus cambios con commits claros:
   ```bash
   # Ver archivos modificados
   git status

   # Preparar cambios
   git add .

   # Guardar commit
   git commit -m "feat(frontend): formulario de perfil de estudiante y captura de IMSS"
   ```

---

## Paso 6. Mantenerte sincronizado antes de subir

Antes de enviar tu código, descarga los últimos cambios del proyecto original para evitar conflictos:

```bash
git fetch upstream
git rebase upstream/main
```

---

## Paso 7. Subir tu rama a tu Fork (`origin`)

Envía tu rama local a tu repositorio de GitHub:

```bash
git push -u origin feature/portal-estudiante
```

---

## Paso 8. Crear el Pull Request (PR)

1. Ve a tu repositorio en GitHub (`https://github.com/<TU_USUARIO>/TecNM_SocialService`).
2. Verás un banner amarillo que dice: **Compare & pull request**. Haz clic en él.
   *(Si no aparece, ve a la pestaña "Pull requests" -> "New pull request")*.
3. Asegúrate de que la configuración indique:
   - **Base repository:** `RubenTecNM/TecNM_SocialService` | **base:** `main`
   - **Head repository:** `<TU_USUARIO>/TecNM_SocialService` | **compare:** `feature/portal-estudiante`
4. Escribe un título y descripción de los cambios que implementaste.
5. Haz clic en **Create pull request**.

---

## Paso 9. ¿Qué hacer si te piden correcciones en la revisión?

No es necesario abrir un nuevo PR. Simplemente:

1. Realiza los cambios solicitados en tu misma rama local.
2. Guarda el commit y vuelve a subirlo:
   ```bash
   git add .
   git commit -m "fix: corrige validación de formulario según feedback"
   git push origin feature/portal-estudiante
   ```
3. El Pull Request en GitHub se actualizará automáticamente con los nuevos cambios.

