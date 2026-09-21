const themeType = {
    dark: 'dark',
    obc2019: 'obc2019',
    bliss: 'bliss',
    light: 'light',
    default: 'light',
}

const themeColor = {
    coffee: 'coffee',
    bliss: 'bliss',
    cobalt: 'cobalt',
    sunlit: 'sunlit',
    royalty: 'royalty',
    nobility: 'nobility',
    harmony: 'harmony',
    witness: 'witness',
    whisper: 'whisper',
    cane: 'cane',
    spice: 'spice',
    custom: 'custom',
}

const hexColorRe = /^#[0-9a-fA-F]{6}$/;

const themeFont = {
    gotham: 'gotham',
    ssp: 'ssp',
}

const avPageStyleType = {
    Modern: 'Modern',
    Legacy: 'Legacy',
}

const catalogPageStyle = {
    Modern: 'Modern',
    Legacy: 'Legacy',
}

const tradePageStyle = {
    Modern: 'Modern',
    Legacy: 'Legacy',
}

const groupPagesStyle = {
    Modern: 'Modern',
    Legacy: 'Legacy',
}

const searchUserPageStyle = {
    Modern: 'Modern',
    Legacy: 'Legacy',
}

const trueFalseStyle = {
    Yes: 'Yes',
    No: 'No',
}

const isLocalStorageAvailable = (() => {
    // @ts-ignore
    if (!process.browser) return false;
    if (typeof window === 'undefined' || !window.localStorage || !window.localStorage.getItem || !window.localStorage.setItem) return false;
    
    return true;
})()

const getTheme = () => {
    if (!isLocalStorageAvailable) return themeType.default;
    
    let value = localStorage.getItem('rbx_theme_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(themeType).includes(value)) return themeType.default;
    return themeType[value];
}

const setTheme = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_theme_v1', themeString)
}

const getThemeRibbon = () => {
    if (!isLocalStorageAvailable) return 'false';
    
    let value = localStorage.getItem('rbx_theme_ribbon_v1');
    // validate
    if (typeof value !== 'string' || !/^(true|false)$/i.test(value)) return 'false';
    return value;
}

const setThemeRibbon = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_theme_ribbon_v1', themeString)
}

const getThemeColor = () => {
    if (!isLocalStorageAvailable) return themeColor.coffee;

    let value = localStorage.getItem('rbx_theme_color_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(themeColor).includes(value)) return themeColor.coffee;
    return themeColor[value];
}

const setThemeColor = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_theme_color_v1', themeString);
}

const getThemeCustomColor = () => {
    if (!isLocalStorageAvailable) return null;
    const value = localStorage.getItem('rbx_theme_custom_color_v1');
    if (typeof value !== 'string' || !hexColorRe.test(value)) return null;
    return value.toLowerCase();
}

const setThemeCustomColor = (hexColor) => {
    if (!isLocalStorageAvailable) return;
    if (typeof hexColor !== 'string' || !hexColorRe.test(hexColor)) return;
    localStorage.setItem('rbx_theme_custom_color_v1', hexColor.toLowerCase());
}

const getThemeForumHeader = () => {
    if (!isLocalStorageAvailable) return 'false';
    
    let value = localStorage.getItem('rbx_theme_forum_header_v1');
    // validate
    if (typeof value !== 'string' || !/^(true|false)$/i.test(value)) return 'false';
    return value;
}

const setThemeForumHeader = (bool) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_theme_forum_header_v1', bool)
}

const getAvPageStyle = () => {
    if (!isLocalStorageAvailable) return avPageStyleType.default;
    
    let value = localStorage.getItem('rbx_av_page_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(avPageStyleType).includes(value)) return avPageStyleType.default;
    return avPageStyleType[value];
}

const setAvPageStyle = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_av_page_style_v1', themeString)
}

const getCatalogPageStyle = () => {
    if (!isLocalStorageAvailable) return catalogPageStyle["Modern"];

    let value = localStorage.getItem('rbx_cat_page_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(catalogPageStyle).includes(value)) return catalogPageStyle["Modern"];
    return catalogPageStyle[value];
}

const setCatalogPageStyle = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_cat_page_style_v1', themeString);
}

const getCatalogStyle = () => {
    if (!isLocalStorageAvailable) return catalogPageStyle["Modern"];

    let value = localStorage.getItem('rbx_cat_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(catalogPageStyle).includes(value)) return catalogPageStyle["Modern"];
    return catalogPageStyle[value];
}

const setCatalogStyle = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_cat_style_v1', themeString);
}

const getTradeStyle = () => {
    if (!isLocalStorageAvailable) return tradePageStyle["Modern"];

    let value = localStorage.getItem('rbx_trade_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(tradePageStyle).includes(value)) return tradePageStyle["Modern"];
    return tradePageStyle[value];
}

const setTradeStyle = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_trade_style_v1', themeString);
}

const getGroupPagesStyle = () => {
    if (!isLocalStorageAvailable) return groupPagesStyle["Modern"];

    let value = localStorage.getItem('rbx_group_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(groupPagesStyle).includes(value)) return groupPagesStyle["Modern"];
    return groupPagesStyle[value];
}

const setGroupPagesStyle = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_group_style_v1', themeString);
}

const getSearchUserPageStyle = () => {
    if (!isLocalStorageAvailable) return searchUserPageStyle["Modern"];

    let value = localStorage.getItem('rbx_search_user_page_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(searchUserPageStyle).includes(value)) return searchUserPageStyle["Modern"];
    return searchUserPageStyle[value];
}

const setSearchUserPageStyle = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_search_user_page_style_v1', themeString);
}

const getHideRE = () => {
    if (!isLocalStorageAvailable) return trueFalseStyle["No"];

    let value = localStorage.getItem('rbx_fuck_re_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(trueFalseStyle).includes(value)) return trueFalseStyle["No"];
    return trueFalseStyle[value];
}

const setHideRE = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_fuck_re_v1', themeString);
}

export {
    getTheme,
    setTheme,
    
    getThemeColor,
    setThemeColor,

    getThemeCustomColor,
    setThemeCustomColor,
    
    getThemeForumHeader,
    setThemeForumHeader,
    
    getThemeRibbon,
    setThemeRibbon,
    
    getAvPageStyle,
    setAvPageStyle,
    
    getCatalogPageStyle,
    setCatalogPageStyle,

    getCatalogStyle,
    setCatalogStyle,

    getTradeStyle,
    setTradeStyle,

    getGroupPagesStyle,
    setGroupPagesStyle,

    getSearchUserPageStyle,
    setSearchUserPageStyle,

    getHideRE,
    setHideRE,

    themeType,
    themeColor,
    themeFont,
    avPageStyleType,
    catalogPageStyle,
    tradePageStyle,
    searchUserPageStyle,
}