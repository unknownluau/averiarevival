import { publicRuntimeConfig } from "./publicConfig";

const getFlag = (flag, defaultValue) => {
  const v = publicRuntimeConfig.backend?.flags?.[flag];
  if (typeof v === 'undefined') return defaultValue;
  return v;
}

export default getFlag;
