import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'GPX Utility Analyzer',
  tagline: "Suite d'outils pour l'analyse de fichiers GPX",
  favicon: 'img/favicon.ico',

  url: 'https://jchable.github.io',
  baseUrl: '/gpx-utility-analyzer/',
  organizationName: 'jchable',
  projectName: 'gpx-utility-analyzer',
  trailingSlash: false,

  onBrokenLinks: 'throw',

  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'warn',
    },
  },

  i18n: {
    defaultLocale: 'fr',
    locales: ['fr'],
    localeConfigs: {
      fr: {
        label: 'Français',
        htmlLang: 'fr-FR',
      },
    },
  },

  presets: [
    [
      'classic',
      {
        docs: {
          path: 'content',
          routeBasePath: 'docs',
          sidebarPath: './sidebars.ts',
          editUrl: ({docPath}) => {
            // Remap generated docs back to their source in sub-projects
            if (docPath.startsWith('cli/')) {
              const file = docPath.replace('cli/index.md', 'README.md').replace('cli/cli-usage.md', 'docs/CLI_USAGE.md');
              return `https://github.com/jchable/gpx-utility-analyzer/edit/main/cli/${file}`;
            }
            if (docPath.startsWith('ai-analyzer/')) {
              const file = docPath.replace('ai-analyzer/index.md', 'README.md');
              return `https://github.com/jchable/gpx-utility-analyzer/edit/main/ai-analyzer/${file}`;
            }
            return `https://github.com/jchable/gpx-utility-analyzer/edit/main/docs/content/${docPath}`;
          },
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    navbar: {
      title: 'GPX Utility Analyzer',
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'mainSidebar',
          position: 'left',
          label: 'Documentation',
        },
        {
          href: 'https://github.com/jchable/gpx-utility-analyzer',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Projets',
          items: [
            {label: 'gpx-analyzer (CLI)', to: '/docs/cli'},
            {label: 'gpx-ai-analyzer (.NET)', to: '/docs/ai-analyzer'},
          ],
        },
        {
          title: 'Code source',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/jchable/gpx-utility-analyzer',
            },
          ],
        },
      ],
      copyright: 'GPX Utility Analyzer',
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
