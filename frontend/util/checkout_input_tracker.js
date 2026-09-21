let counter = 0;
let attached = false;

const onEvent = () => {
  counter++;
  if (counter > 1_000_000) counter = 1_000_000;
};

const attachIfNeeded = () => {
  if (attached) return;
  if (typeof window === 'undefined' || typeof document === 'undefined') return;
  document.addEventListener('mousemove', onEvent, { passive: true });
  document.addEventListener('keydown', onEvent, { passive: true });
  document.addEventListener('click', onEvent, { passive: true });
  document.addEventListener('touchstart', onEvent, { passive: true });
  document.addEventListener('pointermove', onEvent, { passive: true });
  attached = true;
};

attachIfNeeded();

export const getBehaviorScore = () => {
  attachIfNeeded();
  return counter;
};

export const resetBehaviorScore = () => {
  counter = 0;
};

export default getBehaviorScore;
