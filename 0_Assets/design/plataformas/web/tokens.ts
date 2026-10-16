// boaz · tokens do design system (Material Design 3)
// Gerado por 0_Assets/design/scripts/gerar.mjs a partir de identidade/marca.json e tokens/*.json — não edite à mão.
// Os estilos ficam em tokens.css; aqui estão os valores para lógica em TypeScript
// (ex.: escolher a navegação pela janela) e para bibliotecas que pedem objetos.

export type WindowClass = 'compact' | 'medium' | 'expanded' | 'large' | 'extra-large';

/** Classes de tamanho de janela do M3 (largura em px/dp). */
export const windows = {
  "compact": {
    "min": 0,
    "max": 599,
    "margin": 16,
    "columns": 4,
    "gutter": 16,
    "panes": 1,
    "navigation": "navigation-bar",
    "exemplos": "celular em pé"
  },
  "medium": {
    "min": 600,
    "max": 839,
    "margin": 24,
    "columns": 8,
    "gutter": 24,
    "panes": 1,
    "navigation": "navigation-rail",
    "exemplos": "tablet em pé, celular dobrável aberto"
  },
  "expanded": {
    "min": 840,
    "max": 1199,
    "margin": 24,
    "columns": 12,
    "gutter": 24,
    "panes": 2,
    "navigation": "navigation-rail",
    "exemplos": "tablet deitado, notebook pequeno"
  },
  "large": {
    "min": 1200,
    "max": 1599,
    "margin": 24,
    "columns": 12,
    "gutter": 24,
    "panes": 2,
    "navigation": "navigation-drawer",
    "exemplos": "desktop"
  },
  "extraLarge": {
    "min": 1600,
    "max": null,
    "margin": 24,
    "columns": 12,
    "gutter": 24,
    "panes": 3,
    "navigation": "navigation-drawer",
    "exemplos": "monitor grande"
  }
} as const;

/** Classe de janela para uma largura, ex.: windowClassOf(window.innerWidth). */
export function windowClassOf(width: number): WindowClass {
  if (width >= 1600) return 'extra-large';
  if (width >= 1200) return 'large';
  if (width >= 840) return 'expanded';
  if (width >= 600) return 'medium';
  if (width >= 0) return 'compact';
  return 'compact';
}

/** Media query de cada janela, para matchMedia ou useMediaQuery. */
export const windowMedia: Record<WindowClass, string> = {
  "compact": "(max-width: 599px)",
  "medium": "(min-width: 600px) and (max-width: 839px)",
  "expanded": "(min-width: 840px) and (max-width: 1199px)",
  "large": "(min-width: 1200px) and (max-width: 1599px)",
  "extra-large": "(min-width: 1600px)"
};

