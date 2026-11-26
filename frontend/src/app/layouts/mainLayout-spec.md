# MainLayout

## Por qué existe

Una única ruta padre monta `MainLayout.vue`. Sus rutas hijas aparecen dentro de `RouterView`.
Al navegar entre páginas hijas, Vue conserva header, sidebar y desplegables; cambia únicamente contenido.
No usar `key` basada en ruta sobre MainLayout ni duplicarlo dentro de vistas.
Login vive fuera: entrar o salir de sesión sí monta/desmonta layout.
Persistente significa conservar componentes durante navegación SPA; recargar navegador reinicia estado.

## Archivos

- `MainLayout.vue`: composición persistente, tema, logout, conexión SignalR (`useRealtime` + avisos `UiToast` en vivo) y `RouterView`.
- `mainLayout.config.ts`: configuración específica de aplicación y enlaces.
- `shared/components/UiShell.vue`: estructura y espacio de contenido.
- `shared/components/UiHeader.vue`: header independiente.
- `shared/components/UiSidebar.vue`: sidebar independiente.
- `shared/components/UiNavigation.vue`: enlaces y grupos compartidos.
- `app/router/index.ts`: ruta padre protegida. Agregar rutas de módulos a `children`.

## Header, sidebar o ambos

Editar `mainLayoutConfig` en `mainLayout.config.ts`:

| Diseño | showHeader | showSidebar |
| --- | --- | --- |
| Ambos | true | true |
| Solo header | true | false |
| Solo sidebar, configuración actual | false | true |
| Ninguno | false | false |

Sin sidebar, contenido ocupa ancho disponible. No queda columna vacía.
Logout aparece en sidebar; sin sidebar, en header; sin ambos, en footer.
Control de tema en sidebar usa un botón compacto de icono dinámico (luna en modo claro para activar modo oscuro; sol en modo oscuro para volver a modo claro) junto a cierre de sesión; en header o sin ambos conserva switch/botón de tema accesible.
Footer institucional permanece en las cuatro opciones.

## Título y logo independientes

Cada configuración `header` y `sidebar` acepta:

```ts
header: {
  title: 'TecNM',
  subtitle: 'Campus Monclova',
  showTitle: true,
  showLogo: true,
}
```

- `showTitle`: muestra título y subtítulo.
- `showLogo`: muestra logo. Ambos flags independientes; pueden estar apagados.
- `logo`: URL opcional para otra aplicación. Por defecto usa `shared/assets/tecnm-isologo.svg`.
- Mantener `title` aunque se oculte: da nombre accesible al enlace del logo.

## Enlaces y grupos

Editar `headerNavigation` y `sidebarNavigation`. Mismo contrato para ambos:

```ts
[
  { label: 'Inicio', to: '/' },
  {
    label: 'Gestión',
    children: [
      { label: 'Alumnos', to: { name: 'students' } },
      { label: 'Reportes', to: '/reportes' },
    ],
  },
]
```

Registrar rutas de ejemplo antes de usarlas. `to` acepta destinos de Vue Router.
Grupo tiene `children`, sin ruta propia, y enlaces aceptan `badge` e `icon` opcionales. Un nivel de grupos; hijos son páginas.
Header despliega panel; sidebar expande lista. Teclado usa Tab, Enter/Espacio y Escape para cerrar.
En móvil navegación permanece disponible. No se oculta listado del header.
Muestra actual enlaza secciones de bienvenida mediante `/#colores`, `/#datos`, etc.
Reemplazar destinos por páginas reales al agregar módulos; no crear vistas ficticias para template.

## Verificación

- `pnpm --prefix frontend run build`
- Desde `frontend`: `node scripts/check-layout.mjs`. Comprueba cuatro composiciones, opciones de marca y navegación agrupada.

## Posición de sidebar

En escritorio, sidebar ocupa borde izquierdo de ventana y permanece visible mediante `position: sticky`.
Su densidad vertical compacta, etiquetas completas en una línea con iconos semánticos, todos los grupos de módulos visibles/desplegados por defecto (incluyendo `Catálogos Oficiales`) y pie organizado permiten visualizar navegación completa, ficha de usuario y acciones inferiores dentro del viewport de escritorio sin barras de desplazamiento vertical ni horizontal. Header permanece en flujo normal.
UiShell reserva todo ancho disponible. Cada vista elige ancho mediante UiBasePage; sidebar no depende de ese límite.
En móvil (`<= 900px`), `MainLayout` presenta una barra superior compacta fija con marca institucional, botón rápido de tema (luna/sol) y disparador de menú (`☰`) que despliega `UiSidebar` como cajón lateral deslizable (`drawer`) con fondo semitransparente, cierre al navegar, tocar fuera o pulsar `Escape`, manteniendo intacto el diseño de escritorio (`>= 901px`).

## Ancho por página

Usar `UiBasePage` dentro de cada vista autenticada. MainLayout no impone límite central.
`fluid` ocupa todo ancho disponible; sin `fluid`, máximo 960 px centrados.
SessionView usa `fluid`. Márgenes responsive siguen en UiShell.
