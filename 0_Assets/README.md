# 0_Assets

Recursos reutilizáveis do projeto e dos diagramas.

```
0_Assets/
├── projeto/      # marca, logos, imagens, fontes, mockups — usados no produto e nos documentos
├── requisitos/   # tema.html: marca d'água, estilo, formato e componentes dos documentos de requisito
└── diagramas/    # tema.svg (estilo padrão), ícones de cloud/tecnologias, logos de integrações
```

- Prefira `.svg`; raster (`.png`, `.jpg`, `.webp`) só quando não houver vetor.
- Nomes em kebab-case, sem acentos: `funcao-serverless.svg`, `logo-principal.svg`.
- `diagramas/icones/` traz os ícones oficiais de AWS, GCP e Azure (instalados pelo
  `./boaz.sh init`, veja o README da pasta). Use-os só quando a arquitetura for
  de um provedor específico; por padrão ela é neutra e vale para Azure, AWS e GCP.
- As skills de diagrama procuram aqui antes de desenhar e **embutem** o asset no SVG
  gerado (o diagrama continua autocontido e renderiza em qualquer lugar).
- `diagramas/tema.svg` define o visual de todos os diagramas: edite-o para mudar
  cores e tipografia dos próximos diagramas (os antigos não mudam).
- `requisitos/tema.html` define o padrão dos documentos de `1_Requisitos/` (marca
  d'água, estilo, formato e componentes). Abra-o no navegador para ver o catálogo;
  para mudar o visual use a skill `/requisito-estilo` — cada documento adota o
  tema novo quando for atualizado por ela.
