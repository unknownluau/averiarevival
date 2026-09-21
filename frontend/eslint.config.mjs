import nextVitals from 'eslint-config-next/core-web-vitals';

const config = [
  {
    ignores: [
      '.next/**',
      'node_modules/**',
      'public/js/**',
    ],
  },
  ...nextVitals,
  {
    rules: {
      '@next/next/no-html-link-for-pages': 'off',
      '@next/next/no-sync-scripts': 'off',
      'react/display-name': 'off',
      'react/jsx-key': 'off',
      'react/no-children-prop': 'off',
      'react/no-unescaped-entities': 'off',
      'react-hooks/immutability': 'off',
      'react-hooks/purity': 'off',
      'react-hooks/preserve-manual-memoization': 'off',
      'react-hooks/refs': 'off',
      'react-hooks/rules-of-hooks': 'off',
      'react-hooks/set-state-in-effect': 'off',
      'react-hooks/static-components': 'off',
    },
  },
];

export default config;
