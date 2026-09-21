import { ok as assert } from 'assert';

interface IWebsiteConfiguration {
	authorization: string;
	baseUrl: string;
	rccPort: number;
	port: number;
	thumbnailWebsocketPort: number;
	rcc?: string;
	content?: string;
	dockerDisabled?: boolean;
	websiteBotAuth: string;
}

function loadConfig(): IWebsiteConfiguration {
	let fileConfig: Partial<IWebsiteConfiguration> = {};

	try {
		const fs = require('fs');
		const path = require('path');
		const filePath = path.join(__dirname, '../../config.json');
		fileConfig = JSON.parse(fs.readFileSync(filePath).toString());
	} catch {
		// config.json is optional when environment variables are provided
	}

	const getStr = (envKey: string, fileVal?: string): string =>
		process.env[envKey] ?? fileVal ?? '';

	const getInt = (envKey: string, fileVal?: number): number => {
		const raw = process.env[envKey];
		return raw !== undefined ? parseInt(raw, 10) : fileVal ?? 0;
	};

	const getBool = (envKey: string, fileVal?: boolean): boolean => {
		const raw = process.env[envKey];
		if (raw !== undefined) return raw.toLowerCase() === 'true';
		return fileVal ?? false;
	};

	console.log('[env] AUTHORIZATION:', JSON.stringify(process.env.AUTHORIZATION));

	return {
		authorization:            getStr('AUTHORIZATION',              fileConfig.authorization),
		baseUrl:                  getStr('BASE_URL',                   fileConfig.baseUrl),
		rccPort:                  getInt('RCC_PORT',                   fileConfig.rccPort),
		port:                     getInt('PORT',                       fileConfig.port),
		thumbnailWebsocketPort:   getInt('THUMBNAIL_WEBSOCKET_PORT',   fileConfig.thumbnailWebsocketPort),
		websiteBotAuth:           getStr('WEBSITE_BOT_AUTH',           fileConfig.websiteBotAuth),
		rcc:                      process.env.RCC                   ?? fileConfig.rcc,
		content:                  process.env.CONTENT               ?? fileConfig.content,
		dockerDisabled:           getBool('DOCKER_DISABLED',           fileConfig.dockerDisabled),
	};
}

const conf: Readonly<IWebsiteConfiguration> = loadConfig();
export default conf;

assert(typeof conf.authorization === 'string' && conf.authorization.length > 0,         'authorization is missing or empty');
assert(typeof conf.baseUrl === 'string' && conf.baseUrl.length > 0,                     'baseUrl is missing or empty');
assert(typeof conf.rccPort === 'number' && !isNaN(conf.rccPort) && conf.rccPort > 0,    'rccPort is missing or invalid');
assert(typeof conf.port === 'number' && !isNaN(conf.port) && conf.port > 0,             'port is missing or invalid');
assert(typeof conf.websiteBotAuth === 'string' && conf.websiteBotAuth.length > 0,       'websiteBotAuth is missing or empty');
assert(typeof conf.thumbnailWebsocketPort === 'number' && !isNaN(conf.thumbnailWebsocketPort) && conf.thumbnailWebsocketPort > 0, 'thumbnailWebsocketPort is missing or invalid');
if (typeof conf.rcc !== 'undefined') {
    assert(typeof conf.rcc === 'string' && conf.rcc.length > 0,                         'rcc is defined but empty');
}