export const colors = {
  "light": {
    "background": "#FDF7FF",
    "onBackground": "#1D1B20",
    "surface": "#FDF7FF",
    "surfaceDim": "#DED8E0",
    "surfaceBright": "#FDF7FF",
    "surfaceContainerLowest": "#FFFFFF",
    "surfaceContainerLow": "#F8F2FA",
    "surfaceContainer": "#F2ECF4",
    "surfaceContainerHigh": "#ECE6EE",
    "surfaceContainerHighest": "#E6E0E9",
    "onSurface": "#1D1B20",
    "surfaceVariant": "#E7E0EB",
    "onSurfaceVariant": "#49454E",
    "inverseSurface": "#322F35",
    "inverseOnSurface": "#F5EFF7",
    "outline": "#7A757F",
    "outlineVariant": "#CAC4CF",
    "shadow": "#000000",
    "scrim": "#000000",
    "surfaceTint": "#65558F",
    "primary": "#65558F",
    "onPrimary": "#FFFFFF",
    "primaryContainer": "#E9DDFF",
    "onPrimaryContainer": "#4D3D75",
    "inversePrimary": "#CFBDFE",
    "secondary": "#625B71",
    "onSecondary": "#FFFFFF",
    "secondaryContainer": "#E8DEF8",
    "onSecondaryContainer": "#4A4458",
    "tertiary": "#7E5260",
    "onTertiary": "#FFFFFF",
    "tertiaryContainer": "#FFD9E3",
    "onTertiaryContainer": "#633B48",
    "error": "#BA1A1A",
    "onError": "#FFFFFF",
    "errorContainer": "#FFDAD6",
    "onErrorContainer": "#93000A",
    "primaryFixed": "#E9DDFF",
    "primaryFixedDim": "#CFBDFE",
    "onPrimaryFixed": "#201047",
    "onPrimaryFixedVariant": "#4D3D75",
    "secondaryFixed": "#E8DEF8",
    "secondaryFixedDim": "#CBC2DB",
    "onSecondaryFixed": "#1E192B",
    "onSecondaryFixedVariant": "#4A4458",
    "tertiaryFixed": "#FFD9E3",
    "tertiaryFixedDim": "#EFB8C8",
    "onTertiaryFixed": "#31101D",
    "onTertiaryFixedVariant": "#633B48",
    "sucesso": "#006D43",
    "onSucesso": "#FFFFFF",
    "sucessoContainer": "#92F7BC",
    "onSucessoContainer": "#002111",
    "aviso": "#AD3212",
    "onAviso": "#FFFFFF",
    "avisoContainer": "#FFDAD2",
    "onAvisoContainer": "#3C0700"
  },
  "dark": {
    "background": "#141218",
    "onBackground": "#E6E0E9",
    "surface": "#141218",
    "surfaceDim": "#141218",
    "surfaceBright": "#3B383E",
    "surfaceContainerLowest": "#0F0D13",
    "surfaceContainerLow": "#1D1B20",
    "surfaceContainer": "#211F24",
    "surfaceContainerHigh": "#2B292F",
    "surfaceContainerHighest": "#36343A",
    "onSurface": "#E6E0E9",
    "surfaceVariant": "#49454E",
    "onSurfaceVariant": "#CAC4CF",
    "inverseSurface": "#E6E0E9",
    "inverseOnSurface": "#322F35",
    "outline": "#948F99",
    "outlineVariant": "#49454E",
    "shadow": "#000000",
    "scrim": "#000000",
    "surfaceTint": "#CFBDFE",
    "primary": "#CFBDFE",
    "onPrimary": "#36275D",
    "primaryContainer": "#4D3D75",
    "onPrimaryContainer": "#E9DDFF",
    "inversePrimary": "#65558F",
    "secondary": "#CBC2DB",
    "onSecondary": "#332D41",
    "secondaryContainer": "#4A4458",
    "onSecondaryContainer": "#E8DEF8",
    "tertiary": "#EFB8C8",
    "onTertiary": "#4A2532",
    "tertiaryContainer": "#633B48",
    "onTertiaryContainer": "#FFD9E3",
    "error": "#FFB4AB",
    "onError": "#690005",
    "errorContainer": "#93000A",
    "onErrorContainer": "#FFDAD6",
    "primaryFixed": "#E9DDFF",
    "primaryFixedDim": "#CFBDFE",
    "onPrimaryFixed": "#201047",
    "onPrimaryFixedVariant": "#4D3D75",
    "secondaryFixed": "#E8DEF8",
    "secondaryFixedDim": "#CBC2DB",
    "onSecondaryFixed": "#1E192B",
    "onSecondaryFixedVariant": "#4A4458",
    "tertiaryFixed": "#FFD9E3",
    "tertiaryFixedDim": "#EFB8C8",
    "onTertiaryFixed": "#31101D",
    "onTertiaryFixedVariant": "#633B48",
    "sucesso": "#76DAA1",
    "onSucesso": "#003920",
    "sucessoContainer": "#005231",
    "onSucessoContainer": "#92F7BC",
    "aviso": "#FFB4A2",
    "onAviso": "#621200",
    "avisoContainer": "#8A1D00",
    "onAvisoContainer": "#FFDAD2"
  },
  "lightHighContrast": {
    "background": "#FDF7FF",
    "onBackground": "#1D1B20",
    "surface": "#FDF7FF",
    "surfaceDim": "#BCB7BF",
    "surfaceBright": "#FDF7FF",
    "surfaceContainerLowest": "#FFFFFF",
    "surfaceContainerLow": "#F5EFF7",
    "surfaceContainer": "#E6E0E9",
    "surfaceContainerHigh": "#D8D2DA",
    "surfaceContainerHighest": "#CAC5CC",
    "onSurface": "#000000",
    "surfaceVariant": "#E7E0EB",
    "onSurfaceVariant": "#000000",
    "inverseSurface": "#322F35",
    "inverseOnSurface": "#FFFFFF",
    "outline": "#2E2B33",
    "outlineVariant": "#4B4851",
    "shadow": "#000000",
    "scrim": "#000000",
    "surfaceTint": "#65558F",
    "primary": "#312259",
    "onPrimary": "#FFFFFF",
    "primaryContainer": "#4F4078",
    "onPrimaryContainer": "#FFFFFF",
    "inversePrimary": "#CFBDFE",
    "secondary": "#2F293C",
    "onSecondary": "#FFFFFF",
    "secondaryContainer": "#4C465B",
    "onSecondaryContainer": "#FFFFFF",
    "tertiary": "#45212E",
    "onTertiary": "#FFFFFF",
    "tertiaryContainer": "#663D4B",
    "onTertiaryContainer": "#FFFFFF",
    "error": "#600004",
    "onError": "#FFFFFF",
    "errorContainer": "#98000A",
    "onErrorContainer": "#FFFFFF",
    "primaryFixed": "#4F4078",
    "primaryFixedDim": "#382960",
    "onPrimaryFixed": "#FFFFFF",
    "onPrimaryFixedVariant": "#FFFFFF",
    "secondaryFixed": "#4C465B",
    "secondaryFixedDim": "#353043",
    "onSecondaryFixed": "#FFFFFF",
    "onSecondaryFixedVariant": "#FFFFFF",
    "tertiaryFixed": "#663D4B",
    "tertiaryFixedDim": "#4C2734",
    "onTertiaryFixed": "#FFFFFF",
    "onTertiaryFixedVariant": "#FFFFFF",
    "sucesso": "#006D43",
    "onSucesso": "#FFFFFF",
    "sucessoContainer": "#92F7BC",
    "onSucessoContainer": "#002111",
    "aviso": "#AD3212",
    "onAviso": "#FFFFFF",
    "avisoContainer": "#FFDAD2",
    "onAvisoContainer": "#3C0700"
  },
  "darkHighContrast": {
    "background": "#141218",
    "onBackground": "#E6E0E9",
    "surface": "#141218",
    "surfaceDim": "#141218",
    "surfaceBright": "#524F55",
    "surfaceContainerLowest": "#000000",
    "surfaceContainerLow": "#211F24",
    "surfaceContainer": "#322F35",
    "surfaceContainerHigh": "#3D3A41",
    "surfaceContainerHighest": "#48464C",
    "onSurface": "#FFFFFF",
    "surfaceVariant": "#49454E",
    "onSurfaceVariant": "#FFFFFF",
    "inverseSurface": "#E6E0E9",
    "inverseOnSurface": "#000000",
    "outline": "#F4EEF9",
    "outlineVariant": "#C6C0CB",
    "shadow": "#000000",
    "scrim": "#000000",
    "surfaceTint": "#CFBDFE",
    "primary": "#F5EDFF",
    "onPrimary": "#000000",
    "primaryContainer": "#CBB9FA",
    "onPrimaryContainer": "#0F0033",
    "inversePrimary": "#4E3F77",
    "secondary": "#F5EDFF",
    "onSecondary": "#000000",
    "secondaryContainer": "#C8BFD8",
    "onSecondaryContainer": "#0D081A",
    "tertiary": "#FFEBEF",
    "onTertiary": "#000000",
    "tertiaryContainer": "#EBB4C4",
    "onTertiaryContainer": "#1D020D",
    "error": "#FFECE9",
    "onError": "#000000",
    "errorContainer": "#FFAEA4",
    "onErrorContainer": "#220001",
    "primaryFixed": "#E9DDFF",
    "primaryFixedDim": "#CFBDFE",
    "onPrimaryFixed": "#000000",
    "onPrimaryFixedVariant": "#16033D",
    "secondaryFixed": "#E8DEF8",
    "secondaryFixedDim": "#CBC2DB",
    "onSecondaryFixed": "#000000",
    "onSecondaryFixedVariant": "#130E20",
    "tertiaryFixed": "#FFD9E3",
    "tertiaryFixedDim": "#EFB8C8",
    "onTertiaryFixed": "#000000",
    "onTertiaryFixedVariant": "#240613",
    "sucesso": "#76DAA1",
    "onSucesso": "#003920",
    "sucessoContainer": "#005231",
    "onSucessoContainer": "#92F7BC",
    "aviso": "#FFB4A2",
    "onAviso": "#621200",
    "avisoContainer": "#8A1D00",
    "onAvisoContainer": "#FFDAD2"
  }
} as const;

