import {createUseStyles} from "react-jss";
import { getTheme } from "../../services/theme";
import { useEffect, useState } from "react";
import DownloadStore, {DownloadPlatforms} from "./stores/DownloadStore";
import DownloadDropdown from "./components/DownloadDropdown";

const useStyles = createUseStyles({
    container: {
        maxWidth: 'calc(1154px + 165px)!important',
        "@media(max-width: 1380px)": {
            maxWidth: '1154px!important',
        },
        "@media(max-width: 1200px)": {
            maxWidth: '992px!important',
        },
        "@media(max-width: 1030px)": {
            maxWidth: '768px!important',
        },
    },
    containerWrapper: {
        width: 'calc(100% - 165px)',
        marginLeft: 'auto',
        "@media(max-width: 1380px)": {
            width: '100%',
            margin: 0,
        },
    },
    wrapper: {
        width: '100%',
        display: 'flex',
        flexDirection: 'column',
    },
    header: {},
    subHeader: {},
    headerBg: {
        background: 'url(/UnsecuredContent/ThumbnailSignUp.png) repeat center center',
        backgroundSize: 'cover',
        height: '500px',
        position: 'relative',
    },
    playAveriaText: {
        aspectRatio: '981 / 198',
        width: '50%',
    },
    playAveriaImg: {
        background: 'url(/img/play-averia.png) no-repeat center center',
        backgroundSize: 'contain',
        width: '100%',
        height: '100%',
        display: 'inline-block',
    },
    downloadContainer: {
        width: 'calc(50% - 11px)',
        marginRight: '10px',
    },
    infoContainer: {
        width: 'calc(50% - 11px)',
        marginLeft: '10px',
    },
    divider: {
        height: '90%',
        margin: 'auto 0',
        width: 2,
        background: 'var(--primary-color)',
        borderRadius: '50%',
    },
});

function DownloadPage() {
    const s = useStyles({theme: getTheme()});
    const { platform, setPlatform } = DownloadStore.useContainer();

    useEffect(() => {
        if (typeof navigator === 'undefined') {
            return;
        }
        const ua = navigator.userAgent.toLowerCase();
        if (ua.includes("windows nt")) {
            setPlatform(DownloadPlatforms.WinNT61Above);
        } else if (ua.includes("linux") && ua.includes("x86")) {
            setPlatform(DownloadPlatforms.Linux);
        } else if (ua.includes("android")) {
            setPlatform(DownloadPlatforms.Android);
        } else if (ua.includes("iphone")) {
            setPlatform(DownloadPlatforms.IOS);
        }
    }, []);
    
    return <div className={`container ${s.container} padding-none`}>
        <div className={s.containerWrapper}>
            <div className={`${s.wrapper} section-content padding-none`}>
                <div className={`flex ${s.headerBg} w-100`}>
                    <div className={`flex flex-column justify-center ${s.downloadContainer}`}>
                        <div className={s.playAveriaText}>
                            <span className={s.playAveriaImg} />
                        </div>
                        <DownloadDropdown />
                    </div>
                    <span className={`${s.divider}`} />
                    <div className={`flex flex-column justify-content ${s.infoContainer}`}>
                        INFORMATIONS
                    </div>
                </div>
                <div className={`flex flex-column padding-15`}>
                    <h1 className={s.header}>Get Averia</h1>
                    <h3 className={s.subHeader}>Join the fun and play Averia today! Download and install the Averia app for your device.</h3>
                </div>
            </div>
            <div className={`${s.wrapper} section-content`}>
                <h1 className={s.header}>Frequently Asked Questions</h1>
                <h3 className={s.subHeader}>Below are some frequently asked questions by users during or before the installation process. If none of these help you, you may make a support ticket or ask for help in the Discord.</h3>
            </div>
        </div>
    </div>
}

export default DownloadPage;
