export const themes = [
  'All',
  'Everyday',
  'World & culture',
  'Science & space',
] as const
export type Theme = (typeof themes)[number]

export const categoryThemes: Record<string, Exclude<Theme, 'All'>> = {
  'months-of-the-year': 'Everyday',
  'european-capitals': 'World & culture',
  'zodiac-signs': 'World & culture',
  'chemical-elements': 'Science & space',
  'greek-letters': 'Science & space',
  'si-base-units': 'Science & space',
  'solar-system-planets': 'Science & space',
}
