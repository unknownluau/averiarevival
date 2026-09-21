import {createContainer} from "unstated-next";
import {useState} from "react";

const DownloadStore = createContainer(() => {
    const [platform, setPlatform] = useState(DownloadPlatforms.WinNT61Above);

    return ({
        platform, setPlatform,
    })
});

export enum DownloadPlatforms {
    WinNT61Above = 0,
    Linux = 1,
    Android = 2,
    IOS = 3,
}

export function getPlatformName(platform: number) {
    switch (platform) {
        case DownloadPlatforms.WinNT61Above:
            return 'Windows';
        case DownloadPlatforms.Linux:
            return 'Linux';
        case DownloadPlatforms.Android:
            return 'Android';
        case DownloadPlatforms.IOS:
            return 'iOS';
        default:
            return 'Incompatible Device';
    }
}

export default DownloadStore;