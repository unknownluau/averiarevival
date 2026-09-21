import axios, { AxiosHeaders } from 'axios';
const configuredBaseUrl = (window as Window & { ADMIN_API_BASE_URL?: string }).ADMIN_API_BASE_URL;
export const adminApiBaseUrl: string = configuredBaseUrl || 'https://admin.averia.lol/v1/';
let goodCsrf = '';

export function adminApiUrl(path: string): string {
	const cleanPath = path.startsWith('/') ? path.slice(1) : path;
	return new URL(cleanPath, adminApiBaseUrl).toString();
}

const client = axios.create({
	baseURL: adminApiBaseUrl,
	maxRedirects: 0,
	withCredentials: true,
});
client.interceptors.request.use(ok => {
	if (!ok.headers) {
		ok.headers = new AxiosHeaders();
	}
	ok.headers['x-csrf-token'] = goodCsrf;
	return ok;
})
client.interceptors.response.use(undefined, (e) => {
	if (e.isAxiosError && e.response && e.response.headers) {
		if (e.response.headers['x-csrf-token']) {
			goodCsrf = e.response.headers['x-csrf-token'];
			return client.request(e.config);
		}
        if (typeof e.response.data === 'string') {
            e.message = e.response.data;
        }else if (typeof e.response.data === 'object') {
            if (e.response.data.errors && e.response.data.errors.length) {
                let msg = e.response.data.errors[0].message;
                if (msg === 'Unauthorized') {
                    e.message = 'You do not have the proper permissions to perform this action.';
                }else{
                    e.message = msg;
                }
            }
        }
	}

	return Promise.reject(e);
})
export default client;
