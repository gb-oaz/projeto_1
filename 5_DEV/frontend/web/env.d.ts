/// <reference types="vite/client" />

// Núcleos do design system (cópias geradas em core/commons/design/, em JS):
// só o que as telas usam, para o TypeScript conferir as chamadas.
declare module '@core/commons/design/marca.mjs' {
  type Variante = 'longo' | 'curto';
  export const FRENTES: { landing: Variante; hub: Variante; quest: Variante };
  export const marca: {
    logo(variante?: Variante, cores?: Record<string, string>): string;
    simbolo(variante?: Variante, cor?: string | Record<string, string>): string;
  };
}
declare module '@core/commons/design/mascotes.mjs' {
  type Opcoes = { forma?: string; proporcao?: string; cor?: string; movimento?: string | string[] };
  export function animado(expressao: string, op?: Opcoes): string;
  export function estatico(expressao: string, op?: Opcoes): string;
}
declare module '@core/commons/design/fundos.mjs' {
  export const PAPEIS: string[];
  export const fundos: { desenhar(conceito: string, op?: Record<string, unknown>): string };
}
