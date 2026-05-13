# UI Guidelines (Razor + CSS)

## Shared CSS Layers
- `wwwroot/css/site.css`: base/reset/layout legacy.
- `wwwroot/css/ui-components.css`: reusable components + utilities.
- `wwwroot/css/dashboard.css`: page-specific visuals for portal pages.

## Reusable Classes
- **Spacing**: `u-mt-05`, `u-mt-075`, `u-mt-1`, `u-mt-125`, `u-mb-1`, `u-mb-125`.
- **Display**: `u-hidden`, `u-inline`, `u-wrap`.
- **Layout**: `u-gap-1`, `u-gap-2`, `u-flex-head`, `u-actions-wrap-end`.
- **State**: `u-danger-text`.

## Component Rules
- Use `surface-card` for white rounded containers.
- Use `kpi-grid` + `kpi-card` for metric strips.
- Use shared `modal-*` classes for all popup dialogs.
- Prefer `device-card` pattern for device summary cards.

## Razor Rules
- Avoid inline `style="..."` unless value is dynamic and cannot be class-based.
- If inline style repeats 2+ times, promote to utility/component class.
- Keep visible text in Vietnamese.

## Modal UX
- Always include `modal-header` + `modal-title` + close button.
- Keep primary action on the right in modal footer.
- Use `modal-wide` only when tabular/complex content is required.
