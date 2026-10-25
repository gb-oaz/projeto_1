// ESLint do projeto web (03_ci-qualidade RNF-17): o CI roda com
// --max-warnings 0. Além das regras de Vue e TypeScript, reprova import que
// viole as dependências do core (14_estrutura-projetos RNF-182):
//   RN-146  um contexto importa só core/commons/, nunca outro contexto;
//   RN-147  o core não importa os apps, e só components/ usa o framework de
//           interface (Vue).
import pluginVue from 'eslint-plugin-vue';
import skipFormatting from '@vue/eslint-config-prettier/skip-formatting';
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript';

const contextos = ['landing_page', 'hub', 'quest'];

// Imports proibidos no core/<contexto>/: os apps e os outros contextos (para
// commons/, todos), pelo alias @core ou por caminho relativo.
function proibidos(contexto) {
  const outros = contextos.filter((c) => c !== contexto);
  return [
    { group: ['**/apps/**'], message: 'o core não importa os apps (RN-147)' },
    {
      group: outros.flatMap((c) => [`@core/${c}`, `@core/${c}/**`, `**/${c}/**`]),
      message: 'um contexto importa só core/commons/ (RN-146)',
    },
  ];
}

// Fora de components/, o core também não importa o Vue. Um bloco de regras
// substitui o anterior no mesmo arquivo, por isso este repete os padrões.
const regrasDoCore = ['commons', ...contextos].flatMap((contexto) => [
  {
    name: `boaz/core-${contexto}`,
    files: [`core/${contexto}/**/*.{ts,vue}`],
    rules: { 'no-restricted-imports': ['error', { patterns: proibidos(contexto) }] },
  },
  {
    name: `boaz/core-${contexto}-sem-vue`,
    files: [`core/${contexto}/**/*.ts`],
    ignores: [`core/${contexto}/components/**`, `core/${contexto}/*/components/**`],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [{ name: 'vue', message: 'no core, só components/ usa o Vue (RN-147)' }],
          patterns: proibidos(contexto),
        },
      ],
    },
  },
]);

export default defineConfigWithVueTs(
  { ignores: ['dist/**', 'coverage/**', 'core/commons/design/**'] },
  pluginVue.configs['flat/essential'],
  vueTsConfigs.recommended,
  ...regrasDoCore,
  skipFormatting,
);
