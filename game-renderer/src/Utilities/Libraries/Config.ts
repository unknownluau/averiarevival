import * as path from "path";
import * as fs from "fs";

export interface IConfig {
    BaseUrl: string;
    RCCUrl: string;
    Debug: boolean;
    Ports: {
        Process: number;
        RCC: {
            All: number[];
            Player: number[];
            Image: number[];
            Place: number[];
            Catalog: number[];
        };
    };
    Paths: {
        RCCService: string;
    };
}

function parsePortList(envKey: string, fallback: number[]): number[] {
    const raw = process.env[envKey];
    if (raw !== undefined) {
        return raw.split(',').map(p => parseInt(p.trim(), 10));
    }
    return fallback ?? [];
}

function loadConfig(): IConfig {
    let file: Partial<IConfig> = {};

    try {
        const filePath = path.join(path.resolve(process.cwd()), '/config.json');
        file = JSON.parse(fs.readFileSync(filePath).toString()) as IConfig;
    } catch {
        // config.json is optional when environment variables are provided
    }

    const baseUrl = process.env.BASE_URL ?? file.BaseUrl ?? '';
    const rccUrl = process.env.RCC_URL ?? file.RCCUrl ?? '';

    return {
        BaseUrl: baseUrl.trim().replace(/\/+$/, ''),
        RCCUrl:  rccUrl.trim().replace(/\/+$/, ''),
        Debug: process.env.DEBUG !== undefined 
            ? process.env.DEBUG.toLowerCase() === 'true' 
            : file.Debug ?? false,        
        Ports: {
            Process: process.env.PORT_PROCESS !== undefined
                ? parseInt(process.env.PORT_PROCESS, 10)
                : file.Ports?.Process ?? 0,
            RCC: {
                All:     parsePortList('PORT_RCC_ALL',     file.Ports?.RCC?.All     ?? []),
                Player:  parsePortList('PORT_RCC_PLAYER',  file.Ports?.RCC?.Player  ?? []),
                Image:   parsePortList('PORT_RCC_IMAGE',   file.Ports?.RCC?.Image   ?? []),
                Place:   parsePortList('PORT_RCC_PLACE',   file.Ports?.RCC?.Place   ?? []),
                Catalog: parsePortList('PORT_RCC_CATALOG', file.Ports?.RCC?.Catalog ?? []),
            },
        },
        Paths: {
            RCCService: process.env.PATH_RCC_SERVICE ?? file.Paths?.RCCService ?? '',
        },
    };
}

export const Config: IConfig = loadConfig();
export default Config;
