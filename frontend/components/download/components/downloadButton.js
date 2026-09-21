import { createUseStyles } from "react-jss";
import {getTheme} from "../../../services/theme";

const useStyles = createUseStyles({
    image: {
        width: '100%',
        height: '100px',
        maxWidth: '150px',
        maxHeight: '150px',
        margin: '0 auto',
        display: 'block',
        objectFit: 'contain',
    },
    card: {
        filter: 'grayscale(100%)',
        transition: '100ms',
        '&:hover': {
            transition: '200ms',
            filter: 'grayscale(0)',
        },
    },
    downloadText: {},
});

const DownloadButton = ({ url, title, imageUrl }) => {
    const s = useStyles();

    return <div className='col-6 col-lg-4 mx-auto mb-4'>
        <div className={'card ' + s.card}>
            <div className='card-body'>
                <a href={url}>
                    <img alt={`${title} Player Icon`} src={imageUrl} className={s.image} />
                    <div className={`mt-4`} style={{ height: 80 }}>
                        <h4 className={`text-center ${s.downloadText}`}>{title}</h4>
                    </div>
                </a>
            </div>
        </div>
    </div>
}

export default DownloadButton;