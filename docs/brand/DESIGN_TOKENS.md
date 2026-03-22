# LuSplit Design Tokens

Version 1.0 - Foundational Identity

## Color Tokens

### Primary Palette

| Token | HEX | RGB | Usage |
|---|---|---|---|
| `color.panel.left.teal` | `#2CB1A1` | `44, 177, 161` | Left split panel |
| `color.panel.right.indigo` | `#5A74FF` | `90, 116, 255` | Right split panel |
| `color.text.primary.navy` | `#1F2937` | - | Primary text |
| `color.background.snow` | `#F6F8FB` | - | Primary background |
| `color.divider.white` | `#FFFFFF` | - | Split divider |

### Secondary Palette

| Token | HEX | Usage |
|---|---|---|
| `color.state.positive.softGreen` | `#16A34A` | Positive amounts |
| `color.state.owes.warmAmber` | `#F59E0B` | Owes states |
| `color.state.error.softRed` | `#DC2626` | Error states |
| `color.text.muted.gray` | `#6B7280` | Secondary text |
| `color.background.dark` | `#0F172A` | Dark mode background |

## Typography Tokens

- Font style: rounded sans-serif
- Preferred stack:
  - `Inter Rounded`
  - `SF Pro Rounded`
  - `Nunito`
  - `Manrope` (fallback)
- Wordmark weight: Medium
- Wordmark tracking: `+2%`

## Shape Tokens

- `radius.card = 16px`
- `radius.button = 12px`
- `radius.input = 10px`

## Elevation Tokens

- `shadow.soft.1 = 0 2px 8px rgba(0,0,0,0.05)`

Use very soft elevation only. Avoid heavy shadows.

## Motion Tokens

- `motion.duration.fast = 120ms`
- `motion.duration.standard = 180ms`
- `motion.easing.default = ease-out cubic`

Interaction notes:

- Split animation: panels move `4px` apart, then settle
- Settle animation: subtle opacity fade + compress
- No bouncing

## Accessibility Tokens and Rules

- Minimum touch target: `44px`
- Text scaling: fully responsive
- Contrast minimum: `4.5:1`

## Global Color Rules

- Never exceed 70% saturation
- No neon colors
- Teal is always left, indigo is always right
- Divider is always white
