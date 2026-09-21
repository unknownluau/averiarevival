import axios from 'axios';
import { publicRuntimeConfig } from './publicConfig';

let _csrf = '';

const getServerRuntimeConfig = () => {
    if (typeof window !== 'undefined') {
        return {};
    }

    try {
        const nodeRequire = eval('require');
        const fs = nodeRequire('fs');
        const path = nodeRequire('path');
        const configPath = path.join(process.cwd(), 'config.json');
        return JSON.parse(fs.readFileSync(configPath, 'utf-8')).serverRuntimeConfig || {};
    } catch (e) {
        return {};
    }
};

const resolveRequestUrl = (url) => {
    const isBrowser = typeof window !== 'undefined';
    if (isBrowser || typeof url !== 'string' || !url.startsWith('/')) {
        return url;
    }

    const serverRuntimeConfig = getServerRuntimeConfig();
    const baseUrl = serverRuntimeConfig.backend?.baseUrl || publicRuntimeConfig.backend?.baseUrl;
    if (!baseUrl) {
        return url;
    }

    return baseUrl.replace(/\/+$/g, '') + url;
}

/**
 * @param {string} service
 * @param {string} url
 * @returns {string}
 */
export const getFullUrl = (service, url) => {
    return publicRuntimeConfig.backend.apiFormat.replace(/\{0\}/g, service).replace(/\{1\}/g, url);
}

// TODO - neva: make getFullUrlNew the default.
/**
 * @param {string} service
 * @param {string} url
 * @returns {string}
 */
export const getFullUrlNew = (service, url) => {
    return publicRuntimeConfig.backend.apiFormat.replace(/\{0\}/g, service).replace(/\{1\}/g, url);
}

/**
 * @returns {string}
 */
export const getBaseUrl = () => {
    return publicRuntimeConfig.backend.baseUrl;
}

/**
 * @param {string} str
 * @returns {string}
 */
export const getBaseUrl2 = (str) => {
    return publicRuntimeConfig.backend.baseUrl + (str.charAt(0) === '/' ? str : '/' + str);
}

/**
 * @param {string} url
 * @returns {string}
 */
export const getUrlWithProxy = (url) => {
    if (publicRuntimeConfig.backend.proxyEnabled)
        return '/api/proxy?url=' + encodeURIComponent(url);
    return url;
}

/**
 * @param {string} method
 * @param {string} url
 * @param {any?} data
 * @param {boolean?} verbose
 * @param {Record<string,string>?} extraHeaders
 * @returns {Promise<axios.AxiosResponse<any>>}
 */
const request = async (method, url, data, verbose = false, extraHeaders) => {
    const isBrowser = typeof window !== 'undefined';
    try {
        let headers = {
            'x-csrf-token': _csrf,
        }
        if (!isBrowser) {
            const serverRuntimeConfig = getServerRuntimeConfig();
            // Auth header, if required
            const authHeaderValue = serverRuntimeConfig.backend?.authorization;
            if (typeof authHeaderValue === 'string')
                headers[serverRuntimeConfig.backend?.authorizationHeader || 'authorization'] = authHeaderValue;

            // Custom user agent
            headers['user-agent'] = 'Roblox2016/1.0';
        }
        const cfClientId = publicRuntimeConfig.backend.cfClientId;
        const cfClientSecret = publicRuntimeConfig.backend.cfClientSecret;
        if (typeof cfClientId === 'string' && typeof cfClientSecret === 'string') {
            headers['CF-Access-Client-Id'] = cfClientId;
            headers['CF-Access-Client-Secret'] = cfClientSecret;
        }
        if (extraHeaders && typeof extraHeaders === 'object') {
            for (const k of Object.keys(extraHeaders)) {
                headers[k] = extraHeaders[k];
            }
        }

        return await axios.request({
            method,
            url: resolveRequestUrl(getUrlWithProxy(url)),
            data: data,
            headers: headers,
            maxRedirects: 0,
        });
    } catch (e) {
        if (e.response) {
            let resp = e.response;
            console.log(resp.headers)
            if (resp.status === 403 && resp.headers['x-csrf-token']) {
                _csrf = resp.headers['x-csrf-token'];
                return request(method, url, data, verbose, extraHeaders);
            }
        }
        if (isBrowser) {
            // attempt to make errors easier to diagnose
            if (e.response) {
                // check for regular
                if (e.response.data && e.response.data.errors && e.response.data.errors.length) {
                    let err = e.response.data.errors[0]
                    e.message = e.message + ': ' + (err.code + ': ' + err.message);
                    // TODO: confirm this is causing issues
                    if (verbose && Number(String(e.response.status, "Could not parse response status")[0]) !== 5) {
                        return Promise.resolve(e.response);
                    }
                }
            }
            throw e;
        } else {
            throw new Error(e.message);
        }
    }
}

export default request;
