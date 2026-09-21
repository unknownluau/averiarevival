const fs = require('fs');
const path = require('path');
const pkg = require('./package.json');
const configPath = path.join(__dirname, 'config.json');
if (!fs.existsSync(configPath)) {
    throw new Error('Configuration could not be found at location: ' + configPath);
}
const config = JSON.parse(fs.readFileSync(configPath).toString('utf-8'));
const publicRuntimeConfig = {
    ...(config.publicRuntimeConfig || {}),
    frontendVer: pkg.version,
};

const withBundleAnalyzer = require('@next/bundle-analyzer')({
    enabled: process.env.ANALYZE === 'true',
    //analyzerMode: 'json', openAnalyzer: false,
});

module.exports = withBundleAnalyzer({
    reactStrictMode: true,
    outputFileTracingRoot: __dirname,
    turbopack: {
        root: __dirname,
    },
    env: {
        NEXT_PUBLIC_AVERIA_PUBLIC_CONFIG: JSON.stringify(publicRuntimeConfig),
    },
    async redirects() {
        return [
            /*{
                source: '/catalog.aspx',
                destination: '/catalog',
                permanent: true,
            },*/
            /*
            {
              source: '/catalog/:id/:name',
              destination: '/redirect-item?id=:id',
              permanent: false,
            },
             */
            {
                source: '/My/Groups.aspx',
                has: [
                    {
                        type: 'query',
                        key: 'gid',
                        value: '(?<id>.*)',
                    },
                ],
                destination: '/groups/:id/--',
                permanent: true,
            },
            {
                source: '/internal/create-place',
                destination: '/places/create',
                permanent: true,
            },
            {
                source: '/support',
                destination: 'https://support.averia.one/',
                permanent: true,
            },
            // {
            //     source: '/donate/stripe',
            //     destination: 'https://buy.stripe.com/3cI6oI9dobzAeVlbLw2Ji04',
            //     permanent: false,
            // },
            // {
            //     source: '/donate/ko-fi',
            //     destination: 'https://ko-fi.com/oldroblox',
            //     permanent: false,
            // },
        ]
    }
})
