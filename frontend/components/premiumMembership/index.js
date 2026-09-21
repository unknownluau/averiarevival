import { useState, useEffect } from 'react';
import { createUseStyles } from 'react-jss';
import axios from 'axios';
import AuthenticationStore from '../../stores/authentication';
import { getMembershipType, getUserPlaytime } from '../../services/users';
import { getBaseUrl2, getUrlWithProxy } from '../../lib/request';
import { getTheme, themeType } from '../../services/theme';

const FREE_ICON = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAFoAAABZCAMAAACJ4sOeAAAAA3NCSVQICAjb4U/gAAAAk1BMVEX///+rq6urq6urq6urq6urq6urq6urq6urq6urq6urq6urq6urq6urq6urq6v////7+/v5+fn19fXz8/Pv7+/p6enn5+fl5eXj4+Ph4eHf39/c3Nza2trZ2dnX19fV1dXR0dHPz8/MzMzJycnFxcXBwcG/v7+8vLy6urq5ubm3t7e0tLSzs7OwsLCvr6+tra2rq6v9vSmPAAAAMXRSTlMAESIzRFVmd4iZqrvM3e7/////////////////////////////////////////////aIUvegAAAAlwSFlzAAALEgAACxIB0t1+/AAAABx0RVh0U29mdHdhcmUAQWRvYmUgRmlyZXdvcmtzIENTNui8sowAAAAVdEVYdENyZWF0aW9uIFRpbWUANy8xNi8xM0TRClMAAAJ9SURBVFiF7ZltW4JQDIYVUUBQkCS1sizBsFf+/69LhBNH4WzjwD7V80Uudbdz53XbYIDLtGzXy4RmrmOZQ4IZppHtZk3ybLMT17BnjdxCc0ebPvEAbhkcSycyE8hhyXW7LXxMAxfwNmCjeeiUYaHH3G4FzjWlRaWlyy0cN+ca5JMslDzRA5/kIGRHm3xaoGDAu5BhdjcyxO5KVrP1R7BS81iaPZCzrGnVG5rz+VrjOhrfQWma18Ldft9Qyb0ij3oj15a8zpak0mVI+ph3lS5mIP1MIcngcvrCbYrTD36pO8KXDbLT8XYvo9Nt/E10G1ktSej7tzJ65/thAtsMSXP6OectZPQ6f9mBRhZlIe4L4LuEvjm/vkBWHmEQ34ICGFfor/IhhewMPB7rkvNYoZPyIYLsLDQe3wIYVeidePoADF10+zgKzKJCiz/iHyDLHA19nqUC469+0SEJbWLnVoUOa78Bo08nmUVE138DRk+xWd2A9mloDxzFJIqWEHoZRcB6BzeQAOIWCtTWQ2iChDg6VFubEPp4f4fo/qiJ7iYcfVDFIv5H/w00dvnVRg/Ri6Q2Gr9Y66I9bL/WR0/x7KhAb54lPQYEtI2djb9eR/vP8o10G1C8NvGEoArIcr19eliFxIAQEqR3VazBy1NxD8EyJMUZBhwCuSzKxT1pRiPxMEgp40sTGb4Di5sqmjOmm8Uld7F6RUxE7thXDi1JpI59J2ByCtZz2ignjox5I2O2y5mjM1YWOOshjFUcztoTZ8WMsc7HWZ3krKlyVoI569ecVXfOXgFnh4OzLzNg7Ca1d7xFD2zA2LnLxdZvPMO5uqS52Hq7Z3F1pEtp9tF/AHc4HlCSyVVHAAAAAElFTkSuQmCC';
const CLASSIC_ICON = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAFoAAABZCAMAAACJ4sOeAAAAA3NCSVQICAjb4U/gAAAA51BMVEX///8Ao9kAo9kAo9kAo9kAo9kAo9kAo9kAo9kAo9kAo9kAo9kAo9kAo9kAo9n////7/v73/P7y+v3v+f3r+Pzl9vvk9fvh9Pvf8/rb8vrR7vjN7fjA6Pa85/W45fSy4/Sv4vOt4fOr4fKp4PKl3/Kn3/Kh3fGV2e+Q1+6N1u6I1O2F0+2A0ex2zut1zepyzOpwy+pqyellx+hdxedfxedZw+ZVwuZUweZSweVQwOVOv+VJveREvONCu+NAuuM8ueI3t+ExteAutOArs98lsN8frt4drd0ZrN0SqdwPqNsKp9oEpNoAo9n1K2H6AAAATXRSTlMAESIzRFVmd4iZqrvM3e7//////////////////////////////////////////////////////////////////////////////////9pCfIUAAAAJcEhZcwAACxIAAAsSAdLdfvwAAAAcdEVYdFNvZnR3YXJlAEFkb2JlIEZpcmV3b3JrcyBDUzbovLKMAAAAFXRFWHRDcmVhdGlvbiBUaW1lADcvMTYvMTNE0QpTAAACnElEQVRYhe2ZaV/bMAyH2xCatDFNoTA6oNu4Ch3HYAfHOLp23OT7f541TdykJJZkJ3rF/m+SHnqin+zYllSp4LId1xOB1IJXd+wqwQzTnOsFeRKuXYhruQu53EjNujG9JgBuHBzHJDI1yOGU664ufJ4GjuA6YCt/6JRhocfc1QKHatCioumyhuN204A8loOSa2bgseoIuW5MHr+gYMCLkGF2MTLELkpWs81HMFH+WNolkIMg7623DOfzW81n0aIcctDMhFt/3VDJe0OeK42ceeVNliSVZkNSxrxLNDMD6XsKSRaX0zNuU5ze92P1CH+2qE6/XvZ/pdGj/sUr0W0B/++m4/uf0uhj3+9cwzZV0pw+DXnLafR2ePkGGjmUF/FnBLxPoT9OrqeQlSAM4t1iBDxL0M/RtTWE7Cw8Htsx8GuCvolvupCdg8bjpSU5CfpY3j0Chh66fPyVmHaC3pJ3A8gyREO/B0OJ8T9P0R15dwtZ2ti+laBXMs+A0eOdzCGis8+A0Q1sVuegfRpagKN41d1Yh9BrG90rtTW4gLQhbqS22roKTZBVHL2qtrYh9Givh2hvZIguJhx9q4rF5X/0+0ALLnQVPUgao/GDtSlaYOu1ObqBZ0cTdKt3ktLBEgHtYnvj1Ovuj6f4i2F/ieK1jScEg2kE1nb7h/tfPsiPv2E7QoL0oIo1eHiKziFYhqTYwzqwlUM5uF/no89hK4uUMn5v5ZCPYBtBzBlHO8uz3PbmH8RE5o4CY+tLpo5lJ2DpFKzktDGdODLmjYzZLmeOzlhZ4KyHMFZxOGtPnBUzxjofZ3WSs6bKWQnmrF9zVt05ewWcHQ7OvkyFsZuk77hGD6zC2LkLxdZvnMAFHgqjLmkott7uRFwd6ViGffR/cJtmR2gQLyoAAAAASUVORK5CYII=';
const OBC_ICON = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAFoAAABZCAMAAACJ4sOeAAAAA3NCSVQICAjb4U/gAAAA4VBMVEX////ZAADZAADZAADZAADZAADZAADZAADZAADZAADZAADZAADZAADZAADZAAD////++/v+9/f98vL97+/86+v75eX74eH639/629v40dH4zc32wMD1vLz1u7v0uLj0srLzr6/zra3yq6vyqanyp6fypaXxoaHvlZXukJDujY3tiIjthYXsgIDrdnbqcnLqcHDpamroZWXnX1/nXV3mWVnmVFTlUlLlUFDlTk7kSUnjRETjQkLjQEDiPDzhNzfgMDDgLi7fKyvfJSXeHx/dHR3dGRncEhLbDw/aCgraBATZAADUK4+iAAAAS3RSTlMAESIzRFVmd4iZqrvM3e7///////////////////////////////////////////////////////////////////////////////+joE8mAAAACXBIWXMAAAsSAAALEgHS3X78AAAAHHRFWHRTb2Z0d2FyZQBBZG9iZSBGaXJld29ya3MgQ1M26LyyjAAAABV0RVh0Q3JlYXRpb24gVGltZQA3LzE2LzEzRNEKUwAAAplJREFUWIXtmdl200AMhu3GxZOOHYemNJQStkLaAIGytNANF1qWzvs/EPE+OR5L8ti6gv/GPj3VFx3NIktyHFyu5w9lqAoFUvgewQyVJwJlkhSDbtzhyMjNFIoNS67rhwA3D849G7CAHNZcbw0nglP4Zhuwh4dClyTH3N1qBU4kWFxu4bhvAV5phJ+ioR15JWSruOajR9OQjQyyO5IBdmdyM9t+BSuZ11L0QFbKtAc3eyGrUf3suPT7CFZQQ8ueyPX7xPJ4G7We2XoLRyLZ+76rpO/AjV7JKmRzWneb5PRRlGuO/29IdvrudPFZR8eL0zua2y5CvphG0RMdvYyi6TloUpwbZE8fJ7wdHX2QPN6BRvnehu/SjxnwRkM/TJ/HkJUgLOKP+xnwpEL/zp7jGDALCfE4yIGvK/RF/jKD7AboxfRnXHAq9LJ4+wkYphEBnf5eYCYV+kXxdgUYJheJB6LjAhM9LdHT4u0SsnSwvFWhH9R+A0avMtkWEV3/DRjtY7vagI5oaAGu4tls/xGE3tufnTUaS/ACmUDcTJNG4wDcILs4erfZGkRfH84RHV5borsJR182xeLLf/Q/gcbSuTU6QDJBB7TEbj57tMDua3u0j1dHKXo8f6/pzTYB7WG5sfT68adf+R/ixTbFazSjK3VVRmDv5eLt0bMyf30FzSShQLptijX08ZR/h2AVUkMOm8JWA8qH+7kZfQIahZQvVaU+jA3kJWwjiDVj/GpnnTt5/g0xGdCqAguV1XTfBZhWgvVcNq4Vjnx1I2e1y1ijc3YWGPshnF0czt4TY8eMs8/H2Z3k7KkydoI5+9ecXfdOa4mPlfgmHJxzGc5pUnvH6TOwRGyTO4dz3ugwTklTcc12czrTRDqT5Rz9LwCqPLldvONZAAAAAElFTkSuQmCC';
const PREMIUM_ICON = 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCA0NCA0NCI+PHBhdGggZD0iTTQwIDRINHY0MGE0IDQgMCAwMS00LTRWNGE0IDQgMCAwMTQtNGgzNmE0IDQgMCAwMTQgNHYzNmE0IDQgMCAwMS00IDRIMjF2LTRoMTl6bS03IDdIMTF2MzNIN1Y3aDMwdjMwSDIxdi00aDEyem0tNyA3aC04djI2aC00VjE0aDE2djE2aC05di00aDV6IiBmaWxsPSIjMzkzYjNkIiBmaWxsLXJ1bGU9ImV2ZW5vZGQiLz48L3N2Zz4='

