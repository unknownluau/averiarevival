import { useEffect, useState } from "react";
import { createUseStyles } from "react-jss";
import { getBaseUrl } from "../../lib/request";
import { reportImageFail } from "../../services/metrics";
import { multiGetUserHeadshots } from "../../services/thumbnails";
import { ThumbnailFromState } from "../AvatarEditorPage/components/avatarCardList";

const useStyles = createUseStyles({
    image: {
        maxWidth: '400px',
        width: '100%',
        margin: '0 auto',
        height: 'auto',
        display: 'block',
    },
})

/**
 * Player headshot
 * @param {{id: number; name?: string; size?: string; className?: any;}} props
 * @returns
 */
const PlayerHeadshot = (props) => {
    const s = useStyles();
    const size = props.size || 420;
    const [image, setImage] = useState(null);
    const [retryCount, setRetryCount] = useState(0);
    
    useEffect(() => {
    setRetryCount(0);
    multiGetUserHeadshots({
        userIds: [props.id],
        size: size + "x" + size,
    }).then((results) => {
        const u = results.find(v => String(v.targetId) === String(props.id));
        if (!u) {
        setImage("/img/error.png");
        return;
        }

        setImage(ThumbnailFromState(u.imageUrl, u.state));
    });
    }, [props.id, size]);

    
    return <img className={`${s.image} ${props.className}`} src={image} alt={props?.name || ''} onError={(e) => {
        if (retryCount >= 3) return;
        reportImageFail({
            errorEvent: e,
            type: 'playerHeadshot',
            src: image,
        })
        setRetryCount(retryCount + 1);
        setImage('/img/error.png');
    }}></img>
}

export default PlayerHeadshot;