import {useState} from "react";
import {createUseStyles} from "react-jss";
import DownloadStore, {DownloadPlatforms, getPlatformName} from "../stores/DownloadStore";

const useStyles = createUseStyles({
    btnContainer: {},
    dropdownContainer: {},
});

const DownloadDropdown = () => {
    const s = useStyles();
    const [asd, setAsd] = useState(false);
    const store = DownloadStore.useContainer();

    return <div className={`${s.btnContainer}`}>
        <a className={s.labelLink}>
            <span>{getPlatformName(store.platform)}</span>
        </a>
        <div className={s.dropdownContainer}>
            <span className={`${s.dropdownLabel}`}></span>
            <div className={`${s.dropdown}`}>
                {Object.values(DownloadPlatforms).map(v => {
                    return <a key={v} className={`${s.dropdownItem}`} onClick={() => {
                        store.setPlatform(v);
                    }}>{getPlatformName(v)}</a>
                })}
            </div>
        </div>
    </div>
};

export default DownloadDropdown;