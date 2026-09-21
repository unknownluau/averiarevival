import '../styles/globals.css';
import '../styles/helpers/textHelpers.css';
//import 'bootstrap/dist/css/bootstrap.min.css';
// Roblox CSS
//import '../styles/roblox/icons.css';
// js
import React, {useEffect} from 'react';
import Head from 'next/head';
import LoginModalStore from '../stores/loginModal';
import AuthenticationStore from '../stores/authentication';
import NavigationStore from '../stores/navigation';
import { getTheme, getThemeColor, themeType, themeFont } from '../services/theme';
import MainWrapper from '../components/mainWrapper';
import ThumbnailStore from "../stores/thumbnailStore";
import FeedbackStore from "../stores/feedback";
import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import dynamic from "next/dynamic";

if (typeof window !== 'undefined') {
    console.log(String.raw`
      _______      _________      _____       ______     _
     / _____ \    |____ ____|    / ___ \     | ____ \   | |
    / /     \_\       | |       / /   \ \    | |   \ \  | |
    | |               | |      / /     \ \   | |   | |  | |
    \ \______         | |      | |     | |   | |___/ /  | |
     \______ \        | |      | |     | |   |  ____/   | |
            \ \       | |      | |     | |   | |        | |
     _      | |       | |      \ \     / /   | |        |_|
    \ \_____/ /       | |       \ \___/ /    | |         _
     \_______/        |_|        \_____/     |_|        |_|

     Keep your account safe! Do not paste any text here.`);
}

/*const Chat = dynamic(() => import('../components/chat'), { ssr: false });*/
const Footer = dynamic(() => import('../components/footer'), { ssr: false });
const Navbar = dynamic(() => import('../components/navbar'), { ssr: false });
const GlobalAlert = dynamic(() => import('../components/globalAlert'), { ssr: false });

function RobloxApp({Component, pageProps}) {
    // set theme:
    // jss globals apparently don't support parameters/props, so the only way to do a dynamic global style is to either append a <style> element, use setAttribute(), or append a css file.
    // @ts-ignore
    const isChristmas = false
    useEffect(() => {
        dayjs.extend(relativeTime);
        const el = typeof window !== 'undefined' && document.getElementsByTagName('body');
        if (!el || !el.length) return;

        const theme = getTheme();
        const divBackground =
            theme === themeType.dark || theme === themeType.obc2019
                ?
                'url(/img/Unofficial/obc_theme_2016_bg.png) repeat-x #222224'
                :
                document.getElementById('theme-2016-enabled')
                    ?
                    '#e3e3e3'
                    :
                    '#fff'
        ;
        el[0].setAttribute('style', 'background: ' + divBackground);

        (async () => {
            const themeColor = getThemeColor();
            const { ChangeVarsForTheme, ChangeVarsForThemeColor, ChangeVarsForThemeFont } = await import('../lib/ThemeUtil');

            ChangeVarsForTheme(theme);
            ChangeVarsForThemeColor(themeColor);
            ChangeVarsForThemeFont(themeFont.ssp);
        })();
    }, [pageProps]);

    return <div style={pageProps.disableWebsiteTheming ? {minHeight: '100vh'} : null}>
        <Head>
            <link rel="preconnect" href="https://fonts.googleapis.com"/>
            <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin={''}/>
            <title>{pageProps.title || 'Averia'}</title>
            <link rel='icon' type="image/vnd.microsoft.icon" href='/favicon.ico'/>
            <meta name='viewport' content='width=device-width, initial-scale=1'/>
        </Head>
        <AuthenticationStore.Provider>
            {pageProps.disableWebsiteTheming ? null : <>
                <LoginModalStore.Provider>
                    <NavigationStore.Provider>
                        <Navbar/>
                    </NavigationStore.Provider>
                </LoginModalStore.Provider>
                <GlobalAlert/>
            </>}
            <FeedbackStore.Provider>
                <MainWrapper mainFlex={pageProps.disableWebsiteTheming}>
                    <ThumbnailStore.Provider>
                        <Component {...pageProps} />
                        {/*<Chat/>*/}
                    </ThumbnailStore.Provider>
                </MainWrapper>
            </FeedbackStore.Provider>
            {pageProps.disableWebsiteTheming ? null : <Footer/>}
        </AuthenticationStore.Provider>
    </div>
}

export default RobloxApp;