export const isTouchDevice = () => {
  // From https://stackoverflow.com/questions/4817029/whats-the-best-way-to-detect-a-touch-screen-device-using-javascript
  return (('ontouchstart' in window) ||
    (navigator.maxTouchPoints > 0) ||
    // @ts-ignore
    (navigator.msMaxTouchPoints > 0));
}

export const Random = (min, max) => {
  return Math.floor(Math.random() * (max - min) ) + min;
}

/**
 * @param {number} seconds
 * @returns {Promise}
 */
export const wait = (seconds) =>
    new Promise(resolve => setTimeout(resolve, seconds * 1000));

export const tick = () =>
    new Promise(resolve => setTimeout(resolve, 0));

export function IsNullOrEmpty(value) {
    return !value || value.trim().length === 0;
}

export function IsValidNum(value) {
    if (typeof value === "number") {
        return Number.isSafeInteger(value) && Number.isFinite(value);
    }

    if (typeof value !== "string") return false;
    if (!/^-?\d+$/.test(value.trim())) return false;

    const num = Number(value);
    return Number.isSafeInteger(num) && Number.isFinite(num);
}

export function chunk(arr, size) {
    const out = [];
    for (let i = 0; i < arr.length; i += size) {
        out.push(arr.slice(i, i + size));
    }
    return out;
}

export class Stopwatch {
  startTime = 0;
  endTime = 0;
  
  Start() {
    this.startTime = Date.now();
  }
  
  Stop() {
    this.endTime = Date.now();
    return this.endTime - this.startTime;
  }
  
  ElapsedMilliseconds() {
    return this.endTime - this.startTime;
  }
  
  ElapsedSeconds() {
    return (this.endTime - this.startTime) / 1000;
  }
  
  ElapsedMinutes() {
    return (this.endTime - this.startTime) / 1000 * 60;
  }
  
  ElapsedHours() {
    return (this.endTime - this.startTime) / 1000 * 60 * 60;
  }
}

/**
 * @template T
 * @typedef {[T, import('react').Dispatch<T>]} UseState
 */
