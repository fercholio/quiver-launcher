# Quiver Launcher — Backlog (fork `fercholio/quiver-launcher`)

Estado del trabajo de mejoras sobre Quiver Launcher. Este fork vive en
`https://github.com/fercholio/quiver-launcher`. **Nada va al upstream** (`tgeorgiadis/quiver-launcher`).

> Última actualización: 2026-10-29. Rama de trabajo: `feature/batch-improvements` (ya mergeada a `main`
> vía PR #1, merge commit `eff0399`). Build: .NET 10 / Avalonia. Tests: xUnit + FluentAssertions.
> Verificación de cada tanda: build Release 0 errores + suite completa en verde salvo **un fallo
> preexistente** de AppImage (`GameDownloadInstallServiceTests.DownloadAndInstallAsync_into_a_located_appimage_folder_keeps_the_other_appimages`),
> que **ya falla en `main` limpio** (AppImage en Windows) — no es regresión.

Complejidad estimada: **L** baja · **M** media · **H** alta.

---

## ✅ Hecho (mergeado a `main`)

### Rendimiento
- Carga progresiva de la librería (lista al instante, estado por app se rellena) — #190.
- Escaneo de estado inicial con concurrencia acotada; gamepad a ritmo idle cuando no se usa — #205.

### Portadas e IA (opcional, local: ComfyUI/LLM)
- Generación de portadas SDXL para apps sin arte + **upscaling** de portadas.
- Fetch de portadas desde **SteamGridDB**.
- **Auto-tagging** con IA + panel de Settings para la IA (sin variables de entorno).

### Detección de ejecutable y archivos base
- Auto-detección del ejecutable (heurística + IA) en vez de preguntar siempre.
- Preferir un `launcher` incluido al autodetectar.
- Selector de ejecutable apunta a Program Files / carpeta real para instaladores.
- Badge **"Needs your own ROM/ISO"** + **"Provide Base Game File"** para recomps/ports.

### Update tracking robusto (Tier 2)
- **Nightly que renombra su asset** bajo el mismo tag: match por identidad normalizada + checksum.
- **Versión en el nombre del archivo**: limpia `.exe` versionados viejos + retarget del ejecutable guardado — #185/#180/#177.
- **Repos tag-only**: fallback a git tags cuando no hay releases con archivos (ej. yquake2).

### Gestión de biblioteca (Tier 1 + extras)
- **Launch options por juego**: argumentos, variables de entorno y working dir.
- **"Select Different Executable"** en toda app instalada (lista también exes en subcarpetas).
- **Abrir / copiar carpeta de saves**.
- **Filtro rápido por tipo** (All/Recomp/Decomp/Port/Emulator) + sort **"Project"**.
- **Colecciones / categorías** de usuario, con filtro en la barra.
- **Back Up / Restore** de la carpeta de instalación (saves incluidos).
- "Recently Installed" + "Most Played" sorts, tiempo jugado, refresco de imágenes sin reinicio, F5 recarga catálogo.

### Ya existían en el código (verificado, no requirió cambios)
- **No pisar tags al resync** (#186) → `CatalogLibrarySync` usa snapshot: solo añade/quita lo que el catálogo cambió.
- **Ver changelog al actualizar** → botón "Versions" por app en el diálogo de updates abre el historial con notas; también pestaña Releases + menú "Show Changelog".

---

## ⏳ Pendiente (priorizado)

### Siguiente tanda sugerida (valor/esfuerzo)
- [ ] **Metadata scraping** más allá de portadas: descripción, fecha de release, género, dev; con fallback al README del repo cuando no hay IGDB. **M**
- [ ] **Indicador WIP vs completo + versión/stars en la tarjeta del catálogo** — #169/#201/#154. **L**
- [ ] **Ocultar entradas del catálogo** (no solo de la biblioteca) + silenciar autores — #173/#160. **L**
- [ ] **Download queue** con pausa/reanudar + límite de concurrencia/ancho de banda (estilo Heroic). **M**
- [ ] **Colisión de folderName** para ports duplicados del mismo juego (namespacing por entrada) — #181/#187. **M**

### Power-user / fiabilidad
- [ ] Toggles globales: desactivar verificación / asset-matching; soporte de **repos privados** (token) — #170/#171. **M**
- [ ] **Steam Deck / shortcut no-Steam**: estabilidad al actualizar + instalar el build de la plataforma correcta — #177/#212. **M**
- [ ] **Prerequisitos / runtimes / dependencias** (redistribuibles, cores de emulador) con manifiesto por app. **M**
- [ ] Opción de **conservar el zip original** sin auto-extraer + preservar fecha de GitHub — #174. **L**
- [ ] "**Report New Project**" para crowdsourcing del catálogo — #166. **L**

### Grandes apuestas (Tier 3)
- [ ] **Install recipes/scripts** declarativos (estilo Lutris): aplicar parche a la ROM del usuario, colocar archivos, fetch de dependencia. **M/H**
- [ ] **Cloud / folder save-sync** (Nextcloud/Drive/SMB) — se complementa con Back Up/Restore. **M/H**
- [ ] **RetroAchievements** para los proyectos basados en emulador. **M**
- [ ] **Plugin / extension API** (estilo Playnite): metadata providers, installers, fuentes de librería. **H**
- [ ] **Theme packs / skin engine** de comunidad (el editor de temas ya existe). **M/H**
- [ ] **Sync de librería multi-máquina** (export/import o self-hosted, estilo Retrom). **M/H**

### Plataformas (infra, no solo código)
- [ ] **Flatpak / Flathub** release — el request más votado (#28). **M**
- [ ] **Android**: teclado desaparece, Back cierra la app — #218/#219/#220. **L/M**
- [ ] **macOS**: selección de carpeta / madurez del build — #192. **M/H**

---

## Notas de arquitectura (para retomar rápido)
- **Lógica pura y testeable** vive en `lib/QuiverLauncher.Core/Services/` (ej. `ExecutableRanker`, `BaseFileRequirement`,
  `LaunchOptions`, `SaveFolderLocator`, `DownloadAssetSelection` + `NormalizedAssetKey`, `InstallBackup`). La UI/IO vive en `Services/`, `Views/`, `ViewModels/`.
- **apps.json**: serialización curada en `AppCatalogService.SerializeApp` + parse en `ParseAppArrayStatic` (añadir campos en ambos).
- **Settings** por-app y globales en `Services/AppSettings.cs` (ej. `Collections`, `UserAppTags`, `TagDisplayFilters`).
- **Releases/updates**: `GitHubReleaseService` (fetch + fallback a tags), `CatalogReleaseSelection` (elige "latest"),
  `InstalledBuild` (checksum de rolling releases), `LibraryUpdateChecker` (decide update disponible), `GameInfo.RefreshInstalledStatus`.
- **Menú contextual de la biblioteca**: `Views/LibraryActionKind.cs` (enum) + `Views/LibraryView.axaml` (items) +
  `Views/LibraryView.axaml.cs` (handlers/switch) + `Services/LibraryActions.cs` (lógica).
- **Diálogos code-only** (patrón): `Views/ShortcutExecutableDialog.cs`, `LaunchOptionsDialog.cs`, `CollectionsDialog.cs`.
- **Filtros de biblioteca**: `GameManager.GetVisibleGames` aplica scope → tag filter → tipo → colección → búsqueda.
