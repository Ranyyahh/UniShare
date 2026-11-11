# Modern Design System Specification (`design.md`)

A comprehensive, production-ready design foundation tailored for modern web applications, SaaS dashboards, and digital platforms.

---

## 1. Brand Philosophy & Visual Narrative
- **Tone & Voice**: Purposeful, modern, refined, high-clarity, effortless.
- **Aesthetic Direction**: Neo-minimalist with subtle micro-depth. Crisp typographic hierarchies, generous negative space, intentional border contrasts, and vibrant functional accents against soft, calibrated neutral backdrops.
- **Surface Elevation**: Layered neutral surfaces with subtle hairline borders (`1px solid rgba(0, 0, 0, 0.06)` or dark-mode equivalent `rgba(255, 255, 255, 0.08)`) and diffuse ambient drop-shadows.

---

## 2. Color Palette & Token System

### Functional Palette
- **Primary / Brand**:
  - `primary-50`: `#EEF2FF`
  - `primary-100`: `#E0E7FF`
  - `primary-500`: `#6366F1` (Indigo Vibrant)
  - `primary-600`: `#4F46E5` (Interactive default)
  - `primary-700`: `#4338CA` (Interactive hover/active)
  - `primary-900`: `#312E81`
- **Secondary / Accent**:
  - `accent-teal`: `#0EA5E9` (Cyan / Info highlight)
  - `accent-violet`: `#8B5CF6` (Creative punch)
- **Neutrals (Slate-Zinc Calibration)**:
  - `surface-canvas`: `#F8FAFC` (App background)
  - `surface-card`: `#FFFFFF` (Card container)
  - `surface-elevated`: `#FFFFFF` (Modals, Popovers, Dropdowns)
  - `surface-subtle`: `#F1F5F9` (Dividers, muted tags, input fills)
  - `border-default`: `#E2E8F0`
  - `border-strong`: `#CBD5E1`
  - `text-primary`: `#0F172A` (Headings, primary body)
  - `text-secondary`: `#475569` (Subheadings, supporting info)
  - `text-muted`: `#94A3B8` (Captions, placeholders, disabled)
- **Feedback & Semantics**:
  - `success`: `#10B981` (Green emerald) / `success-subtle`: `#ECFDF5`
  - `warning`: `#F59E0B` (Amber vibrant) / `warning-subtle`: `#FFFBEB`
  - `error`: `#EF4444` (Rose scarlet) / `error-subtle`: `#FEF2F2`
  - `info`: `#3B82F6` (Electric Blue) / `info-subtle`: `#EFF6FF`

---

## 3. Typography Scale & Hierarchy

- **Primary Typeface**: `Inter`, `-apple-system`, `BlinkMacSystemFont`, `"Segoe UI"`, `sans-serif`
- **Monospace Typeface**: `"JetBrains Mono"`, `"Fira Code"`, `monospace` (Data, code snippets, metrics)

| Token | Size | Line Height | Tracking | Weight | Role |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `display-2xl` | 72px (4.5rem) | 1.05 | -0.025em | 800 / Bold | Hero landing banners |
| `display-xl` | 56px (3.5rem) | 1.1 | -0.02em | 700 / Bold | Major screen titles |
| `heading-lg` | 32px (2.0rem) | 1.2 | -0.015em | 700 / SemiBold | Section headings, page headers |
| `heading-md` | 24px (1.5rem) | 1.3 | -0.01em | 600 / SemiBold | Card titles, group dividers |
| `heading-sm` | 18px (1.125rem) | 1.4 | -0.005em | 600 / Medium | Sub-modules, modal headers |
| `body-base` | 15px (0.9375rem) | 1.5 | 0 | 400 / Regular | Core readable interface text |
| `body-sm` | 13px (0.8125rem) | 1.45 | +0.005em | 400 / 500 | Secondary tables, metadata, helper |
| `caption` | 11px (0.6875rem) | 1.35 | +0.02em | 600 / SemiBold | Overlines, badges, micro-tags |

---

## 4. Spacing, Layout & Grid Rhythm

- **Base Unit**: `4px` grid system.
- **Rhythm Scale**:
  - `space-1`: `4px` (Tight icon margins)
  - `space-2`: `8px` (Button inline gap, icon-label spacing)
  - `space-3`: `12px` (Internal chip/badge padding)
  - `space-4`: `16px` (Standard component inner padding, input padding)
  - `space-6`: `24px` (Card padding, card group gaps)
  - `space-8`: `32px` (Section gaps)
  - `space-12`: `48px` (Screen major division)
  - `space-16`: `64px` (Hero top/bottom gutters)
- **Container Breakpoints**:
  - `max-w-7xl` (`1280px`) centered desktop container with `px-6` / `px-8` horizontal gutters.
  - Multi-column 12-column responsive layout with `gap-6`.

---

## 5. Elevation & Radius Tokens

- **Border Radius**:
  - `rounded-xs`: `4px` (Badges, tags)
  - `rounded-sm`: `6px` (Inputs, dropdown items)
  - `rounded-md`: `10px` (Buttons, small cards, tooltips)
  - `rounded-lg`: `16px` (Dashboard cards, dialog panels)
  - `rounded-xl`: `24px` (Feature highlights, hero containers)
  - `rounded-full`: `9999px` (Avatars, pills, circular icon buttons)
- **Shadow System**:
  - `shadow-xs`: `0 1px 2px 0 rgba(15, 23, 42, 0.05)`
  - `shadow-sm`: `0 1px 3px 0 rgba(15, 23, 42, 0.08), 0 1px 2px -1px rgba(15, 23, 42, 0.06)`
  - `shadow-md`: `0 4px 6px -1px rgba(15, 23, 42, 0.08), 0 2px 4px -2px rgba(15, 23, 42, 0.05)`
  - `shadow-lg`: `0 10px 15px -3px rgba(15, 23, 42, 0.08), 0 4px 6px -4px rgba(15, 23, 42, 0.03)`
  - `shadow-glow`: `0 0 20px -2px rgba(99, 102, 241, 0.25)` (Primary action accents)

---

## 6. Core Component Patterns

### Buttons & Actions
- **Primary CTA**: Solid `primary-600` background, crisp white typography, `rounded-md`, `font-medium`, smooth scale `active:scale-[0.98]` interaction, subtle shadow with hover elevation.
- **Secondary / Ghost**: Transparent or `surface-subtle` background with hairline `border-default`, `text-primary`, hover highlight `bg-slate-100`.
- **Destructive**: Rose-500 fill or outline with alert styling.

### Cards & Surfaces
- Flat, clean backdrop (`#FFFFFF`), `1px solid #E2E8F0`, `rounded-lg`, subtle padding (`24px`).
- Header with title, subtle badge status, and optional action ellipsis or filter tab.

### Forms & Input Fields
- Height: `40px` (desktop standard).
- Default: `bg-white`, `border-slate-300`, `text-slate-900`, placeholder `text-slate-400`.
- Focus state: `border-primary-500`, ring `3px rgba(99, 102, 241, 0.2)`.

### Badges & Status Indicators
- Soft pill tags with light pastel background (`bg-indigo-50 text-indigo-700 border border-indigo-100`).
- Subtle dot indicators for real-time states (Active green `#10B981`, Paused amber `#F59E0B`).

---

## 7. Motion & Interaction Standards
- **Transition curves**: `cubic-bezier(0.16, 1, 0.3, 1)` (snappy ease-out).
- **Duration tokens**:
  - Hover / micro-interaction: `150ms - 200ms`
  - Sheet / Modal drawer entry: `300ms`
  - Complex screen morph: `400ms`