export const typeface = {
  "brand": "Roboto, system-ui, -apple-system, \"Segoe UI\", \"Helvetica Neue\", Arial, sans-serif",
  "plain": "Roboto, system-ui, -apple-system, \"Segoe UI\", \"Helvetica Neue\", Arial, sans-serif"
} as const;

/** Escala tipográfica em px (size, lineHeight, tracking). */
export const typescale = {
  "displayLarge": {
    "font": "brand",
    "size": 57,
    "lineHeight": 64,
    "tracking": -0.25,
    "weight": 400
  },
  "displayMedium": {
    "font": "brand",
    "size": 45,
    "lineHeight": 52,
    "tracking": 0,
    "weight": 400
  },
  "displaySmall": {
    "font": "brand",
    "size": 36,
    "lineHeight": 44,
    "tracking": 0,
    "weight": 400
  },
  "headlineLarge": {
    "font": "brand",
    "size": 32,
    "lineHeight": 40,
    "tracking": 0,
    "weight": 400
  },
  "headlineMedium": {
    "font": "brand",
    "size": 28,
    "lineHeight": 36,
    "tracking": 0,
    "weight": 400
  },
  "headlineSmall": {
    "font": "brand",
    "size": 24,
    "lineHeight": 32,
    "tracking": 0,
    "weight": 400
  },
  "titleLarge": {
    "font": "brand",
    "size": 22,
    "lineHeight": 28,
    "tracking": 0,
    "weight": 400
  },
  "titleMedium": {
    "font": "plain",
    "size": 16,
    "lineHeight": 24,
    "tracking": 0.15,
    "weight": 500
  },
  "titleSmall": {
    "font": "plain",
    "size": 14,
    "lineHeight": 20,
    "tracking": 0.1,
    "weight": 500
  },
  "bodyLarge": {
    "font": "plain",
    "size": 16,
    "lineHeight": 24,
    "tracking": 0.5,
    "weight": 400
  },
  "bodyMedium": {
    "font": "plain",
    "size": 14,
    "lineHeight": 20,
    "tracking": 0.25,
    "weight": 400
  },
  "bodySmall": {
    "font": "plain",
    "size": 12,
    "lineHeight": 16,
    "tracking": 0.4,
    "weight": 400
  },
  "labelLarge": {
    "font": "plain",
    "size": 14,
    "lineHeight": 20,
    "tracking": 0.1,
    "weight": 500
  },
  "labelMedium": {
    "font": "plain",
    "size": 12,
    "lineHeight": 16,
    "tracking": 0.5,
    "weight": 500
  },
  "labelSmall": {
    "font": "plain",
    "size": 11,
    "lineHeight": 16,
    "tracking": 0.5,
    "weight": 500
  }
} as const;

