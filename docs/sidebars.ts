import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  mainSidebar: [
    'intro',
    {
      type: 'category',
      label: 'gpx-analyzer (.NET CLI)',
      collapsed: false,
      items: [
        'cli/index',
        {
          type: 'category',
          label: 'Commands',
          collapsed: false,
          items: [
            'cli/analyze',
            'cli/benchmark',
            'cli/split',
            'cli/merge',
          ],
        },
        'cli/elevation',
        'cli/biometrics',
        'cli/anomalies',
        'cli/recipes',
      ],
    },
    {
      type: 'category',
      label: 'gpx-ai-analyzer (.NET)',
      collapsed: true,
      items: [
        'ai-analyzer/index',
      ],
    },
  ],
};

export default sidebars;
