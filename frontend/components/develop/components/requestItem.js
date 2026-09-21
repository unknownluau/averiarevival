import React, { useState } from 'react';
import { createUseStyles } from 'react-jss';
import { requestUgcItem } from '../../../services/develop';

const useStyles = createUseStyles({
  wrapper: {
    padding: '12px 4px',
  },
  label: {
    fontWeight: 700,
    fontSize: '14px',
    display: 'block',
    marginBottom: '4px',
  },
  input: {
    width: '100%',
    padding: '6px 8px',
    border: '1px solid #c3c3c3',
    borderRadius: '3px',
    fontSize: '14px',
    boxSizing: 'border-box',
  },
  hint: {
    fontSize: '12px',
    color: '#666',
    marginTop: '4px',
  },
  button: {
    marginTop: '12px',
    background: '#00a2ff',
    color: '#fff',
    border: 'none',
    padding: '8px 20px',
    borderRadius: '3px',
    fontSize: '14px',
    cursor: 'pointer',
    '&:disabled': {
      opacity: 0.6,
      cursor: 'not-allowed',
    },
  },
  success: {
    color: '#1ba100',
    marginTop: '10px',
    fontWeight: 600,
  },
  error: {
    color: '#c00',
    marginTop: '10px',
  },
});

const RequestItem = () => {
  const s = useStyles();
  const [url, setUrl] = useState('');
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState(null);
  const [err, setErr] = useState(null);

  const submit = async (e) => {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    setMsg(null);
    setErr(null);
    try {
      await requestUgcItem({ url: url.trim() });
      setMsg('Successfully requested item.');
      setUrl('');
    } catch (e) {
      const detail = e?.response?.data?.errors?.[0]?.message || e?.message || 'Request failed.';
      setErr(detail);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className={s.wrapper}>
      <form onSubmit={submit}>
        <label className={s.label} htmlFor="ugc-request-url">Roblox Catalog URL</label>
        <input
          id="ugc-request-url"
          className={s.input}
          type="text"
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          placeholder="https://www.roblox.com/catalog/17238615/Burro-Pinata"
          disabled={busy}
        />
        <div className={s.hint}>
          Paste a www.roblox.com item URL. Limit: 2 requests every 12 hours.
        </div>
        <button className={s.button} type="submit" disabled={busy || !url.trim()}>
          {busy ? 'Submitting...' : 'Request Item'}
        </button>
        {msg && <div className={s.success}>{msg}</div>}
        {err && <div className={s.error}>{err}</div>}
      </form>
    </div>
  );
};

export default RequestItem;
