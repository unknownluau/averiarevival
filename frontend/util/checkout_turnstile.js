let scriptPromise = null;
let widgetId = null;
let widgetSiteKey = null;
let pendingResolve = null;
let pendingReject = null;
let containerEl = null;

let cachedToken = null;
let cachedTokenAt = 0;
const TOKEN_MAX_AGE_MS = 240_000;

const TURNSTILE_SRC = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';

const loadScript = () => {
  if (typeof window === 'undefined') {
    return Promise.reject(new Error('Could not verify purchase'));
  }
  if (window.turnstile) return Promise.resolve();
  if (scriptPromise) return scriptPromise;
  scriptPromise = new Promise((resolve, reject) => {
    const existing = document.querySelector('script[src^="https://challenges.cloudflare.com/turnstile/v0/api.js"]');
    if (existing) {
      if (window.turnstile) return resolve();
      existing.addEventListener('load', () => resolve());
      existing.addEventListener('error', () => reject(new Error('Could not verify purchase')));
      const poll = setInterval(() => {
        if (window.turnstile) {
          clearInterval(poll);
          resolve();
        }
      }, 50);
      setTimeout(() => clearInterval(poll), 10000);
      return;
    }
    const s = document.createElement('script');
    s.src = TURNSTILE_SRC;
    s.async = true;
    s.defer = true;
    s.onload = () => resolve();
    s.onerror = () => reject(new Error('Could not verify purchase'));
    document.head.appendChild(s);
  });
  return scriptPromise;
};

const waitForGlobal = async () => {
  const start = Date.now();
  while (!window.turnstile || typeof window.turnstile.render !== 'function') {
    if (Date.now() - start > 8000) throw new Error('Could not verify purchase');
    await new Promise((r) => setTimeout(r, 30));
  }
};

const ensureContainer = () => {
  if (containerEl && document.body.contains(containerEl)) return containerEl;
  containerEl = document.createElement('div');
  containerEl.setAttribute('aria-hidden', 'true');
  containerEl.style.cssText = 'position:absolute;left:-9999px;top:-9999px;width:0;height:0;overflow:hidden;pointer-events:none;';
  document.body.appendChild(containerEl);
  return containerEl;
};

const handleToken = (token) => {
  cachedToken = token;
  cachedTokenAt = Date.now();
  const r = pendingResolve;
  pendingResolve = null;
  pendingReject = null;
  if (r) r(token);
};

const handleError = () => {
  cachedToken = null;
  cachedTokenAt = 0;
  const j = pendingReject;
  pendingResolve = null;
  pendingReject = null;
  if (j) j(new Error('Could not verify purchase'));
};

const ensureWidget = (siteKey) => {
  if (widgetId !== null && widgetSiteKey === siteKey) return widgetId;
  const c = ensureContainer();
  widgetId = window.turnstile.render(c, {
    sitekey: siteKey,
    appearance: 'execute',
    execution: 'execute',
    callback: handleToken,
    'error-callback': handleError,
    'timeout-callback': handleError,
  });
  widgetSiteKey = siteKey;
  return widgetId;
};

export const prewarmTurnstile = async (siteKey) => {
  if (!siteKey || typeof window === 'undefined') return;
  try {
    await loadScript();
    await waitForGlobal();
    ensureWidget(siteKey);
  } catch (e) {}
};

export const prefetchTurnstileToken = async (siteKey) => {
  if (!siteKey || typeof window === 'undefined') return null;
  try {
    if (cachedToken && Date.now() - cachedTokenAt < TOKEN_MAX_AGE_MS) return cachedToken;
    await prewarmTurnstile(siteKey);
    return new Promise((resolve) => {
      pendingResolve = (t) => resolve(t);
      pendingReject = () => resolve(null);
      try { window.turnstile.reset(widgetId); } catch (e) {}
      try { window.turnstile.execute(widgetId); } catch (e) { resolve(null); }
      setTimeout(() => resolve(cachedToken || null), 12000);
    });
  } catch (e) {
    return null;
  }
};

export const getInvisibleTurnstileToken = async (siteKey) => {
  if (!siteKey) throw new Error('Could not verify purchase');
  if (cachedToken && Date.now() - cachedTokenAt < TOKEN_MAX_AGE_MS) {
    const t = cachedToken;
    cachedToken = null;
    cachedTokenAt = 0;
    return t;
  }
  await loadScript();
  await waitForGlobal();
  ensureWidget(siteKey);
  if (pendingReject) {
    try { pendingReject(new Error('Could not verify purchase')); } catch (e) {}
  }
  const tokenPromise = new Promise((resolve, reject) => {
    pendingResolve = (t) => {
      cachedToken = null;
      cachedTokenAt = 0;
      resolve(t);
    };
    pendingReject = reject;
  });
  try { window.turnstile.reset(widgetId); } catch (e) {}
  try {
    window.turnstile.execute(widgetId);
  } catch (e) {
    throw new Error('Could not verify purchase');
  }
  const overall = new Promise((_, reject) => {
    setTimeout(() => reject(new Error('Could not verify purchase')), 15000);
  });
  return Promise.race([tokenPromise, overall]);
};

export default getInvisibleTurnstileToken;
