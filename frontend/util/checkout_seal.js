const canonical = (assetId, expectedPrice, ticketId) =>
  `${assetId}|${expectedPrice}|${ticketId}`;

const toHex = (buf) =>
  Array.from(new Uint8Array(buf), (b) => b.toString(16).padStart(2, '0')).join('');

const decodeBase64Key = (b64) => {
  const bin = atob(b64);
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return bytes;
};

export const forgeCheckoutSeal = async ({ assetId, expectedPrice, ticketId, keyMaterial }) => {
  const cryptoKey = await crypto.subtle.importKey(
    'raw',
    decodeBase64Key(keyMaterial),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign'],
  );
  const sig = await crypto.subtle.sign(
    'HMAC',
    cryptoKey,
    new TextEncoder().encode(canonical(assetId, expectedPrice, ticketId)),
  );
  return `k=${ticketId};v=${toHex(sig)}`;
};

export default forgeCheckoutSeal;
