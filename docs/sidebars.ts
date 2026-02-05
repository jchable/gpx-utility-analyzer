import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  mainSidebar: [
    'intro',
    {
      type: 'category',
      label: 'gpx-analyzer (CLI Go)',
      collapsed: false,
      items: [
        'cli/index',
        'cli/cli-usage',
      ],
    },
    {
      type: 'category',
      label: 'gpx-ai-analyzer (.NET)',
      collapsed: true,
      items: [
        'dotnet/index',
      ],
    },
  ],
};

export default sidebars;
