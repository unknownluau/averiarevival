// @ts-nocheck
import { useEffect, useState } from "react";
import { getUserInfo } from "../../services/users";

const cache = new Map();
const inflight = new Map();

const fetchVerified = (id) => {
  if (cache.has(id)) return Promise.resolve(cache.get(id));
  if (inflight.has(id)) return inflight.get(id);
  const p = getUserInfo({ userId: id })
    .then((d) => {
      const v = !!(d && d.hasVerifiedBadge);
      cache.set(id, v);
      inflight.delete(id);
      return v;
    })
    .catch(() => {
      cache.set(id, false);
      inflight.delete(id);
      return false;
    });
  inflight.set(id, p);
  return p;
};

/**
 * @param {{ userId?: number|string|null, verified?: boolean, className?: string, style?: object }} props
 */
const VerifiedBadge = ({ userId, verified, className, style } = {}) => {
  const [isVerified, setIsVerified] = useState(
    typeof verified === 'boolean' ? verified : (userId && cache.get(Number(userId))) || false
  );

  useEffect(() => {
    if (typeof verified === 'boolean') {
      setIsVerified(verified);
      if (userId) cache.set(Number(userId), !!verified);
      return;
    }
    const id = Number(userId);
    if (!id) return;
    let cancelled = false;
    fetchVerified(id).then((v) => {
      if (!cancelled) setIsVerified(!!v);
    });
    return () => { cancelled = true; };
  }, [userId, verified]);

  if (!isVerified) return null;
  return (
    <span
      className={`icon-verified ${className || ''}`}
      style={{ width: 16, height: 16, backgroundSize: 'contain', marginLeft: 4, verticalAlign: 'middle', ...style }}
      title="Verified"
    />
  );
};

export default VerifiedBadge;
