import Download from "../components/download2"
import Theme2016 from "../components/theme2016";
import DownloadStore from "../components/download2/stores/DownloadStore";

const DownloadPage = () => {
    return <Theme2016>
        <DownloadStore.Provider>
            <Download></Download>
        </DownloadStore.Provider>
    </Theme2016>
}

export default DownloadPage;