const TIERS = [
  {
    id: 'free',
    label: 'Free',
    membershipType: 'None',
    membershipValue: 0,
    icon: FREE_ICON,
    dailyRobux: null,
    activePlaces: 100,
    joinGroups: 100,
    createGroups: 100,
    signingBonus: null,
    paidAccess: '70%',
    sellStuff: true,
    virtualHat: false,
    bonusGear: false,
    bcBeta: false,
    tradeSystem: true,
    buttonLabel: null,
  },
  {
    id: 'classic',
    label: 'Classic',
    membershipType: 'BuildersClub',
    membershipValue: 1,
    icon: CLASSIC_ICON,
    dailyRobux: 15,
    activePlaces: 100,
    joinGroups: 100,
    createGroups: 100,
    signingBonus: null,
    paidAccess: '70%',
    sellStuff: true,
    virtualHat: false,
    bonusGear: false,
    bcBeta: false,
    tradeSystem: true,
    buttonLabel: 'Free!',
  },
  {
    id: 'outrageous',
    label: 'Outrageous',
    membershipType: 'OutrageousBuildersClub',
    membershipValue: 3,
    icon: OBC_ICON,
    dailyRobux: 85,
    activePlaces: 100,
    joinGroups: 100,
    createGroups: 100,
    signingBonus: null,
    paidAccess: '70%',
    sellStuff: true,
    virtualHat: false,
    bonusGear: false,
    bcBeta: false,
    tradeSystem: true,
    buttonLabel: 'Free!',
  },
  {
    id: 'premium',
    label: 'Premium',
    membershipType: 'Premium',
    membershipValue: 4,
    icon: PREMIUM_ICON,
    dailyRobux: 85,
    activePlaces: 100,
    joinGroups: 100,
    createGroups: 100,
    signingBonus: null,
    paidAccess: '70%',
    sellStuff: true,
    virtualHat: false,
    bonusGear: false,
    bcBeta: false,
    tradeSystem: true,
    buttonLabel: 'Free!',
  },
];

