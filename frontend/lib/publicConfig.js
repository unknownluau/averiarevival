const fallbackPublicRuntimeConfig = {
  backend: {
    flags: {},
  },
};

const parsePublicRuntimeConfig = () => {
  const rawConfig = process.env.NEXT_PUBLIC_AVERIA_PUBLIC_CONFIG;
  if (typeof rawConfig !== 'string' || rawConfig.length === 0) {
    return fallbackPublicRuntimeConfig;
  }

  try {
    return JSON.parse(rawConfig);
  } catch (e) {
    console.warn('Failed to parse public runtime config', e);
    return fallbackPublicRuntimeConfig;
  }
};

export const publicRuntimeConfig = parsePublicRuntimeConfig();

export default {
  publicRuntimeConfig,
};
