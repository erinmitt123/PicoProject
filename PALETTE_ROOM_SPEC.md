# Palette Room: Build Spec

> Feed this file to your AI agent at the start of every session. Keep it at the root of the repo.

## One-line pitch
A mixed reality design tool for PICO: place true-scale furniture in your real room, pick a color palette, and watch everything restyle instantly using the 60-30-10 design rule. Then fine-tune any piece by hand.

## Target
- **Headset:** PICO 4 Ultra (confirm with DevRel; swap if the cohort targets Space Pro)
- **Engine:** Unity + PICO Unity Integration SDK (latest), unless the Launch Pad toolchain dictates otherwise
- **Mode:** mixed reality passthrough
- **Input:** hand tracking first (pinch / grab / poke), controllers as fallback; the app must work fully with either
- **Deadline:** November 13, 2026

## Rating criteria (every feature should serve at least one)
Creativity · Visuals · Interaction · Features · Completeness

---

## Core loop
1. User launches the app. Passthrough is on, and a welcome panel appears at a comfortable height.
2. User taps **"Start with a sample room"**. A pre-arranged set of furniture drops onto the detected floor in front of them, already styled with a palette.
3. User opens the **palette dock** and picks a new palette. All objects recolor with a smooth 0.4s color blend plus a soft shimmer.
4. User fine-tunes: grab and move objects, use the paint wand on single pieces, shuffle, change materials and lighting.
5. User saves snapshot A, tries another look, saves snapshot B, and flips between them.

## Feature list (in build priority order)

### P0: must ship (the MVP)
- **Passthrough + floor detection:** use plane detection to find the floor (and walls if available). Fallback: "tap the floor to set height."
- **Furniture catalog (home mode), about 10 items:** sofa, armchair, coffee table, rug, floor lamp, side table, bookshelf, plant, wall art, throw pillows (x2).
  - Each item has a **role** tag: `Dominant`, `Secondary`, or `Accent`.
  - Each item has 1–3 named **paintable zones** (e.g. sofa: body = dominant, legs = fixed wood).
- **Placement:** grab to move, two-hand to rotate, objects snap to the floor; wall items snap to walls.
- **Palette system:** a palette = 5 colors: `dominant`, `secondary`, `accent`, `neutral`, `highlight`.
  - **Auto-apply (60-30-10):** Dominant role → dominant color, Secondary → secondary, Accent → accent (alternating with highlight for variety).
  - **8 presets:** Scandinavian, Moody Jewel, Coastal, Terracotta Desert, Mid-Century, Monochrome Sage, Japandi, Sunset Pop.
- **Palette dock UI:** a floating panel of palette cards (5 swatches each) that follows the user lazily, pinned near the left hand / wrist.
- **Sample room button** on the welcome panel.

### P1: what makes it 4★
- **Paint wand:** pinch a swatch from the dock and a colored orb attaches to the fingertip; touch any paintable zone to recolor it with a paint-splash particle and a soft "splat" sound. Overrides are remembered per object.
- **Shuffle:** keeps the current palette but randomly reassigns which color goes to which role (while keeping the 60-30-10 balance). Button + flourish animation.
- **Reset overrides:** returns all objects to the palette's auto-assignment.
- **Material toggle per object:** Matte, Fabric, Wood, Metal, Gloss.
- **Lighting presets:** Morning (cool, bright), Overcast (soft, flat), Evening (warm lamp). This affects virtual objects only; blend over 1s.
- **Accent wall:** a virtual panel that snaps flush to a detected real wall and takes a palette color. **Never attempt to recolor the real walls.**

### P2: what makes it 5★
- **Snapshots A/B:** save up to 3 looks (palette + overrides + materials + lighting + layout); a toggle flips between them with a crossfade.
- **Office mode:** a second catalog: desk, office chair, monitor, whiteboard, filing cabinet, phone booth pod, planter, rug.
- **Build-your-own palette:** pick 5 colors from a color wheel; save it as a custom preset.
- **Persistence:** save the room layout with spatial anchors so it reappears in the same spot next session.
- **Ambient audio + UI sounds:** soft room tone, satisfying clicks, a paint splat, a palette "whoosh."

---

## Visual direction
- Soft, premium, "design-studio" feel: frosted-glass panels, rounded corners, generous spacing, subtle drop shadows on the real floor.
- PBR materials; no flat unlit shading on furniture.
- All UI readable at 1.5m; minimum body text about 24px equivalent; never place UI closer than 0.5m.
- Color changes always animate (never snap instantly).

## Interaction rules
- Every button responds to poke, pinch-ray, and controller trigger.
- Hover and press states on every interactive element, plus a light haptic on controllers.
- No artificial locomotion; the user walks in their real room.
- The palette dock can be summoned with a palm-up gesture or a menu button.

## Completeness checklist (must all be true before submitting)
- [ ] App launches straight into a working state in under 10 seconds
- [ ] A first-time user understands what to do within 30 seconds (welcome panel + sample room)
- [ ] No dead buttons; every visible control does something
- [ ] Pause / settings menu: lighting, sound on/off, reset room, recenter
- [ ] Works with hands only AND controllers only
- [ ] Graceful fallback when plane detection fails
- [ ] Holds a stable frame rate (target the headset's native refresh rate)
- [ ] No placeholder text, debug logs on screen, or fake data

## Out of scope (do not build)
- Recoloring real walls or real furniture via the camera
- Multiplayer
- Shopping links or real product catalogs
- Paint-chip color accuracy claims

## Milestones
| Dates | Goal |
|---|---|
| Oct 19–25 | Onboarding + P0 MVP: passthrough, floor, 10 items, palettes, auto-apply, sample room |
| Oct 26–Nov 1 | P1: paint wand, shuffle, materials, lighting, accent wall |
| Nov 2–8 | P2: snapshots, office mode, custom palettes, persistence, audio |
| Nov 9–13 | Polish only: completeness checklist, bug bash, record demo video, submit |
