# Project Plan

Goal
Create a customizable, offline photo calendar maker app using .NET MAUI for Windows first. Users configure parameters (no WYSIWYG in v1, but design for future), include monthly photos and a cover page, and export print-ready PDFs via QuestPDF. Images and fonts remain local.

Scope (v1)
- Platform: Windows (WinUI) first; keep code portable.
- Calendar type: Monthly view only; yearly projects with adjustable start month.
- Layouts: Photo-first monthly calendars with flexible placement:
  - Photo top / calendar bottom, photo bottom / calendar top, photo left / calendar right, photo right / calendar left.
  - Adjustable split ratio (e.g., 30/70, 40/60, 50/50) per template.
  - Cover page: single full photo or simple photo grid (e.g., 2x3) presets; customizable cover text.
- Default page presets:
  - 5x7 in Landscape: vertical split (left/right) at 50/50; default placement: Photo left, Calendar right; user can flip sides.
  - Letter in Portrait: horizontal split (top/bottom) at 50/50; default placement: Photo top, Calendar bottom; user can flip halves.
- Templates: Developer-authored (not user-editable), templating fonts/colors/headers/photo slot/grid styling.
- Photos: At least one photo per month; fill modes (cover/contain), alignment; optional background color; drag-and-drop photo import on Windows.
- Fonts: Use system fonts initially (Segoe UI by default); allow uploading custom font files per project. Embed fonts in PDF when license/embedding flags allow; otherwise fall back to non-embedded or require an uploaded embeddable font.
- Export: QuestPDF vector output; page sizes 5x7, A4, Letter, 11x17, 13x19; orientation; margins (no bleed/crop marks).
- Privacy: Strictly offline, no telemetry; no downscaling on import (scale only at render time); assume sRGB color space.
- Storage: Multiple local projects with assets; SQLite for data; assets (images, fonts) stored under per-project folders; project export/import as a zip.
- Tests: Unit and UI tests included.

Out of scope for v1 (deferred)
- Events (single or recurring), categories, and US federal holidays overlay. Keep engine extensible for these features later.

Architecture
- MVVM with CommunityToolkit.Mvvm.
- Services:
  - CalendarEngine (month grid generation; first-day-of-week; no holidays in v1).
  - TemplateService (register/dev templates), ThemeService (fonts/colors/styles).
  - LayoutService (split ratios, photo placement, cover grid presets, default presets per page size).
  - RenderService (SkiaSharp preview), PdfExportService (QuestPDF shared layout primitives).
  - ProjectStorageService (create/open/export/import as zip), AssetService (images/fonts; drag-and-drop integration on Windows).
- Rendering pipeline:
  - Shared layout primitives (grid, header, photo region, day cells) used by both Skia preview and QuestPDF export.
  - Margins handled consistently; assume sRGB imagery and colors.

Data Model (initial)
- CalendarProject: id, name, year, startMonth, firstDayOfWeek, templateKey, pageSpec, margins, theme, layoutSpec, coverSpec, createdUtc, updatedUtc.
- PageSpec: enum size (FiveBySeven, A4, Letter, Tabloid_11x17, SuperB_13x19, Custom), orientation, customWidthPt, customHeightPt.
- Margins: leftPt, topPt, rightPt, bottomPt. Defaults = 0.2 in each (14.4 pt).
- LayoutSpec: placement (PhotoTopCalendarBottom | PhotoBottomCalendarTop | PhotoLeftCalendarRight | PhotoRightCalendarLeft), splitRatio (0-1), photoFillMode (Cover | Contain), alignment.
- CoverSpec: layout (FullPhoto | Grid2x3 | Grid3x3), titleText, subtitleText, titleFont, subtitleFont, titleAlignment, subtitleAlignment, titleColor, subtitleColor.
- ThemeSpec (referenced by CalendarProject.theme): baseFontFamily (default Segoe UI), headerFontSizePt (default 28), dayFontSizePt (default 11), titleFontSizePt (default 36), subtitleFontSizePt (default 18), primaryTextColor (default #000000), accentColor (default #0078D4), backgroundColor (default #FFFFFF).
- ImageAsset: id, projectId, path, role (monthPhoto, coverPhoto), monthIndex optional.
- FontAsset: id, projectId, path, familyName, style.

Templates (developer-only)
- Interface: ICalendarTemplate -> TemplateDescriptor (fonts/colors/header/photo slot/cell styles/safe area/cover text areas) and a render hook.
- Default templates:
  - PhotoMonthlyClassic: large photo region with configurable split; clean grid; month/year header.
  - PhotoCover: single full photo or 2x3 grid presets with customizable title/subtitle placement and styling.

Pages & Sizes
- Supported: 5x7, A4, Letter, 11x17 (Tabloid), 13x19 (Super B), and Custom.
- Default presets applied as above for 5x7 Landscape (vertical 50/50) and Letter Portrait (horizontal 50/50).
- Units stored in points (1 pt = 1/72 in); UI shows in inches/mm.

UI/Flows
- Projects: list/create/open/duplicate/export/import (zip with JSON + assets).
- New Project: year, start month, first day (default Sunday), size/orientation, margins (default 0.2 in), template, fonts/colors, layout selection (placement + split ratio), default preset picker.
- Designer/Preview: parameter panel (fonts/colors/margins/layout/photo selection); live Skia preview; month switcher; cover preview; drag-and-drop images onto photo regions.
- Export: PDF settings (page range, embed fonts when permitted).

Fonts
- System fonts (Windows defaults) available; add custom fonts per project by uploading files; embed registered fonts into PDF via QuestPDF when allowed by the font's embedding rights.

Storage & Export
- SQLite for projects; assets on disk at AppData/Projects/{projectId}/Assets/{Images|Fonts}.
- Project export: zip with project.json and assets/.

Milestones
1) Bootstrap app, DI, models, SQLite init, navigation.
2) CalendarEngine + monthly grid + first template (PhotoMonthlyClassic) without events.
3) Skia preview of one month; page sizes/margins and split layouts (top/bottom/left/right); default presets for 5x7 Landscape and Letter Portrait.
4) Photo handling: drag-and-drop; per-month photo selection; cover page (single and 2x3 grid) with customizable title/subtitle preview.
5) QuestPDF export: full year + cover; font embedding where permitted.
6) Project storage and zip import/export; asset management (images/fonts).
7) Templates and theming parameters (fonts/colors/headers); user settings UI.
8) Tests: unit (engine/layout) and UI tests (Windows) for critical flows; golden-image checks for layout.

Future (post-v1)
- Events, categories, recurrence editor, US federal holidays overlay and styling.
- Additional photo layouts (collages, per-day thumbnails), WYSIWYG designer.
- Locale support, custom holidays, and ICS import.