const FEATURE_ROWS = [
  { key: 'dailyRobux', label: 'Daily ROBUX', format: v => v ? `R$${v}` : 'No' },
  { key: 'activePlaces', label: 'Active Places', format: v => String(v) },
  { key: 'joinGroups', label: 'Join Groups', format: v => String(v) },
  { key: 'createGroups', label: 'Create Groups', format: v => v ? String(v) : 'No' },
  { key: 'signingBonus', label: 'Signing Bonus*', format: v => v ? `R$${v}` : 'No' },
  { key: 'paidAccess', label: 'Paid Access', format: v => String(v) },
  { key: 'sellStuff', label: 'Sell Stuff', format: v => v ? '✔' : 'No' },
  { key: 'virtualHat', label: 'Virtual Hat', format: v => v ? '✔' : 'No' },
  { key: 'bonusGear', label: 'Bonus Gear', format: v => v ? '✔' : 'No' },
  { key: 'bcBeta', label: 'BC Beta Features', format: v => v ? '✔' : 'No' },
  { key: 'tradeSystem', label: 'Trade System', format: v => v ? '✔' : 'No' },
];

const useStyles = createUseStyles({
  page: {
    maxWidth: 960,
    margin: '24px auto',
    padding: '0 16px',
  },
  header: {
    textAlign: 'center',
    marginBottom: 24,
    '& h1': {
      fontSize: 26,
      fontWeight: 700,
      color: p => (p.theme === themeType.dark || p.theme === themeType.obc2019) ? '#fff' : 'var(--text-color-primary)',
      margin: 0,
    },
  },
  layout: {
    display: 'flex',
    gap: 24,
    alignItems: 'flex-start',
    '@media (max-width: 768px)': {
      flexDirection: 'column',
    },
  },
  leftColumn: {
    flex: '1 1 auto',
    overflowX: 'auto',
  },
  rightColumn: {
    flexShrink: 0,
    width: 220,
    '@media (max-width: 768px)': {
      width: '100%',
    },
  },
  table: {
    width: '100%',
    borderCollapse: 'collapse',
    background: 'var(--white-color)',
    borderRadius: 6,
    overflow: 'hidden',
    boxShadow: '0 1px 4px rgba(0,0,0,0.10)',
  },
  tierHeader: {
    textAlign: 'center',
    padding: '12px 8px 8px',
    borderBottom: '2px solid var(--background-color)',
    '& h2': {
      margin: '8px 0 6px',
      fontSize: 16,
      fontWeight: 700,
      color: 'var(--text-color-primary)',
    },
  },
  tierIcon: {
    width: 90,
    height: 'auto',
    display: 'block',
    margin: '0 auto',
  },
  featureLabel: {
    padding: '7px 12px',
    color: 'var(--text-color-tertiary)',
    fontSize: 13,
    borderTop: '1px solid var(--background-color)',
    fontWeight: 500,
    whiteSpace: 'nowrap',
  },
  featureValue: {
    padding: '7px 8px',
    textAlign: 'center',
    fontSize: 13,
    borderTop: '1px solid var(--background-color)',
    color: 'var(--text-color-primary)',
    fontWeight: 400,
  },
  featureValueHighlight: {
    color: 'var(--primary-color)',
    fontWeight: 700,
  },
  currentTierCol: {
    background: 'color-mix(in srgb, var(--primary-color) 8%, transparent)',
  },
  footnote: {
    padding: '6px 12px',
    color: 'var(--text-color-tertiary)',
    fontSize: 12,
    fontStyle: 'italic',
    borderTop: '1px solid var(--background-color)',
  },
  buttonCell: {
    padding: '10px 8px',
    textAlign: 'center',
    borderTop: '1px solid var(--background-color)',
  },
  upgradeBtn: {
    display: 'inline-block',
    padding: '6px 14px',
    background: 'var(--primary-color)',
    color: '#fff',
    border: 'none',
    borderRadius: 4,
    fontSize: 13,
    fontWeight: 600,
    cursor: 'pointer',
    '&:hover': {
      background: 'var(--primary-color-hover)',
    },
    '&:disabled': {
      opacity: 0.6,
      cursor: 'not-allowed',
    },
  },
  currentBadge: {
    display: 'inline-block',
    padding: '4px 10px',
    background: 'var(--success-color)',
    color: '#fff',
    borderRadius: 4,
    fontSize: 12,
    fontWeight: 600,
  },
  alert: {
    padding: '10px 14px',
    borderRadius: 4,
    marginBottom: 16,
    fontSize: 13,
    fontWeight: 500,
  },
  alertSuccess: {
    background: 'color-mix(in srgb, var(--success-color) 15%, transparent)',
    color: 'var(--success-color)',
    border: '1px solid var(--success-color)',
  },
  alertError: {
    background: 'color-mix(in srgb, var(--bad-color) 15%, transparent)',
    color: 'var(--bad-color)',
    border: '1px solid var(--bad-color)',
  },
  card: {
    background: 'var(--white-color)',
    borderRadius: 6,
    padding: 16,
    boxShadow: '0 1px 4px rgba(0,0,0,0.10)',
    marginBottom: 16,
    '& h3': {
      margin: '0 0 8px',
      fontSize: 15,
      fontWeight: 700,
      color: 'var(--text-color-primary)',
    },
    '& p': {
      margin: '0 0 10px',
      fontSize: 12,
      color: 'var(--text-color-tertiary)',
      lineHeight: '1.5em',
    },
  },
  robuxBtn: {
    display: 'inline-block',
    padding: '6px 14px',
    background: 'var(--primary-color)',
    color: '#fff',
    borderRadius: 4,
    fontSize: 13,
    fontWeight: 600,
    textDecoration: 'none',
    '&:hover': {
      background: 'var(--primary-color-hover)',
      color: '#fff',
    },
  },
});

