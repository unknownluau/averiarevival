import fs from 'fs';
import path from 'path';
import pkg from '../package.json';

const configPath = path.join(process.cwd(), 'config.json');

const readConfig = () => {
  if (!fs.existsSync(configPath)) {
    throw new Error('Configuration could not be found at location: ' + configPath);
  }

  return JSON.parse(fs.readFileSync(configPath, 'utf-8'));
};

const config = readConfig();

export const serverRuntimeConfig = config.serverRuntimeConfig || {};
export const publicRuntimeConfig = {
  ...(config.publicRuntimeConfig || {}),
  frontendVer: pkg.version,
};

export default {
  serverRuntimeConfig,
  publicRuntimeConfig,
};
