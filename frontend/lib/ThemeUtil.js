import {getThemeCustomColor, themeColor, themeFont, themeType} from "../services/theme";

function hexToRgb(hexColor) {
    const m = /^#?([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(hexColor);
    if (!m) return null;
    return { r: parseInt(m[1], 16), g: parseInt(m[2], 16), b: parseInt(m[3], 16) };
}

function rgbToHsl(r, g, b) {
    r /= 255; g /= 255; b /= 255;
    const max = Math.max(r, g, b), min = Math.min(r, g, b);
    let h = 0, s = 0;
    const l = (max + min) / 2;
    if (max !== min) {
        const d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        switch (max) {
            case r: h = (g - b) / d + (g < b ? 6 : 0); break;
            case g: h = (b - r) / d + 2; break;
            case b: h = (r - g) / d + 4; break;
        }
        h /= 6;
    }
    return { h, s, l };
}

function hslToHex(h, s, l) {
    let r, g, b;
    if (s === 0) {
        r = g = b = l;
    } else {
        const hue2rgb = (p, q, t) => {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1/6) return p + (q - p) * 6 * t;
            if (t < 1/2) return q;
            if (t < 2/3) return p + (q - p) * (2/3 - t) * 6;
            return p;
        };
        const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        const p = 2 * l - q;
        r = hue2rgb(p, q, h + 1/3);
        g = hue2rgb(p, q, h);
        b = hue2rgb(p, q, h - 1/3);
    }
    const toHex = (x) => Math.round(x * 255).toString(16).padStart(2, '0');
    return '#' + toHex(r) + toHex(g) + toHex(b);
}

function shiftLightness(hexColor, delta) {
    const rgb = hexToRgb(hexColor);
    if (!rgb) return hexColor;
    const { h, s, l } = rgbToHsl(rgb.r, rgb.g, rgb.b);
    const newL = Math.max(0, Math.min(1, l + delta));
    return hslToHex(h, s, newL);
}

function ChangeVarsForTheme(theme) {
    switch (theme) {
        case themeType.dark:
            document.documentElement.style.setProperty('--text-color-primary', '#fff');
            document.documentElement.style.setProperty('--text-color-secondary', '#5a5a5a');
            document.documentElement.style.setProperty('--text-color-tertiary', '#999');
            document.documentElement.style.setProperty('--white-color', '#191919');
            document.documentElement.style.setProperty('--white-color-hover', '#212121');
            //document.documentElement.style.setProperty('--background-color', '#393939');
            document.documentElement.style.setProperty('--background-color', 'transparent');
            document.documentElement.style.setProperty('--text-color-quinary', '#b8b8b8');
            document.documentElement.setAttribute('data-bs-theme', 'dark');
            document.documentElement.style.setProperty('--text-color-quinary', '#5b5b5b');
            break;
        default:
            break;
    }
}

function ChangeVarsForThemeColor(theme, customHex) {
    if (theme === themeColor.custom) {
        const hexColor = (typeof customHex === 'string' ? customHex : getThemeCustomColor());
        if (!hexColor) return;
        const primaryColor = hexColor;
        const primaryColorHover = shiftLightness(hexColor, 0.08);
        const secondaryColor = shiftLightness(hexColor, -0.08);
        document.documentElement.style.setProperty('--primary-color', primaryColor);
        document.documentElement.style.setProperty('--primary-color-2', primaryColor);
        document.documentElement.style.setProperty('--primary-color-hover', primaryColorHover);
        document.documentElement.style.setProperty('--secondary-color', secondaryColor);
        return;
    }
    switch (theme) {
        case themeColor.coffee:
            document.documentElement.style.setProperty('--primary-color', '#8A5149');
            document.documentElement.style.setProperty('--primary-color-2', '#915A4D');
            document.documentElement.style.setProperty('--primary-color-hover', '#9C6A5E');
            document.documentElement.style.setProperty('--secondary-color', '#653E35');
            break;
        case themeColor.bliss:
            document.documentElement.style.setProperty('--primary-color', 'var(--blue-color)');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--blue-color-2)');
            document.documentElement.style.setProperty('--primary-color-hover', 'var(--blue-color-hover)');
            document.documentElement.style.setProperty('--secondary-color', '#0074bd');
            break;
        case themeColor.cobalt:
            document.documentElement.style.setProperty('--primary-color', '#313233');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--primary-color)');
            document.documentElement.style.setProperty('--primary-color-hover', '#454647');
            document.documentElement.style.setProperty('--secondary-color', '#272828');
            break;
        case themeColor.sunlit:
            document.documentElement.style.setProperty('--primary-color', '#ff911c');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--primary-color)');
            document.documentElement.style.setProperty('--primary-color-hover', '#FFA749');
            document.documentElement.style.setProperty('--secondary-color', '#CC7416');
            break;
        case themeColor.royalty:
            document.documentElement.style.setProperty('--primary-color', '#5100A5');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--primary-color)');
            document.documentElement.style.setProperty('--primary-color-hover', '#6600CF');
            document.documentElement.style.setProperty('--secondary-color', '#400084');
            break;
        case themeColor.nobility:
            document.documentElement.style.setProperty('--primary-color', '#6600CF');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--primary-color)');
            document.documentElement.style.setProperty('--primary-color-hover', '#8432D8');
            document.documentElement.style.setProperty('--secondary-color', '#5100A5');
            break;
        case themeColor.harmony:
            document.documentElement.style.setProperty('--primary-color', '#5FA554');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--primary-color)');
            document.documentElement.style.setProperty('--primary-color-hover', '#6FAE65');
            document.documentElement.style.setProperty('--secondary-color', '#42733A');
            break;
        case themeColor.witness:
            document.documentElement.style.setProperty('--primary-color', '#74CD81');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--primary-color)');
            document.documentElement.style.setProperty('--primary-color-hover', '#8FD79A');
            document.documentElement.style.setProperty('--secondary-color', '#5CA467');
            break;
        case themeColor.whisper:
            document.documentElement.style.setProperty('--primary-color', '#EEA1CD');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--primary-color)');
            document.documentElement.style.setProperty('--primary-color-hover', '#f1b3d7');
            document.documentElement.style.setProperty('--secondary-color', '#F4B8DA');
            break;
        case themeColor.cane: // christmas theme!
            document.documentElement.style.setProperty('--primary-color', '#ae003e');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--primary-color)');
            document.documentElement.style.setProperty('--primary-color-hover', '#d20057');
            document.documentElement.style.setProperty('--secondary-color', '#960033');
            break;
        case themeColor.spice: // halloween theme!
            document.documentElement.style.setProperty('--primary-color', '#ae003e');
            document.documentElement.style.setProperty('--primary-color-2', 'var(--primary-color)');
            document.documentElement.style.setProperty('--primary-color-hover', '#d20057');
            document.documentElement.style.setProperty('--secondary-color', '#960033');
            break;
        default:
            break;
    }
}

function ChangeVarsForThemeFont(theme) {
    switch (theme) {
        case themeFont.ssp:
            document.documentElement.classList.add("ssp");
            break;
        default:
            if (document.documentElement.classList.contains("ssp")) document.documentElement.classList.remove("ssp");
            break;
    }
}

export {
    ChangeVarsForTheme,
    ChangeVarsForThemeColor,
    ChangeVarsForThemeFont,
};