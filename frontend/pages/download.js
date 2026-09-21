import Download from "../components/download"
import getFlag from "../lib/getFlag";

const DownloadPage = () => {
    if (!getFlag('downloadPageEnabled', false)) return null;
    return <Download></Download>
}

DownloadPage.getInitialProps = () => {
    return {
        title: 'Download - Averia',
    }
}

export default DownloadPage;