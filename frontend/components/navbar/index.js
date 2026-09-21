import React, { useRef } from "react";
import { createUseStyles } from "react-jss";
import { getTheme, getThemeRibbon, themeColor, themeType } from "../../services/theme";
import AuthenticationStore from "../../stores/authentication";
import NavSideBar from "../navSidebar";
import LoggedInArea from "./components/loggedinArea";
import LoginArea from "./components/loginArea";
import Logo from "./components/logo";
import NavigationLinks from "./components/navigationLinks";
import Search from "./components/search";

const useNavBarStyles = createUseStyles({
    navbar: {
        //backgroundColor: p => p.theme === themeType.dark ? '#393939' : 'var(--primary-color)',
        background:
            p =>
                p.themeRibbon !== 'true' && (p.theme === themeType.dark || p.theme === themeType.obc2019)
                ?
                '#393939'
                :
                p.theme === themeColor.cobalt || p.theme === themeColor.royalty || p.theme === themeColor.nobility ||
                p.theme === themeColor.harmony || p.theme === themeColor.witness || p.theme === themeColor.whisper ||
                p.theme === themeColor.sunlit
                ?
                'var(--primary-color)'
                :
                'var(--secondary-color)',
        paddingTop: '0!important',
        backgroundImage: p =>
            p.theme === themeColor.cane
            ?
            'url(/img/holiday/snow-4.png)'
            :
            'unset',
        paddingBottom: '0!important',
        display: 'block',
        boxShadow: '0 3px 3px -3px rgba(25, 25, 25, 0.3)',
        backgroundRepeat: 'repeat-x',
        verticalAlign: 'bottom',
    },
    navContainer: {
        maxWidth: '100%!important',
        padding: 0,
        display: 'block!important'
    },
    leftContainer: {},
    row: {
        width: '100%',
        margin: '0!important',
        padding: '0!important',
        justifyContent: 'center',
        alignItems: 'center',
    },
    wrapper: {
        marginBottom: '40px',
        maxWidth: '100vw',
        overflow: 'auto',
        '@media(max-width: 991px)': {
            marginBottom: '90px',
        }
    },
    rowOne: {
        display: 'block',
    },
    column: {
        margin: 0,
        padding: 0,
        '@media(max-width: 991px)': {
            padding: '6px',
        }
    }
});

const Navbar = () => {
    const s = useNavBarStyles({
        theme: getTheme(),
        themeRibbon: getThemeRibbon(),
    });
    const authStore = AuthenticationStore.useContainer();
    const mainNavBarRef = useRef(null);
    
    return <div className={s.wrapper + ' navbar-wrapper-main'}>
        <nav className={`navbar fixed-top navbar-expand-lg ${s.navbar}`} ref={mainNavBarRef} id="stylable-nav-bar">
            <div className={`${s.navContainer} container`}>
                <div className={`${s.row} ${s.rowOne} row`}>
                    <div className={`${s.column} col-12 col-lg-12`}>
                        <div className={`${s.row} row`}>
                            <Logo></Logo>
                            <NavigationLinks></NavigationLinks>
                            <Search></Search>
                            {authStore.isPending ? null : authStore.isAuthenticated ? <LoggedInArea></LoggedInArea> :
                                                          <LoginArea></LoginArea>}
                        </div>
                    </div>
                </div>
            </div>
        </nav>
        {authStore.isAuthenticated && <NavSideBar mainNavBarRef={mainNavBarRef}></NavSideBar>}
    </div>
}

export default Navbar;