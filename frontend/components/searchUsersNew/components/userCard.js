import { createUseStyles } from "react-jss";
import { useEffect, useState } from "react";
import Link from "../../link";
import PlayerImage from "../../playerImage";
import AuthenticationStore from "../../../stores/authentication";
import { getFriendStatus, sendFriendRequest, unfriendUser, acceptFriendRequest } from "../../../services/friends";

const useStyles = createUseStyles({
  playerItem: {
    display: 'block',
    margin: '0 0 12px',
    listStyle: 'none',
  },
  cardContainer: {
    background: '#ffffff',
    border: '1px solid #b8b8b8',
    borderRadius: '3px',
    overflow: 'hidden',
    position: 'relative',
  },
  cardContent: {
    cursor: 'pointer',
    display: 'flex',
    flexDirection: 'row',
    alignItems: 'flex-start',
    color: 'inherit',
    textDecoration: 'none',
    minHeight: '120px',
  },
  headshotWrap: {
    position: 'relative',
    margin: '12px',
    width: '92px',
    height: '92px',
    minWidth: '92px',
    flexShrink: 0,
    marginRight: '20px',
  },
  headshot: {
    width: '92px',
    height: '92px',
    borderRadius: '50%',
    border: '1px solid #b8b8b8',
    overflow: 'hidden',
    background: '#ffffff',
  },
  headshotImg: {
    width: '100%',
    height: '100%',
    objectFit: 'cover',
  },
  statusDot: {
    position: 'absolute',
    bottom: '4px',
    right: '4px',
    width: '18px',
    height: '18px',
    borderRadius: '50%',
    border: '2px solid #ffffff',
    boxSizing: 'border-box',
  },
  statusOnline: { background: '#00a2ff' },
  statusGame: { background: '#02b757' },
  statusStudio: { background: '#f68802' },
  caption: {
    flex: 1,
    minWidth: 0,
    padding: '12px 0',
  },
  avatarName: {
    fontSize: '16px',
    fontWeight: 300,
    color: '#191919',
    margin: 0,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  cardLabel: {
    color: '#b8b8b8',
    fontSize: '14px',
    padding: '3px 0 0',
    margin: 0,
  },
  gameLink: {
    color: '#00a2ff',
    fontSize: '14px',
    padding: '3px 0 0',
    margin: 0,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
    display: 'block',
    textDecoration: 'none',
    fontWeight: 400,
    '&:hover': { textDecoration: 'underline', color: '#00a2ff' },
  },
  groupLink: {
    color: '#00a2ff',
    fontSize: '14px',
    margin: 0,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
    display: 'block',
    textDecoration: 'none',
    fontWeight: 400,
    '&:hover': { textDecoration: 'underline', color: '#00a2ff' },
  },
  cardBtns: {
    background: '#f2f2f2',
    borderTop: '1px solid #e3e3e3',
    padding: '8px',
  },
  cardBtn: {
    width: '100%',
    display: 'block',
    background: '#ffffff',
    color: '#191919',
    border: '1px solid #b8b8b8',
    fontSize: '18px',
    fontWeight: 380,
    cursor: 'pointer',
    textAlign: 'center',
    padding: '8px 8px',
    borderRadius: '3px',
    lineHeight: '100%',
    '&:hover': {
      boxShadow: '0 1px 3px rgba(150,150,150,0.74)',
    },
    '&:disabled': {
      opacity: 1,
      color: '#b8b8b8',
      cursor: 'default',
      boxShadow: 'none',
    },
  },
});

/**
 * @param {any} presence
 */
const presenceKind = presence => {
  if (!presence) return 'offline';
  const t = presence.userPresenceType;
  if (t === 2 || t === '2' || t === 'InGame') return 'game';
  if (t === 3 || t === '3' || t === 'InStudio') return 'studio';
  if (t === 1 || t === '1' || t === 'Online') return 'online';
  return 'offline';
};

const UserCard = ({ user, presence, primaryGroup }) => {
  const s = useStyles();
  const auth = AuthenticationStore.useContainer();
  const [status, setStatus] = useState(/** @type {string|null} */(null));
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!auth.userId || auth.userId === user.UserId) return;
    getFriendStatus({ authenticatedUserId: auth.userId, userId: user.UserId })
      .then(setStatus)
      .catch(() => {});
  }, [auth.userId, user.UserId]);

  const profileUrl = user.UserProfilePageUrl || `/users/${user.UserId}/profile`;
  const isYou = auth.userId === user.UserId;
  const kind = presenceKind(presence);

  const statusDotClass = (() => {
    if (kind === 'online') return s.statusOnline;
    if (kind === 'game') return s.statusGame;
    if (kind === 'studio') return s.statusStudio;
    return null;
  })();

  const buttonLabel = (() => {
    if (isYou) return 'This is you';
    if (status === 'Friends') return 'Unfriend';
    if (status === 'RequestSent') return 'Request Sent';
    if (status === 'RequestReceived') return 'Accept Request';
    return 'Add Friend';
  })();

  const onAction = (/** @type {any} */ e) => {
    e.stopPropagation();
    e.preventDefault();
    if (busy || isYou) return;
    setBusy(true);
    const done = (/** @type {string} */ next) => { setStatus(next); setBusy(false); };
    if (!status || status === 'NotFriends') {
      sendFriendRequest({ userId: user.UserId }).then(() => done('RequestSent')).catch(() => setBusy(false));
    } else if (status === 'Friends') {
      unfriendUser({ userId: user.UserId }).then(() => done('NotFriends')).catch(() => setBusy(false));
    } else if (status === 'RequestReceived') {
      acceptFriendRequest({ userId: user.UserId }).then(() => done('Friends')).catch(() => setBusy(false));
    } else {
      setBusy(false);
    }
  };

  const renderStatusLine = () => {
    if (kind === 'game' && presence && presence.placeId) {
      return <Link href={`/games/${presence.placeId}`}>
        <a className={s.gameLink} onClick={e => e.stopPropagation()} title={presence.lastLocation}>
          {presence.lastLocation || 'In Game'}
        </a>
      </Link>;
    }
    if (kind === 'studio') return <div className={s.cardLabel}>Studio</div>;
    if (kind === 'online') return <div className={s.cardLabel}>Online</div>;
    return <div className={s.cardLabel}>Offline</div>;
  };

  return <li className={s.playerItem}>
    <div className={s.cardContainer}>
      <Link href={profileUrl}>
        <a className={s.cardContent}>
          <div className={s.headshotWrap}>
            <div className={s.headshot}>
              <PlayerImage id={user.UserId} name={user.Name} className={s.headshotImg} />
            </div>
            {statusDotClass && <span className={`${s.statusDot} ${statusDotClass}`} />}
          </div>
          <div className={s.caption}>
            <div className={s.avatarName}>{user.DisplayName || user.Name}</div>
            {renderStatusLine()}
            {primaryGroup && primaryGroup.group && <>
              <div className={s.cardLabel}>Primary Group</div>
              <Link href={`/groups/${primaryGroup.group.id}/${encodeURIComponent(primaryGroup.group.name || '')}`}>
                <a className={s.groupLink} onClick={e => e.stopPropagation()}>[ {primaryGroup.group.name} ]</a>
              </Link>
            </>}
          </div>
        </a>
      </Link>
      <div className={s.cardBtns}>
        <button
          className={s.cardBtn}
          onClick={onAction}
          disabled={busy || isYou || status === 'RequestSent'}
        >
          {buttonLabel}
        </button>
      </div>
    </div>
  </li>;
};

export default UserCard;