const getAntiforgeryToken = async () => {
  const url = getUrlWithProxy(getBaseUrl2('/internal/membership'));
  const resp = await axios.get(url, { withCredentials: true });
  const html = typeof resp.data === 'string' ? resp.data : '';
  const match = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
  return match ? match[1] : '';
};

const postMembership = async (membershipType, antiforgeryToken) => {
  const url = getUrlWithProxy(getBaseUrl2('/internal/membership'));
  const body = new URLSearchParams();
  body.append('membershipType', membershipType);
  if (antiforgeryToken) body.append('__RequestVerificationToken', antiforgeryToken);
  return axios.post(url, body.toString(), {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    withCredentials: true,
    validateStatus: () => true,
  });
};

const PremiumMembership = () => {
  const s = useStyles({ theme: getTheme() });
  const auth = AuthenticationStore.useContainer();
  const [currentMembership, setCurrentMembership] = useState(0);
  const [status, setStatus] = useState(null);
  const [loading, setLoading] = useState(false);
  const [playtime, setPlaytime] = useState(null);

  useEffect(() => {
    if (!auth.userId) return;
    getMembershipType({ userId: auth.userId }).then(setCurrentMembership).catch(() => {});
    getUserPlaytime({ userId: auth.userId }).then(setPlaytime).catch(() => {});
  }, [auth.userId]);

  const handleUpgrade = async (tier) => {
    setLoading(true);
    setStatus(null);
    if (tier.membershipValue >= 3 && playtime && !playtime.meetsRequirement) {
      setStatus({ success: false, message: `You need ${playtime.remainingHours} more hour(s) of playtime to get this membership.` });
      setLoading(false);
      return;
    }
    try {
      const token = await getAntiforgeryToken();
      const resp = await postMembership(tier.membershipType, token);
      if (resp.status >= 200 && resp.status < 400) {
        setCurrentMembership(tier.membershipValue);
        setStatus({ success: true, message: `Successfully changed to ${tier.label} membership!` });
      } else {
        setStatus({ success: false, message: 'Failed to update membership. Please try again.' });
      }
    } catch {
      setStatus({ success: false, message: 'An error occurred. Please try again.' });
    } finally {
      setLoading(false);
    }
  };

  const isDark = getTheme() === themeType.dark;

  return (
    <div className={s.page}>
      <div className={s.header}>
        <h1>Upgrade to Builders Club</h1>
      </div>

      {status && (
        <div className={`${s.alert} ${status.success ? s.alertSuccess : s.alertError}`}>
          {status.message}
        </div>
      )}

      <div className={s.layout}>
        <div className={s.leftColumn}>
          <table className={s.table}>
            <thead>
              <tr>
                <th style={{ width: 140 }} />
                {TIERS.map(tier => (
                  <th
                    key={tier.id}
                    className={`${s.tierHeader}${currentMembership === tier.membershipValue ? ' ' + s.currentTierCol : ''}`}
                  >
                    <img src={tier.icon} alt={tier.label} className={s.tierIcon} style={tier.id === 'premium' && isDark ? { filter: 'brightness(0) invert(1)' } : {}} />
                    <h2>{tier.label}</h2>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {FEATURE_ROWS.map(row => (
                <tr key={row.key}>
                  <td className={s.featureLabel}>{row.label}</td>
                  {TIERS.map(tier => {
                    const val = tier[row.key];
                    const formatted = row.format(val);
                    const isHighlight = val === true || (typeof val === 'number' && val > 0 && row.key !== 'activePlaces');
                    return (
                      <td
                        key={tier.id}
                        className={`${s.featureValue}${currentMembership === tier.membershipValue ? ' ' + s.currentTierCol : ''}${isHighlight ? ' ' + s.featureValueHighlight : ''}`}
                      >
                        {formatted}
                      </td>
                    );
                  })}
                </tr>
              ))}
              <tr>
                <td className={s.footnote} colSpan={TIERS.length + 1}>
                  * Signing bonus is for first time membership purchase only.
                </td>
              </tr>
              <tr>
                <td className={s.buttonCell} />
                {TIERS.map(tier => (
                  <td
                    key={tier.id}
                    className={`${s.buttonCell}${currentMembership === tier.membershipValue ? ' ' + s.currentTierCol : ''}`}
                  >
                    {currentMembership === tier.membershipValue ? (
                      <span className={s.currentBadge}>Current</span>
                    ) : tier.buttonLabel ? (
                      <>
                        <button
                          className={s.upgradeBtn}
                          disabled={loading || (tier.membershipValue >= 3 && playtime && !playtime.meetsRequirement)}
                          onClick={() => handleUpgrade(tier)}
                        >
                          {tier.buttonLabel}
                        </button>
                        {tier.membershipValue >= 3 && playtime && !playtime.meetsRequirement && (
                          <div style={{ fontSize: 11, color: 'var(--bad-color)', marginTop: 4 }}>
                            Requires 6h playtime ({playtime.remainingHours}h remaining)
                          </div>
                        )}
                      </>
                    ) : null}
                  </td>
                ))}
              </tr>
            </tbody>
          </table>
        </div>

        <div className={s.rightColumn}>
          <div className={s.card}>
            <h3>Redeem Promocode</h3>
            <p>Have a promocode? Redeem it for ROBUX or membership perks.</p>
            <a href="/internal/promocodes" className={s.robuxBtn}>Redeem Promocode</a>
          </div>
        </div>
      </div>
    </div>
  );
};

export default PremiumMembership;