export const shape = {
  "none": 0,
  "extraSmall": 4,
  "small": 8,
  "medium": 12,
  "large": 16,
  "largeIncreased": 20,
  "extraLarge": 28,
  "extraLargeIncreased": 32,
  "extraExtraLarge": 48,
  "full": 9999
} as const;
export const elevation = {
  "level0": 0,
  "level1": 1,
  "level2": 3,
  "level3": 6,
  "level4": 8,
  "level5": 12
} as const;
export const spacing = {
  "0": 0,
  "1": 4,
  "2": 8,
  "3": 12,
  "4": 16,
  "5": 20,
  "6": 24,
  "8": 32,
  "10": 40,
  "12": 48,
  "14": 56,
  "16": 64,
  "20": 80,
  "24": 96
} as const;
export const layout = {
  "paneSpacer": 24,
  "contentMaxWidth": 1040,
  "touchTarget": 48,
  "iconSize": 24
} as const;
export const motion = {
  "easing": {
    "standard": [
      0.2,
      0,
      0,
      1
    ],
    "standardAccelerate": [
      0.3,
      0,
      1,
      1
    ],
    "standardDecelerate": [
      0,
      0,
      0,
      1
    ],
    "emphasized": [
      0.2,
      0,
      0,
      1
    ],
    "emphasizedAccelerate": [
      0.3,
      0,
      0.8,
      0.15
    ],
    "emphasizedDecelerate": [
      0.05,
      0.7,
      0.1,
      1
    ]
  },
  "duration": {
    "short1": 50,
    "short2": 100,
    "short3": 150,
    "short4": 200,
    "medium1": 250,
    "medium2": 300,
    "medium3": 350,
    "medium4": 400,
    "long1": 450,
    "long2": 500,
    "long3": 550,
    "long4": 600,
    "extraLong1": 700,
    "extraLong2": 800,
    "extraLong3": 900,
    "extraLong4": 1000
  }
} as const;
export const state = {
  "hoverOpacity": 0.08,
  "focusOpacity": 0.1,
  "pressedOpacity": 0.1,
  "draggedOpacity": 0.16,
  "disabledContentOpacity": 0.38,
  "disabledContainerOpacity": 0.12,
  "focusIndicatorThickness": 3,
  "focusIndicatorOffset": 2
} as const;
